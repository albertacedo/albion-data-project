# Registre del projecte

Cronologia de decisions i comprovacions. Serveix per reprendre el fil des de qualsevol
altre lloc sense haver de llegir el codi.

## 2 de novembre de 2026

### Decisions inicials

- **Stack**: C# + .NET 10 + WPF + SQLite. Escollit per l'usuari enfront d'alternatives
  web o Python.
- **Abast**: fases 0 a 3 — API, motor d'oportunitats, interfície i temps real (NATS) —
  més el fork del sniffer.
- **Integració del sniffer**: no es reescriu el descodificador de Photon. S'executa el
  client oficial amb `-i http://localhost:5099` i l'app escolta amb el seu propi
  `IngestServer`.
- **Idioma**: tot en català (UI, logs, documentació).

### Construcció i verificació

| Què | Resultat |
|---|---|
| Preus des de l'API | 439 files (després 440) |
| Oportunitats detectades | 46 reals |
| Exemple | `T8_BAG` Thetford 240.000 → Black Market 293.016 (+41.295, 17,2 %) |
| `POST /marketorders.ingest` | HTTP 200 |
| NATS | 107 missatges reals |

### Incidents apresos

- **Rate limit d'Europa**: `ratelimit-limit: 1`, finestra alineada als `:00/:10/:20`.
  L'app espera el `ratelimit-reset` i mostra caché. No s'hi pot anar a cop de refresc.
- **NATS no és un lot**: `marketorders.deduped` envia **una ordre per missatge** i
  `LocationId` **numèric**. Cal un conversor flexible (implementat).
- **Hideouts**: es desen a `orders` però no entren als càlculs; només les 8 ciutats.

### Mudança a `Documents\Albert\Albion Data Client`

L'usuari va demanar separar aplicació, client i base de dades. Es va crear:

```
Albion Data Client\
├── Aplicacio\
├── Client\
└── Dades\market.db
```

I es va afegir `AppPaths.cs` perquè cap ruta quedi escampada pel codi.

### Go i compilació del fork

- **Go 1.27.3** instal·lat amb el zip oficial a `%LOCALAPPDATA%\Programs\go`
  (sense administrador) i afegit al PATH d'usuari.
- **gcc 16.2 + make 4.4.1** via *w64devkit* a `%LOCALAPPDATA%\Programs\w64devkit`:
  feien falta perquè `gopacket/pcap` usa cgo i enllaça amb `wpcap.dll`.
- Frontend: `npm ci` + `vite build`.
- Binari: `go build` amb `CGO_ENABLED=1`, `CGO_CFLAGS` i `CGO_LDFLAGS` apuntant a
  `thirdparty/WpdPack`. Resultat: `albiondata-client.exe` de 24,5 MB.

!!! note "Pedra trobada pel camí"
    `npm` no s'executava: PowerShell té els scripts desactivats (`npm.ps1`). Solució:
    cridar-lo amb `cmd /c "npm …"`.

### Mudança a `OpenCode\Albion Data Project`

L'usuari va moure el conjunt a `Documents\Albert\OpenCode\Albion Data Project`.
S'actualitza `AppPaths` i el README; la resta de codi no canvia perquè tot passa per
`AppPaths`.

### Unificació de la base de dades

Hi havia dues bases: la nova (440 preus) i l'antiga (8.133 preus i 18.710 ordres, encara
en marxa mentre es movia el projecte). Es queda la gran:

- `Dades\market.db` → 8.133 preus, 18.710 ordres, 11.132 items (7,9 MB).
- Còpia de la petita a `Dades\backup\market-440-files-2026-11-02.db`.

### Eliminació del client oficial antic

S'havia instal·lat `Albion Data Client` a `C:\Program Files`. Es va:

1. aturar el procés `albiondata-client` (PID 28216),
2. esborrar `C:\Program Files\Albion Data Client`,
3. eliminar la tasca programada **Albion Data Client**,
4. eliminar l'entrada de desinstal·lació de `HKLM\SOFTWARE\WOW6432Node\…\Uninstall`.

Tot amb elevació UAC. Queda només `%APPDATA%\albiondata-client\window.json` (posició de la
finestra, es regenera sol).

### Documentació i GitHub

- MkDocs Material amb aquestes pàgines i publicació automàtica a GitHub Pages.
- Repositori `albertacedo/albion-data-project` (públic) amb `Aplicacio/` i `docs/`.
- El fork viu al seu propi repositori i s'enllaça com a **submòdul** `Client/`.

## Com reprendre

1. Clonar el repositori amb submòduls:
   ```powershell
   git clone --recurse-submodules https://github.com/albertacedo/albion-data-project.git
   ```
2. Seguir [Instal·lació](instalacio.md).
3. `dotnet build` i `.\run.ps1`.

La base de dades no és al repositori: es regenera amb l'API i el sniffer.
