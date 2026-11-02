namespace AlbionMarket.Core.Models;

/// <summary>
/// A cross-city opportunity: buy at <see cref="BuyCity"/> and sell at <see cref="SellCity"/>.
/// Two flavours are computed:
///  * Instant  - sell into an existing buy order (BuyPriceMax). Lower margin, almost always fills.
///  * Listed   - place your own sell order at SellPriceMin (undercutting). Higher margin, may not fill.
/// </summary>
public sealed class Flip
{
    public string ItemId { get; init; } = "";
    public int Quality { get; init; } = 1;

    public string BuyCity { get; init; } = "";
    public int BuyPrice { get; init; }

    public string SellCity { get; init; } = "";
    public int InstantSellPrice { get; init; }
    public int ListedSellPrice { get; init; }

    public int InstantNetProfit { get; init; }
    public int ListedNetProfit { get; init; }

    public double InstantRoiPercent { get; init; }
    public double ListedRoiPercent { get; init; }

    public double BuyAgeHours { get; init; }
    public double SellAgeHours { get; init; }
    public double WorstAgeHours => Math.Max(BuyAgeHours, SellAgeHours);

    /// <summary>"api", "sniffer" or "mixed" - where the two sides came from.</summary>
    public string Source { get; init; } = "api";

    public bool IsStale(double maxAgeHours) => WorstAgeHours > maxAgeHours;
}

public sealed class FlipOptions
{
    /// <summary>Market transaction tax. 4% with Premium, 8% without.</summary>
    public double TaxRate { get; set; } = 0.04;

    /// <summary>Fee paid when creating a sell order (only relevant for the "listed" flavour).</summary>
    public double SetupFee { get; set; } = 0.025;

    /// <summary>Ignore any side of the trade older than this.</summary>
    public double MaxAgeHours { get; set; } = 48;

    /// <summary>Only keep opportunities above this return on investment.</summary>
    public double MinRoiPercent { get; set; } = 10;

    /// <summary>Capital you are willing to put into a single trade (0 = no limit).</summary>
    public int MaxInvestment { get; set; } = 0;

    /// <summary>Also compute the "list it yourself" flavour.</summary>
    public bool IncludeListed { get; set; } = true;

    /// <summary>Cities excluded as a buy source (e.g. the one you are sitting in).</summary>
    public HashSet<string> ExcludedBuyCities { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
