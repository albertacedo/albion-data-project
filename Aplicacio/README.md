# Albion Market

Eina personal per consultar el mercat d'Albion Online des del PC, amb tres fonts de dades
que es poden combinar:

1. **API pública d'AODP** (`europe|west|east.albion-online-data.com`) — instantànies de preus.
2. **El teu propi sniffer** — el client oficial (`albiondata-client.exe`) apuntat a aquesta app.
3. **Feed NATS públic** — el canal en temps real que fan servir les eines de pagament.

Tot queda en una base de dades SQLite local. Cap compte, cap subscripció.

```
            ┌──────────────────────────────┐
            │  Albion Online (el teu PC)   │
            └──────────────┬───────────────┘
                           │ UDP (Photon)
                           ▼
            ┌──────────────────────────────┐        ┌─────────────────────┐
            │  albiondata-client.exe       │───────►│  API REST AODP      │
            │  -i http://localhost:5099    │        │  (snapshots)        │
            └──────────────┬───────────────┘        └──────────┬──────────┘
                           │ POST /marketorders.ingest          │
                           ▼                                    │
   ┌───────────────────────────────────────────────────────────▼──────────┐
   │                        AlbionMarket.exe (WPF)                        │
   │  IngestServer ──► SQLite ◄── AlbionApiClient ◄── NatsClient          │
   │                       │                                              │
   │                       └──► FlipAnalyzer ──► taula d'oportunitats     │
   └──────────────────────────────────────────────────────────────────────┘
```

## Requisits

- Windows 10/11
- .NET SDK 10 (ja instal·lat en aquest equip)
- El fork d'`albiondata-client` a `..\Client\albiondata-client.exe` (ja compilat)
- Npcap instal·lat (el sniffer enllaça amb `wpcap.dll`)

## Posar-lo en marxa

```powershell
cd "C:\Users\alber\OneDrive\Documents\Albert\OpenCode\Albion Data Project\Aplicacio"
dotnet build
.\run.ps1
```

O manualment:

```powershell
dotnet run --project src\AlbionMarket.App
```

La primera execució baixa el catàleg d'items (~11.000 noms) i demana preus a l'API.

## Pestanyes

| Pestanya | Què fa |
|---|---|
| **Oportunitats** | Compravenda entre ciutats: on comprar, on vendre, marge net i ROI, amb l'edat de cada preu. |
| **Preus** | Consulta un item concret a totes les ciutats. |
| **Sniffer local** | Inicia/atura el client oficial apuntant a aquesta app, o connecta el feed NATS. |
| **Configuració** | Servidor, taxes, inversió màxima, ruta del client i llista de seguiment. |

## Les dues maneres de vendre (llegeix això)

L'API retorna quatre preus per item i ciutat. No són intercanviables:

