# Moving to another machine

This page is the handover note: what is in Git, what is **not**, and the exact steps to
have the tool running again on a different PC with the same context.

## Read first (5 minutes)

1. [Status](status.md) — what is pending right now.
2. [Changelog](changelog.md) — the last sessions, with what was tried and verified.
3. This page — the mechanics of setting the machine up again.

## What is in Git and what is not

| Item | In Git? | Notes |
|---|---|---|
| `Aplicacio/` sources | yes | .NET 10 solution |
| `docs/` + `mkdocs.yml` | yes | this site |
| `Client/` sources | yes | **submodule**, own repository |
| `Dades/market.db` | **no** | ignored (`Dades/`); copy it by hand |
| `Dades/backup/*.db` | **no** | pre-merge backup |
| `Client/albiondata-client.exe` | **no** | the fork ignores `*.exe`; rebuild or copy it |
| `Client/frontend/dist/` | **no** | built by `npm run build`, embedded in the exe |
| `bin/`, `obj/`, `site/`, logs | **no** | regenerated |

So a fresh clone gives you the **code and the docs**, not the data and not the sniffer
binary.

## Option A — copy the whole folder (easiest)

The project lives inside OneDrive, so on another PC with the same account the folder shows
up on its own (or copy it from a USB stick).

1. **Close the app** first (it stops the client and flushes SQLite).
2. Copy the whole `Albion Data Project` folder, including `Dades\` and `Client\`.
3. Follow [First-run checklist](#first-run-checklist).

Verify that `Client\albiondata-client.exe` and `Dades\market.db` arrived.

## Option B — fresh clone

```powershell
git clone --recurse-submodules https://github.com/albertacedo/albion-data-project.git
```

!!! warning "The folder path is not free"
    `AppPaths` resolves everything from **My Documents** as
    `Documents\Albert\OpenCode\Albion Data Project`. Place the clone exactly there
    (or edit `Aplicacio/src/AlbionMarket.Core/AppPaths.cs`).

Then you still need to **build the client** and **bring the database** (below).

## Requirements

| Tool | Version on the old PC | Needed for |
|---|---|---|
| .NET SDK | 10 | the app and the CLI |
| Npcap | **1.10.6** | packet capture (`wpcap.dll`) |
| Go | 1.27.3 | building the sniffer fork |
| gcc (w64devkit) | 16.2 | cgo for the sniffer |
| Node.js | 24 | building the sniffer frontend |
| Python + mkdocs-material | any recent | serving/building these docs |
| GitHub CLI | optional | issues, workflows |

Everything was installed **per user, without admin**: Go in
`%LOCALAPPDATA%\Programs\go`, w64devkit in `%LOCALAPPDATA%\Programs\w64devkit`, gh in
`%LOCALAPPDATA%\Programs\gh`. All three `bin` folders are on the **user PATH**: open a
**new** terminal after installing.

## Build the sniffer

```powershell
cd "C:\Users\<you>\Documents\Albert\OpenCode\Albion Data Project\Client"

cmd /c "npm --prefix frontend ci"
cmd /c "npm --prefix frontend run build"

