# Albion Market (application)

The .NET 10 side of **Albion Data Project**: a WPF desktop app plus a CLI that merge the
AODP REST API, a local packet sniffer and the public NATS feed into one SQLite database and
compute cross-city opportunities.

Full documentation (architecture, data sources, database, roadmap):
**<https://albertacedo.github.io/albion-data-project/>**

## Requirements

- Windows 10/11
- .NET SDK 10
- The sniffer binary at `..\Client\albiondata-client.exe` (see
  [Client](https://albertacedo.github.io/albion-data-project/client/))
- Npcap (the sniffer links against `wpcap.dll`)

## Run

```powershell
cd "C:\Users\alber\OneDrive\Documents\Albert\OpenCode\Albion Data Project\Aplicacio"
dotnet build
.\run.ps1
```

Or `dotnet run --project src\AlbionMarket.App`.

The first run downloads the item catalogue (~11,000 names) and asks the API for prices.

## The two ways of selling

The API returns four prices per item and city. They are not interchangeable:

| Field | Meaning | When you use it |
|---|---|---|
| `sell_price_min` | cheapest sell order | **buying** (you pay this) |
| `buy_price_max` | highest buy order | **instant sale** (you receive this) |
| `buy_price_min` | cheapest buy order | reference (undercut it) |
| `sell_price_max` | most expensive sell order | reference |

So the app computes **two** numbers:

- **NET / INSTANT**: buy at one city's `sell_price_min`, sell into another city's
  `buy_price_max`. It sells for sure, lower margin.
- **NET LISTED**: buy the same way but place your own sell order under `sell_price_min`.
  Bigger margin, might not sell.

Each side carries its own date. Anything older than **Max age** (48 h by default) is
discarded: a five-day-old price is not an opportunity, it is a trap.

## Taxes

Configurable in the settings tab:

- Market fee: **4 %** with Premium, **8 %** without.
- Sell-order setup fee: **2.5 %**.

Verify them in game: they change with patches.

## Tabs

| Tab | What it does |
|---|---|
| **Opportunities** | Cross-city flipping grid: where to buy, where to sell, net margin and ROI, with the age of each price. |
| **Prices** | One item across all cities. |
| **Local sniffer** | Start/stop the client pointing at this app, or connect the NATS feed. |
| **Settings** | Region, taxes, max investment, client path and watchlist. |

## The sniffer

### A. Client pointing at the app (recommended)

```
albiondata-client.exe -i "http://localhost:5099" -minimize
```

The client POSTs to `http://localhost:5099/marketorders.ingest`; the app stores the orders,
recomputes aggregates and refreshes opportunities. Add
`https+pow://pow.europe.albion-online-data.com` after a comma to also contribute to the
public project.

### B. Fork with file output

The fork in `..\Client` adds a `file://` uploader:

```
albiondata-client.exe -i "file://C:/albion/orders.jsonl"
```

Each line: `{"ts":"…","topic":"marketorders.ingest","data":{"Orders":[...]}}`.

## NATS

Public credentials, no sign-up:

```
nats://public:thenewalbiondata@nats.albion-online-data.com:34222   # Europe
```

Subjects: `marketorders.deduped`, `markethistories.deduped`, `goldprices.deduped`.
`marketorders.deduped` sends **one order per message** with a **numeric** `LocationId`; the
client handles both shapes.

## Rate limits

On 2 Nov 2026 Europe answered `ratelimit-limit: 1` with a window aligned to `:00/:10/:20`.
The app batches up to 100 items per request, waits at least 1.2 s, and honours
`ratelimit-reset` on `429`. Do not refresh in a loop.

## Structure

```
Aplicacio\
├── src\
│   ├── AlbionMarket.Core\     # no WPF dependency
│   │   ├── Models\            # MarketPrice, MarketOrder, Flip
│   │   ├── Data\              # MarketDatabase (SQLite)
│   │   ├── Api\               # AlbionApiClient (REST + throttling)
│   │   ├── Realtime\          # IngestServer, NatsClient
│   │   ├── Engine\            # FlipAnalyzer
│   │   ├── AppPaths.cs        # central paths
│   │   └── MarketService.cs   # orchestrator
│   ├── AlbionMarket.App\      # WPF
│   └── AlbionMarket.Cli\      # headless harness
└── run.ps1
```

Database: `..\Dades\market.db`. Because the folder lives in OneDrive, the app uses
`journal_mode=DELETE` instead of WAL.

## CLI

```powershell
dotnet run --project src\AlbionMarket.Cli -- flips
dotnet run --project src\AlbionMarket.Cli -- flips T4_BAG T5_BAG
dotnet run --project src\AlbionMarket.Cli -- fetch
dotnet run --project src\AlbionMarket.Cli -- serve
dotnet run --project src\AlbionMarket.Cli -- nats
dotnet run --project src\AlbionMarket.Cli -- items bag
dotnet run --project src\AlbionMarket.Cli -- catalog
dotnet run --project src\AlbionMarket.Cli -- selftest
```

`serve` listens on `http://localhost:5099` and prints every incoming order: the fastest way
to test the sniffer without the UI.

## Disclaimer

Unofficial tool, not affiliated with Sandbox Interactive. It only reads and analyses data,
which is what Sandbox has declared acceptable. It never modifies or interferes with the
game.
