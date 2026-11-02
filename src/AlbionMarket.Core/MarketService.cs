using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AlbionMarket.Core.Api;
using AlbionMarket.Core.Data;
using AlbionMarket.Core.Engine;
using AlbionMarket.Core.Models;
using AlbionMarket.Core.Realtime;

namespace AlbionMarket.Core;

/// <summary>
/// Orchestrates the three data sources so the UI only has to talk to one object:
///  1. the public REST API (watchlist snapshots),
///  2. the local sniffer (albiondata-client POSTing to our ingest port),
///  3. the public NATS deduped feed (realtime, same data paid tools use).
/// </summary>
public sealed class MarketService : IAsyncDisposable
{
    private static readonly string[] DefaultWatchlist =
    {
        "T4_WOOD", "T5_WOOD", "T6_WOOD", "T7_WOOD", "T8_WOOD",
        "T4_ORE", "T5_ORE", "T6_ORE", "T7_ORE", "T8_ORE",
        "T4_HIDE", "T5_HIDE", "T6_HIDE", "T7_HIDE", "T8_HIDE",
        "T4_FIBER", "T5_FIBER", "T6_FIBER", "T7_FIBER", "T8_FIBER",
        "T4_ROCK", "T5_ROCK", "T6_ROCK", "T7_ROCK", "T8_ROCK",
        "T4_PLANKS", "T5_PLANKS", "T6_PLANKS", "T7_PLANKS", "T8_PLANKS",
        "T4_METALBAR", "T5_METALBAR", "T6_METALBAR", "T7_METALBAR", "T8_METALBAR",
        "T4_LEATHER", "T5_LEATHER", "T6_LEATHER", "T7_LEATHER", "T8_LEATHER",
        "T4_CLOTH", "T5_CLOTH", "T6_CLOTH", "T7_CLOTH", "T8_CLOTH",
        "T4_STONEBLOCK", "T5_STONEBLOCK", "T6_STONEBLOCK", "T7_STONEBLOCK", "T8_STONEBLOCK",
        "T4_BAG", "T5_BAG", "T6_BAG", "T7_BAG", "T8_BAG"
    };

    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private int _recomputeScheduled;
    private Process? _clientProcess;

    public MarketDatabase Db { get; }
    public AlbionApiClient Api { get; }
    public IngestServer Ingest { get; }
    public NatsClient? Nats { get; private set; }

    public FlipOptions Options { get; } = new();
    public AlbionRegion Region { get; private set; } = AlbionRegion.Europe;
    public List<string> Watchlist { get; } = new();
    public string ClientPath { get; set; } =
        @"C:\Program Files\Albion Data Client\albiondata-client.exe";
    public bool AlsoUploadToPublicProject { get; set; }
    public DateTime? LastRefreshUtc { get; private set; }

    public event Action<List<Flip>>? FlipsChanged;
    public event Action? StateChanged;
    public event Action<string>? Log;
    public event Action<MarketOrder>? OrderReceived;

    public MarketService(string? dbPath = null)
    {
        var path = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AlbionMarket", "market.db");

        Db = new MarketDatabase(path);
        Api = new AlbionApiClient(Region);
        Ingest = new IngestServer(5099);
        Ingest.Handler = OnIngestAsync;
        Ingest.Log += m => Log?.Invoke(m);
        Api.Log += m => Log?.Invoke(m);

        LoadSettings();
    }

    public int IngestPort => Ingest.Port;

    public async Task StartAsync(CancellationToken ct = default)
    {
        Ingest.Start();

        // Only download the 1 MB item dump once.
        if (await Db.CountItemsAsync(ct).ConfigureAwait(false) == 0)
        {
            try { await ItemCatalog.SyncAsync(Db, m => Log?.Invoke(m), ct).ConfigureAwait(false); }
            catch (Exception ex) { Log?.Invoke($"No s'ha pogut baixar el catàleg: {ex.Message}"); }
        }

        await RecomputeFlipsAsync(ct).ConfigureAwait(false);
        StateChanged?.Invoke();
    }

    // ------------------------------------------------------------- settings

