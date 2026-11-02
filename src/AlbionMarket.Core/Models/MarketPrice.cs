using System.Globalization;
using System.Text.Json.Serialization;

namespace AlbionMarket.Core.Models;

/// <summary>Game server / region. Each one has its own API host.</summary>
public enum AlbionRegion
{
    Europe,
    Americas,
    Asia
}

public static class AlbionHosts
{
    public static string ApiBaseUrl(this AlbionRegion region) => region switch
    {
        AlbionRegion.Americas => "https://west.albion-online-data.com",
        AlbionRegion.Asia => "https://east.albion-online-data.com",
        _ => "https://europe.albion-online-data.com"
    };

    public static string PublicPowIngest(this AlbionRegion region) => region switch
    {
        AlbionRegion.Americas => "https+pow://pow.west.albion-online-data.com",
        AlbionRegion.Asia => "https+pow://pow.east.albion-online-data.com",
        _ => "https+pow://pow.europe.albion-online-data.com"
    };

    public static int NatsPort(this AlbionRegion region) => region switch
    {
        AlbionRegion.Americas => 4222,
        AlbionRegion.Asia => 24222,
        _ => 34222
    };

    public static string NatsUrl(this AlbionRegion region) =>
        $"nats://public:thenewalbiondata@nats.albion-online-data.com:{region.NatsPort()}";
}

/// <summary>
/// One aggregated price row exactly as returned by /api/v2/stats/prices.
/// A value of 0 means "no order of that kind in that city".
/// </summary>
public sealed class MarketPrice
{
    [JsonPropertyName("item_id")] public string ItemId { get; set; } = "";
    [JsonPropertyName("city")] public string City { get; set; } = "";
    [JsonPropertyName("quality")] public int Quality { get; set; } = 1;

    // Cheapest sell order: what you pay if you buy right now.
    [JsonPropertyName("sell_price_min")] public int SellPriceMin { get; set; }
    [JsonPropertyName("sell_price_min_date")] public DateTime SellPriceMinDate { get; set; }

    // Most expensive sell order: useful as a reference, rarely tradeable.
    [JsonPropertyName("sell_price_max")] public int SellPriceMax { get; set; }
    [JsonPropertyName("sell_price_max_date")] public DateTime SellPriceMaxDate { get; set; }

    // Cheapest buy order: if you sell right now you get undercut by this one.
    [JsonPropertyName("buy_price_min")] public int BuyPriceMin { get; set; }
    [JsonPropertyName("buy_price_min_date")] public DateTime BuyPriceMinDate { get; set; }

    // Highest buy order: what you receive if you sell instantly into the market.
    [JsonPropertyName("buy_price_max")] public int BuyPriceMax { get; set; }
    [JsonPropertyName("buy_price_max_date")] public DateTime BuyPriceMaxDate { get; set; }

    [JsonIgnore] public bool HasSell => SellPriceMin > 0;
    [JsonIgnore] public bool HasBuy => BuyPriceMax > 0;

    /// <summary>"api", "sniffer" or "mixed" - set when read back from the local database.</summary>
    [JsonIgnore] public string Source { get; set; } = "api";
}

/// <summary>Maps the numeric location ids used by the game packets to display names.</summary>
public static class Locations
{
    public static readonly IReadOnlyDictionary<string, string> ById = new Dictionary<string, string>
    {
        ["7"] = "Thetford",
        ["1002"] = "Lymhurst",
        ["2004"] = "Bridgewatch",
        ["3005"] = "Caerleon",
        ["3008"] = "Martlock",
        ["4002"] = "Fort Sterling",
        ["5003"] = "Brecilien",
        ["3003"] = "Black Market"
    };

    public static readonly string[] MainCities =
    {
        "Bridgewatch", "Caerleon", "Fort Sterling", "Lymhurst",
        "Martlock", "Thetford", "Brecilien", "Black Market"
    };

    public static string Name(string locationIdOrName) =>
        ById.TryGetValue(locationIdOrName, out var name) ? name : locationIdOrName;

    public static string Id(string cityName) =>
        ById.FirstOrDefault(kv => string.Equals(kv.Value, cityName, StringComparison.OrdinalIgnoreCase)).Key
        ?? cityName;
}

public static class DateParsing
{
    /// <summary>
    /// The API returns naive UTC timestamps ("2026-11-02T04:40:00") and the
    /// game client sends C# ticks in a few fields. Everything we store is UTC.
    /// </summary>
    public static DateTime ParseUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("0001-01-01", StringComparison.Ordinal))
            return DateTime.MinValue;

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            return FromTicks(ticks);

        return DateTime.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : DateTime.MinValue;
    }

    public static DateTime FromTicks(long ticks)
    {
        if (ticks <= 0) return DateTime.MinValue;
        try { return new DateTime(ticks, DateTimeKind.Utc); }
        catch { return DateTime.MinValue; }
    }
}
