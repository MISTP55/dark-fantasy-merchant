# Technical debt

Known shortcomings that work today but should be fixed. Each entry says what is wrong, what it costs, and how to fix it. Remove an entry when it is fixed.

## The city panel rebuilds on every harbour change

- **Where:** `CityInfoPanelController.OnCityStateChanged` (`Assets/Scripts/Game/WorldMap/CityInfoPanelController.cs`).
- **What:** while a city's panel is open, every change of state of a ship of that city (`ShipDocking.CityStateChanged`: queued, arriving, docked, leaving, back at sea) calls `ShowShipsIn`, which destroys and recreates every row of the ships tab, and `ShowMarketOf`, which shows the whole market section again. A ship goes through up to five states per stopover, and every AI ship of the city triggers it.
- **Cost:** in fast forward with many ships, the panel is rebuilt often while the player uses it; a click on a ship row can be lost when the row is replaced at that moment. The result shown is always right.
- **Fix:** show the market again only when the set of docked ships (the only ones that trade) has changed; update the state text of the existing rows instead of recreating them, and rebuild the list only when a ship enters or leaves the city.
