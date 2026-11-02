# Properes passes

Idees ordenades per valor/espera. Cap és urgent.

## 1. Historial i liquiditat

`stats/history` ja és al client API. Falta:

- desar l'historial a una taula `history`,
- graficar volum i preu mitjà de 7/30 dies,
- marcar com a "sense mercat" els items amb poc volum encara que el marge sigui alt.

És el filtre que més falsos positius elimina.

## 2. Pes i transport

Afegir el `weight` dels items (surten d'ao-bin-dumps) i calcular si el viatge val la pena
segons la capacitat de la muntura. Un marge del 40 % en un item de 300 kg no és el mateix
que en una bossa.

## 3. Alertes

Notificació d'escriptori quan un item de la llista de seguiment superi un ROI objectiu,
mentre el joc és en segon pla. Amb el `NatsClient` ja connectat és poca feina.

## 4. Pestanya d'or

`stats/gold` i el seu historial per veure quan comprar or amb plata.

## 5. Import de bolcs diaris

`albion-online-data.com/database/` publica volcats diaris. Importar-los omple l'històric
sense gastar ni una petició de l'API (que està molt limitada).

## 6. Acabar el mode `file://`

El fork ja compila i l'uploader escriu JSON Lines. Falta:

- provar una captura real llarga,
- un importador `file://` → `market.db`,
- comparar el resultat amb el de la ingesta HTTP.

## 7. Qualitat de vida

- Exportar la taula d'oportunitats a CSV.
- Filtres persistents per pestanya (ara es guarden a `settings`, però sense perfils).
- Tema fosc a la UI.
- Tests unitaris de `FlipAnalyzer` i `MarketDatabase` (ara tot es valida amb el CLI).

## 8. Empaquetar

Publicar un `dotnet publish` autocontingut per no dependre del SDK, i un petit instal·lador
que col·loqui `Aplicacio`, `Client` i `Dades` on toqui.
