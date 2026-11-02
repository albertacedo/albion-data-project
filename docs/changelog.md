# Changelog

Chronological log of decisions and verifications. Enough to pick the thread back up from
anywhere without reading the code.

## 2026-11-02 — Session 3: end-to-end test, headless client, price unit fix

### Full test

| Item | Result |
|---|---|
| Build | 0 warnings, 0 errors |
| App start | 8,191 prices and 19,314 orders loaded from `market.db` |
| `GET /health` | 200 |
| Synthetic `POST /marketorders.ingest` | 200, row in `orders` (`source=sniffer`) and aggregate in `prices` |
| NATS | 547 messages in ~40 s, all written |
| Real sniffer capture | 217 orders (Bridgewatch) while browsing the in-game market |
| UI | Opportunities, Prices, Local sniffer and Settings tabs load and refresh |

Two things broke during the test and were fixed on the spot:

- The app could not bind port 5099 because the CLI (`nats`) was already listening.
  **Rule**: never run the CLI and the app at the same time.
- `ORDERS IN DB` always showed 0 (`DbOrdersText` was never assigned). It is now
  refreshed every 5 s.

### Headless client and auto-start

- Fork: new `-nogui` flag (`client/config.go`, `albiondata-client.go`). It skips Wails
  entirely — no dashboard, no tray, no console — and runs capture on the main goroutine.
  Commit `6453e74`, pushed to `albertacedo/albiondata-client` (`master`). Rebuilt binary:
  18 MB (with `-s -w`).
- App: `StartLocalClient` now runs `albiondata-client.exe -i … -nogui`, as a child
  process (closing the app stops it, verified).
- App: new `autoStart` setting (**on** by default). On launch it starts the client and
  subscribes to NATS; checkbox in *Configuració*.
- App: if the binary exits within 5 s the log warns that the path may not point at the
  fork.

### Sniffer prices were 10,000x too high

First real capture showed absurd values (`T6_BAG` at 420,000,000, `T6_RUNE` at 1,430,000).

- **Cause**: the game sends `UnitPriceSilver` in its internal unit, **1/10,000 silver**.
  The public AODP ingest normalises it (that is why NATS and the API arrive in silver),
  but the local endpoint received the raw client JSON.
- **Fix**: `MarketService.OnIngestAsync` divides sniffer prices by 10,000. NATS and the
  API are untouched.
- **Verification**: raw 1,234,567,890 → stored as 123,456. Cross-check with other cities:
  `T4_WOOD` 770,000 → 77 (Fort Sterling 90, Lymhurst 86); `T4_RUNE` 120,000 → 10
  (Thetford 6); `T4_SOUL` 1,040,000 → 104 (Thetford 103); `T6_BAG` 420,000,000 → 42,000
  (Thetford 40,952).
- **Repair**: 217 sniffer orders normalised in place and 53 Bridgewatch aggregates
  recomputed from the orders table.
- Documented in [Data sources](data-sources.md); any `file://` importer must divide too.

### Docs

- `handover.md`: new page for continuing on another PC (what is not in Git, rebuild
  steps, `settings` table, traps, data snapshot). Linked from `AGENTS.md`, `index.md`
  and the nav.
- `client.md`: `-nogui`, auto-start, new build size.
- `data-sources.md`: the 1/10,000 unit and the `-nogui` invocation.
- `usage.md`: the routine now starts with "open the app, it does the rest".
- `.gitignore`: `albiondata-client.log*`.

## 2026-11-02 — Session 2: documentation in English, session protocol

- All documentation translated to English and renamed to English slugs
  (`architecture`, `installation`, `data-sources`, `database`, `client`, `usage`,
  `roadmap`, `changelog`, `status`).
- Added `AGENTS.md` at the project root describing the **session protocol**: report open
  tasks at the start, keep `docs/status.md` current, log the session and run
  `scripts/session-end.ps1` at the end.
- Added `scripts/session-end.ps1`: validates the docs build, commits and pushes.
- `README.md` files translated as well. The app UI and logs stay in Catalan.

## 2026-11-02 — Session 1: build, relocation, GitHub