$env:CGO_ENABLED = 1
$env:CC = "gcc"
$env:CGO_CFLAGS = "-I$PWD\thirdparty\WpdPack\Include"
$env:CGO_LDFLAGS = "-L$PWD\thirdparty\WpdPack\Lib\x64"
go build -ldflags "-s -w" -o albiondata-client.exe albiondata-client.go
```

Result: ~18 MB. Check that it starts headless and captures:

```powershell
.\albiondata-client.exe -i "http://localhost:5099" -nogui
# no window, no tray; Ctrl+C or stop the process to end it
Get-Content .\albiondata-client.log -Tail 10   # should list the capture device
```

## Bring the database

`Dades\market.db` is not in Git. Stop the app, then copy the file (and `Dades\backup\` if
you want the old snapshots).

Without it the app starts empty: the first run downloads the item catalogue (~11,000
names) and asks the API for the watchlist. With Europe's limit of **1 request per
10 minutes** that first fill takes a while; the sniffer and NATS fill the rest.

### Settings live inside the database

The `settings` table stores everything the UI configures. Current values (2026-11-02):

| Key | Value | Meaning |
|---|---|---|
| `region` | `Europe` | API + NATS endpoints |
| `tax` | `0.04` | market fee (4 % with premium) |
| `setup` | `0.025` | sell-order setup fee |
| `minRoi` | `10` | opportunities filter |
| `maxAge` | `48` | hours |
| `maxInvestment` | `0` | 0 = no limit |
| `includeListed` | `1` | include listed (non-instant) sales |
| `alsoPublic` | `0` | don't upload to the public AODP |
| `autoStart` | *(absent = on)* | start client + NATS when the app opens |
| `clientPath` | absolute path | **machine-specific** |
| `watchlist` | 55 items | T4–T8 raw materials and bags |

`clientPath` is the only truly machine-specific value: if the stored path does not exist,
the app silently falls back to `Client\albiondata-client.exe`. `autoStart` only appears in
the table after you press **Desar**.

Inspect or fix with any SQLite client:

```sql
select key, value from settings;
```

## First-run checklist

1. `cd Aplicacio` → `dotnet build` (expect 0 warnings, 0 errors).
2. Start `src\AlbionMarket.App\bin\Debug\net10.0-windows\AlbionMarket.App.exe`.
3. Top bar must read **Ingesta: activa · NATS: connectat** and the log
   **«Client local iniciat en segon pla»**.
4. `Invoke-WebRequest http://localhost:5099/health` → `200`.
5. *Sniffer local* tab: counters move as NATS writes orders.
6. *Preus* tab: search `T4_BAG`; you should see rows with FONT `api`/`nats`/`sniffer`.
7. *Oportunitats*: 40–60 rows with ROI ≥ 10 % and age ≤ 48 h.

## Traps learned the hard way

- **Port 5099 is exclusive.** The app and the CLI (`serve`, `nats`) both bind it. If a CLI
  run is still alive, the app starts but its ingest server never listens and `/health`
  refuses connections. Stop one before starting the other.
- **Europe API rate limit**: `ratelimit-limit: 1`, windows aligned to `:00/:10/:20`.
  Never refresh in a loop.
- **Sniffer prices are 1/10,000 silver** in the raw client JSON; the app divides on
  ingest. Any `file://` importer must do the same.
- **Only the 8 cities** produce price aggregates. Hideouts stay in `orders`.
- **OneDrive + SQLite**: `journal_mode=DELETE` and `synchronous=FULL` on purpose; do not
  copy the database while the app is running.
- **PowerShell has script execution disabled** on the old PC: use the `.cmd` wrappers
  (`scripts\session-end.cmd`, `run.cmd`) and call npm as `cmd /c "npm …"`.
- **The client is a child of the app**: closing the app stops capture (intended). If the
  app is force-killed, the client survives; kill `albiondata-client` by hand.
- **`gh`, `go` and `gcc`** only appear in a terminal opened *after* the PATH change.

## Where to pick up

- Pending work: [Status](status.md) — history/liquidity, `file://` end-to-end, partial
  sniffer batches.
- Conventions: docs in English, UI and logs in Catalan, chat in Catalan.
- Close a session with:

  ```powershell
  .\scripts\session-end.cmd -Message "Session YYYY-MM-DD: <summary>"
  ```

## Snapshot of the data (2026-11-02, end of session 3)

| Table | Rows |
|---|---|
| `items` | 11,132 |
| `prices` | 9,516 (Black Market 7,382 · Fort Sterling 851 · Martlock 365 · Lymhurst 279 · Caerleon 238 · Thetford 188 · Bridgewatch 115 · Brecilien 98) |
| `orders` | 39,128 (38,911 from NATS, 217 captured with the sniffer in Bridgewatch) |