    private void LoadSettings()
    {
        var region = Db.GetSetting("region");
        if (Enum.TryParse<AlbionRegion>(region, out var r)) Region = r;
        Api.Region = Region;

        Options.TaxRate = GetDouble("tax", Options.TaxRate);
        Options.SetupFee = GetDouble("setup", Options.SetupFee);
        Options.MaxAgeHours = GetDouble("maxAge", Options.MaxAgeHours);
        Options.MinRoiPercent = GetDouble("minRoi", Options.MinRoiPercent);
        Options.MaxInvestment = (int)GetDouble("maxInvestment", Options.MaxInvestment);
        Options.IncludeListed = Db.GetSetting("includeListed") != "0";

        ClientPath = Db.GetSetting("clientPath") ?? ClientPath;
        AlsoUploadToPublicProject = Db.GetSetting("alsoPublic") == "1";

        var list = Db.GetSetting("watchlist");
        Watchlist.Clear();
        if (!string.IsNullOrWhiteSpace(list))
            Watchlist.AddRange(list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (Watchlist.Count == 0) Watchlist.AddRange(DefaultWatchlist);
    }

    public void SaveSettings()
    {
        Db.SetSetting("region", Region.ToString());
        Db.SetSetting("tax", Options.TaxRate.ToString(CultureInfo.InvariantCulture));
        Db.SetSetting("setup", Options.SetupFee.ToString(CultureInfo.InvariantCulture));
        Db.SetSetting("maxAge", Options.MaxAgeHours.ToString(CultureInfo.InvariantCulture));
        Db.SetSetting("minRoi", Options.MinRoiPercent.ToString(CultureInfo.InvariantCulture));
        Db.SetSetting("maxInvestment", Options.MaxInvestment.ToString(CultureInfo.InvariantCulture));
        Db.SetSetting("includeListed", Options.IncludeListed ? "1" : "0");
        Db.SetSetting("clientPath", ClientPath);
        Db.SetSetting("alsoPublic", AlsoUploadToPublicProject ? "1" : "0");
        Db.SetSetting("watchlist", string.Join(",", Watchlist));
        StateChanged?.Invoke();
    }

    public void SetRegion(AlbionRegion region)
    {
        Region = region;
        Api.Region = region;
        SaveSettings();
    }

    private double GetDouble(string key, double fallback) =>
        double.TryParse(Db.GetSetting(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    // ---------------------------------------------------------------- flips

    public async Task<List<Flip>> RecomputeFlipsAsync(CancellationToken ct = default)
    {
        var prices = await Db.GetPricesAsync(ct: ct).ConfigureAwait(false);
        var flips = FlipAnalyzer.Analyze(prices, Options);
        flips = flips.Where(f => !f.IsStale(Options.MaxAgeHours)).ToList();
        FlipsChanged?.Invoke(flips);
        return flips;
    }

    private async Task ScheduleRecomputeAsync()
    {
        if (Interlocked.Exchange(ref _recomputeScheduled, 1) == 1) return;
        try
        {
            await Task.Delay(750).ConfigureAwait(false);
            await RecomputeFlipsAsync().ConfigureAwait(false);
        }
        catch (Exception ex) { Log?.Invoke($"Error recalculant oportunitats: {ex.Message}"); }
        finally { Interlocked.Exchange(ref _recomputeScheduled, 0); }
    }

    // ------------------------------------------------------------------ api

    public async Task RefreshWatchlistAsync(CancellationToken ct = default)
    {
        if (!await _refreshGate.WaitAsync(0, ct).ConfigureAwait(false))
        {
            Log?.Invoke("Ja hi ha una actualització en curs.");
            return;
        }

        try
        {
            Log?.Invoke($"Actualitzant {Watchlist.Count} items des de {Api.BaseUrl}...");
            var prices = await Api.GetPricesAsync(Watchlist, Locations.MainCities, new[] { 1 }, ct)
                .ConfigureAwait(false);

            var usable = prices.Where(p => p.City.Length > 0).ToList();
            await Db.MergePricesAsync(usable, "api", ct).ConfigureAwait(false);

            LastRefreshUtc = DateTime.UtcNow;
            Log?.Invoke($"Rebut {usable.Count} files de preus.");

            await RecomputeFlipsAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Error actualitzant: {ex.Message}");
        }
        finally
        {
            _refreshGate.Release();
            StateChanged?.Invoke();
        }
    }

    public async Task<List<MarketPrice>> QueryItemAsync(string itemId, CancellationToken ct = default)
    {
        // Prefer the API for an on-demand search, then fall back to the cache.
        try
        {
            var fresh = await Api.GetPricesAsync(new[] { itemId }, Locations.MainCities, null, ct).ConfigureAwait(false);
            var usable = fresh.Where(p => p.HasSell || p.HasBuy).ToList();
            if (usable.Count > 0)
            {
                await Db.MergePricesAsync(usable, "api", ct).ConfigureAwait(false);
                return usable;
            }
        }
        catch (Exception ex) { Log?.Invoke($"Consulta en línia fallida: {ex.Message}"); }

        return await Db.GetPricesAsync(itemId, ct: ct).ConfigureAwait(false);
    }

    // -------------------------------------------------------------- sniffer

    private async Task OnIngestAsync(string topic, string body)
    {
        if (!topic.StartsWith("marketorders", StringComparison.OrdinalIgnoreCase))
            return;

        MarketUpload? upload;
        try { upload = JsonSerializer.Deserialize<MarketUpload>(body); }
        catch (Exception ex) { Log?.Invoke($"JSON d'ordres no vàlid: {ex.Message}"); return; }
        if (upload?.Orders is null || upload.Orders.Count == 0) return;

        await HandleOrdersAsync(upload.Orders, "sniffer").ConfigureAwait(false);
    }

    private async Task HandleOrdersAsync(List<MarketOrder> orders, string source)
    {
        var saved = await Db.SaveOrdersAsync(orders, source).ConfigureAwait(false);
        await Db.RebuildPricesFromOrdersAsync(orders, source).ConfigureAwait(false);
        Ingest.AddOrders(saved);

        foreach (var order in orders.Take(50))
            OrderReceived?.Invoke(order);

        Log?.Invoke($"[{source}] {saved} ordres rebudes ({string.Join(", ", orders.Select(o => o.City).Distinct().Take(3))})");
        await ScheduleRecomputeAsync().ConfigureAwait(false);
        StateChanged?.Invoke();
    }

    public bool IsLocalClientRunning => _clientProcess is { HasExited: false };
    public int? LocalClientPid => IsLocalClientRunning ? _clientProcess!.Id : null;

    public void StartLocalClient()
    {
        if (IsLocalClientRunning) return;
        if (!File.Exists(ClientPath))
        {
            Log?.Invoke($"No trobo el client a '{ClientPath}'. Configura'n la ruta.");
            return;
        }

        // A client started by the Windows task scheduler (or by the user) is not
        // pointing at us. Two packet captures would duplicate everything, so the
        // one already running is replaced by ours.
        var existing = Process.GetProcessesByName("albiondata-client");
        foreach (var p in existing)
        {
            try
            {
                Log?.Invoke($"Hi ha un client en marxa (PID {p.Id}) que no envia dades aquí; el reinicio.");
                p.Kill(entireProcessTree: true);
                p.WaitForExit(4000);
            }
            catch (Exception ex) { Log?.Invoke($"No s'ha pogut aturar el client {p.Id}: {ex.Message}"); }
        }

        var ingest = $"http://localhost:{Ingest.Port}";
        if (AlsoUploadToPublicProject)
            ingest += "," + Region.PublicPowIngest();

        var psi = new ProcessStartInfo
        {
            FileName = ClientPath,
            Arguments = $"-i \"{ingest}\" -minimize",
            UseShellExecute = false
        };

        try
        {
            _clientProcess = Process.Start(psi);
            Log?.Invoke($"Client local iniciat (ingesta: {ingest}).");
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Log?.Invoke($"No s'ha pogut iniciar el client: {ex.Message}");
        }
    }

    public void StopLocalClient()
    {
        try
        {
            if (_clientProcess is { HasExited: false }) _clientProcess.Kill(entireProcessTree: true);
        }
        catch (Exception ex) { Log?.Invoke($"Error aturant el client: {ex.Message}"); }
        finally
        {
            _clientProcess = null;
            StateChanged?.Invoke();
        }
    }

    // ----------------------------------------------------------------- nats

    public void StartNats()
    {
        if (Nats is not null) return;

        Nats = new NatsClient(Region.NatsUrl(), "marketorders.deduped");
        Nats.Log += m => Log?.Invoke(m);
        Nats.Message += (subject, json) => { _ = HandleNatsMessageAsync(json); };
        Nats.Start();
        StateChanged?.Invoke();
    }

    public async Task StopNatsAsync()
    {
        if (Nats is null) return;
        await Nats.StopAsync().ConfigureAwait(false);
        Nats = null;
        StateChanged?.Invoke();
    }

    private async Task HandleNatsMessageAsync(string json)
    {
        try
        {
            // marketorders.deduped publishes ONE order per message, while the
            // local HTTP ingest sends {"Orders":[...]}. Support both.
            var single = JsonSerializer.Deserialize<MarketOrder>(json);
            if (single is { } o && o.Id != 0 && !string.IsNullOrWhiteSpace(o.ItemId))
            {
                await HandleOrdersAsync(new List<MarketOrder> { o }, "nats").ConfigureAwait(false);
                return;
            }
        }
        catch (JsonException) { /* potser és un lot */ }
        catch (Exception ex) { Log?.Invoke($"Missatge NATS no processat: {ex.Message}"); return; }

        try
        {
            var upload = JsonSerializer.Deserialize<MarketUpload>(json);
            if (upload?.Orders is { Count: > 0 })
                await HandleOrdersAsync(upload.Orders, "nats").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Missatge NATS no processat: {ex.Message}");
        }
    }

    // --------------------------------------------------------------- teardown

    public async ValueTask DisposeAsync()
    {
        StopLocalClient();
        if (Nats is not null) await Nats.DisposeAsync().ConfigureAwait(false);
        await Ingest.DisposeAsync().ConfigureAwait(false);
        Api.Dispose();
    }
}
