---
paths:
  - "Assets/Scripts/*/Economy/**"
  - "Assets/Tests/EditMode/Economy/**"
  - "Assets/Data/Economy/Economy.asset"
  - "Assets/Data/Goods/**"
  - "Assets/Art/Goods/**"
---

# Economy

Every setting is a field of `Assets/Data/Economy/Economy.asset` or of the good assets (`Assets/Data/Goods`), meant to be tuned by playing; no test pins the base prices.

- **Goods and units.** Stocks, consumption, production, thresholds and prices are all in the good's unit, barrels or bushels (`GoodUnit`): the price of grain is that of a bushel. A bushel takes `barrelsPerBushel` barrels of room in a hold (`GoodUnits`).
- **Consumption per class.** A good gives what 1 000 rich, wealthy and poor consume in a week and how much its stock weighs in each class's satisfaction; the beggars consume nothing. A city's consumption, and so its thresholds and prices, follows the mix of its classes.
- **Thresholds.** `CityMarket` works them out at its creation and at the end of every `StartDay`, and `ThresholdsOf` returns them unchanged in between: every workshop of a day decides on those of the morning. Anything that changes the population outside `StartDay` leaves them as they were until the evening.
- **Index order.** In `Core` a good is its index among the active goods, in the catalogue's order. What is shown is sorted by name, never by index.
- **Active goods.** Each `CatalogueGood` is ticked active or not before Play mode. An inactive good does not exist in the game: `EconomyDefinition.Goods`, which everything reads, holds only the active ones; `Catalogue` is every good, for the Editor and the content tests. A city keeps its workshop for an inactive good in its `CityDefinition` but has none in the game.
- **Cities' productions.** `CityDefinition` lists the goods the city produces efficiently and inefficiently, edited through `CityDefinitionEditor` (`CityProductionEditing.SetProduction`).
- **Markets.** `WorldEconomy` owns one `CityMarket` (Core) per city and calls their `StartDay` on `DayStarted`. The order of a day is in `population.md`.
- **Validation.** `EconomyDefinition.TryValidateSettings` checks every setting; one that breaks a rule leaves every city without a market, with a warning in the Inspector (`OnValidate`) and an error from `WorldEconomy`. `WorldEconomy` also needs a valid `BuildingsDefinition`.
- **Stocks.** There is no limit to what a city stores. Stocks are fractional; what is shown and can be bought is the whole part.
- **Inputs.** The recipe of a city's workshop is on its good (`GoodDefinition.Inputs`: per unit made, each in its own unit), validated by `EconomyDefinition.TryValidate` for every good of the catalogue, active or not; an input that is not active is left out, the workshop needing only the others. A workshop takes its inputs from the stocks as they stand, in proportion to the scarcest, and makes as much; above T3 it still takes them and nothing enters the stock (chosen: a saturated city stays a sink for raw materials). The player's workshops produce nothing yet, so they take nothing.
- **Demand.** `DailyDemandOf` = what the inhabitants consume + what the workshops need as inputs with their workers of the moment and no shortage (`DailyInputNeedOf`), so that a city short of an input does not pay less for it. The thresholds, the starting stocks and the AI merchants read the demand; the AI's projection keeps the nominal production, a shortage of inputs aside. `DailyConsumptionOf` stays the inhabitants' alone: the satisfaction and `MissingOf` read it.
- **Trade.** `Trade` (Core) moves goods between a market, an `IGoodStore` (a `CargoHold`, or a `Warehouse`) and the `Treasury`, unit by unit; its overloads without a treasury are for the AI merchants.
- **Stores.** `IGoodSource`/`IGoodStore` are the inventory seen by `Trade` and `Transfer.Move` (free of charge, limited by what the source holds and the destination has room for). A `Warehouse` takes everything (`RoomFor` is `int.MaxValue`); what it stores above its capacity is the `Excess`, in `long` barrels, charged `Warehouse.WeeklyRentOf`.
- **The player's trading posts.** `WorldEconomy` owns them (`PlayerTradingPosts`, made with the markets): at most one per city, a `Warehouse` of the catalogue's `TradingPost.Capacity` and the economy's `GoodUnits`, read with `TradingPostOf` (null when the player has none there) and `TradingPosts`. A warehouse opens only once the trading post stands on the city's grid: `OpenTradingPost` (after `PlayerBuilder.TryBuildTradingPost` placed it; throws when the city has one) or `TryGiveTradingPost` (placed and opened for nothing, empty; false without a grid or a free site); both raise `TradingPostOpened`. Never place a trading post on a grid another way, or the grid shows one the player cannot use.
- **Market tab.** `TradingShipChoice` (Core) picks the ship that trades; choosing it does not select it on the map. A price is always a `Button`, disabled whenever its trade is impossible, and a disabled price is styled as plain text, as readable as the others (`.market__price` in `CityInfoPanel.uss`).
- **Goods' icons.** `Assets/Art/Goods/Icons/<good's asset name>.png`, cut from `icon-set-a.png` (painted, not pixel art), with mipmaps since they are shown much smaller.
