using System.Globalization;
using System.Windows;
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
        AppendLog($"Base de dades: {_svc.Db.FilePath}");
        AppendLog($"Catàleg: {_vm.ItemNames.Count} items · preus en caché: {await _svc.Db.CountPricesAsync()}");

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
        WatchlistBox.Text = string.Join(Environment.NewLine, _svc.Watchlist);
        PortText.Text = _svc.IngestPort.ToString();
    }

    private void Loaded_Region(AlbionRegion region)
    {
        RegionCombo.SelectedItem = region;
        RegionLabel.Text = region.ToString();
    }

    // ---------------------------------------------------------------- events

    private void OnFlipsChanged(List<Flip> flips)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _vm.Flips.Clear();
            foreach (var f in flips) _vm.Flips.Add(_vm.ToRow(f));

            FlipsCountText.Text = $"{flips.Count} oportunitats";
            RefreshState();
        });
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
