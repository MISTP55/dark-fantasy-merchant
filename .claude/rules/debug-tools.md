---
paths:
  - "Assets/Scripts/*/Debug/**"
  - "Assets/Tests/EditMode/Debug/**"
  - "Assets/UI/Debug/**"
---

# Debug tools

- In the game, not in Editor windows, and only in the Editor and development builds (`Debug.isDebugBuild`). They read the simulation and change nothing, except the ships tab's buttons that add and remove AI ships. Their `UIDocument` has a sorting order of 100, over the game's panels.
- F1 toggles them: `WorldMapInput` reads it (`DebugKey`) and leaves it out of `AnyInput`, so that they can be watched during fast forward. A new debug key goes the same way.
- `MarketWeekLog` (Core) is what a city actually produced and consumed over the last week; what the player buys or delivers is not counted.
- The cities table (`DebugCitiesTable`) is built the first time the panel is opened, since the economy starts after the panel is enabled. UI Toolkit has no sticky position: the cities' columns and the goods' are two containers side by side, built row by row, only the goods' in a horizontal `ScrollView`; rows have one height, so the two sides line up.
- The ships table (`DebugShipsTable`) is rewritten every frame while it is shown, and not otherwise.
- Removing an AI ship goes through `AiMerchants.RemoveShip`, which refuses a ship that is not its own: the fleet forgets it, then `ShipsView.RemoveShip` deselects it, takes it out of its port (raising `Undocked`), stops reporting its arrivals and destroys its view. A new ship's number is never one a removed ship had.
