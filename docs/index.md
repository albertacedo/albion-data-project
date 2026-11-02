# Albion Data Project

Eina **personal** per consultar i explotar el mercat d'Albion Online des del PC. Combina
tres fonts de dades en una sola base de dades SQLite i calcula oportunitats de
compravenda entre ciutats.

!!! info "Estat"
    Funcional a data **2 de novembre de 2026**: API, motor d'oportunitats, interfície WPF,
    ingestió local del sniffer i feed NATS en temps real. El fork del client ja està
    compilat i el client antic del PC s'ha desinstal·lat.

## Què fa

1. **API pública d'AODP** — instantànies de preus de totes les ciutats.
2. **Sniffer propi** — fork d'`albiondata-client`: captura el que obres al mercat del joc
   i ho envia a l'app per HTTP.
3. **Feed NATS públic** — el canal en temps real que fan servir les eines de pagament.

Amb això l'app mostra, per a cada item:

- on comprar-lo més barat i on vendre'l més car,
- el marge **NET** (venda instantània a una ordre de compra) i el **NET LISTAT**
  (posant la teva pròpia ordre de venda),
- el ROI, les taxes aplicades i l'**edat** de cada preu.

## Per on començar

| Vull… | Ves a |
|---|---|
| Entendre com encaixa tot | [Arquitectura](arquitectura.md) |
| Instal·lar-ho en un PC nou | [Instal·lació](instalacio.md) |
| Saber d'on surten les dades | [Fonts de dades](fonts-de-dades.md) |
| Tocar la base de dades | [Base de dades](base-de-dades.md) |
| Compilar el sniffer | [Client (sniffer)](client-sniffer.md) |
| Fer-lo servir cada dia | [Ús diari](us-diar.md) |
| Saber què falta | [Properes passes](properes-passes.md) |
| Veure què hem fet i per què | [Registre del projecte](registre.md) |

## Estructura del projecte

```
Albion Data Project\
├── Aplicacio\    aplicació .NET (nucli + WPF + CLI)   → repositori principal
├── Client\       fork d'albiondata-client              → submòdul (repo propi)
├── Dades\        market.db (SQLite, no es puja a GitHub)
└── docs\         aquesta documentació (MkDocs)
```

## Avís

Eina no oficial, sense cap relació amb Sandbox Interactive. Només llegeix i analitza
dades, que és el que el propi Sandbox ha declarat acceptable. No modifica ni intervé
en el joc.
