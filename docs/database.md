# Database

File: `Dades\market.db` (SQLite 3, single file).

Open it with [DB Browser for SQLite](https://sqlitebrowser.org/) or from Python:

```python
import sqlite3
c = sqlite3.connect(r"C:\Users\alber\OneDrive\Documents\Albert\OpenCode\Albion Data Project\Dades\market.db")
print(c.execute("select count(*) from prices").fetchone())
```

## Schema

### `prices` — aggregates per item, city and quality

| Column | Type | Notes |
|---|---|---|
| `item_id` | TEXT | `T4_BAG`, `T6_CAPE@3`… |
| `city` | TEXT | city name |
| `quality` | INTEGER | 1–5 |
| `sell_price_min` | INTEGER | cheapest sell order |
| `sell_price_min_date` | TEXT | UTC |
| `sell_price_max` | INTEGER | most expensive sell order |
| `sell_price_max_date` | TEXT | UTC |
| `buy_price_min` | INTEGER | cheapest buy order |
| `buy_price_min_date` | TEXT | UTC |
| `buy_price_max` | INTEGER | highest buy order |
| `buy_price_max_date` | TEXT | UTC |
| `updated_at` | TEXT | when the app touched it |
| `source` | TEXT | `api`, `ingest`, `nats` |

Primary key: `(item_id, city, quality)`.

### `orders` — individual orders

| Column | Notes |
|---|---|
| `id` | in-game order id |
| `item_id`, `location_id`, `quality`, `enchantment` | identification |
| `price`, `amount` | unit price and quantity |
| `auction_type` | `offer` / `request` |
| `expires_utc` | expiry |
| `first_seen_utc`, `last_seen_utc` | tracking |
| `source` | `ingest`, `nats` |

Indexes on `(item_id, location_id)` and `last_seen_utc`. Expired orders are kept as
history.

### `items` — catalogue

`item_id`, `name`, `tier`, `enchant`. Downloaded once and reused.

### `settings` — preferences

Text key/value. Keys: `region`, `tax`, `setup`, `maxAge`, `minRoi`, `maxInvestment`,
`includeListed`, `clientPath`, `alsoPublic`, `watchlist`.

## Journal mode

The file lives inside OneDrive. A half-synced `-wal` file can corrupt the database, so
when the path contains `OneDrive`/`Dropbox`/`Google Drive` the app uses:

```sql
PRAGMA journal_mode = DELETE;
PRAGMA synchronous  = FULL;
PRAGMA busy_timeout = 5000;
```

Outside the cloud it uses WAL (`journal_mode=WAL`, `synchronous=NORMAL`). Nothing to
configure: `MarketDatabase` decides automatically.

!!! tip "Backups"
    With `journal_mode=DELETE`, copying `market.db` while the app is closed already
    yields a consistent copy. `Dades\backup\` holds the state from before the 2 Nov 2026
    merge.

## Real sizes (2 Nov 2026)

| Table | Rows |
|---|---|
| `prices` | ~8,100 |
| `orders` | ~18,700 |
| `items` | ~11,100 |

File: ~7.9 MB.

## Useful queries

```sql
-- 20 most recent prices
SELECT item_id, city, quality, sell_price_min, buy_price_max, updated_at
FROM prices ORDER BY updated_at DESC LIMIT 20;

-- Live orders for one item
SELECT location_id, auction_type, price, amount, expires_utc
FROM orders WHERE item_id = 'T8_BAG' ORDER BY price;

-- Where prices came from
SELECT source, count(*) FROM prices GROUP BY source;
```
