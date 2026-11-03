# Status

_Last updated: **2026-11-03** (session 4)_

This page is the single source of truth for **what is pending**. It is read at the start
of every work session and updated at the end — see `AGENTS.md`.

## Open tasks

### Next up

- [ ] **Flips vs bulk split** — keep two separate opportunity lists and price
      interpretations: *flips* (expensive items, very few units, margin matters more than
      volume) and *bulk* (cheap items that only pay off in large stacks, heavy to haul).
      Needs per-item unit weight and stack size to be useful.
- [ ] **History and liquidity** — store `stats/history`, chart volume and 7/30-day
      average, flag low-volume items as "no market".
- [ ] **`file://` end-to-end** — real capture, importer into `market.db`, compare with
      HTTP ingest. Remember: those payloads carry prices in 1/10,000 silver.
- [ ] **Partial sniffer batches** — a capture that only sees part of the market list
      (page not scrolled, filtered by quality) can raise a city's minimum sell price
      until the API or NATS refreshes it. Decide whether to trust sniffer aggregates
      only for items seen in a "complete" batch, or to keep them apart.

### Later

- [ ] **City-pair routes** — pick two cities and show only the opportunities between them;
      save named routes (e.g. "Thetford → Caerleon, ore") and reload them later.
- [ ] **Weight and transport** — item weight from ao-bin-dumps, compare margin against
      mount capacity.
- [ ] **Alerts** — desktop notification when a watchlist item beats a target ROI.
- [ ] **Gold tab** — `stats/gold` plus history.
- [ ] **Daily dumps import** — `albion-online-data.com/database/` to fill history without
      spending API requests.
- [ ] **Quality of life** — CSV export, persistent filter profiles, dark theme, unit tests
      for `FlipAnalyzer` and `MarketDatabase`.
- [ ] **Packaging** — self-contained `dotnet publish` and a small installer.

### Housekeeping

- [ ] **`Aplicacio/README.md`** — it duplicates part of this site; consider trimming it to
      a pointer.
- [ ] **Fork upstream sync** — `Client/` tracks `ao-data/albiondata-client`; review
      upstream now and then and rebase the `file://` and `-nogui` commits. Last check
      (2026-11-02): 0 commits behind.

## Done

- [x] .NET core, API client, opportunity engine, WPF UI and CLI.
- [x] Local ingest server on port 5099.
- [x] NATS client with one-order-per-message and numeric `LocationId`.
- [x] Fork of `albiondata-client` with `file://` uploader, compiled with cgo.
- [x] Go 1.27.3 + mingw-w64 installed per-user.
- [x] Project relocated to `Documents\Albert\OpenCode\Albion Data Project`.
- [x] Databases merged (8,133 prices / 18,710 orders).
- [x] Old official client removed from the machine.
- [x] MkDocs site published at <https://albertacedo.github.io/albion-data-project/>.
- [x] Repositories created and pushed (project + fork submodule).
- [x] Documentation translated to English.
- [x] Session protocol (`AGENTS.md` + `scripts/session-end.ps1`).
- [x] Full end-to-end test: build, `/health`, synthetic ingest, NATS (547 messages in
      40 s) and real sniffer capture (217 orders from Bridgewatch).
- [x] Headless client `-nogui` in the fork; the app starts it (plus NATS) on launch,
      by default, with a checkbox in *Configuració*.
- [x] Sniffer prices normalised (`/10,000`); existing rows repaired.
- [x] *Orders in DB* counter, which always showed 0.
- [x] Npcap 1.10.6 confirmed working on this machine (wpcap.dll, device
      `\Device\NPF_{DE900BB9-5F2F-4C6B-8572-76EA35DE1A7C}`).
- [x] Handover page for moving machines: [`docs/handover.md`](handover.md) (what is not
      in Git, rebuild steps, settings, traps, data snapshot).
- [x] Dark `ComboBox` template: the *Configuració → Servidor* drop-down was white with
      light text (invisible). Now dark, like the rest of the window.
- [x] *Preus* tab shows the item quality (`Q`); 8 cities × 5 qualities looked like
      duplicated rows. The header also stops saying "dades en caché" after a successful
      on-demand query (`QueryItemAsync` now updates `LastRefreshUtc`).
- [x] Item icons in the *Oportunitats* and *Preus* grids
      (`render.albiononline.com/v1/item/{id}?quality={q}&size=96`), loaded only for
      visible rows, 4 downloads at a time, cached in memory and under
      `Temp\icons` inside the project (~10–15 KB per icon). Transient service failures
      retry after two minutes. Icons are displayed at 60 px.
- [x] Compact opportunity selectors for buy city, sell city, item tier (`T4`–`T8`),
      enchantment (`0`–`4`) and quality (`1`–`5`), saved in SQLite.
- [x] API watchlist refresh requests all qualities 1–5 instead of only quality 1, so
      enchanted and quality variants are available for route filters.
- [x] Background catalogue explorer: every 11 minutes it requests the next 100 catalogue
      items (all 8 cities and qualities 1–5), stores the cursor in SQLite and wraps around
      after the 11,132-item catalogue. The watchlist refresh remains available separately.
- [x] Route filters use compact multi-select drop-downs for cities, tiers, enchantments and
      qualities instead of separate checkboxes or comma-separated text.

## How to update this page

- When a task is finished, move it from **Open tasks** to **Done** with a short note.
- When a new idea appears, add it under the right heading (not in the roadmap only).
- Bump the _Last updated_ line.
- `docs/roadmap.md` explains the longer-term ideas; this page tracks the actionable
  state.
