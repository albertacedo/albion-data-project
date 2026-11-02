# Status

_Last updated: **2026-11-02** (session 2)_

This page is the single source of truth for **what is pending**. It is read at the start
of every work session and updated at the end — see `AGENTS.md`.

## Open tasks

### Next up

- [ ] **History and liquidity** — store `stats/history`, chart volume and 7/30-day
      average, flag low-volume items as "no market".
- [ ] **`file://` end-to-end** — real capture, importer into `market.db`, compare with
      HTTP ingest.

### Later

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

- [ ] **Npcap check** — confirm Npcap is installed (capture depends on it) and document
      the exact version on this machine.
- [ ] **`Aplicacio/README.md`** — it duplicates part of this site; consider trimming it to
      a pointer.
- [ ] **Fork upstream sync** — `Client/` tracks `ao-data/albiondata-client`; review
      upstream now and then and rebase the `file://` commit.

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

## How to update this page

- When a task is finished, move it from **Open tasks** to **Done** with a short note.
- When a new idea appears, add it under the right heading (not in the roadmap only).
- Bump the _Last updated_ line.
- `docs/roadmap.md` explains the longer-term ideas; this page tracks the actionable
  state.
