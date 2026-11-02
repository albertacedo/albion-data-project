# Arquitectura

## Vista general

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

Tres entrades, una sola base de dades, un sol motor de càlcul.

## Components (AlbionMarket.Core)

| Fitxer | Responsabilitat |
|---|---|
| `AppPaths.cs` | Totes les rutes del producte (`Dades\market.db`, `Client\…`). Punt únic de veritat. |
| `MarketService.cs` | Orquestrador: arrenca la ingesta, connecta NATS, llança el client, refresca preus, exposa l'estat. |
| `Api/AlbionApiClient.cs` | REST d'AODP amb batching (100 items), throttle, reintents i respecte del `ratelimit-reset`. |
| `Realtime/IngestServer.cs` | Servidor HTTP mínim sobre `TcpListener` al port **5099**. Rep els `POST` del sniffer. |
| `Realtime/NatsClient.cs` | Implementació mínima del protocol de text de NATS (connect, sub, msg). Sense dependències. |
| `Data/MarketDatabase.cs` | SQLite: `prices`, `orders`, `items`, `settings`. Fa el *merge* de preus i ordres. |
| `Engine/FlipAnalyzer.cs` | Calcula NET, NET LISTAT, ROI i filtra per edat, inversió i ROI mínim. |
| `Models/` | `MarketPrice`, `MarketOrder`, `Flip`. |

## Aplicacions

| Projecte | Què és |
|---|---|
| `AlbionMarket.App` | Interfície WPF. Quatre pestanyes: **Oportunitats**, **Preus**, **Sniffer local**, **Configuració**. |
| `AlbionMarket.Cli` | Mateix nucli sense interfície. Ordres: `flips`, `show`, `fetch`, `serve`, `nats`, `items`, `catalog`, `selftest`. |

El CLI és el banc de proves: si una cosa falla a la UI, es reprodueix aquí en segons.

## Flux de dades

### 1. API (snapshot)

`AlbionApiClient` demana `/api/v2/stats/prices/{items}.json?locations=…&qualities=…`.
La resposta s'inserta a `prices` amb `source = 'api'`. El *merge* només sobreescriu un
preu si el nou és més recent (o si el camp concret ha canviat), per no trepitjar dades
bones amb respostes velles.

### 2. Sniffer (ingesta local)

El client fa `POST http://localhost:5099/marketorders.ingest` amb el JSON de les ordres
que has obert al mercat del joc. L'`IngestServer` el passa a `MarketDatabase`, que:

- desa cada ordre a `orders` (amb `first_seen_utc` / `last_seen_utc`),
- recalcula els agregats de `prices` per a les **8 ciutats conegudes**,
- descarta hideouts i ubicacions desconegudes per al càlcul d'oportunitats (es continuen desant a `orders`).

`GET /health` serveix per comprovar que el servidor és viu.

### 3. NATS (temps real)

Subscripció a `marketorders.deduped` a
`nats://public:thenewalbiondata@nats.albion-online-data.com:34222`.

Cada missatge porta **una sola ordre** (no un lot) i `LocationId` **numèric**: el
deserialitzador ho accepta amb un conversor flexible. El resultat s'emmagatzema igual que
el del sniffer.

## Decàlcul de les oportunitats

Per a cada item i qualitat es creuen totes les ciutats:

- **cost** = `sell_price_min` de la ciutat d'origen (el que pagues),
- **ingrés instantani** = `buy_price_max` de la ciutat de destí,
- **ingrés listat** = `sell_price_min` de destí × (1 − setup fee) — hi poses una ordre per sota,
- **NET** = ingrés − cost − comissió de mercat,
- **ROI** = NET / cost.

Cada costat porta la seva data: si qualsevol supera `Antiguitat màx.`, l'oportunitat
s'elimina. Vegeu [Ús diari](us-diar.md) per a la interpretació.

## Rutes i fitxers

```
Albion Data Project\
├── Aplicacio\                     repositori principal (.NET)
├── Client\                        submòdul: fork d'albiondata-client
├── Dades\market.db                base de dades (fora del repositori)
└── docs\                          aquesta documentació
```

`AppPaths` ho resol tot a partir de `Documents` de l'usuari, així que el projecte
funciona encara que la carpeta de Documents estigui redirigida (OneDrive, etc.).

## Decisions tècniques

- **SQLite en un sol fitxer**: inspeccionable amb DB Browser o Python, sense servidor.
- **Sense ORM**: SQL a mà, esquema explícit, menys màgia.
- **Journal `DELETE` quan la BD és dins de OneDrive**: evita sincronitzar `-wal`/`-shm`
  a mitges. Fos del núvol es fa servir WAL.
- **El sniffer no s'ha reescrit**: es reutilitza el client oficial en mode `-i` i, al fork,
  només s'hi afegeix un *uploader* `file://`.
- **NATS a mà**: 150 línies en lloc d'una dependència; el protocol és text i estable.
