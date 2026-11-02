using System.Collections.ObjectModel;
using AlbionMarket.Core.Models;

namespace AlbionMarket.App;

public sealed class FlipRow
{
    public string ItemId { get; init; } = "";
    public string Name { get; init; } = "";
    public int Quality { get; init; }
    public string BuyCity { get; init; } = "";
    public int BuyPrice { get; init; }
    public string SellCity { get; init; } = "";
    public int InstantSellPrice { get; init; }
    public int InstantNetProfit { get; init; }
    public double InstantRoiPercent { get; init; }
    public int ListedNetProfit { get; init; }
    public double ListedRoiPercent { get; init; }
    public double WorstAgeHours { get; init; }
    public string Source { get; init; } = "";

    public System.Windows.Media.Brush InstantBrush =>
        InstantNetProfit > 0 ? System.Windows.Media.Brushes.MediumSpringGreen
                             : System.Windows.Media.Brushes.IndianRed;

    public System.Windows.Media.Brush ListedBrush =>
        ListedNetProfit > 0 ? System.Windows.Media.Brushes.MediumSpringGreen
                            : System.Windows.Media.Brushes.IndianRed;
}

public sealed class PriceRow
{
    public string ItemId { get; init; } = "";
    public string City { get; init; } = "";
    public int SellPriceMin { get; init; }
    public int BuyPriceMax { get; init; }
    public string SellDateText { get; init; } = "";
    public string BuyDateText { get; init; } = "";
    public string Source { get; init; } = "";
}

public sealed class OrderRow
{
    public string TimeText { get; init; } = "";
    public string ItemId { get; init; } = "";
    public string City { get; init; } = "";
    public string Side { get; init; } = "";
    public int Price { get; init; }
    public int Amount { get; init; }
    public string Source { get; init; } = "";
}

public sealed class MainViewModel
{
    public ObservableCollection<FlipRow> Flips { get; } = new();
    public ObservableCollection<PriceRow> Prices { get; } = new();
    public ObservableCollection<OrderRow> Orders { get; } = new();

    public Dictionary<string, string> ItemNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string NameOf(string itemId) =>
        ItemNames.TryGetValue(itemId, out var n) ? n : "";

    public FlipRow ToRow(Flip f) => new()
    {
        ItemId = f.ItemId,
        Name = NameOf(f.ItemId),
        Quality = f.Quality,
        BuyCity = f.BuyCity,
        BuyPrice = f.BuyPrice,
        SellCity = f.SellCity,
        InstantSellPrice = f.InstantSellPrice,
        InstantNetProfit = f.InstantNetProfit,
        InstantRoiPercent = f.InstantRoiPercent,
        ListedNetProfit = f.ListedNetProfit,
        ListedRoiPercent = f.ListedRoiPercent,
        WorstAgeHours = f.WorstAgeHours,
        Source = f.Source
    };

    public PriceRow ToRow(MarketPrice p) => new()
    {
        ItemId = p.ItemId,
        City = p.City,
        SellPriceMin = p.SellPriceMin,
        BuyPriceMax = p.BuyPriceMax,
        SellDateText = Fmt(p.SellPriceMinDate),
        BuyDateText = Fmt(p.BuyPriceMaxDate),
        Source = p.Source
    };

    public OrderRow ToRow(StoredOrder o) => new()
    {
        TimeText = o.LastSeenUtc == DateTime.MinValue ? "" : o.LastSeenUtc.ToLocalTime().ToString("HH:mm:ss"),
        ItemId = o.ItemId,
        City = o.City,
        Side = o.IsOffer ? "VENDA" : "COMPRA",
        Price = o.Price,
        Amount = o.Amount,
        Source = o.Source
    };

    private static string Fmt(DateTime dt) =>
        dt == DateTime.MinValue ? "-" : dt.ToLocalTime().ToString("dd/MM HH:mm");
}
