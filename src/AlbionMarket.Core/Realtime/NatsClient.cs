using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace AlbionMarket.Core.Realtime;

/// <summary>
/// Minimal NATS client (just what this app needs: connect, subscribe, read).
/// The wire protocol is plain text, so pulling a dependency would be overkill.
///
/// Public feed used by every third-party tool:
///   nats://public:thenewalbiondata@nats.albion-online-data.com:34222  (Europe)
///   marketorders.deduped / markethistories.deduped / goldprices.deduped
/// </summary>
public sealed class NatsClient : IAsyncDisposable
{
    private readonly Uri _uri;
    private readonly string[] _subjects;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private TcpClient? _tcp;
    private NetworkStream? _stream;

    public bool IsConnected { get; private set; }
    public long MessagesReceived => Interlocked.Read(ref _messages);

    private long _messages;

    /// <summary>subject, JSON payload.</summary>
    public event Action<string, string>? Message;
    public event Action<string>? Log;

    public NatsClient(string url, params string[] subjects)
    {
        _uri = new Uri(url);
        _subjects = subjects.Length > 0 ? subjects : new[] { "marketorders.deduped" };
    }

    public void Start()
    {
        if (_loop is not null) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        try { _stream?.Close(); } catch { }
        try { _tcp?.Close(); } catch { }
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); } catch { }
            _loop = null;
        }
        _cts?.Dispose();
        _cts = null;
        IsConnected = false;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(2);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectAndReadAsync(ct).ConfigureAwait(false);
                delay = TimeSpan.FromSeconds(2);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                IsConnected = false;
                Log?.Invoke($"NATS desconnectat: {ex.Message}. Reintent en {delay.TotalSeconds:0}s.");
            }

            try { await Task.Delay(delay, ct).ConfigureAwait(false); } catch { break; }
            delay = TimeSpan.FromSeconds(Math.Min(60, delay.TotalSeconds * 2));
        }

        IsConnected = false;
    }

    private async Task ConnectAndReadAsync(CancellationToken ct)
    {
        _tcp = new TcpClient { NoDelay = true };
        await _tcp.ConnectAsync(_uri.Host, _uri.Port, ct).ConfigureAwait(false);
        _stream = _tcp.GetStream();

        var user = "";
        var pass = "";
        if (!string.IsNullOrEmpty(_uri.UserInfo))
        {
            var parts = _uri.UserInfo.Split(':', 2);
            user = Uri.UnescapeDataString(parts[0]);
            pass = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
        }

        var connect = JsonSerializer.Serialize(new
        {
            verbose = false,
            pedantic = false,
            tls_required = false,
            name = "albion-market",
            lang = "csharp",
            version = "0.1",
            protocol = 1,
            user,
            pass
        });

        await WriteAsync($"CONNECT {connect}\r\n", ct).ConfigureAwait(false);
        await WriteAsync("PING\r\n", ct).ConfigureAwait(false);

        // Wait for PONG so we know the credentials were accepted.
        while (true)
        {
            var line = await ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null) throw new IOException("El servidor ha tancat la connexió.");
            if (line == "PONG") break;
            if (line.StartsWith("-ERR", StringComparison.Ordinal))
                throw new InvalidOperationException(line);
            if (line == "PING") await WriteAsync("PONG\r\n", ct).ConfigureAwait(false);
        }

        foreach (var subject in _subjects)
            await WriteAsync($"SUB {subject} 1\r\n", ct).ConfigureAwait(false);

        IsConnected = true;
        Log?.Invoke($"NATS connectat ({_uri.Host}:{_uri.Port}) - subscrit a {string.Join(", ", _subjects)}");

        while (!ct.IsCancellationRequested)
        {
            var line = await ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null) throw new IOException("Connexió NATS tancada.");

            if (line == "PING")
            {
                await WriteAsync("PONG\r\n", ct).ConfigureAwait(false);
                continue;
            }
            if (line.StartsWith("-ERR", StringComparison.Ordinal))
            {
                Log?.Invoke($"NATS: {line}");
                continue;
            }
            if (!line.StartsWith("MSG ", StringComparison.Ordinal)) continue;

            // MSG <subject> <sid> [reply-to] <#bytes>
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;

            var subject = parts[1];
            if (!int.TryParse(parts[^1], out var size) || size < 0) continue;

            var payload = await ReadExactAsync(size, ct).ConfigureAwait(false);
            await ReadExactAsync(2, ct).ConfigureAwait(false); // trailing CRLF

            Interlocked.Increment(ref _messages);
            try { Message?.Invoke(subject, Encoding.UTF8.GetString(payload)); }
            catch (Exception ex) { Log?.Invoke($"Error processant missatge NATS: {ex.Message}"); }
        }
    }

    private async Task WriteAsync(string text, CancellationToken ct)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        await _stream!.WriteAsync(bytes, ct).ConfigureAwait(false);
    }

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        var sb = new StringBuilder(128);
        var one = new byte[1];
        while (true)
        {
            var n = await _stream!.ReadAsync(one.AsMemory(0, 1), ct).ConfigureAwait(false);
            if (n == 0) return sb.Length > 0 ? sb.ToString() : null;
            if (one[0] == (byte)'\n') break;
            if (one[0] != (byte)'\r') sb.Append((char)one[0]);
            if (sb.Length > 64 * 1024) throw new IOException("Línia NATS massa llarga.");
        }
        return sb.ToString();
    }

    private async Task<byte[]> ReadExactAsync(int size, CancellationToken ct)
    {
        var buffer = new byte[size];
        var read = 0;
        while (read < size)
        {
            var n = await _stream!.ReadAsync(buffer.AsMemory(read, size - read), ct).ConfigureAwait(false);
            if (n == 0) throw new IOException("Flux NATS tallat.");
            read += n;
        }
        return buffer;
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
