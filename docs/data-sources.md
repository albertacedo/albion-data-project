# Data sources

The app combines three sources. All of them end up in the same `prices` (or `orders`)
table, and all of them go through the same filter before becoming opportunities.

## 1. AODP REST API

- Base: `https://{region}.albion-online-data.com/api/v2/…`
- Regions: `europe` (in use), `west`, `east`.
- Endpoints used:
  - `/stats/prices/{item1,item2,…}.json?locations=…&qualities=…` — up to 100 items per request.
  - `/stats/history/…` — planned for the history phase.
  - `/stats/gold.json` — planned.
- Catalogue: `items.json` (~11,000 names) stored in the `items` table.

### Rate limit (important)

On 2 November 2026 the Europe region answered:

```
ratelimit-limit: 1
ratelimit-remaining: 0
ratelimit-reset: 1793616000   (window aligned to :00, :10, :20…)
```

That is: **one request per 10-minute window** at that moment, far stricter than the
180 req/min the project documents. The app:

- batches up to 100 items per request,
- the background catalogue explorer sends one 100-item batch every 11 minutes and stores
  its cursor in SQLite, so it eventually covers the full ~11,000-item catalogue without
  hammering the API,
- waits at least 1.2 s between requests,
- on `429` waits until `ratelimit-reset` (up to 4 minutes),
- always shows cached data in the meantime.

!!! warning "Do not refresh in a loop"
    Every extra `fetch` can cost 10 minutes of blocking. For interactivity the sniffer and
    NATS already do the job.

The app's **watchlist refresh** and **catalogue explorer** are separate. The former
updates the configured watchlist immediately; the latter starts in the background and
walks the complete item catalogue in 100-item batches every 11 minutes. With 11,132
catalogue entries, one complete pass takes about 18 hours 40 minutes at the observed
Europe limit. The cursor survives restarts.


## 2. Own sniffer (local ingest)

- Protocol: plain HTTP against the app.
- Endpoint: `POST http://localhost:5099/marketorders.ingest`
- Body: the JSON produced by `albiondata-client` (`{"Orders":[…]}` with **string**
  `LocationId`).
- Health: `GET http://localhost:5099/health`.

!!! warning "Prices arrive in 1/10,000 silver"
    The game sends `UnitPriceSilver` in its internal unit: **10,000 units = 1 silver**.
    The public AODP ingest normalises it; because the app receives the raw client JSON,
    it divides every incoming sniffer price by 10,000 before storing. NATS and the REST
    API already arrive in silver and are **not** divided again. Any future importer of
    `file://` captures must divide too.

The official client (or the fork) must be started with:

```
albiondata-client.exe -i "http://localhost:5099" -nogui
```

`-nogui` is the fork's headless mode (no window, no tray). The app passes it when it
starts the client by itself.

Optionally, to also contribute to the public project:

```
albiondata-client.exe -i "http://localhost:5099,https+pow://pow.europe.albion-online-data.com" -nogui
```

## 3. Public NATS feed

```
nats://public:thenewalbiondata@nats.albion-online-data.com:34222     # Europe
```

| Subject | Content |
|---|---|
| `marketorders.deduped` | market orders (active subscription) |
| `markethistories.deduped` | sales history |
| `goldprices.deduped` | gold price |

Quirks the app already handles:

- each message contains **a single order**,
- `LocationId` arrives as a **number**, not a string,
- credentials are public, no sign-up needed.

## How they are merged

| Source | Table | Effect |
|---|---|---|
| API | `prices` | full snapshot; only overwrites if more recent |
| Sniffer | `orders` + `prices` | single order; recomputes that city's aggregate |
| NATS | `orders` + `prices` | like the sniffer, but real time and from everyone |

Aggregates are only computed for the **8 known cities**:

Thetford, Fort Sterling, Lymhurst, Bridgewatch, Martlock, Caerleon, Black Market,
Brecilien.

Hideouts and other locations are stored in `orders` but never enter the opportunity
calculation (they are not comparable markets).

## Quality and enchantment

Every price is stored per `(item_id, city, quality)`. Enchantment is part of the
`item_id` (`T6_BAG@3`). Opportunities are computed per item and quality.

## Taxes

| Concept | Default |
|---|---|
| Market fee | 4 % with Premium / 8 % without |
| Sell-order setup fee | 2.5 % |

Both are configurable and change with patches: verify them in game.
