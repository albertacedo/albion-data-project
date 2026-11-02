# Roadmap

Ideas ordered by value/effort. None of them is urgent.

## 1. History and liquidity

The API client already knows `stats/history`. Missing:

- store history in a `history` table,
- chart volume and 7/30-day average price,
- flag items with low volume as "no market", even when the margin looks great.

This is the filter that removes the most false positives.

## 2. Weight and transport

Add item `weight` (from ao-bin-dumps) and work out whether the trip is worth it given the
mount's capacity. A 40 % margin on a 300 kg item is not the same as on a bag.

## 3. Alerts

Desktop notification when a watchlist item beats a target ROI while the game is in the
background. With `NatsClient` already connected this is little work.

## 4. Gold tab

`stats/gold` and its history, to see when to buy gold with silver.

## 5. Daily dumps import

`albion-online-data.com/database/` publishes daily dumps. Importing them fills history
without spending a single API request (which is heavily limited).

## 6. Finish the `file://` mode

The fork compiles and the uploader writes JSON Lines. Still missing:

- a long real capture test,
- an importer from `file://` to `market.db`,
- comparing its output with HTTP ingest.

## 7. Quality of life

- Export the opportunities grid to CSV.
- Persistent filters per tab (they are already saved in `settings`, but without profiles).
- Dark theme for the UI.
- Unit tests for `FlipAnalyzer` and `MarketDatabase` (today everything is validated
  through the CLI).

## 8. Packaging

Publish a self-contained `dotnet publish` so the SDK is not required, plus a small
installer that lays down `Aplicacio`, `Client` and `Dades` where they belong.
