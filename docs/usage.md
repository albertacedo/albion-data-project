# Usage

## Start

```powershell
cd "C:\Users\alber\OneDrive\Documents\Albert\OpenCode\Albion Data Project\Aplicacio"
.\run.ps1
```

The **Albion Market** window opens and, if the cache is empty, downloads the catalogue
and prices.

The UI and logs are in Catalan (the tool is personal); this documentation is in English.

## The four tabs

| Tab | What you find |
|---|---|
| **Opportunities** | Cross-city buy/sell grid, sorted by margin. |
| **Prices** | One item across all cities, with the four figures and their dates. |
| **Local sniffer** | Start/stop the client, connect NATS, watch ingest status. |
| **Settings** | Region, taxes, max investment, max age, minimum ROI, client path and watchlist. |

Item icons come from the community render service (`render.albiononline.com`). They are
downloaded only when a row becomes visible (~10–15 KB each) and cached in
`Temp\icons` inside the project; deleting that folder is safe and only costs a
re-download.

The *Oportunitats* tab also has compact persistent selectors for **Compra**, **Venda**,
**Nivell**, **Encanteri** and **Qualitat**. Choose a route such as
`Martlock → Black Market`, optionally choose one tier, enchantment or quality, and press
**Aplica filtres**. The level and enchantment are read from the item ID; no extra columns
are needed. The selection is saved in the local SQLite settings and restored next time.

At the bottom there is a log: every line says what happened (requests, ingests, errors).
If something looks wrong, read that first.

## Reading an opportunity

```
ITEM      BUY AT        PAY      SELL AT         INSTANT    NET      ROI   NET LIST   ROI L   AGE
T8_BAG    Thetford      240,000  Black Market    293,016    41,295   17.2% 87,240     36.4%   6h
```

- **BUY AT / PAY**: the city where you buy and the price (`sell_price_min`).
- **SELL AT**: the city where you sell.
- **INSTANT**: the price you get immediately (`buy_price_max`).
- **NET**: profit after taxes when selling instantly.
- **NET LIST**: profit if you place your own sell order (may take a while to sell).
- **AGE**: age of the older of the two prices.

If `NET` is negative but `NET LIST` is positive, the opportunity is **not immediate**: you
need a buyer to show up.

## Recommended routine

1. **Open the app**. By default it starts the local client in the background (no window)
   and connects to NATS on its own (*Configuració* → auto-start checkbox).
2. **Play**: open the market for the items you care about. The sniffer POSTs the orders
   and the grid in *Local sniffer* refreshes.
3. If you unticked auto-start, press **Start client** and/or **Connect NATS** in the
   *Local sniffer* tab.
4. When you want a full snapshot, **refresh the API** (carefully: see
   [Data sources](data-sources.md)).

The app also runs a slow background catalogue exploration: every 11 minutes it asks the
API for the next 100 catalogue items and stores their prices. This is independent of the
watchlist and continues from its saved position after restarting the app.


## Configure taxes correctly

- **Premium**: 4 % fee. **Without**: 8 %.
- **Setup fee** for sell orders: 2.5 %.

Wrong values mean wrong margins. They change with patches: review them.

## Filters worth tuning

| Filter | Why |
|---|---|
| **Max age** | Drops stale prices (default 48 h). Lower it for safety. |
| **Minimum ROI** | Hides irrelevant margin noise. |
| **Max investment** | Avoids proposing purchases you cannot afford. |
| **Listed only** | Also shows sales that require waiting. |

## CLI (quick checks)

```powershell
cd "…\Albion Data Project\Aplicacio"
dotnet run --project src\AlbionMarket.Cli -- flips
dotnet run --project src\AlbionMarket.Cli -- flips T4_BAG T5_BAG
dotnet run --project src\AlbionMarket.Cli -- show T8_BAG
dotnet run --project src\AlbionMarket.Cli -- fetch
dotnet run --project src\AlbionMarket.Cli -- serve     # ingest without the UI
dotnet run --project src\AlbionMarket.Cli -- nats
dotnet run --project src\AlbionMarket.Cli -- items bag
```

`serve` is the best way to verify the sniffer is sending data: it prints every order to
the console.
