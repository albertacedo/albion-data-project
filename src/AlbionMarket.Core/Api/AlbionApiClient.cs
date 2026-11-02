using System.Net;
using System.Text.Json;
using AlbionMarket.Core.Models;

namespace AlbionMarket.Core.Api;

/// <summary>
/// Thin client over the public AODP REST API. The API is rate limited
/// (~180 req/min per IP and it is strict), so every call goes through a
/// gate that enforces a minimum interval between requests.
/// </summary>
public sealed class AlbionApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastRequest = DateTime.MinValue;

    public AlbionRegion Region { get; set; } = AlbionRegion.Europe;
    public int MinRequestIntervalMs { get; set; } = 1200;
    public int MaxItemsPerRequest { get; set; } = 100;

    public event Action<string>? Log;

    public AlbionApiClient(AlbionRegion region = AlbionRegion.Europe)
    {
        Region = region;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AlbionMarket/0.1 (personal tool)");
    }

    public string BaseUrl => Region.ApiBaseUrl();

    public Task<List<MarketPrice>> GetPricesAsync(
        IEnumerable<string> itemIds,
        IEnumerable<string>? cities = null,
        IEnumerable<int>? qualities = null,
        CancellationToken ct = default)
    {
        var items = itemIds.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var query = new List<string>();
        if (cities is not null) query.Add("locations=" + string.Join(",", cities.Select(Uri.EscapeDataString)));
        if (qualities is not null) query.Add("qualities=" + string.Join(",", qualities));

        return FetchChunkedAsync<MarketPrice>("/api/v2/stats/prices/", items, query, ct);
    }

    public Task<List<MarketPrice>> GetPricesAsync(string itemId, CancellationToken ct = default) =>
        GetPricesAsync(new[] { itemId }, null, null, ct);

    /// <summary>Aggregated history (hourly/daily buckets) used for volume and volatility.</summary>
    public async Task<List<HistoryEntry>> GetHistoryAsync(
        string itemId,
        IEnumerable<string>? cities = null,
        int quality = 1,
        string timeScale = "24",
        CancellationToken ct = default)
    {
        var query = new List<string>
        {
            "qualities=" + quality,
            "time-scale=" + timeScale
        };
        if (cities is not null) query.Add("locations=" + string.Join(",", cities.Select(Uri.EscapeDataString)));

        var url = $"{BaseUrl}/api/v2/stats/history/{Uri.EscapeDataString(itemId)}.json?{string.Join("&", query)}";
        var json = await GetStringAsync(url, ct).ConfigureAwait(false);
        if (json is null) return new List<HistoryEntry>();
        try { return JsonSerializer.Deserialize<List<HistoryEntry>>(json, JsonOptions) ?? new(); }
        catch (Exception ex) { Log?.Invoke($"Historial: resposta no vàlida ({ex.Message})"); return new(); }
    }

    public async Task<List<GoldPrice>> GetGoldAsync(int count = 24, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/api/v2/stats/gold?count={count}";
        var json = await GetStringAsync(url, ct).ConfigureAwait(false);
        if (json is null) return new List<GoldPrice>();
        try { return JsonSerializer.Deserialize<List<GoldPrice>>(json, JsonOptions) ?? new(); }
        catch { return new List<GoldPrice>(); }
    }

    private async Task<List<T>> FetchChunkedAsync<T>(
        string pathPrefix, List<string> items, List<string> query, CancellationToken ct)
    {
        var result = new List<T>();
        foreach (var chunk in items.Chunk(MaxItemsPerRequest))
        {
            var url = $"{BaseUrl}{pathPrefix}{string.Join(",", chunk.Select(Uri.EscapeDataString))}.json";
            if (query.Count > 0) url += "?" + string.Join("&", query);

            var json = await GetStringAsync(url, ct).ConfigureAwait(false);
            if (json is null) continue;
            try
            {
                var page = JsonSerializer.Deserialize<List<T>>(json, JsonOptions);
                if (page is not null) result.AddRange(page);
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Resposta no vàlida: {ex.Message}");
            }
        }
        return result;
    }

    private async Task<string?> GetStringAsync(string url, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await ThrottleAsync(ct).ConfigureAwait(false);
            try
            {
                using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);

                if (resp.StatusCode == HttpStatusCode.TooManyRequests || (int)resp.StatusCode == 503)
                {
                    var wait = ComputeWait(resp, attempt);
                    Log?.Invoke($"Límit de peticions assolit ({(int)resp.StatusCode}). Esperant {wait.TotalSeconds:0}s...");
                    await Task.Delay(wait, ct).ConfigureAwait(false);
                    continue;
                }

                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                Log?.Invoke($"Error consultant l'API (intent {attempt}/4): {ex.Message}");
                if (attempt == 4) return null;
                await Task.Delay(1000 * attempt, ct).ConfigureAwait(false);
            }
        }
        return null;
    }

    /// <summary>
    /// The API reports its own window in <c>ratelimit-reset</c> (unix seconds) and
    /// it is much stricter than the old 180/min figure suggests. Honour it instead
    /// of hammering the endpoint and getting stuck in a 429 loop.
    /// </summary>
    private static TimeSpan ComputeWait(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.TryGetValues("ratelimit-reset", out var values) &&
            long.TryParse(values.FirstOrDefault(), out var resetUnix))
        {
            var reset = DateTimeOffset.FromUnixTimeSeconds(resetUnix);
            var wait = reset - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero && wait < TimeSpan.FromMinutes(4))
                return wait + TimeSpan.FromSeconds(1.5);
        }

        if (response.Headers.RetryAfter?.Delta is { } delta && delta < TimeSpan.FromMinutes(4))
            return delta + TimeSpan.FromSeconds(1);

        return TimeSpan.FromSeconds(Math.Min(120, 15 * attempt));
    }

    private async Task ThrottleAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_lastRequest != DateTime.MinValue)
            {
                var elapsed = (DateTime.UtcNow - _lastRequest).TotalMilliseconds;
                var remaining = MinRequestIntervalMs - elapsed;
                if (remaining > 0) await Task.Delay((int)remaining, ct).ConfigureAwait(false);
            }
            _lastRequest = DateTime.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _gate.Dispose();
    }
}

public sealed class HistoryEntry
{
    [System.Text.Json.Serialization.JsonPropertyName("item_id")] public string ItemId { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("location")] public string Location { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("quality")] public int Quality { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("data")] public List<HistoryPoint> Data { get; set; } = new();
}

public sealed class HistoryPoint
{
    [System.Text.Json.Serialization.JsonPropertyName("item_count")] public long ItemCount { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("avg_price")] public long AvgPrice { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("timestamp")] public string Timestamp { get; set; } = "";
}

public sealed class GoldPrice
{
    [System.Text.Json.Serialization.JsonPropertyName("price")] public long Price { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("timestamp")] public string Timestamp { get; set; } = "";
}
