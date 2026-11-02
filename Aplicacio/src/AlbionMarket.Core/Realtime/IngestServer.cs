using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AlbionMarket.Core.Realtime;

/// <summary>
/// A tiny HTTP/1.1 endpoint that receives the JSON that albiondata-client
/// POSTs when it runs with <c>-i http://localhost:PORT</c>.
///
/// Using a raw TcpListener instead of HttpListener avoids the Windows URL ACL
/// requirement: a non-elevated process cannot always bind http://+:port/.
/// The Go client always sends a Content-Length body (bytes.Buffer), so a
/// minimal parser is enough.
/// </summary>
public sealed class IngestServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;

    public int Port { get; }
    public bool IsRunning { get; private set; }
    public long RequestCount => Interlocked.Read(ref _requests);
    public long OrderCount => Interlocked.Read(ref _orders);

    private long _requests;
    private long _orders;

    /// <summary>topic (e.g. "marketorders.ingest"), JSON body.</summary>
    public Func<string, string, Task>? Handler { get; set; }

    public event Action<string>? Log;

    public IngestServer(int port = 5099)
    {
        Port = port;
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    public string? LastError { get; private set; }

    public void Start()
    {
        if (IsRunning) return;
        try
        {
            _listener.Start();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log?.Invoke($"No s'ha pogut obrir el port {Port}: {ex.Message}. " +
                        "L'ingesta local queda desactivada (potser ja hi ha una altra instància oberta).");
            return;
        }
        _cts = new CancellationTokenSource();
        IsRunning = true;
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
        Log?.Invoke($"Ingesta local activa a http://localhost:{Port}/");
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;
        _cts?.Cancel();
        _listener.Stop();
        if (_acceptLoop is not null)
        {
            try { await _acceptLoop.ConfigureAwait(false); } catch { /* ignored */ }
        }
        _cts?.Dispose();
        _cts = null;
        IsRunning = false;
        Log?.Invoke("Ingesta local aturada.");
    }

    public void AddOrders(int n) => Interlocked.Add(ref _orders, n);

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { break; }

            _ = HandleClientAsync(client, ct);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            try
            {
                client.NoDelay = true;
                using var stream = client.GetStream();

                var head = await ReadHeadAsync(stream, ct).ConfigureAwait(false);
                if (head is null)
                {
                    await WriteResponseAsync(stream, 400, "Bad Request", ct).ConfigureAwait(false);
                    return;
                }

                var lines = head.Split("\r\n");
                var requestLine = lines[0].Split(' ');
                if (requestLine.Length < 2)
                {
                    await WriteResponseAsync(stream, 400, "Bad Request", ct).ConfigureAwait(false);
                    return;
                }

                var method = requestLine[0];
                var path = requestLine[1].Split('?')[0].TrimStart('/');

                var contentLength = 0;
                foreach (var line in lines.Skip(1))
                {
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        int.TryParse(line["Content-Length:".Length..].Trim(), out contentLength);
                }

                string body = "";
                if (contentLength > 0)
                {
                    // The client can batch a lot of orders; be generous but bounded.
                    if (contentLength > 32 * 1024 * 1024)
                    {
                        await WriteResponseAsync(stream, 413, "Payload Too Large", ct).ConfigureAwait(false);
                        return;
                    }
                    var buffer = new byte[contentLength];
                    var read = 0;
                    while (read < contentLength)
                    {
                        var n = await stream.ReadAsync(buffer.AsMemory(read, contentLength - read), ct).ConfigureAwait(false);
                        if (n == 0) break;
                        read += n;
                    }
                    body = Encoding.UTF8.GetString(buffer, 0, read);
                }

                Interlocked.Increment(ref _requests);

                if (method.Equals("GET", StringComparison.OrdinalIgnoreCase) && path is "health" or "")
                {
                    await WriteResponseAsync(stream, 200, "OK", ct).ConfigureAwait(false);
                    return;
                }

                var handler = Handler;
                if (handler is not null && body.Length > 0)
                {
                    try { await handler(path, body).ConfigureAwait(false); }
                    catch (Exception ex) { Log?.Invoke($"Error processant '{path}': {ex.Message}"); }
                }

                await WriteResponseAsync(stream, 200, "OK", ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Error HTTP: {ex.Message}");
            }
        }
    }

    private static async Task<string?> ReadHeadAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var total = 0;

        while (total < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(total, 1), ct).ConfigureAwait(false);
            if (n == 0) return null;
            total++;

            if (total >= 4 &&
                buffer[total - 4] == '\r' && buffer[total - 3] == '\n' &&
                buffer[total - 2] == '\r' && buffer[total - 1] == '\n')
            {
                return Encoding.ASCII.GetString(buffer, 0, total);
            }
        }
        return null;
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int status, string reason, CancellationToken ct)
    {
        var body = reason;
        var head = $"HTTP/1.1 {status} {reason}\r\n" +
                   "Content-Type: text/plain\r\n" +
                   $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n" +
                   "Connection: close\r\n\r\n";
        var bytes = Encoding.UTF8.GetBytes(head + body);
        await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}
