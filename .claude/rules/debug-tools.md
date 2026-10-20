---
paths:
  - "Assets/Scripts/*/Debug/**"
  - "Assets/Tests/EditMode/Debug/**"
  - "Assets/UI/Debug/**"
---

# Debug tools

- In the game, not in Editor windows, and only in the Editor and development builds (`Debug.isDebugBuild`). They read the simulation and change nothing, except the ships tab's buttons that add and remove AI ships, and the header's button that runs the world at `GameSpeed.Turbo` (a fast forward like the player's: any input ends it). Their `UIDocument` has a sorting order of 100, over the game's panels.
- F1 toggles them: `WorldMapInput` reads it (`DebugKey`) and leaves it out of `AnyInput`, so that they can be watched during fast forward. A new debug key goes the same way.
- `MarketWeekLog` (Core) is what a city actually produced and consumed over the last week; what the player buys or delivers is not counted.
- Removing an AI ship goes through `AiMerchants.RemoveShip`, which refuses a ship that is not its own: the fleet forgets it, then `ShipsView.RemoveShip` takes it out of its port (raising `Undocked`, which the panels follow) and off the map. A new ship's number is never one a removed ship had.
