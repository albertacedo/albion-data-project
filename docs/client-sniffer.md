# Client (sniffer)

El projecte no reimplementa la captura de paquets: reutilitza
[`albiondata-client`](https://github.com/ao-data/albiondata-client), el client oficial de
la comunitat, i n'hi ha un **fork** a `Client/` (submòdul del repositori).

## Per què un fork

Del client original n'hi ha prou per enviar dades a l'app (`-i http://localhost:5099`).
El fork afegeix **una sola cosa**: un *uploader* `file://` que escriu cada payload a un
fitxer JSON Lines, sense xarxa. Serveix per capturar en un portàtil i processar-ho després.

Canvis respecte de l'original (`ao-data/albiondata-client`):

| Fitxer | Canvi |
|---|---|
| `client/uploader_file.go` | nou uploader que afegeix línies `{"ts","topic","data"}` a un fitxer |
| `client/dispatcher.go` | registra l'esquema `file://` |

```json
{"ts":"2026-11-02T10:00:00Z","topic":"marketorders.ingest","data":{"Orders":[...]}}
```

## Mode recomanat: enviar a l'app

```powershell
albiondata-client.exe -i "http://localhost:5099" -minimize
```

L'app ja fa això des del botó **Iniciar client** de la pestanya *Sniffer local*. Abans
d'arrencar-lo, atura qualsevol client previ: dos snifters alhora dupliquen les dades.

Per contribuir també al projecte públic:

```
-i "http://localhost:5099,https+pow://pow.europe.albion-online-data.com"
```

## Mode captura a fitxer

```powershell
albiondata-client.exe -i "file://C:/albion/orders.jsonl"
```

No fa cap petició de xarxa. El fitxer resultant es pot reproduir més tard o importar a la
base de dades.

## Compilar

Requisits: Go 1.25+, gcc (mingw-w64), Node, i el paquet WpdPack que ja és dins del repo.

```powershell
cd "C:\Users\alber\OneDrive\Documents\Albert\OpenCode\Albion Data Project\Client"

# 1. Frontend (obligatori: el binari l'incrusta amb go:embed)
cmd /c "npm --prefix frontend ci"
cmd /c "npm --prefix frontend run build"

# 2. Binari, amb cgo
$env:CGO_ENABLED = 1
$env:CC = "gcc"
$env:CGO_CFLAGS = "-I$PWD\thirdparty\WpdPack\Include"
$env:CGO_LDFLAGS = "-L$PWD\thirdparty\WpdPack\Lib\x64"
go build -o albiondata-client.exe albiondata-client.go
```

Resultat: `Client\albiondata-client.exe` (~24,5 MB). És el camí que l'app espera per
defecte (`AppPaths.ForkedClientExe`).

!!! warning "Per què cal cgo"
    `gopacket/pcap` enllaça amb `wpcap.dll` a Windows. Sense gcc i les llibreries
    d'importació de `thirdparty/WpdPack/Lib/x64` el build falla amb errors
    `-lwpcap -lPacket`.

## Problemes habituals

| Símptoma | Causa / solució |
|---|---|
| `cannot find -lwpcap` | Falta `CGO_LDFLAGS` amb `thirdparty/WpdPack/Lib/x64`. |
| `pcap.h: No such file` | Falta `CGO_CFLAGS` amb `thirdparty/WpdPack/Include`. |
| `pattern all:frontend/dist: no matching files` | No s'ha fet `npm run build` al frontend. |
| El client arrenca però no captura res | Falta **Npcap** (o una VPN amb adaptador L3; vegeu `docs/windows-capture-pcap.md` del fork). |
| `npm.ps1 no se puede cargar` | PowerShell té els scripts desactivats: fes servir `cmd /c "npm …"`. |

## Client antic

La instal·lació oficial a `C:\Program Files\Albion Data Client` s'ha eliminat d'aquest
equip (carpeta, tasca programada i entrada de desinstal·lació). El projecte només fa
servir el binari de `Client\`.
