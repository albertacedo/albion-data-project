# Client (sniffer)

The project does not reimplement packet capture: it reuses
[`albiondata-client`](https://github.com/ao-data/albiondata-client), the community's
official client, and keeps a **fork** in `Client/` (a repository submodule).

## Why a fork

The stock client is enough to feed the app (`-i http://localhost:5099`). The fork adds
**two things**: a `file://` uploader that writes every payload to a JSON Lines file with
no network at all (useful to capture on a laptop and process later), and a `-nogui`
mode that runs the client with no window and no tray icon.

Changes against upstream (`ao-data/albiondata-client`):

| File | Change |
|---|---|
| `client/uploader_file.go` | new uploader that appends `{"ts","topic","data"}` lines to a file |
| `client/dispatcher.go` | registers the `file://` scheme |
| `client/config.go` | new `-nogui` flag |
| `albiondata-client.go` | `-nogui` skips Wails entirely: no dashboard, no tray, capture on the main goroutine |

```json
{"ts":"2026-11-02T10:00:00Z","topic":"marketorders.ingest","data":{"Orders":[...]}}
```

## Recommended mode: send to the app

```powershell
albiondata-client.exe -i "http://localhost:5099" -nogui
```

`-nogui` is what the app uses: the process has **no window and no tray icon**, it only
sniffs and POSTs to the ingest port, and it is a child of the app (closing the app stops
it). With `-nogui` the console is hidden too, so nothing flickers on screen.

The app does this on its own when it opens (see *Auto-start* below), and the
**Start client** button in the *Local sniffer* tab restarts it if needed. Stop any other
client first: two sniffers at once duplicate the data.

Prefer `-minimize` instead if you want the client's own dashboard (status, counters,
log) reachable from the tray icon.

### Auto-start

The *Configuració* tab has **En obrir l'app: client local en segon pla i NATS connectat**,
on by default (`autoStart` setting). When the app opens it starts the client with
`-nogui` and subscribes to NATS. Untick it if you prefer to start each source by hand.

To also contribute to the public project:

```
-i "http://localhost:5099,https+pow://pow.europe.albion-online-data.com" -nogui
```

## Capture-to-file mode

```powershell
albiondata-client.exe -i "file://C:/albion/orders.jsonl"
```

No network requests. The resulting file can be replayed later or imported into the
database.

## Building

Requirements: Go 1.25+, gcc (mingw-w64), Node, and the WpdPack bundle already in the repo.

```powershell
cd "C:\Users\alber\OneDrive\Documents\Albert\OpenCode\Albion Data Project\Client"

# 1. Frontend (mandatory: the binary embeds it with go:embed)
cmd /c "npm --prefix frontend ci"
cmd /c "npm --prefix frontend run build"

# 2. Binary, with cgo
$env:CGO_ENABLED = 1
$env:CC = "gcc"
$env:CGO_CFLAGS = "-I$PWD\thirdparty\WpdPack\Include"
$env:CGO_LDFLAGS = "-L$PWD\thirdparty\WpdPack\Lib\x64"
go build -o albiondata-client.exe albiondata-client.go
```

Output: `Client\albiondata-client.exe` (~18 MB with `-s -w`). That is the path the app
expects by default (`AppPaths.ForkedClientExe`).

!!! warning "Why cgo is required"
    `gopacket/pcap` links against `wpcap.dll` on Windows. Without gcc and the import
    libraries in `thirdparty/WpdPack/Lib/x64` the build fails with
    `-lwpcap -lPacket` errors.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `cannot find -lwpcap` | Missing `CGO_LDFLAGS` pointing to `thirdparty/WpdPack/Lib/x64`. |
| `pcap.h: No such file` | Missing `CGO_CFLAGS` pointing to `thirdparty/WpdPack/Include`. |
| `pattern all:frontend/dist: no matching files` | `npm run build` was not run in `frontend`. |
| Client starts but captures nothing | **Npcap** missing (or a VPN with an L3 adapter; see `docs/windows-capture-pcap.md` in the fork). |
| `npm.ps1 no se puede cargar` | PowerShell scripts are disabled: use `cmd /c "npm …"`. |

## Legacy client

The official installation at `C:\Program Files\Albion Data Client` has been removed from
this machine (folder, scheduled task and uninstall entry). The project only uses the
binary in `Client\`.
