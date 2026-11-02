# Installation

## Requirements

| Tool | Version | Purpose |
|---|---|---|
| Windows | 10/11 | WPF |
| .NET SDK | 10 | build and run the app |
| Node.js | 20+ (tested on 24) | build the sniffer frontend (`vite build`) |
| Go | 1.25+ (tested 1.27.3) | build the sniffer fork |
| gcc (mingw-w64) | any recent | cgo for the sniffer (links against `wpcap.dll`) |
| Npcap | latest | packet capture on Windows |
| Python + MkDocs Material | optional | preview this documentation |

!!! note "No-admin installs"
    On this machine everything was installed per-user, without administrator rights:

    - Go in `%LOCALAPPDATA%\Programs\go` (official zip)
    - gcc/make in `%LOCALAPPDATA%\Programs\w64devkit` (w64devkit)
    - GitHub CLI in `%LOCALAPPDATA%\Programs\gh`

    All three `bin` folders were added to the **user PATH**. Open a **new** terminal for
    the changes to apply.

## Running the app

```powershell
cd "C:\Users\alber\OneDrive\Documents\Albert\OpenCode\Albion Data Project\Aplicacio"
dotnet build
.\run.ps1
```

Or manually:

```powershell
dotnet run --project src\AlbionMarket.App
```

The first run downloads the item catalogue (~11,000 names) and asks the API for prices.
If the database already has cached prices it starts instantly.

## Building the client (sniffer)

```powershell
cd "C:\Users\alber\OneDrive\Documents\Albert\OpenCode\Albion Data Project\Client"

cmd /c "npm --prefix frontend ci"
cmd /c "npm --prefix frontend run build"

$env:CGO_ENABLED = 1
$env:CC = "gcc"
$env:CGO_CFLAGS = "-I$PWD\thirdparty\WpdPack\Include"
$env:CGO_LDFLAGS = "-L$PWD\thirdparty\WpdPack\Lib\x64"

go build -o albiondata-client.exe albiondata-client.go
```

Details and common problems in [Client](client.md).

## Documentation

```powershell
python -m pip install --user mkdocs-material
cd "C:\Users\alber\OneDrive\Documents\Albert\OpenCode\Albion Data Project"
python -m mkdocs serve
```

Open `http://127.0.0.1:8000`. Publishing happens on `push` to `main`: the workflow at
`.github/workflows/docs.yml` builds the site and deploys it to GitHub Pages.

## Expected layout

```
Albion Data Project\
├── Aplicacio\
│   ├── src\AlbionMarket.Core\
│   ├── src\AlbionMarket.App\
│   ├── src\AlbionMarket.Cli\
│   ├── AlbionMarket.slnx
│   └── run.ps1
├── Client\            (submodule)
├── Dades\market.db
└── docs\
```

`AppPaths` resolves these paths automatically; there is nothing to configure when the
folder structure matches.

## Removing the old official client

The project no longer uses the `C:\Program Files\Albion Data Client` installation. On
another machine, if it is present:

1. Stop the `albiondata-client` process.
2. Run `C:\Program Files\Albion Data Client\uninstall.exe`.
3. Delete the scheduled task **Albion Data Client** (`taskschd.msc`).
4. Check that no uninstall entry is left in the registry.

All of this was done on this machine (UAC elevation required) on 2 November 2026.
