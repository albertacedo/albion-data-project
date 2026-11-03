using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AlbionMarket.Core;
using AlbionMarket.Core.Engine;
using AlbionMarket.Core.Models;

namespace AlbionMarket.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private MarketService? _svc;
    private bool _busy;
    private int _countTicks;
    private List<Flip> _allFlips = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        RegionCombo.ItemsSource = Enum.GetValues<AlbionRegion>();
        _timer.Tick += (_, _) => UpdateCounters();
        _timer.Start();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _svc = new MarketService();
        _svc.Log += AppendLog;
        _svc.FlipsChanged += OnFlipsChanged;
        _svc.StateChanged += () => Dispatcher.BeginInvoke(RefreshState);

        await _svc.StartAsync();
        _vm.ItemNames = await _svc.Db.GetItemNamesAsync();

        LoadSettingsIntoUi();
        LoadOpportunityFilters();
        AppendLog($"Base de dades: {_svc.Db.FilePath}");
        AppendLog($"Catàleg: {_vm.ItemNames.Count} items · preus en caché: {await _svc.Db.CountPricesAsync()}");
        await RefreshApiItemsCountAsync();

        // First run or stale cache: pull the watchlist straight away.
        if (await _svc.Db.CountPricesAsync() < 50)
        {
            AppendLog("Primera càrrega: consultant l'API...");
            await _svc.RefreshWatchlistAsync();
        }
        else
        {
            await _svc.RecomputeFlipsAsync();
        }

        RefreshState();

        // Default behaviour: the app is the only UI, so the sniffer and the
        // public feed start on their own. The checkbox in Configuració
        // (autoStart) turns this off.
        if (_svc.AutoStart)
        {
            AppendLog("Arrencada automàtica: client local en segon pla i NATS.");
            _svc.StartLocalClient();
            _svc.StartNats();
            RefreshState();
        }
    }

    private void LoadSettingsIntoUi()
    {
        if (_svc is null) return;

        Loaded_Region(_svc.Region);
        TaxBox.Text = (_svc.Options.TaxRate * 100).ToString("0.##", CultureInfo.InvariantCulture);
        SetupBox.Text = (_svc.Options.SetupFee * 100).ToString("0.##", CultureInfo.InvariantCulture);
        MaxInvestBox.Text = _svc.Options.MaxInvestment.ToString(CultureInfo.InvariantCulture);
        MinRoiBox.Text = _svc.Options.MinRoiPercent.ToString("0.#", CultureInfo.InvariantCulture);
        MaxAgeBox.Text = _svc.Options.MaxAgeHours.ToString("0.#", CultureInfo.InvariantCulture);
        ListedCheck.IsChecked = _svc.Options.IncludeListed;
        ClientPathBox.Text = _svc.ClientPath;
        AlsoPublicCheck.IsChecked = _svc.AlsoUploadToPublicProject;
        AutoStartCheck.IsChecked = _svc.AutoStart;
        WatchlistBox.Text = string.Join(Environment.NewLine, _svc.Watchlist);
        PortText.Text = _svc.IngestPort.ToString();
    }

    private void Loaded_Region(AlbionRegion region)
    {
        RegionCombo.SelectedItem = region;
        RegionLabel.Text = region.ToString();
    }

    // ---------------------------------------------------------------- events

    private void FlipsGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is FlipRow row) row.LoadIcon();
    }

    private void PricesGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is PriceRow row) row.LoadIcon();
    }

    private void OnFlipsChanged(List<Flip> flips)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _allFlips = flips;
            ApplyOpportunityFilters(save: false);
            RefreshState();
        });
    }

    private void ApplyOpportunityFilters_Click(object sender, RoutedEventArgs e) =>
        ApplyOpportunityFilters(save: true);

    private void ApplyOpportunityFilters(bool save)
    {
        var buyCities = BuyCityFilterCombo.SelectedValues();
        var sellCities = SellCityFilterCombo.SelectedValues();
        var tiers = TierFilterCombo.SelectedValues();
        var enchantments = EnchantmentFilterCombo.SelectedValues();
        var qualities = QualityFilterCombo.SelectedValues();

        var filtered = _allFlips.Where(f =>
            (buyCities.Count == 0 || buyCities.Contains(f.BuyCity)) &&
            (sellCities.Count == 0 || sellCities.Contains(f.SellCity)) &&
            (tiers.Count == 0 || tiers.Contains(ItemTier(f.ItemId))) &&
            (enchantments.Count == 0 || enchantments.Contains(ItemEnchantment(f.ItemId).ToString(CultureInfo.InvariantCulture))) &&
            (qualities.Count == 0 || qualities.Contains(f.Quality.ToString(CultureInfo.InvariantCulture)))).ToList();

        _vm.Flips.Clear();
        foreach (var flip in filtered) _vm.Flips.Add(_vm.ToRow(flip));
        FlipsCountText.Text = $"{filtered.Count} de {_allFlips.Count} oportunitats";

        if (save && _svc is not null)
        {
            _svc.Db.SetSetting("filterBuyCity", string.Join(",", buyCities));
            _svc.Db.SetSetting("filterSellCity", string.Join(",", sellCities));
            _svc.Db.SetSetting("filterTier", string.Join(",", tiers));
            _svc.Db.SetSetting("filterEnchantment", string.Join(",", enchantments));
            _svc.Db.SetSetting("filterQuality", string.Join(",", qualities));
            AppendLog("Filtres d'oportunitats desats.");
        }
    }

    private void LoadOpportunityFilters()
    {
        if (_svc is null) return;

        SetFilterSelection(BuyCityFilterCombo, _svc.Db.GetSetting("filterBuyCity"));
        SetFilterSelection(SellCityFilterCombo, _svc.Db.GetSetting("filterSellCity"));
        SetFilterSelection(TierFilterCombo, _svc.Db.GetSetting("filterTier"));
        SetFilterSelection(EnchantmentFilterCombo, _svc.Db.GetSetting("filterEnchantment"));
        SetFilterSelection(QualityFilterCombo, _svc.Db.GetSetting("filterQuality"));
        ApplyOpportunityFilters(save: false);
    }

    private static void SetFilterSelection(MultiSelectComboBox filter, string? value) =>
        filter.SetSelectedValues((value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string ItemTier(string itemId) =>
        itemId.Split('_', 2)[0].ToUpperInvariant();

    private static int ItemEnchantment(string itemId)
    {
        var at = itemId.LastIndexOf('@');
        return at >= 0 && int.TryParse(itemId[(at + 1)..], out var enchantment)
            ? enchantment
            : 0;
    }

    private void AppendLog(string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            LogBox.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
            if (LogBox.Text.Length > 120_000)
                LogBox.Text = LogBox.Text[^80_000..];
            LogBox.ScrollToEnd();
        });
    }

    private void UpdateCounters()
    {
        if (_svc is null) return;
        IngestRequestsText.Text = _svc.Ingest.RequestCount.ToString("N0");
        IngestOrdersText.Text = _svc.Ingest.OrderCount.ToString("N0");
        NatsCountText.Text = (_svc.Nats?.MessagesReceived ?? 0).ToString("N0");

        // The total keeps changing as NATS writes rows, but counting 20k rows
        // every second is wasted work: refresh it every five ticks.
        if (++_countTicks % 5 == 0) _ = RefreshDbCountAsync();
    }

    private async Task RefreshDbCountAsync()
    {
        if (_svc is null) return;
        try
        {
            var n = await _svc.Db.CountOrdersAsync();
            DbOrdersText.Text = n.ToString("N0");
            await RefreshApiItemsCountAsync();
        }
        catch { /* comptador informatiu: si falla, no destorbem */ }
    }

    private async Task RefreshApiItemsCountAsync()
    {
        if (_svc is null) return;
        try
        {
            var n = await _svc.Db.CountPriceItemsAsync();
            ApiItemsText.Text = n.ToString("N0");
        }
        catch { /* comptador informatiu */ }
    }

    private void RefreshState()
    {
        if (_svc is null) return;

        var apiState = _svc.LastRefreshUtc is { } last
            ? $"API: {last.ToLocalTime():HH:mm:ss}"
            : $"API: dades en caché ({_vm.Flips.Count} oportunitats)";

        StatusText.Text =
            $"{apiState}" +
            $"  ·  Ingesta: {(_svc.Ingest.IsRunning ? "activa" : "aturada")}" +
            $"  ·  NATS: {(_svc.Nats is { IsConnected: true } ? "connectat" : "desconnectat")}";

        SnifferStatusText.Text = _svc.IsLocalClientRunning
            ? $"Client local en marxa (PID {_svc.LocalClientPid})"
            : "Client local aturat";

        ClientButton.Content = _svc.IsLocalClientRunning ? "Aturar client" : "Iniciar client";
        NatsButton.Content = _svc.Nats is null ? "Connectar NATS" : "Desconnectar NATS";
    }

    // --------------------------------------------------------------- actions

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_svc is null || _busy) return;
        _busy = true;
        try
        {
            ApplyFilters(showLog: false);
            await _svc.RefreshWatchlistAsync();
        }
        finally { _busy = false; }
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        if (_svc is null) return;

        var id = ItemSearchBox.Text.Trim();
        if (id.Length == 0) return;

        StatusText.Text = $"Consultant {id}...";
        var prices = await _svc.QueryItemAsync(id);

        _vm.Prices.Clear();
        foreach (var p in prices.OrderBy(p => p.City)) _vm.Prices.Add(_vm.ToRow(p));

        AppendLog($"Consulta {id}: {prices.Count} files.");
        RefreshState();
    }

    private void Client_Click(object sender, RoutedEventArgs e)
    {
        if (_svc is null) return;
        if (_svc.IsLocalClientRunning) _svc.StopLocalClient();
        else _svc.StartLocalClient();
        RefreshState();
    }

    private async void Nats_Click(object sender, RoutedEventArgs e)
    {
        if (_svc is null) return;
        if (_svc.Nats is null) _svc.StartNats();
        else await _svc.StopNatsAsync();
        RefreshState();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_svc is null) return;

        if (RegionCombo.SelectedItem is AlbionRegion region) _svc.SetRegion(region);
        _svc.Options.TaxRate = ReadDouble(TaxBox.Text, 4) / 100.0;
        _svc.Options.SetupFee = ReadDouble(SetupBox.Text, 2.5) / 100.0;
        _svc.Options.MaxInvestment = (int)ReadDouble(MaxInvestBox.Text, 0);
        _svc.ClientPath = ClientPathBox.Text.Trim();
        _svc.AlsoUploadToPublicProject = AlsoPublicCheck.IsChecked == true;
        _svc.AutoStart = AutoStartCheck.IsChecked == true;

        _svc.Watchlist.Clear();
        _svc.Watchlist.AddRange(WatchlistBox.Text
            .Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase));

        ApplyFilters(showLog: false);
        _svc.SaveSettings();
        RegionLabel.Text = _svc.Region.ToString();
        AppendLog("Configuració desada.");
        RefreshState();
    }

    private async void SyncItems_Click(object sender, RoutedEventArgs e)
    {
        if (_svc is null) return;
        var n = await ItemCatalog.SyncAsync(_svc.Db, AppendLog);
        _vm.ItemNames = await _svc.Db.GetItemNamesAsync();
        await _svc.RecomputeFlipsAsync();
        AppendLog($"Catàleg sincronitzat ({n} items).");
    }

    private void ApplyFilters(bool showLog)
    {
        if (_svc is null) return;

        _svc.Options.MinRoiPercent = ReadDouble(MinRoiBox.Text, 10);
        _svc.Options.MaxAgeHours = ReadDouble(MaxAgeBox.Text, 48);
        _svc.Options.IncludeListed = ListedCheck.IsChecked == true;
        _svc.SaveSettings();

        _ = _svc.RecomputeFlipsAsync();
        if (showLog) AppendLog("Filtres aplicats.");
    }

    private static double ReadDouble(string text, double fallback)
    {
        text = text.Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
        try { _svc?.DisposeAsync().AsTask().Wait(3000); }
        catch { /* al tancar, no val la pena molestar l'usuari */ }
    }
}
