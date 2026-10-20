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

- `GameClock` (Core) cuts the simulated time of a frame (real time multiplied by the speed: `Normal`, `FastForward`, or the debug tools' `Turbo`) into equal steps of at most `MaxStepDays`, and raises `Stepped` for each, after the `DayStarted` of the days the step starts. Everything simulated advances with `Stepped`, never once per frame with `DeltaTime`: what is checked once per update (an arrival, the end of a stopover) would otherwise lose up to a frame of time, and at high speed several days of economy would pass with no ship moving between them. A speed change by a listener applies from the next step. `MaxStepsPerAdvance` drops the time it cannot hold rather than freeze the game.
- The cost of high speed is the AI ships' departures (destination choice and pathfinding), about 26 ms per simulated day in the Editor with 48 AI ships: past about 40 days a second the game can no longer keep up and the frame rate falls (Unity's `maximumDeltaTime` bounds each frame). That is why `Turbo` is x600 and not more.
- `Time.timeScale` stays at 1, so the camera and the UI stay in real time; do not speed the game up through it.
- `WorldClock` is optional for `ShipsView`, `WorldMapInteraction` and `WorldMapCameraController`; `PlayerExpenses`, `WorldEconomy` and `AiMerchants` need it.
- Fast forward (`IsFastForward`, any speed but normal) is a mode in which the map is only watched; `FastForwardChanged` is raised only when it starts or ends, `SpeedChanged` on every change. `WorldMapInteraction` clears hover and selection when it starts and ends it on `WorldMapInput.AnyInput`, then calls `ConsumeInput`, so that the same input gives no click, order or zoom, not even through the release of the button (`PointerGesture.PressWithoutClick`). The HUD's button is over the UI: it does not raise `AnyInput`. Fast forward also ends when the ship selected and under way when it started arrives (`awaitedShip`); the camera then shows a close-up of it. However it ends, the previous selection is restored, or the city whose port the ship has entered.
