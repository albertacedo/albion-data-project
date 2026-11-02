using AlbionMarket.Core.Models;

namespace AlbionMarket.Core.Engine;

/// <summary>
/// Turns a set of price rows into ranked cross-city opportunities.
///
/// The important detail most tools get wrong: you buy at <c>sell_price_min</c>
/// (someone's sell order) and you sell at <c>buy_price_max</c> (someone's buy
/// order). Those are different books, and each one carries its own timestamp.
/// </summary>
public static class FlipAnalyzer
{
    public static List<Flip> Analyze(
        IEnumerable<MarketPrice> prices,
        FlipOptions options,
        DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var flips = new List<Flip>();

        foreach (var group in prices.GroupBy(p => (p.ItemId, p.Quality)))
        {
            var rows = group
                .Where(p => !options.ExcludedBuyCities.Contains(p.City))
                .ToList();
            if (rows.Count < 2) continue;

            var buys = rows
                .Where(p => p.SellPriceMin > 0 && AgeHours(p.SellPriceMinDate, now) <= options.MaxAgeHours)
                .ToList();
            var sells = rows
                .Where(p => p.BuyPriceMax > 0 && AgeHours(p.BuyPriceMaxDate, now) <= options.MaxAgeHours)
                .ToList();
            if (buys.Count == 0 || sells.Count == 0) continue;

            var buy = buys.MinBy(p => p.SellPriceMin)!;
            if (options.MaxInvestment > 0 && buy.SellPriceMin > options.MaxInvestment) continue;

            // Instant: sell into the best existing buy order in another city.
            var instant = sells
                .Where(p => !SameCity(p.City, buy.City))
                .MaxBy(p => p.BuyPriceMax);
            if (instant is null) continue;

            // Listed: undercut the cheapest sell order in that other city.
            var listed = rows
                .Where(p => !SameCity(p.City, buy.City) && p.SellPriceMin > 0 &&
                            AgeHours(p.SellPriceMinDate, now) <= options.MaxAgeHours)
                .MaxBy(p => p.SellPriceMin);

            var instantNet = NetAfterTaxes(instant.BuyPriceMax, buy.SellPriceMin, options.TaxRate, 0);
            var instantRoi = Roi(instantNet, buy.SellPriceMin);

            var listedNet = listed is null
                ? 0
                : NetAfterTaxes(listed.SellPriceMin, buy.SellPriceMin, options.TaxRate, options.SetupFee);
            var listedRoi = listed is null ? 0 : Roi(listedNet, buy.SellPriceMin);

            var passesInstant = instantNet > 0 && instantRoi >= options.MinRoiPercent;
            var passesListed = options.IncludeListed && listedNet > 0 && listedRoi >= options.MinRoiPercent;
            if (!passesInstant && !passesListed) continue;

            flips.Add(new Flip
            {
                ItemId = buy.ItemId,
                Quality = buy.Quality,
                BuyCity = buy.City,
                BuyPrice = buy.SellPriceMin,
                SellCity = instant.City,
                InstantSellPrice = instant.BuyPriceMax,
                ListedSellPrice = listed?.SellPriceMin ?? 0,
                InstantNetProfit = instantNet,
                ListedNetProfit = listedNet,
                InstantRoiPercent = Math.Round(instantRoi, 1),
                ListedRoiPercent = Math.Round(listedRoi, 1),
                BuyAgeHours = Math.Round(AgeHours(buy.SellPriceMinDate, now), 1),
                SellAgeHours = Math.Round(AgeHours(instant.BuyPriceMaxDate, now), 1),
                Source = CombineSources(buy, instant)
            });
        }

        return flips
            .OrderByDescending(f => Math.Max(f.InstantNetProfit, f.ListedNetProfit))
            .ThenByDescending(f => f.InstantRoiPercent)
            .ToList();
    }

    public static double AgeHours(DateTime dateUtc, DateTime nowUtc) =>
        dateUtc == DateTime.MinValue ? double.MaxValue : Math.Max(0, (nowUtc - dateUtc).TotalHours);

    /// <summary>Net profit per unit after market tax (and the sell-order setup fee).</summary>
    public static int NetAfterTaxes(int sellPrice, int buyPrice, double taxRate, double setupFee)
        => (int)Math.Floor(sellPrice * (1.0 - taxRate - setupFee)) - buyPrice;

    public static double Roi(int netProfit, int cost) =>
        cost <= 0 ? 0 : netProfit * 100.0 / cost;

    private static bool SameCity(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string CombineSources(params MarketPrice[] rows)
    {
        var set = rows.Select(r => r.Source).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToArray();
        return set.Length switch
        {
            0 => "api",
            1 => set[0],
            _ => "mixed"
        };
    }
}
