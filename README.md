# Albion Data Project

Eina personal per consultar el mercat d'Albion Online des del PC, combinant l'API pública
d'AODP, un fork del sniffer `albiondata-client` i el feed NATS en temps real.

📖 **Documentació completa: <https://albertacedo.github.io/albion-data-project/>**

## Estructura

```
Albion Data Project\
├── Aplicacio\    aplicació .NET 10 (nucli + WPF + CLI)
├── Client\       submòdul: fork d'albiondata-client (+ uploader file://)
├── Dades\        market.db (SQLite, fora del repositori)
└── docs\         documentació MkDocs
```

## Posar-lo en marxa

```powershell
cd Aplicacio
dotnet build
.\run.ps1
```

Requisits i compilació del sniffer: [docs/instalacio.md](docs/instalacio.md).

## Documentació local

```powershell
python -m pip install --user mkdocs-material
python -m mkdocs serve
```

## Clonar

```powershell
git clone --recurse-submodules https://github.com/albertacedo/albion-data-project.git
```

## Avís

Projecte no oficial, sense cap relació amb Sandbox Interactive. Només llegeix i analitza
dades del mercat.
