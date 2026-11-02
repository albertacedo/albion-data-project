# Albion Data Project

Personal tool to explore the Albion Online market from a Windows PC, combining the public
AODP API, a fork of the `albiondata-client` packet sniffer and the real-time NATS feed.

📖 **Full documentation: <https://albertacedo.github.io/albion-data-project/>**

## Layout

```
Albion Data Project\
├── Aplicacio\    .NET 10 application (core + WPF + CLI)
├── Client\       submodule: albiondata-client fork (+ file:// uploader)
├── Dades\        market.db (SQLite, not in the repository)
└── docs\         MkDocs documentation
```

## Run it

```powershell
cd Aplicacio
dotnet build
.\run.ps1
```

Requirements and how to build the sniffer: [docs/installation.md](docs/installation.md).

## Documentation locally

```powershell
python -m pip install --user mkdocs-material
python -m mkdocs serve
```

## Clone

```powershell
git clone --recurse-submodules https://github.com/albertacedo/albion-data-project.git
```

## Session protocol

Open tasks live in [docs/status.md](docs/status.md) and the workflow for each work session
is described in [AGENTS.md](AGENTS.md).

## Disclaimer

Unofficial project, not affiliated with Sandbox Interactive. It only reads and analyses
market data.