| Camp | Significat | Quan l'uses |
|---|---|---|
| `sell_price_min` | Ordre de venda més barata | **Compres** (pagues aquest preu) |
| `buy_price_max` | Ordre de compra més alta | **Vens a l'instant** (reps aquest preu) |
| `buy_price_min` | Ordre de compra més barata | Referència (t'hi hauries de posar per sobre) |
| `sell_price_max` | Ordre de venda més cara | Referència |

Per això l'app calcula **dues** xifres:

- **NET/UT (instantani)**: comprens a `sell_price_min` d'una ciutat i vens a `buy_price_max` d'una altra. Es ven segur, marge més baix.
- **NET LISTAT**: comprens igual però poses la teva pròpia ordre de venda sota `sell_price_min`. Marge més alt, pot no vendre's.

Cada costat porta la seva pròpia data. L'app descarta el que superi `Antiguitat màx.` (48 h per defecte):
un preu de fa cinc dies no és una oportunitat, és un parany.

## Taxes

Configurables a la pestanya de configuració:

- Taxa de mercat: **4%** amb Premium, **8%** sense.
- *Setup fee* de les ordres de venda: **2,5%**.

Verifica els valors actuals al joc: canvien amb els pedaços.

## El sniffer propi

Hi ha dues integrals, totes dues suportades per aquest codi:

### A. Client oficial apuntant a l'app (recomanat)

L'app executa:

```
albiondata-client.exe -i "http://localhost:5099" -minimize
```

El client fa `POST http://localhost:5099/marketorders.ingest` amb el JSON de les ordres
que has obert al mercat del joc. L'app les desa, recalcula els preus agregats i actualitza
les oportunitats. **No cal fork ni compilar res.**

Si actives "També enviar les meves dades al projecte públic", l'argument passa a ser:

```
-i "http://localhost:5099,https+pow://pow.europe.albion-online-data.com"
```

Així contribueixes al projecte cooperatiu *i* alimentes la teva eina alhora.

> Important: el client només captura el que obres al mercat. Si no mires un item, no en tens el preu.

### B. Fork amb sortida a fitxer

A `..\Client` hi ha el repositori oficial clonat amb una petita modificació:
un uploader `file://` que escriu cada payload com a JSON Lines.

```
albiondata-client.exe -i "file://C:/albion/orders.jsonl"
```

Cada línia:

```json
{"ts":"2026-11-02T10:00:00Z","topic":"marketorders.ingest","data":{"Orders":[...]}}
```

El fork ja és compilat a `..\Client\albiondata-client.exe`. Per refer-lo (Go 1.27 i
gcc/mingw-w64 ja instal·lats en aquest equip):

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

`npm run build` és obligatori: el binari incrusta `frontend/dist` amb `go:embed`.
cgo també ho és, perquè el sniffer enllaça amb `wpcap.dll` (Npcap).

## Feed NATS (temps real)

Credencials públiques, sense registre:

```
nats://public:thenewalbiondata@nats.albion-online-data.com:34222   # Europe
```

Temes: `marketorders.deduped`, `markethistories.deduped`, `goldprices.deduped`.
L'app se subscriu al primer des del botó "Connectar NATS" i desa les ordres igual que les del sniffer.
El client NATS és una implementació mínima del protocol de text (sense dependències).

## Límit de peticions (important)

El 2 de novembre de 2026 l'API d'Europa responia:

```
ratelimit-limit: 1
ratelimit-remaining: 0
ratelimit-reset: 1793616000   (finestra alineada als :00, :10, :20...)
```

És a dir: en aquest moment el marge real és **molt** més estricte que els 180 req/min que
documenta el projecte. L'app:

- batching de fins a 100 items per petició,
- 1,2 s mínim entre peticions,
- espera fins al `ratelimit-reset` quan rep un 429 (fins a 4 minuts),
- mostra sempre les dades en caché mentre espera.

Per això l'arquitectura no depèn de l'API per a la interactivitat: el sniffer i NATS són la via ràpida.

## Estructura

```
Albion Data Project\
├── Aplicacio\                     # aquest repositori (.NET)
│   ├── src\
│   │   ├── AlbionMarket.Core\     # sense dependència de WPF
│   │   │   ├── Models\            # MarketPrice, MarketOrder, Flip
│   │   │   ├── Data/MarketDatabase.cs  # SQLite: prices, orders, items, settings
│   │   │   ├── Api/AlbionApiClient.cs  # REST + throttle + reintents
│   │   │   ├── Realtime/IngestServer.cs# HTTP mínim per al client
│   │   │   ├── Realtime/NatsClient.cs  # protocol NATS a mà
│   │   │   ├── Engine/FlipAnalyzer.cs  # càlcul d'oportunitats
│   │   │   ├── AppPaths.cs        # rutes centrals del producte
│   │   │   └── MarketService.cs   # orquestrador
│   │   ├── AlbionMarket.App\      # WPF
│   │   └── AlbionMarket.Cli\      # harness sense interfície
│   └── run.ps1
├── Client\                        # fork d'albiondata-client (+ uploader file://)
└── Dades\                         # market.db (SQLite)
```

Base de dades: `Dades\market.db` (SQLite, consultable amb DB Browser for SQLite
o Python). Com que la carpeta viu dins de OneDrive, l'app desactiva el mode WAL i
fa servir journal `DELETE`: així no es sincronitzen fitxers `-wal`/`-shm` a mitges.

## CLI

```powershell
dotnet run --project src\AlbionMarket.Cli -- flips
dotnet run --project src\AlbionMarket.Cli -- flips T4_BAG T5_BAG
dotnet run --project src\AlbionMarket.Cli -- fetch
dotnet run --project src\AlbionMarket.Cli -- serve
dotnet run --project src\AlbionMarket.Cli -- items bag
dotnet run --project src\AlbionMarket.Cli -- catalog
dotnet run --project src\AlbionMarket.Cli -- selftest
```

`serve` escolta a `http://localhost:5099` i mostra per consola cada ordre que arribi:
serveix per provar el sniffer sense obrir la interfície gràfica.

## Properes passes suggerides

1. **Historial i liquiditat**: `stats/history` ja està al client; falta graficar volum i mitjana de 7/30 dies per filtrar oportunitats sense mercat.
2. **Pes i transport**: afegir `weight` dels items (ao-bin-dumps) per calcular si el viatge val la pena.
3. **Alertes**: notificació d'escriptori quan un item de la llista superi un ROI.
4. **Pestanya d'or**: `stats/gold` i el seu historial.
5. **Import dels bolcs diaris** (`albion-online-data.com/database/`) per omplir l'històric sense gastar API.
6. **Provat**: fork compilat (Go 1.27 + cgo) i ingesta local funcionant; falta provar el mode `file://` end-to-end.

## Avís

Eina no oficial, sense cap relació amb Sandbox Interactive. Només llegeix i analitza dades,
que és el que el propi Sandbox ha declarat acceptable. No modifica ni intervé en el joc.
