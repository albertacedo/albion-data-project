# Albion Data Project

A **personal** tool to query and exploit the Albion Online market from a Windows PC.
It merges three data sources into a single SQLite database and computes cross-city
buy/sell opportunities.

!!! info "Status"
    Working as of **2 November 2026**: AODP API, opportunity engine, WPF UI, local
    sniffer ingest and the real-time NATS feed. The fork of `albiondata-client` is
    compiled and the old official client has been removed from this machine.

## What it does

1. **AODP public API** — price snapshots for every city.
2. **Own sniffer** — a fork of `albiondata-client`: captures whatever you open in the
   in-game market and POSTs it to the app.
3. **Public NATS feed** — the real-time channel commercial tools use.

For every item the app shows:

- where to buy cheapest and where to sell dearest,
- **NET** profit (instant sale into an existing buy order) and **NET LISTED**
  (placing your own sell order),
- ROI, applied taxes and the **age** of each price.

## Where to start

| I want to… | Go to |
|---|---|
| Understand how it fits together | [Architecture](architecture.md) |
| Install it on a new PC | [Installation](installation.md) |
| Know where the data comes from | [Data sources](data-sources.md) |
| Inspect the database | [Database](database.md) |
| Build the sniffer | [Client](client.md) |
| Use it day to day | [Usage](usage.md) |
| Know what is left | [Roadmap](roadmap.md) |
| See what we did and why | [Changelog](changelog.md) |
| Check what is pending right now | [Status](status.md) |

## Project layout

```
Albion Data Project\
├── Aplicacio\    .NET application (core + WPF + CLI)   → main repository
├── Client\       fork of albiondata-client              → submodule (own repo)
├── Dades\        market.db (SQLite, not pushed to GitHub)
└── docs\         this documentation (MkDocs)
```

## Disclaimer

Unofficial tool, not affiliated with Sandbox Interactive. It only reads and analyses
data, which is what Sandbox has declared acceptable. It never modifies or interferes
with the game.
