# Base de dades

Fitxer: `Dades\market.db` (SQLite 3, un sol fitxer).

Es pot obrir amb [DB Browser for SQLite](https://sqlitebrowser.org/) o des de Python:

```python
import sqlite3
c = sqlite3.connect(r"C:\Users\alber\OneDrive\Documents\Albert\OpenCode\Albion Data Project\Dades\market.db")
print(c.execute("select count(*) from prices").fetchone())
```

## Esquema

### `prices` — agregats per item, ciutat i qualitat

| Columna | Tipus | Notes |
|---|---|---|
| `item_id` | TEXT | `T4_BAG`, `T6_CAPE@3`… |
| `city` | TEXT | nom de la ciutat |
| `quality` | INTEGER | 1–5 |
| `sell_price_min` | INTEGER | ordre de venda més barata |
| `sell_price_min_date` | TEXT | UTC |
| `sell_price_max` | INTEGER | ordre de venda més cara |
| `sell_price_max_date` | TEXT | UTC |
| `buy_price_min` | INTEGER | ordre de compra més barata |
| `buy_price_min_date` | TEXT | UTC |
| `buy_price_max` | INTEGER | ordre de compra més alta |
| `buy_price_max_date` | TEXT | UTC |
| `updated_at` | TEXT | quan l'app l'ha tocat |
| `source` | TEXT | `api`, `ingest`, `nats` |

Clau primària: `(item_id, city, quality)`.

### `orders` — ordres individuals

| Columna | Notes |
|---|---|
| `id` | id de l'ordre al joc |
| `item_id`, `location_id`, `quality`, `enchantment` | identificació |
| `price`, `amount` | preu unitari i quantitat |
| `auction_type` | `offer` / `request` |
| `expires_utc` | caducitat |
| `first_seen_utc`, `last_seen_utc` | seguiment |
| `source` | `ingest`, `nats` |

Índexs per `(item_id, location_id)` i per `last_seen_utc`. Les ordres caducades no
s'esborren: serveixen d'historial.

### `items` — catàleg

`item_id`, `name`, `tier`, `enchant`. Es baixa una vegada i es reutilitza.

### `settings` — preferències

Clau/valor en text. Claus: `region`, `tax`, `setup`, `maxAge`, `minRoi`,
`maxInvestment`, `includeListed`, `clientPath`, `alsoPublic`, `watchlist`.

## Mode de journal

El fitxer viu dins d'OneDrive. Un `-wal` sincronitzat a mitges pot corrompre la BD, així
que quan el camí conté `OneDrive`/`Dropbox`/`Google Drive` l'app fa servir:

```sql
PRAGMA journal_mode = DELETE;
PRAGMA synchronous  = FULL;
PRAGMA busy_timeout = 5000;
```

Fora del núvol es fa servir WAL (`journal_mode=WAL`, `synchronous=NORMAL`). No cal fer
res: es decideix sol a `MarketDatabase`.

!!! tip "Còpia de seguretat"
    Amb journal `DELETE`, copiar `market.db` en fred ja és una còpia coherent. A
    `Dades\backup\` hi ha una còpia de l'estat anterior a la unificació del 2/11/2026.

## Mides reals (2/11/2026)

| Taula | Files |
|---|---|
| `prices` | ~8.100 |
| `orders` | ~18.700 |
| `items` | ~11.100 |

Fitxer: ~7,9 MB.

## Consultes útils

```sql
-- Els 20 preus més recents
SELECT item_id, city, quality, sell_price_min, buy_price_max, updated_at
FROM prices ORDER BY updated_at DESC LIMIT 20;

-- Ordres vives d'un item
SELECT location_id, auction_type, price, amount, expires_utc
FROM orders WHERE item_id = 'T8_BAG' ORDER BY price;

-- Què ha arribat per cada font
SELECT source, count(*) FROM prices GROUP BY source;
```
