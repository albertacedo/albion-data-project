using System.Globalization;
using AlbionMarket.Core.Models;
using Microsoft.Data.Sqlite;

namespace AlbionMarket.Core.Data;

/// <summary>
/// Local store. Everything is UTC. The database is a plain SQLite file so you can
/// inspect it with DB Browser or query it from Python whenever you want.
/// </summary>
public sealed class MarketDatabase
{
    private readonly string _connectionString;

    public string FilePath { get; }

    public MarketDatabase(string filePath)
    {
        FilePath = filePath;
        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = filePath,
            DefaultTimeout = 30,
            Pooling = true
        }.ToString();

        Initialize();
    }

    private SqliteConnection Open()
    {
        var cn = new SqliteConnection(_connectionString);
        cn.Open();
        using var pragma = cn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        pragma.ExecuteNonQuery();
        return cn;
    }

    private void Initialize()
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS prices (
                item_id            TEXT    NOT NULL,
                city               TEXT    NOT NULL,
                quality            INTEGER NOT NULL,
                sell_price_min      INTEGER NOT NULL DEFAULT 0,
                sell_price_min_date TEXT    NOT NULL DEFAULT '',
                sell_price_max      INTEGER NOT NULL DEFAULT 0,
                sell_price_max_date TEXT    NOT NULL DEFAULT '',
                buy_price_min       INTEGER NOT NULL DEFAULT 0,
                buy_price_min_date  TEXT    NOT NULL DEFAULT '',
                buy_price_max       INTEGER NOT NULL DEFAULT 0,
                buy_price_max_date  TEXT    NOT NULL DEFAULT '',
                updated_at          TEXT    NOT NULL,
                source              TEXT    NOT NULL DEFAULT 'api',
                PRIMARY KEY (item_id, city, quality)
            );

            CREATE TABLE IF NOT EXISTS orders (
                id             INTEGER NOT NULL PRIMARY KEY,
                item_id        TEXT    NOT NULL,
                location_id    TEXT    NOT NULL,
                quality        INTEGER NOT NULL,
                enchantment    INTEGER NOT NULL,
                price          INTEGER NOT NULL,
                amount         INTEGER NOT NULL,
                auction_type   TEXT    NOT NULL,
                expires_utc    TEXT    NOT NULL,
                first_seen_utc TEXT    NOT NULL,
                last_seen_utc  TEXT    NOT NULL,
                source         TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_orders_item ON orders(item_id, location_id);
            CREATE INDEX IF NOT EXISTS ix_orders_seen ON orders(last_seen_utc);

            CREATE TABLE IF NOT EXISTS items (
                item_id TEXT NOT NULL PRIMARY KEY,
                name    TEXT NOT NULL,
                tier    INTEGER,
                enchant INTEGER
            );

            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT NOT NULL PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    // ---------------------------------------------------------------- prices

    private const string MergePriceSql = """
        INSERT INTO prices (
            item_id, city, quality,
            sell_price_min, sell_price_min_date, sell_price_max, sell_price_max_date,
            buy_price_min, buy_price_min_date, buy_price_max, buy_price_max_date,
            updated_at, source)
        VALUES (
            $item, $city, $quality,
            $spmin, $spmind, $spmax, $spmaxd,
            $bpmin, $bpmind, $bpmax, $bpmaxd,
            $updated, $source)
        ON CONFLICT(item_id, city, quality) DO UPDATE SET
            sell_price_min = CASE WHEN excluded.sell_price_min > 0
                                   AND excluded.sell_price_min_date >= prices.sell_price_min_date
                                  THEN excluded.sell_price_min ELSE prices.sell_price_min END,
            sell_price_min_date = CASE WHEN excluded.sell_price_min > 0
                                   AND excluded.sell_price_min_date >= prices.sell_price_min_date
                                  THEN excluded.sell_price_min_date ELSE prices.sell_price_min_date END,
            sell_price_max = CASE WHEN excluded.sell_price_max > 0
                                   AND excluded.sell_price_max_date >= prices.sell_price_max_date
                                  THEN excluded.sell_price_max ELSE prices.sell_price_max END,
            sell_price_max_date = CASE WHEN excluded.sell_price_max > 0
                                   AND excluded.sell_price_max_date >= prices.sell_price_max_date
                                  THEN excluded.sell_price_max_date ELSE prices.sell_price_max_date END,
            buy_price_min = CASE WHEN excluded.buy_price_min > 0
                                   AND excluded.buy_price_min_date >= prices.buy_price_min_date
                                  THEN excluded.buy_price_min ELSE prices.buy_price_min END,
            buy_price_min_date = CASE WHEN excluded.buy_price_min > 0
                                   AND excluded.buy_price_min_date >= prices.buy_price_min_date
                                  THEN excluded.buy_price_min_date ELSE prices.buy_price_min_date END,
            buy_price_max = CASE WHEN excluded.buy_price_max > 0
                                   AND excluded.buy_price_max_date >= prices.buy_price_max_date
                                  THEN excluded.buy_price_max ELSE prices.buy_price_max END,
            buy_price_max_date = CASE WHEN excluded.buy_price_max > 0
                                   AND excluded.buy_price_max_date >= prices.buy_price_max_date
                                  THEN excluded.buy_price_max_date ELSE prices.buy_price_max_date END,
            updated_at = excluded.updated_at,
            source = CASE WHEN prices.source = excluded.source THEN prices.source ELSE 'mixed' END;
        """;

    /// <summary>
    /// Merges a batch of price rows. A zero ignores the field, and an older
    /// timestamp never overwrites a newer one - so API snapshots and sniffer
    /// data can live in the same table without fighting.
    /// </summary>
    public async Task MergePricesAsync(IEnumerable<MarketPrice> rows, string source, CancellationToken ct = default)
    {
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = MergePriceSql;

        var pItem = cmd.Parameters.Add("$item", SqliteType.Text);
        var pCity = cmd.Parameters.Add("$city", SqliteType.Text);
        var pQ = cmd.Parameters.Add("$quality", SqliteType.Integer);
        var pSpMin = cmd.Parameters.Add("$spmin", SqliteType.Integer);
        var pSpMinD = cmd.Parameters.Add("$spmind", SqliteType.Text);
        var pSpMax = cmd.Parameters.Add("$spmax", SqliteType.Integer);
        var pSpMaxD = cmd.Parameters.Add("$spmaxd", SqliteType.Text);
        var pBpMin = cmd.Parameters.Add("$bpmin", SqliteType.Integer);
        var pBpMinD = cmd.Parameters.Add("$bpmind", SqliteType.Text);
        var pBpMax = cmd.Parameters.Add("$bpmax", SqliteType.Integer);
        var pBpMaxD = cmd.Parameters.Add("$bpmaxd", SqliteType.Text);
        var pUpdated = cmd.Parameters.Add("$updated", SqliteType.Text);
        var pSource = cmd.Parameters.Add("$source", SqliteType.Text);

        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.ItemId) || string.IsNullOrWhiteSpace(r.City)) continue;

            pItem.Value = r.ItemId;
            pCity.Value = r.City;
            pQ.Value = r.Quality <= 0 ? 1 : r.Quality;
            pSpMin.Value = r.SellPriceMin;
            pSpMinD.Value = Iso(r.SellPriceMinDate);
            pSpMax.Value = r.SellPriceMax;
            pSpMaxD.Value = Iso(r.SellPriceMaxDate);
            pBpMin.Value = r.BuyPriceMin;
            pBpMinD.Value = Iso(r.BuyPriceMinDate);
            pBpMax.Value = r.BuyPriceMax;
            pBpMaxD.Value = Iso(r.BuyPriceMaxDate);
            pUpdated.Value = now;
            pSource.Value = source;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        tx.Commit();
    }

    public async Task<List<MarketPrice>> GetPricesAsync(
        string? itemId = null, string? city = null, int? quality = null, CancellationToken ct = default)
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT * FROM prices WHERE 1=1";
        if (!string.IsNullOrWhiteSpace(itemId)) { cmd.CommandText += " AND item_id = $item"; cmd.Parameters.AddWithValue("$item", itemId); }
        if (!string.IsNullOrWhiteSpace(city)) { cmd.CommandText += " AND city = $city"; cmd.Parameters.AddWithValue("$city", city); }
        if (quality is > 0) { cmd.CommandText += " AND quality = $q"; cmd.Parameters.AddWithValue("$q", quality); }
        cmd.CommandText += " ORDER BY item_id, city;";

        var list = new List<MarketPrice>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new MarketPrice
            {
                ItemId = reader.GetString(reader.GetOrdinal("item_id")),
                City = reader.GetString(reader.GetOrdinal("city")),
                Quality = reader.GetInt32(reader.GetOrdinal("quality")),
                SellPriceMin = reader.GetInt32(reader.GetOrdinal("sell_price_min")),
                SellPriceMinDate = ParseIso(reader.GetString(reader.GetOrdinal("sell_price_min_date"))),
                SellPriceMax = reader.GetInt32(reader.GetOrdinal("sell_price_max")),
                SellPriceMaxDate = ParseIso(reader.GetString(reader.GetOrdinal("sell_price_max_date"))),
                BuyPriceMin = reader.GetInt32(reader.GetOrdinal("buy_price_min")),
                BuyPriceMinDate = ParseIso(reader.GetString(reader.GetOrdinal("buy_price_min_date"))),
                BuyPriceMax = reader.GetInt32(reader.GetOrdinal("buy_price_max")),
                BuyPriceMaxDate = ParseIso(reader.GetString(reader.GetOrdinal("buy_price_max_date"))),
                Source = reader.GetString(reader.GetOrdinal("source"))
            });
        }
        return list;
    }

    public async Task<long> CountPricesAsync(CancellationToken ct = default)
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM prices WHERE sell_price_min > 0 OR buy_price_max > 0;";
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }

    // ---------------------------------------------------------------- orders

    public async Task<int> SaveOrdersAsync(IEnumerable<MarketOrder> orders, string source, CancellationToken ct = default)
    {
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO orders (id, item_id, location_id, quality, enchantment, price, amount,
                                auction_type, expires_utc, first_seen_utc, last_seen_utc, source)
            VALUES ($id, $item, $loc, $q, $ench, $price, $amount, $type, $expires, $now, $now, $source)
            ON CONFLICT(id) DO UPDATE SET
                price        = excluded.price,
                amount       = excluded.amount,
                expires_utc  = excluded.expires_utc,
                last_seen_utc = excluded.last_seen_utc;
            """;

        var pId = cmd.Parameters.Add("$id", SqliteType.Integer);
        var pItem = cmd.Parameters.Add("$item", SqliteType.Text);
        var pLoc = cmd.Parameters.Add("$loc", SqliteType.Text);
        var pQ = cmd.Parameters.Add("$q", SqliteType.Integer);
        var pEnch = cmd.Parameters.Add("$ench", SqliteType.Integer);
        var pPrice = cmd.Parameters.Add("$price", SqliteType.Integer);
        var pAmount = cmd.Parameters.Add("$amount", SqliteType.Integer);
        var pType = cmd.Parameters.Add("$type", SqliteType.Text);
        var pExpires = cmd.Parameters.Add("$expires", SqliteType.Text);
        var pNow = cmd.Parameters.Add("$now", SqliteType.Text);
        var pSource = cmd.Parameters.Add("$source", SqliteType.Text);

        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        pNow.Value = now;
        pSource.Value = source;

        var n = 0;
        foreach (var o in orders)
        {
            if (o.Id == 0 || string.IsNullOrWhiteSpace(o.ItemId)) continue;
            pId.Value = o.Id;
            pItem.Value = o.ItemId;
            pLoc.Value = o.LocationId;
            pQ.Value = o.Quality <= 0 ? 1 : o.Quality;
            pEnch.Value = o.Enchantment;
            pPrice.Value = o.Price;
            pAmount.Value = o.Amount;
            pType.Value = o.AuctionType;
            pExpires.Value = Iso(o.ExpiresUtc);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            n++;
        }

        tx.Commit();
        return n;
    }

    /// <summary>
    /// Recomputes the price aggregate for every (item, city, quality) touched by
    /// the incoming orders. This is what turns raw orders into the same shape the
    /// REST API returns, so the flip engine does not care where data came from.
    /// </summary>
    public async Task RebuildPricesFromOrdersAsync(IEnumerable<MarketOrder> orders, CancellationToken ct = default)
    {
        var groups = orders
            .Where(o => !string.IsNullOrWhiteSpace(o.ItemId))
            .GroupBy(o => (o.ItemId, City: Locations.Name(o.LocationId), Quality: o.Quality <= 0 ? 1 : o.Quality));

        var rows = new List<MarketPrice>();
        var now = DateTime.UtcNow;

        foreach (var g in groups)
        {
            var offers = g.Where(o => o.IsOffer && o.Price > 0).ToList();
            var requests = g.Where(o => !o.IsOffer && o.Price > 0).ToList();
            if (offers.Count == 0 && requests.Count == 0) continue;

            rows.Add(new MarketPrice
            {
                ItemId = g.Key.ItemId,
                City = g.Key.City,
                Quality = g.Key.Quality,
                SellPriceMin = offers.Count > 0 ? offers.Min(o => o.Price) : 0,
                SellPriceMinDate = offers.Count > 0 ? now : DateTime.MinValue,
                SellPriceMax = offers.Count > 0 ? offers.Max(o => o.Price) : 0,
                SellPriceMaxDate = offers.Count > 0 ? now : DateTime.MinValue,
                BuyPriceMin = requests.Count > 0 ? requests.Min(o => o.Price) : 0,
                BuyPriceMinDate = requests.Count > 0 ? now : DateTime.MinValue,
                BuyPriceMax = requests.Count > 0 ? requests.Max(o => o.Price) : 0,
                BuyPriceMaxDate = requests.Count > 0 ? now : DateTime.MinValue
            });
        }

        if (rows.Count > 0)
            await MergePricesAsync(rows, "sniffer", ct).ConfigureAwait(false);
    }

    public async Task<List<StoredOrder>> GetRecentOrdersAsync(int limit = 200, CancellationToken ct = default)
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT * FROM orders ORDER BY last_seen_utc DESC LIMIT $limit;";
        cmd.Parameters.AddWithValue("$limit", limit);

        var list = new List<StoredOrder>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new StoredOrder
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                ItemId = reader.GetString(reader.GetOrdinal("item_id")),
                LocationId = reader.GetString(reader.GetOrdinal("location_id")),
                Quality = reader.GetInt32(reader.GetOrdinal("quality")),
                Enchantment = reader.GetInt32(reader.GetOrdinal("enchantment")),
                Price = reader.GetInt32(reader.GetOrdinal("price")),
                Amount = reader.GetInt32(reader.GetOrdinal("amount")),
                AuctionType = reader.GetString(reader.GetOrdinal("auction_type")),
                ExpiresUtc = ParseIso(reader.GetString(reader.GetOrdinal("expires_utc"))),
                FirstSeenUtc = ParseIso(reader.GetString(reader.GetOrdinal("first_seen_utc"))),
                LastSeenUtc = ParseIso(reader.GetString(reader.GetOrdinal("last_seen_utc"))),
                Source = reader.GetString(reader.GetOrdinal("source"))
            });
        }
        return list;
    }

    public async Task<long> CountOrdersAsync(CancellationToken ct = default)
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM orders;";
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }

    // ----------------------------------------------------------------- items

    public async Task SaveItemsAsync(IEnumerable<(string Id, string Name)> items, CancellationToken ct = default)
    {
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO items (item_id, name, tier, enchant) VALUES ($id, $name, $tier, $ench)
            ON CONFLICT(item_id) DO UPDATE SET name = excluded.name, tier = excluded.tier, enchant = excluded.enchant;
            """;
        var pId = cmd.Parameters.Add("$id", SqliteType.Text);
        var pName = cmd.Parameters.Add("$name", SqliteType.Text);
        var pTier = cmd.Parameters.Add("$tier", SqliteType.Integer);
        var pEnch = cmd.Parameters.Add("$ench", SqliteType.Integer);

        foreach (var (id, name) in items)
        {
            pId.Value = id;
            pName.Value = name;
            pTier.Value = (object?)TierOf(id) ?? DBNull.Value;
            pEnch.Value = (object?)EnchantOf(id) ?? DBNull.Value;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        tx.Commit();
    }

    public async Task<Dictionary<string, string>> GetItemNamesAsync(CancellationToken ct = default)
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT item_id, name FROM items;";
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            dict[reader.GetString(0)] = reader.GetString(1);
        return dict;
    }

    public async Task<long> CountItemsAsync(CancellationToken ct = default)
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items;";
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }

    // -------------------------------------------------------------- settings

    public string? GetSetting(string key)
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $k;";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string;
    }

    public void SetSetting(string key, string value)
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO settings (key, value) VALUES ($k, $v)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    // ----------------------------------------------------------------- utils

    private static string Iso(DateTime dt) =>
        dt == DateTime.MinValue
            ? DateTime.MinValue.ToString("o", CultureInfo.InvariantCulture)
            : dt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    private static DateTime ParseIso(string? s) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : DateTime.MinValue;

    private static int? TierOf(string itemId)
    {
        // T4_BAG -> 4 ; T8_2H_MAIN_SWORD@3 -> 8
        if (itemId.Length > 1 && (itemId[0] == 'T' || itemId[0] == 't') && char.IsDigit(itemId[1]))
            return itemId[1] - '0';
        return null;
    }

    private static int? EnchantOf(string itemId)
    {
        var i = itemId.IndexOf('@');
        if (i >= 0 && i + 1 < itemId.Length && char.IsDigit(itemId[i + 1]))
            return itemId[i + 1] - '0';
        return null;
    }
}
