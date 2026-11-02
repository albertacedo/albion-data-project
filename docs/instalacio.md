# Instal·lació

## Requisits

| Eina | Versió | Per a què |
|---|---|---|
| Windows | 10/11 | WPF |
| .NET SDK | 10 | compilar i executar l'app |
| Node.js | 20+ (provat amb 24) | compilar el frontend del sniffer (`vite build`) |
| Go | 1.25+ (provat 1.27.3) | compilar el fork del sniffer |
| gcc (mingw-w64) | qualsevol recent | cgo del sniffer (enllaça amb `wpcap.dll`) |
| Npcap | última | captura de paquets a Windows |
| Python + MkDocs Material | opcional | previsualitzar aquesta documentació |

!!! note "Instal·lacions sense administrador"
    En aquest equip tot es va instal·lar a l'usuari, sense drets d'administrador:

    - Go a `%LOCALAPPDATA%\Programs\go` (zip oficial)
    - gcc/make a `%LOCALAPPDATA%\Programs\w64devkit` (w64devkit)
    - GitHub CLI a `%LOCALAPPDATA%\Programs\gh`

    Tots tres amb la carpeta `bin` afegida al **PATH d'usuari**. Cal obrir una terminal
    **nova** perquè els canvis es vegin.

## Posar en marxa l'aplicació

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
Si la base de dades ja té preus en caché, arrenca a l'instant.

## Compilar el client (sniffer)

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

Detalls i problemes habituals a [Client (sniffer)](client-sniffer.md).

## Documentació

```powershell
python -m pip install --user mkdocs-material
cd "C:\Users\alber\OneDrive\Documents\Albert\OpenCode\Albion Data Project"
python -m mkdocs serve
```

Obre `http://127.0.0.1:8000`. Per publicar-la, només cal fer `push` a `main`: el workflow
`.github/workflows/docs.yml` la construeix i la penja a GitHub Pages.

## Estructura esperada

```
Albion Data Project\
├── Aplicacio\
│   ├── src\AlbionMarket.Core\
│   ├── src\AlbionMarket.App\
│   ├── src\AlbionMarket.Cli\
│   ├── AlbionMarket.slnx
│   └── run.ps1
├── Client\            (submòdul)
├── Dades\market.db
└── docs\
```

`AppPaths` calcula aquestes rutes sol; no hi ha res a configurar si la carpeta és la
correcta.

## Desinstal·lar el client oficial antic

El projecte ja no fa servir la instal·lació de `C:\Program Files\Albion Data Client`.
Si en un altre equip hi és, cal:

1. Aturar el procés `albiondata-client`.
2. Executar `C:\Program Files\Albion Data Client\uninstall.exe`.
3. Esborrar la tasca programada **Albion Data Client** (`taskschd.msc`).
4. Comprovar que no queda l'entrada de desinstal·lació al registre.

En aquest equip es va fer tot plegat (calgué elevació UAC) el 2 de novembre de 2026.
