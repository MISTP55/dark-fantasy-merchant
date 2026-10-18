---
paths:
  - "Assets/Scripts/*/Economy/**"
  - "Assets/Tests/EditMode/Economy/**"
  - "Assets/Data/Economy/Economy.asset"
  - "Assets/Data/Goods/**"
  - "Assets/Art/Goods/**"
---

# Economy

The rules follow Patrician III; their detail is in `patrician3-re\resultats\` (`Calcul des prix.md`, `Production et consommation des villes.md`, `Villes de la carte.md`) and in the tests of the Core class that implements them. Every setting is a field of `Assets/Data/Economy/Economy.asset` or of the good assets (`Assets/Data/Goods`), meant to be tuned by playing; no test pins the base prices.

- **Goods and units.** Patrician III's twenty goods, in its order. Stocks, consumption, production, thresholds and prices are all in the good's unit, barrels or bushels (`GoodUnit`): the price of grain is that of a bushel. A bushel takes `barrelsPerBushel` barrels of room in a hold (`GoodUnits`).
- **Index order.** In `Core` a good is its index among the active goods, in the catalogue's order. What is shown is sorted by name, never by index.
- **Active goods.** Each `CatalogueGood` is ticked active or not before Play mode. An inactive good does not exist in the game: `EconomyDefinition.Goods`, which everything reads, holds only the active ones; `Catalogue` is every good, for the Editor and the content tests. A city keeps its workshop for an inactive good in its `CityDefinition` but has none in the game.
- **Cities' productions.** `CityDefinition` lists the goods the city produces efficiently and inefficiently, edited through `CityDefinitionEditor` (`CityProductionEditing.SetProduction`).
- **Markets.** `WorldEconomy` owns one `CityMarket` (Core) per city and calls their `StartDay` on `DayStarted`. The order of a day is in `population.md`.
- **Validation.** `EconomyDefinition.TryValidateSettings` checks every setting; one that breaks a rule leaves every city without a market, with a warning in the Inspector (`OnValidate`) and an error from `WorldEconomy`. `WorldEconomy` also needs a valid `BuildingsDefinition`.
- **Stocks.** There is no limit to what a city stores. Stocks are fractional; what is shown and can be bought is the whole part.
- **Raw materials.** Raw and building materials are in large surplus for now, because nothing consumes them as inputs yet. This is expected until production chains are added.
- **Trade.** `Trade` (Core) moves goods between a market, a `CargoHold` and the `Treasury`, unit by unit; its overloads without a treasury are for the AI merchants.
- **Market tab.** `TradingShipChoice` (Core) picks the ship that trades; choosing it does not select it on the map. A price is always a `Button`, disabled whenever its trade is impossible, and a disabled price is styled as plain text, as readable as the others (`.market__price` in `CityInfoPanel.uss`).
- **Goods' icons.** `Assets/Art/Goods/Icons/<good's asset name>.png`, cut from `icon-set-a.png` (painted, not pixel art), with mipmaps since they are shown much smaller.
