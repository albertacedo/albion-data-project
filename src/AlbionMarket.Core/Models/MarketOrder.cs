using System.Text.Json.Serialization;

namespace AlbionMarket.Core.Models;

/// <summary>
/// Payload the local client POSTs to http://localhost:PORT/marketorders.ingest.
/// Field names must match albiondata-client/lib/market.go (Go struct tags).
/// </summary>
public sealed class MarketUpload
{
    [JsonPropertyName("Orders")] public List<MarketOrder> Orders { get; set; } = new();
}

public sealed class MarketOrder
{
    [JsonPropertyName("Id")] public long Id { get; set; }
    [JsonPropertyName("ItemTypeId")] public string ItemId { get; set; } = "";
    [JsonPropertyName("ItemGroupTypeId")] public string GroupTypeId { get; set; } = "";
    [JsonPropertyName("LocationId")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string LocationId { get; set; } = "";
    [JsonPropertyName("QualityLevel")] public int Quality { get; set; } = 1;
    [JsonPropertyName("EnchantmentLevel")] public int Enchantment { get; set; }
    [JsonPropertyName("UnitPriceSilver")] public int Price { get; set; }
    [JsonPropertyName("Amount")] public int Amount { get; set; }
    [JsonPropertyName("AuctionType")] public string AuctionType { get; set; } = "";
    [JsonPropertyName("Expires")] public string Expires { get; set; } = "";

    [JsonIgnore] public string City => Locations.Name(LocationId);

    /// <summary>True for a sell order (someone offers the item).</summary>
    [JsonIgnore] public bool IsOffer =>
        AuctionType.Contains("offer", StringComparison.OrdinalIgnoreCase) ||
        AuctionType.Contains("sell", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore] public DateTime ExpiresUtc => DateParsing.ParseUtc(Expires);
}

/// <summary>A market order as persisted locally (from the sniffer or the NATS feed).</summary>
public sealed class StoredOrder
{
    public long Id { get; init; }
    public string ItemId { get; init; } = "";
    public string LocationId { get; init; } = "";
    public int Quality { get; init; } = 1;
    public int Enchantment { get; init; }
    public int Price { get; init; }
    public int Amount { get; init; }
    public string AuctionType { get; init; } = "";
    public DateTime ExpiresUtc { get; init; }
    public DateTime FirstSeenUtc { get; init; }
    public DateTime LastSeenUtc { get; init; }
    public string Source { get; init; } = "sniffer";

    public string City => Locations.Name(LocationId);
    public bool IsOffer => AuctionType.Contains("offer", StringComparison.OrdinalIgnoreCase) ||
                           AuctionType.Contains("sell", StringComparison.OrdinalIgnoreCase);
}
