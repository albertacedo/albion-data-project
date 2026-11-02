# Ús diari

## Arrencar

```powershell
cd "C:\Users\alber\OneDrive\Documents\Albert\OpenCode\Albion Data Project\Aplicacio"
.\run.ps1
```

L'app obre la finestra **Albion Market** i, si la caché és buida, baixa catàleg i preus.

## Les quatre pestanyes

| Pestanya | Què hi trobes |
|---|---|
| **Oportunitats** | Taula de compravenda entre ciutats, ordenada per marge. |
| **Preus** | Un item concret a totes les ciutats, amb les quatre xifres i les dates. |
| **Sniffer local** | Iniciar/aturar el client, connectar NATS, veure l'estat de la ingesta. |
| **Configuració** | Regió, taxes, inversió màxima, antiguitat, ROI mínim, ruta del client i llista de seguiment. |

A baix de tot hi ha el registre: cada missatge diu què ha passat (peticions, ingestes,
errors). Si alguna cosa no quadra, mira'l primer.

## Com llegir una oportunitat

```
ITEM      COMPRA A      PAGUES    VEN A           INSTANT    NET      ROI   NET LIST   ROI L   EDAT
T8_BAG    Thetford      240.000   Black Market    293.016    41.295   17,2%  87.240    36,4%   6h
```

- **COMPRA A / PAGUES**: la ciutat on compres i el preu (`sell_price_min`).
- **VEN A**: la ciutat on venes.
- **INSTANT**: el preu al qual et compraran de seguida (`buy_price_max`).
- **NET**: benefici després de taxes venent a l'instant.
- **NET LIST**: benefici si hi poses la teva pròpia ordre de venda (pot trigar a vendre's).
- **EDAT**: antiguitat del preu més vell dels dos costats.

Si `NET` és negatiu però `NET LIST` és positiu, l'oportunitat **no és immediata**: et cal
esperar comprador.

## Rutina recomanada

1. **Obre l'app** i comprova que l'estat digui que la ingesta local és activa.
2. **Inicia el client** (pestanya *Sniffer local*) si no el tens en marxa.
3. **Juga**: obre el mercat dels items que t'interessen. El sniffer envia les ordres a
   l'app i la taula es refresca.
4. **Opcional**: connecta **NATS** per rebre ordres de tothom en temps real.
5. Quan vulguis una foto completa, **refresca l'API** (amb moderació: vegeu
   [Fonts de dades](fonts-de-dades.md)).

## Configurar bé les taxes

- **Premium**: comissió 4 %. **Sense**: 8 %.
- **Setup fee** de l'ordre de venda: 2,5 %.

Si els tens mal posats, els marges et mentiran. Canvien amb els pedaços: revisa'ls.

## Filtres que val la pena tocar

| Filtre | Per a què |
|---|---|
| **Antiguitat màx.** | Descarta preus vells (per defecte 48 h). Baixa'l si vols seguretat. |
| **ROI mínim** | Amaga soroll de marges irrellevants. |
| **Inversió màxima** | No et proposa compres que no pots pagar. |
| **Només listades** | Mostra també les vendes que requereixen esperar. |

## CLI (per provar coses de pressa)

```powershell
cd "…\Albion Data Project\Aplicacio"
dotnet run --project src\AlbionMarket.Cli -- flips
dotnet run --project src\AlbionMarket.Cli -- flips T4_BAG T5_BAG
dotnet run --project src\AlbionMarket.Cli -- show T8_BAG
dotnet run --project src\AlbionMarket.Cli -- fetch
dotnet run --project src\AlbionMarket.Cli -- serve     # ingest sense UI
dotnet run --project src\AlbionMarket.Cli -- nats
dotnet run --project src\AlbionMarket.Cli -- items bag
```

`serve` és el millor per comprovar si el sniffer envia dades: mostra cada ordre per consola.
