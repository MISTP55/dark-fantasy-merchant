---
paths:
  - "Assets/Scripts/*/Time/**"
  - "Assets/Tests/EditMode/Time/**"
  - "Assets/Data/Calendar/**"
  - "Assets/UI/WorldMap/TimeHud.*"
  - "Assets/Scripts/Game/WorldMap/WorldMapInteraction.cs"
  - "Assets/Scripts/Game/WorldMap/WorldMapCameraController.cs"
---

# Time and fast forward

- `GameClock` (Core) gives `DeltaTime`, the simulated seconds of the frame, multiplied in fast forward, and raises `DayStarted` once per day that starts, even when one step crosses several. Everything simulated advances with that `DeltaTime`. `Time.timeScale` stays at 1, so the camera and the UI stay in real time; do not speed the game up through it.
- `WorldClock` is optional for `ShipsView`, `WorldMapInteraction` and `WorldMapCameraController`; `PlayerExpenses`, `WorldEconomy` and `AiMerchants` need it.
- Fast forward is a mode in which the map is only watched. `WorldMapInteraction` clears hover and selection when it starts and ends it on `WorldMapInput.AnyInput`, then calls `ConsumeInput`, so that the same input gives no click, order or zoom, not even through the release of the button (`PointerGesture.PressWithoutClick`). The HUD's button is over the UI: it does not raise `AnyInput`. Fast forward also ends when the ship selected and under way when it started arrives (`awaitedShip`); the camera then shows a close-up of it. However it ends, the previous selection is restored, or the city whose port the ship has entered.
