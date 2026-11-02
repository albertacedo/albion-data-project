# Architecture

## Overview

```
             ┌──────────────────────────────┐
             │  Albion Online (your PC)     │
             └──────────────┬───────────────┘
                            │ UDP (Photon)
                            ▼
             ┌──────────────────────────────┐        ┌─────────────────────┐
             │  albiondata-client.exe       │───────►│  AODP REST API      │
             │  -i http://localhost:5099    │        │  (snapshots)        │
             └──────────────┬───────────────┘        └──────────┬──────────┘
                            │ POST /marketorders.ingest          │
                            ▼                                    │
   ┌───────────────────────────────────────────────────────────▼──────────┐
   │                        AlbionMarket.exe (WPF)                        │
   │  IngestServer ──► SQLite ◄── AlbionApiClient ◄── NatsClient          │
   │                       │                                              │
   │                       └──► FlipAnalyzer ──► opportunities grid       │
   └──────────────────────────────────────────────────────────────────────┘
```

Three inputs, one database, one calculation engine.

## Components (AlbionMarket.Core)

| File | Responsibility |
|---|---|
| `AppPaths.cs` | Every product path (`Dades\market.db`, `Client\…`). Single source of truth. |
| `MarketService.cs` | Orchestrator: starts ingest, connects NATS, launches the client, refreshes prices, exposes state. |
| `Api/AlbionApiClient.cs` | AODP REST with batching (100 items), throttling, retries and `ratelimit-reset` handling. |
| `Realtime/IngestServer.cs` | Minimal HTTP server on `TcpListener`, port **5099**. Receives sniffer `POST`s. |
| `Realtime/NatsClient.cs` | Minimal NATS text-protocol client (connect, sub, msg). No dependencies. |
| `Data/MarketDatabase.cs` | SQLite: `prices`, `orders`, `items`, `settings`. Merges prices and orders. |
| `Engine/FlipAnalyzer.cs` | Computes NET, NET LISTED, ROI and filters by age, investment and minimum ROI. |
| `Models/` | `MarketPrice`, `MarketOrder`, `Flip`. |

## Applications

| Project | What it is |
|---|---|
| `AlbionMarket.App` | WPF UI. Four tabs: **Opportunities**, **Prices**, **Local sniffer**, **Settings**. |
| `AlbionMarket.Cli` | Same core without UI. Commands: `flips`, `show`, `fetch`, `serve`, `nats`, `items`, `catalog`, `selftest`. |

The CLI is the test bench: if something breaks in the UI, it can be reproduced there in
seconds.

## Data flow

### 1. API (snapshot)

`AlbionApiClient` requests `/api/v2/stats/prices/{items}.json?locations=…&qualities=…`.
The response is merged into `prices` with `source = 'api'`. The merge only overwrites a
price when the new one is more recent (or a specific field changed), so good data is not
clobbered by stale responses.

### 2. Sniffer (local ingest)

The client POSTs `http://localhost:5099/marketorders.ingest` with the JSON of the orders
you opened in the in-game market. `IngestServer` hands it to `MarketDatabase`, which:

- stores every order in `orders` (with `first_seen_utc` / `last_seen_utc`),
- recomputes the `prices` aggregates for the **8 known cities**,
- drops hideouts and unknown locations from the opportunity calculation (they are still
  stored in `orders`).

`GET /health` tells you whether the server is alive.

### 3. NATS (real time)

Subscription to `marketorders.deduped` on
`nats://public:thenewalbiondata@nats.albion-online-data.com:34222`.

Each message carries **a single order** (not a batch) and a **numeric** `LocationId`:
the deserialiser accepts both shapes through a flexible converter. Results are stored
exactly like sniffer data.

## How opportunities are computed

For every item and quality all cities are cross-joined:

- **cost** = `sell_price_min` in the origin city (what you pay),
- **instant revenue** = `buy_price_max` in the destination city,
- **listed revenue** = destination `sell_price_min` × (1 − setup fee) — you undercut,
- **NET** = revenue − cost − market fee,
- **ROI** = NET / cost.

Each side carries its own timestamp: if either is older than `Max age`, the opportunity
is dropped. See [Usage](usage.md) for interpretation.

## Paths and files

```
Albion Data Project\
├── Aplicacio\                     main repository (.NET)
├── Client\                        submodule: albiondata-client fork
├── Dades\market.db                database (outside the repository)
└── docs\                          this documentation
```

`AppPaths` derives everything from the user's `Documents` folder, so the project works
even when Documents is redirected (OneDrive, etc.).

## Technical decisions

- **SQLite in a single file**: inspectable with DB Browser or Python, no server.
- **No ORM**: hand-written SQL, explicit schema, less magic.
- **`journal_mode=DELETE` when the DB lives in OneDrive**: avoids syncing `-wal`/`-shm`
  files mid-transaction. WAL is used elsewhere.
- **The sniffer is not rewritten**: the official client is reused with `-i`, and the
  fork only adds a `file://` uploader.
- **Hand-rolled NATS**: ~150 lines instead of a dependency; the protocol is text and
  stable.
- **UI and logs stay in Catalan**; documentation is in English.
