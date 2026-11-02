# Fonts de dades

L'app combina tres fonts. Totes acaben a la mateixa taula `prices` (o `orders`) i totes
passen pel mateix filtre abans de convertir-se en oportunitats.

## 1. API REST d'AODP

- Base: `https://{region}.albion-online-data.com/api/v2/…`
- Regions: `europe` (en ús), `west`, `east`.
- Endpoints emprats:
  - `/stats/prices/{item1,item2,…}.json?locations=…&qualities=…` — fins a 100 items per petició.
  - `/stats/history/…` — previst per a la fase d'historial.
  - `/stats/gold.json` — previst.
- Catàleg: `items.json` (~11.000 noms) es desa a la taula `items`.

### Límit de peticions (important)

El 2 de novembre de 2026, la regió d'Europa responia:

```
ratelimit-limit: 1
ratelimit-remaining: 0
ratelimit-reset: 1793616000   (finestra alineada als :00, :10, :20…)
```

És a dir: **1 petició per finestra de 10 minuts** en aquell moment, molt més estricte que
els 180 req/min que documenta el projecte. L'app:

- fa batching de fins a 100 items per petició,
- espera un mínim d'1,2 s entre peticions,
- quan rep un `429`, espera fins al `ratelimit-reset` (fins a 4 minuts),
- mentrestant mostra sempre la caché.

!!! warning "No hi donis a refrescar en bucle"
    Cada `fetch` extra pot costar 10 minuts de bloqueig. Per a la interactivitat ja hi ha
    el sniffer i NATS.

## 2. Sniffer propi (ingestió local)

- Protocol: HTTP pla contra l'app.
- Endpoint: `POST http://localhost:5099/marketorders.ingest`
- Cos: el JSON que genera `albiondata-client` (`{"Orders":[…]}` amb `LocationId` **string**).
- Salut: `GET http://localhost:5099/health`.

El client oficial (o el fork) s'ha d'engegar amb:

```
albiondata-client.exe -i "http://localhost:5099" -minimize
```

Opcionalment, per contribuir també al projecte públic:

```
albiondata-client.exe -i "http://localhost:5099,https+pow://pow.europe.albion-online-data.com" -minimize
```

## 3. NATS públic

```
nats://public:thenewalbiondata@nats.albion-online-data.com:34222     # Europe
```

| Tema | Contingut |
|---|---|
| `marketorders.deduped` | ordres de mercat (subscripció activa) |
| `markethistories.deduped` | historial de vendes |
| `goldprices.deduped` | preu de l'or |

Particularitats que l'app ja gestiona:

- cada missatge conté **una sola ordre**,
- `LocationId` arriba com a **número**, no com a string,
- les credencials són públiques i no cal registre.

## Com es fusionen

| Origen | Taula | Efecte |
|---|---|---|
| API | `prices` | snapshot complet; només sobreescriu si és més recent |
| Sniffer | `orders` + `prices` | ordre individual; recalcula l'agregat de la ciutat |
| NATS | `orders` + `prices` | igual que el sniffer, però en temps real i de tothom |

Els preus agregats només es calculen per a les **8 ciutats conegudes**:

Thetford, Fort Sterling, Lymhurst, Bridgewatch, Martlock, Caerleon, Black Market,
Brecilien.

Els hideouts i la resta d'ubicacions es desen a `orders` però no entren al càlcul
d'oportunitats (no tenen mercat comparable).

## Qualitat i encantament

Cada preu es guarda per `(item_id, city, quality)`. L'encantament forma part de l'`item_id`
(`T6_BAG@3`). Les oportunitats es calculen per item i qualitat per separat.

## Taxes

| Concepte | Valor per defecte |
|---|---|
| Comissió de mercat | 4 % amb Premium / 8 % sense |
| *Setup fee* de l'ordre de venda | 2,5 % |

Són configurables i canvien amb els pedaços: verifica'ls al joc.
