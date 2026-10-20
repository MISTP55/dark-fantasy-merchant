---
paths:
  - "Assets/Scripts/*/Debug/**"
  - "Assets/Tests/EditMode/Debug/**"
  - "Assets/UI/Debug/**"
---

# Debug tools

- In the game, not in Editor windows, and only in the Editor and development builds (`Debug.isDebugBuild`). They read the simulation and change nothing, except the ships tab's buttons that add and remove AI ships, and the header's slider, which sets `GameClock.NormalSpeedMultiplier` from x1 to x600 (not a fast forward: the map stays playable); the header's history drop-down only changes how much the record keeps. Their `UIDocument` has a sorting order of 100, over the game's panels.
- F1 toggles them: `WorldMapInput` reads it (`DebugKey`) and leaves it out of `AnyInput`, so that they can be watched during fast forward. A new debug key goes the same way.
- `MarketWeekLog` (Core) is what a city actually produced and consumed over the last week; what the player buys or delivers is not counted.
- Removing an AI ship goes through `AiMerchants.RemoveShip`, which refuses a ship that is not its own: the fleet forgets it, then `ShipsView.RemoveShip` takes it out of its port (raising `Undocked`, which the panels follow) and off the map. A new ship's number is never one a removed ship had.
- The panel has four tabs (`DebugTabs`, each an `IDebugView`): "Villes" (population, markets of one good or all), "Courbes" (by city, by indicator, composed), "Échanges" (flows of one good between the cities), "Navires". There is no journal of events or trades: the user found them of no use, so nothing records them. The views of the past are made once the record exists. The shown view is refreshed at most once a frame, when the record has new days; the ships tab every frame, as before.
- `SimulationRecorder` (debug builds only) records from the start, panel open or closed: a `SimulationRecord` (Core) of every city with a market, fed by `WorldEconomy.DayEnded` (raised after every city's `StartDay`), `AiMerchants.Traded` and `CityInfoPanelController.PlayerTraded`. A trade belongs to the last day recorded. Its horizon (`HorizonChoices`, 12 months by default) lasts for the session; shortening it trims the history and the trades together, never the last day.
- `CityMarket.LastDay` (`CityDayReport`) is what the city needed and consumed in its last `StartDay`. Filling it must not change a figure of the simulation: it only copies values the day already computes.
- A shortage ("Manque") is what the inhabitants needed and did not get (`MissingOf`), not a stock at 0 in the evening: the city consumes before it produces.
- `Construction` reports the materials it buys through an optional callback; they count as shipped from the city.
- Trades are kept in a `TrimmedList` (constant-time trimming): with `List.RemoveRange`, a day's cost grew with the number kept.
- Editing the panel's USS or UXML in Play mode rebuilds its tree: restart Play mode before checking it.