### Initial decisions

- **Stack**: C# + .NET 10 + WPF + SQLite. Chosen by the user over web or Python
  alternatives.
- **Scope**: phases 0 to 3 — API, opportunity engine, UI and real time (NATS) — plus the
  sniffer fork.
- **Sniffer integration**: the Photon decoder is not rewritten. The official client runs
  with `-i http://localhost:5099` and the app listens through its own `IngestServer`.
- **Language**: UI, logs and (initially) docs in Catalan.

### Build and verification

| Item | Result |
|---|---|
| Prices from the API | 439 rows (later 440) |
| Opportunities found | 46 real ones |
| Example | `T8_BAG` Thetford 240,000 → Black Market 293,016 (+41,295; 17.2 %) |
| `POST /marketorders.ingest` | HTTP 200 |
| NATS | 107 real messages |

### Lessons

- **Europe rate limit**: `ratelimit-limit: 1`, window aligned to `:00/:10/:20`. The app
  waits for `ratelimit-reset` and shows cache. Refreshing in a loop is not an option.
- **NATS is not a batch**: `marketorders.deduped` sends **one order per message** and a
  **numeric** `LocationId`. A flexible converter was needed.
- **Hideouts**: stored in `orders` but excluded from calculations; only the 8 cities
  count.

### Relocation to `Documents\Albert\Albion Data Client`

The user asked to separate application, client and database:

```
Albion Data Client\
├── Aplicacio\
├── Client\
└── Dades\market.db
```

`AppPaths.cs` was added so no path is scattered through the code.

### Go and the fork build

- **Go 1.27.3** installed from the official zip into `%LOCALAPPDATA%\Programs\go`
  (no admin) and added to the user PATH.
- **gcc 16.2 + make 4.4.1** via *w64devkit* in `%LOCALAPPDATA%\Programs\w64devkit`:
  required because `gopacket/pcap` uses cgo and links against `wpcap.dll`.
- Frontend: `npm ci` + `vite build`.
- Binary: `go build` with `CGO_ENABLED=1`, `CGO_CFLAGS` and `CGO_LDFLAGS` pointing at
  `thirdparty/WpdPack`. Result: a 24.5 MB `albiondata-client.exe`.

!!! note "Obstacle"
    `npm` would not run: PowerShell has script execution disabled (`npm.ps1`). Fix: call
    it through `cmd /c "npm …"`.

### Relocation to `OpenCode\Albion Data Project`

The user moved everything to `Documents\Albert\OpenCode\Albion Data Project`.
`AppPaths` and the README were updated; no other code changed because everything goes
through `AppPaths`.

### Database merge

There were two databases: the new one (440 prices) and the old one (8,133 prices and
18,710 orders, still growing while the project was being moved). The large one wins:

- `Dades\market.db` → 8,133 prices, 18,710 orders, 11,132 items (7.9 MB).
- Copy of the small one at `Dades\backup\market-440-files-2026-11-02.db`.

### Removing the old official client

`Albion Data Client` was installed in `C:\Program Files`. We:

1. stopped the `albiondata-client` process (PID 28216),
2. deleted `C:\Program Files\Albion Data Client`,
3. removed the **Albion Data Client** scheduled task,
4. removed the uninstall entry from `HKLM\SOFTWARE\WOW6432Node\…\Uninstall`.

All with UAC elevation. Only `%APPDATA%\albiondata-client\window.json` is left (window
position, regenerated by itself).

### Documentation and GitHub

- MkDocs Material site with the pages you are reading, deployed automatically to GitHub
  Pages.
- Repository `albertacedo/albion-data-project` (public) containing `Aplicacio/` and
  `docs/`.
- The fork lives in its own repository and is linked as a **submodule** at `Client/`.

## How to resume

1. Clone with submodules:
   ```powershell
   git clone --recurse-submodules https://github.com/albertacedo/albion-data-project.git
   ```
2. Follow [Installation](installation.md).
3. `dotnet build` and `.\run.ps1`.

The database is not in the repository: it is rebuilt from the API and the sniffer.
