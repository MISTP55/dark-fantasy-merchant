# Harbour Traffic Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ships cross a city's grid between its edge and a berth, as part of the simulation; a city holds as many ships as it has berths, the others queue; cities without a usable layout are inactive.

**Architecture:** Berths are cells of `CityGrid` (Core/CityView), saved in `CityLayoutDefinition`. `HarbourPaths` (Core/CityView) is the A\* over the grid's navigable water: it validates layouts and gives each berth its exit path. `Harbour` (Core/Ships) is one city's runtime traffic: berths reserved, queue, each ship's progress along its berth's exit path. `ShipDocking<TPort>` gets a harbour per port (`Func<TPort, Harbour>`) and becomes the state machine of a ship relative to a city (`HarbourState`); "docked" keeps its meaning (at the berth). The city view draws the harbour's ships (`HarbourView`) and selects them through the map's ship selection.

**Tech Stack:** Unity 6000.6.4f1, C#, UI Toolkit, NUnit EditMode tests (run through the `run-tests` skill), unity-mcp for play-mode checks.

**Spec:** `docs/superpowers/specs/2026-10-25-harbour-traffic-design.md`

## Global Constraints

- Code, comments, logs, asset names in English; every text the player reads in French; numbers the French way.
- No mention of the game this one draws on, anywhere (code, comments, rules, commits).
- Every list the player reads sorted with `NameOrder`; every list of rows striped with `ListStripes.Apply`.
- Values in assets (`PortDefinition.ShipCellsPerSecond` = 2), generation parameters as constants beside the others (`CityLayoutEditing.GeneratedBerths` = 10).
- Every simulated thing advances on `GameClock.Stepped` (through `ShipsView.Step`), never per frame.
- Every new scene object, asset or reference between components is created by `WorldMapSetup`/`CityViewSetup` when missing.
- Assets created/moved with their `.meta`. Never search `Library/`.
- Commit each task on `main` when its tests pass (French message, no push).
- Update `.claude/rules/ships.md`, `city-view.md`, `world-map.md`, `ai-merchants.md`, `time.md`, `debug-tools.md` where a task changes what they state (in the task that changes it).

## Review Focus

1. A ship ordered elsewhere while waiting, arriving or leaving, then ordered back: no berth leaked, no ship stuck in a state. → Task 5 `OrderBackAndForth_NeverLeaksABerth`.
2. A ship removed from the map (`ShipsView.RemoveShip`) while on the grid or queued frees its berth and lets the queue in. → Task 4 `Remove_OnTheGrid_FreesTheBerthAndAdmitsTheQueue`, Task 5 `Remove_WhileArriving_RaisesNoDocked`.
3. Several ships exit in the same step with a longer queue: exactly as many admitted as berths freed, first come first served. → Task 4 `Advance_TwoExitsInOneStep_AdmitsTwoInQueueOrder`.
4. A large step (fast forward, `MaxStepDays`) larger than a whole crossing: the ship docks that step, never overshoots past the berth or the edge. → Task 4 `Advance_StepLongerThanThePath_StopsAtTheBerth`.
5. An AI ship waiting or arriving is not mistaken for a ship idle at sea and re-sent to its last port. → Task 6 `Advance_ShipInAHarbour_IsNotSentBack`.

---

### Task 1: Berths on the grid and harbour paths (Core)

**Files:**
- Modify: `Assets/Scripts/Core/CityView/CityGrid.cs`
- Create: `Assets/Scripts/Core/CityView/HarbourPaths.cs`
- Create: `Assets/Tests/EditMode/Common/TestHarbours.cs`
- Test: `Assets/Tests/EditMode/CityView/CityGridBerthTests.cs`, `Assets/Tests/EditMode/CityView/HarbourPathsTests.cs`

**Interfaces:**
- Produces:
  - `CityGrid.Berths : IReadOnlyList<Vector2Int>` (placing order), `bool CityGrid.IsBerth(Vector2Int)`, `bool CityGrid.CanPlaceBerth(Vector2Int)` (inside, water, no building, no berth), `void CityGrid.PlaceBerth(Vector2Int)` (throws `ArgumentException` when `!CanPlaceBerth`), `bool CityGrid.IsNavigable(Vector2Int)` (inside, water, no building on it: the port is the only building on water). `CanPlacePort` refuses a site covering a berth.
  - `static class HarbourPaths`: `bool TryFindExit(CityGrid grid, Vector2Int from, List<Vector2Int> path)`; `float LengthOf(IReadOnlyList<Vector2Int> path)`; `bool IsJoinedToEdge(CityGrid grid)` (the port, if any, side-touches a navigable cell that has an exit); `bool TryValidate(CityGrid grid, out string problem)`.
  - Test helper `TestHarbours.FromRows(params string[] rows) : CityGrid` (top row = largest y; `~` water, `.` land; `RoadContact.SidesOnly`) and `TestHarbours.PortSouth(CityGrid grid, int x, int y)` placing the 4×3, 2-water-row port facing south at origin (x, y).

- [ ] **Step 1: Write the failing tests**

`CityGridBerthTests`: `PlaceBerth_OnWater_IsListed` (`Berths` = [(2,1)], `IsBerth` true); `CanPlaceBerth_RefusesLandOutsideThePortAndATakenCell` (each false); `PlaceBerth_WhereRefused_Throws`; `IsNavigable_WaterIsLandAndPortAreNot` (berth cell navigable); `CanPlacePort_OverABerth_IsRefused`.

`HarbourPathsTests` (grids from `TestHarbours.FromRows`):
- `TryFindExit_StraightToTheNearestEdge`: 7×5 all water, from (3,1) → path [(3,1),(3,0)], `LengthOf` = 1.
- `TryFindExit_GoesAroundLand`: a land wall with one gap; path ends on an edge cell, every step is to an 8-neighbour, every cell navigable, and `LengthOf` equals the hand-computed octile length (assert with 1e-4).
- `TryFindExit_DoesNotCutCorners`: two land cells touching diagonally; assert no step goes diagonally between them.
- `TryFindExit_FromAnEdgeCell_IsThatCellAlone` (length 0).
- `TryFindExit_LakeWithNoEdge_IsFalse` and `TryFindExit_FromLand_IsFalse` (path empty).
- `TryValidate_NoBerth_Fails` (problem contains "no berth"); `TryValidate_PortNotJoinedToTheEdge_Fails` (port facing a closed lake; problem contains "not joined"); `TryValidate_UnreachableBerth_Fails` (problem contains "(x, y)" of that berth); `TryValidate_JoinedPortAndReachableBerths_Passes`.

- [ ] **Step 2: Run them, expect compile failure** (`run-tests` skill, filter `CityGridBerthTests|HarbourPathsTests`).

- [ ] **Step 3: Implement**

`TryFindExit`: A\* from `from` to the nearest navigable edge cell (x = 0, y = 0, x = W−1 or y = H−1). 8 moves, cost 1 / √2, a diagonal only when both orthogonal neighbours are navigable. Heuristic: the octile distance to the nearest edge, `min(x, y, W−1−x, H−1−y)` (admissible). `path` is cleared, then filled from `from` to the edge cell, both included. Neighbour order fixed (E, N, W, S, NE, NW, SW, SE) and open-set ties broken by insertion order, so a grid always gives the same path. Problem texts: "it has no berth", "its port is not joined to the edge of the grid by water", "the berth at (x, y) cannot be reached from the edge of the grid".

- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit** — `git commit -m "Vue de la ville : emplacements de navires et chemins du port"`

---

### Task 2: Berths in layouts and generated layouts

**Files:**
- Modify: `Assets/Scripts/Game/CityView/CityLayoutDefinition.cs`, `Assets/Scripts/Core/CityView/StartingLayout.cs`, `Assets/Scripts/Editor/CityView/CityLayoutEditing.cs`
- Create: `Assets/Scripts/Core/CityView/StartingBerths.cs`
- Test: `Assets/Tests/EditMode/CityView/CityLayoutDefinitionTests.cs`, `StartingBerthsTests.cs` (new), `StartingLayoutTests.cs`, `CityLayoutRegenerationTests.cs`, `DefaultLayoutTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces:
  - `CityLayoutDefinition.Berths : IReadOnlyList<Vector2Int>`; `SetContent(CityGrid)` saves the grid's berths; the long `SetContent(...)` gains a last parameter `IEnumerable<Vector2Int> berths`. `TryCreateGrid` places the berths right after the port (problem "the berth at (x, y) cannot stand: it is outside the grid, on land, under the port or on another berth"), then the roads and houses, then fails with `HarbourPaths.TryValidate`'s problem.
  - `static int StartingBerths.Place(CityGrid grid, int count)`: places up to `count` berths, returns how many.
  - `StartingLayout.TryBuild(..., int berths, ...)`: new parameter after `port`; order water, port, berths, town hall, houses; fails ("its port is not joined to the edge of a W by H grid") when no berth could be placed.
  - `CityLayoutEditing.GeneratedBerths = 10`, passed by `TryGenerate`.

- [ ] **Step 1: Write the failing tests**
- `StartingBerthsTests.Place_PutsTheCountNearestThePort`: 20×12 grid, water y < 6, port south at (8, 4); `Place(grid, 10)` returns 10; every berth navigable and with an exit; no non-berth navigable cell is nearer the port (by the same distance the implementation uses: Dijkstra octile distance from the navigable cells side-touching the port) than the farthest berth.
- `Place_NotEnoughWater_PlacesWhatFits`: a pond of 4 navigable cells reachable from the edge → returns 4.
- `Place_PortNotJoined_PlacesNone` → 0.
- `Place_SameGrid_SameBerths` (deterministic: ties by distance, then y, then x).
- `CityLayoutDefinitionTests`: `TryCreateGrid_WithoutBerth_FailsNamingIt`; `TryCreateGrid_BerthOnLand_Fails`; `TryCreateGrid_UnreachableBerth_Fails`; `SetContent_KeepsTheGridsBerths` (round trip). Update the existing fixtures that build a usable layout so they have a reachable berth.
- `StartingLayoutTests.TryBuild_PlacesTenBerthsAndPasses` (`HarbourPaths.TryValidate` passes on the result, `Berths.Count == 10`).
- `CityLayoutRegenerationTests`/`DefaultLayoutTests`: the regenerated / default layout has 10 berths and `TryCreateGrid` succeeds.

- [ ] **Step 2: Run, expect failures.**
- [ ] **Step 3: Implement.** In `CityLayoutEditing`, carry the berths through `Apply` (see Task 3 for the editing operations; here only `Regenerate`/`TryGenerate` and the berths passed through unchanged).
- [ ] **Step 4: Run the whole CityView suite, expect PASS** except `CityViewContentTests`, which fails until Task 3 regenerates the layouts.
- [ ] **Step 5: Commit** — `"Vue de la ville : emplacements dans les layouts et la génération"`

---

### Task 3: Layout window, regeneration of every city, inactive cities

**Files:**
- Modify: `Assets/Scripts/Editor/CityView/CityLayoutEditing.cs`, `Assets/Scripts/Editor/CityView/CityLayoutWindow.cs`, `Assets/Scripts/Game/WorldMap/WorldMapView.cs`, `Assets/Scripts/Editor/WorldMap/WorldMapSetup.cs`
- Create: `Assets/Scripts/Editor/CityView/CityLayoutRegeneration.cs`
- Modify (data): the 24 `Assets/Data/CityLayouts/*Layout.asset`
- Test: `Assets/Tests/EditMode/CityView/CityLayoutEditingBerthTests.cs` (new), `CityViewContentTests.cs`, `Assets/Tests/EditMode/WorldMap/` (new `WorldMapViewActiveCitiesTests.cs` only if `WorldMapView` can be tested without a scene; otherwise a static `WorldMapView.IsUsable(CityDefinition, BuildingsDefinition, out string problem)` tested directly)

**Interfaces:**
- Consumes: Tasks 1–2.
- Produces:
  - `CityLayoutEditing.CanPlaceBerth(layout, buildings, Vector2Int cell) : bool`, `AddBerth(layout, buildings, cell) : bool`, `RemoveBerth(layout, cell) : bool`. `Apply` takes the berths and keeps only those inside the new size, on water and not under the new port, so that resizing, freeing water and placing/moving the port drop the berths they invalidate in the same undo step. `TryMakeGrid` places the berths (false when one cannot stand).
  - `CityLayoutRegeneration.RegenerateAll()`, menu `Tools > Dark Fantasy Merchant > Regenerate All City Layouts` (confirmation dialog): `CityLayoutEditing.Regenerate` for every `MapCities` entry with a layout, seed = its index in `MapCities` (as `CityLayoutWindow.IndexInMap`), mask = the map's; logs each failure; `AssetDatabase.SaveAssets()`. Finds the map, economy and buildings assets as `CityLayoutWindow` does.
  - `static bool WorldMapView.IsUsable(CityDefinition city, BuildingsDefinition buildings, out string problem)`; `WorldMapView` gains `[SerializeField] BuildingsDefinition buildings` and `SpawnMarkers` skips (one warning each: "CityDefinition 'X' is inactive: …") a city without layout or whose `TryCreateGrid` fails. Without buildings: one error, no city skipped. `WorldMapSetup` wires the reference.

- [ ] **Step 1: Write the failing tests**
`CityLayoutEditingBerthTests`: `AddBerth_OnWater_AddsOne`; `AddBerth_OnLandOrUnderThePortOrTwice_IsRefused`; `RemoveBerth_RemovesIt`; `FreeingWater_UnderABerth_DropsIt`; `MovingThePort_OverABerth_DropsIt`; `Resize_DropsBerthsOutside`. `IsUsable_*`: no layout → false; layout without berth → false; usable → true.

- [ ] **Step 2: Run, expect failures.**
- [ ] **Step 3: Implement the editing operations, `IsUsable` and the `WorldMapView` filter.**
- [ ] **Step 4: Layout window**: tool `Berth` (label "Berth") after `Port`; colour of its own for berths; ghost 1×1 green/red from `CanPlaceBerth`; left click `AddBerth`, right click `RemoveBerth`; a "Berths: N" line; warnings (same style as the missing town hall one) from `HarbourPaths.TryValidate` on the grid of `TryMakeGrid`. Check it by opening the window through `Unity_RunCommand` and capturing it, or by the user.
- [ ] **Step 5: Regenerate every layout**: compile, run `CityLayoutRegeneration.RegenerateAll` (without the dialog: call the inner method) through `Unity_RunCommand`; check the console has no failure.
- [ ] **Step 6: Run the whole suite, expect PASS** (`CityViewContentTests` included, with every active city usable).
- [ ] **Step 7: Rules**: `city-view.md` (berths, `HarbourPaths`, the window tool, the regenerate-all menu, usable-layout conditions), `world-map.md` (cities without usable layout are inactive, `WorldMapView.IsUsable`).
- [ ] **Step 8: Commit** — `"Vue de la ville : outil des emplacements, régénération des villes, villes sans layout valide désactivées"`

---

### Task 4: `Harbour` (Core)

**Files:**
- Create: `Assets/Scripts/Core/Ships/Harbour.cs`, `Assets/Scripts/Core/Ships/HarbourState.cs`
- Test: `Assets/Tests/EditMode/Ships/HarbourTests.cs`

**Interfaces:**
- Consumes: Task 1 (`CityGrid.Berths`, `HarbourPaths`), `Ship`.
- Produces:
  - `enum HarbourState { AtSea, Waiting, Arriving, Docked, Leaving }`.
  - `sealed class Harbour`:
    - `Harbour(CityGrid grid, float cellsPerSecond)`: throws `ArgumentException` when `HarbourPaths.TryValidate` fails, `ArgumentOutOfRangeException` for a speed not > 0 or infinite. Works out each berth's exit path once (water never changes).
    - `int Capacity`, `int FreeBerths`.
    - `HarbourState StateOf(Ship)` (`AtSea` for a ship not here), `bool Contains(Ship)`.
    - `void GetShips(List<Ship> results)`: cleared, then every ship here, in the order they came.
    - `HarbourState Admit(Ship)`: reserves the free berth with the shortest exit path (ties: lowest berth index) and puts the ship at the edge end of that path → `Arriving`; no free berth → queued → `Waiting`. Throws for a ship already here.
    - `bool Withdraw(Ship)`: takes a `Waiting` ship out of the queue; false otherwise.
    - `void Leave(Ship)`: `Arriving` or `Docked` → `Leaving` (turns round where it is); `Leaving` unchanged; throws for `Waiting`/absent.
    - `void Return(Ship)`: `Leaving` → `Arriving` back to its berth (still reserved); throws otherwise.
    - `void Remove(Ship)`: forgets the ship in any state, frees its berth, admits the queue (raises `Entered`).
    - `bool TryGetPosition(Ship, out Vector2 gridPoint)`: on the grid only (`Arriving`, `Docked`, `Leaving`); cell (x, y)'s centre is the point (x, y).
    - `void Advance(float seconds)`: moves every ship on the grid by `cellsPerSecond × seconds` along its path, clamped at the berth (→ `Docked`, raises `Berthed`) or at the edge (→ removed, berth freed, raises `Exited`); then admits queued ships, first come first served, one per free berth (raises `Entered` for each). The rest of a step is not carried over. Seconds not > 0: nothing.
    - `event Action<Ship> Entered, Berthed, Exited`.
  - A ship's place is its berth and its distance from the berth along the berth's exit path: arriving decreases it, leaving increases it, turning round only changes the direction.

- [ ] **Step 1: Write the failing tests** (grid: `TestHarbours.FromRows`, 2 berths, speed 2):
`Constructor_UnusableGrid_Throws`; `Admit_FreeBerth_ArrivesFromTheEdge` (state `Arriving`, position = the exit path's edge cell); `Admit_ChoosesTheBerthNearestTheEdge`; `Admit_Full_Queues` (`Waiting`, `FreeBerths` 0, no position); `Advance_ReachesTheBerth_DocksAndRaisesBerthed` (after `length / 2` seconds, position = berth cell); `Advance_StepLongerThanThePath_StopsAtTheBerth`; `Advance_MovesTwoCellsPerSecond` (after 0.5 s on a straight path, 1 cell from the edge); `Leave_FromDocked_ExitsAndFreesTheBerth` (raises `Exited`, `StateOf` `AtSea`, `FreeBerths` back); `Leave_WhileArriving_TurnsRound` (position after a further step is nearer the edge); `Return_WhileLeaving_GoesBackAndDocks`; `Exit_AdmitsTheFirstQueuedShip` (raises `Entered`, it starts at the edge); `Advance_TwoExitsInOneStep_AdmitsTwoInQueueOrder`; `Withdraw_LeavesTheQueue`; `Remove_OnTheGrid_FreesTheBerthAndAdmitsTheQueue`; `BerthStaysReservedUntilTheEdge` (a leaving ship halfway: a new ship is queued, not admitted).

- [ ] **Step 2: Run, expect failure.** - [ ] **Step 3: Implement.** - [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Commit** — `"Navires : trafic du port d'une ville"`

---

### Task 5: `ShipDocking` and `ShipArrivals` through harbours (Core)

**Files:**
- Modify: `Assets/Scripts/Core/Ships/ShipDocking.cs`, `Assets/Scripts/Core/Ships/ShipArrivals.cs`, `Assets/Scripts/Core/Ships/Ship.cs`
- Test: `Assets/Tests/EditMode/Ships/ShipDockingTests.cs`, `ShipDockingHarbourTests.cs` (new), `ShipArrivalsTests.cs`, `ShipTests.cs`

**Interfaces:**
- Consumes: Task 4.
- Produces:
  - `Ship.Stop()`: drops the route (sailed and remaining); no `Arrived`.
  - `ShipDocking(Func<TPort, Harbour> harbourOf = null)`: a port whose harbour is null (or no function) docks at once on arrival, as today.
  - `HarbourState StateOf(Ship)`, `TPort CityOf(Ship)` (the city whose queue or grid the ship is in, docked included; null at sea), `bool IsInCity(Ship)`, `bool IsUnderWay(Ship)` (`Arriving`, `Leaving`, or at sea and moving), `void GetShipsInCity(TPort, List<Ship>)` (every state, in the order they came). `IsDocked`, `PortOf`, `GetShipsIn` keep meaning docked at the berth.
  - `event Action<Ship, TPort> CityStateChanged`: raised after every change of `StateOf` for a ship of that city (queued, entered the grid, docked, started leaving, turned back, exited, withdrawn, removed).
  - `void Update(float deltaTime)` replaces `Update()`: advances the harbours (`Berthed` → `Docked` event; `Exited` → the ship is at sea, at its anchorage, with the route it was given), then the ships that arrived at their port's anchorage enter its harbour (`Admit`) or dock at once without one. Existing calls become `Update(0f)`.
  - Orders, for a ship in a city (harbour present):
    - `OrderToPort` to its own city: `Leaving` → `Return`, `ship.Stop()`, no destination port; other states unchanged; returns true.
    - Any other order: the route is worked out first from the anchorage (an order the ship ignores changes nothing), then `Waiting` → `Withdraw` (sails at once); `Arriving` → `Leave`; `Docked` → `Leave` and `Undocked`; `Leaving` → keeps leaving with the new route.
    - `Remove`: `Harbour.Remove`, `Undocked` if docked, `CityStateChanged`.
  - The ship's route is not sailed while it is in a city: `ShipsView` advances only ships with `!IsInCity` (Task 6).
  - `ShipArrivals`: `Arrived(ship, port)` when it docks at its berth; `Arrived(ship, null)` when it stops at sea; a ship whose route ended at an anchorage and that entered a queue or a grid is not reported then. New `event Action<Ship, TPort> Queued`, raised by `Report` for a ship that entered a queue since the last report.

- [ ] **Step 1: Write the failing tests** in `ShipDockingHarbourTests` (a `Port` with a `Harbour` over a `TestHarbours` grid of 1 berth, speed 2, map from `GridAssert.FromRows` as in `ShipDockingTests`):
`ArrivingAtTheAnchorage_EntersTheGridNotThePort` (state `Arriving`, `IsDocked` false, no `Docked`); `ReachingTheBerth_Docks` (`Docked` event, `PortOf` = port); `Full_Queues` (second ship `Waiting`, `CityOf` = port); `OrderWhileDocked_LeavesThenSailsAtTheEdge` (`Undocked` at once, ship position unchanged until exit, then `IsInCity` false and the ship moves on the next `Advance`); `OrderWhileArriving_TurnsRoundWithoutDocking` (no `Docked`/`Undocked` ever); `OrderWhileWaiting_SailsAtOnce` (`IsInCity` false); `OrderBackToTheCityWhileLeaving_Returns` (state `Arriving`, `ship.IsMoving` false, then docks); `OrderBackAndForth_NeverLeaksABerth` (sequence of orders away/back over several updates; finally `FreeBerths == Capacity` once every ship has left); `Remove_WhileArriving_RaisesNoDocked`; `CityStateChanged_RaisedOnEveryChange` (records the states in order: Waiting/Arriving/Docked/Leaving/AtSea); `NoHarbour_DocksAtOnce` (existing behaviour).
`ShipArrivalsTests`: `ArrivalAtAnAnchorageWithAHarbour_ReportedWhenDocked`; `EnteringTheQueue_RaisesQueued`.
`ShipTests.Stop_DropsTheRouteWithoutArriving`.

- [ ] **Step 2: Run, expect failure.** - [ ] **Step 3: Implement.** - [ ] **Step 4: Run the whole suite, expect PASS** (existing `ShipDockingTests` with `Update(0f)`).
- [ ] **Step 5: Rules**: `ships.md` (states, orders in a city, `Update(deltaTime)`, `CityStateChanged`, arrivals).
- [ ] **Step 6: Commit** — `"Navires : arrivée et départ par le port de la ville"`

---

### Task 6: The game through harbours (Game)

**Files:**
- Modify: `Assets/Scripts/Game/Population/PortDefinition.cs`, `Assets/Scripts/Game/Economy/WorldEconomy.cs`, `Assets/Scripts/Game/Ships/ShipsView.cs`, `Assets/Scripts/Core/AiMerchants/AiMerchantFleet.cs`, `Assets/Scripts/Game/Player/ShipArrivalNotifier.cs`, `Assets/Scripts/Game/WorldMap/WorldMapInteraction.cs`, `Assets/Scripts/Game/WorldMap/CityInfoPanelController.cs`, `Assets/Scripts/Game/Debug/DebugShipsTable.cs`, `Assets/Scripts/Game/Debug/DebugPanelController.cs`, `Assets/Scripts/Editor/WorldMap/WorldMapSetup.cs`
- Test: `AiMerchantFleetTests.cs`, a notifier test if one exists (else a static format test), `CityInfoPanelController` static text tests (new `CityInfoPanelShipRowTests.cs`), `DebugShipsTable` format test, `ShipDefinitionTests`/a `PortDefinition` validation test.

**Interfaces:**
- Consumes: Tasks 4–5.
- Produces:
  - `PortDefinition.ShipCellsPerSecond : float` (`[SerializeField, Min(0.01f)] shipCellsPerSecond = 2f`, tooltip "Cells per second of world time at which ships cross a city's harbour."); `TryValidate` fails for a value not > 0.
  - `WorldEconomy.HarbourOf(CityDefinition) : Harbour` (made with each grid; null without).
  - `ShipsView`: `[SerializeField] WorldEconomy worldEconomy` ("Optional. Without it, ships enter ports at once."); `Docking` created lazily with `city => worldEconomy != null ? worldEconomy.HarbourOf(city) : null`; `Step`: `StepStarted`, advance ships with `!Docking.IsInCity`, `Docking.Update(deltaTime)`, `arrivals.Report()`. A ship in a city is off the map (NaN position, view inactive, unhovered; deselected when it enters a city from the sea), back on the map when it exits (on `CityStateChanged`, replacing the `Docked`/`Undocked` handlers). New `event Action<Ship, CityDefinition> ShipQueued` forwarded from `ShipArrivals.Queued`.
  - `AiMerchantFleet.Advance`: a ship `IsInCity` but not docked is neither counted down nor sent back.
  - `ShipArrivalNotifier.FormatQueued(string shipName, string cityName)` → `"{ship} attend une place au port de {city}."`, posted for the player's ships on `ShipQueued`.
  - `WorldMapInteraction`: the awaited ship is set when the selected ship `IsUnderWay`; it has arrived when `StateOf` is `Docked` (end fast forward, open the city view), `Waiting` (end fast forward; `SelectAgain` selects its city, whose panel opens), or at sea and not moving (close-up). Every "ship in port" test of the selection (orders, `OnCitySelected`, `SelectAgain`, `OnShipUndocked` → on `CityStateChanged` to `AtSea`) uses `IsInCity`/`CityOf` instead of `IsDocked`/`PortOf`.
  - `CityInfoPanelController`: ships tab = `GetShipsInCity`, sorted by name, rows `ShipRowText(name, state)`; trading ships chosen among `GetShipsIn` (docked) only; refreshed on `CityStateChanged`. `static string ShipStateText(HarbourState)` → "à quai" / "en attente" / "arrive" / "repart"; `static string ShipRowText(string name, HarbourState state)` → `"{name} — {state text}"`.
  - `DebugShipsTable.FormatState(HarbourState state, bool isMoving)` → "Au port", "En attente", "Arrive", "Repart", else "En mer"/"À l'arrêt"; the place column shows the city's name when `IsInCity`.
  - `WorldMapSetup` wires `ShipsView.worldEconomy` (and the port asset keeps its default speed).

- [ ] **Step 1: Write the failing tests**: `AiMerchantFleetTests.Advance_ShipInAHarbour_IsNotSentBack` (docking with a harbour; an AI ship queued or arriving gets no order across `Advance` calls) and `Depart_FromAHarbour_SailsTheCachedRouteAfterTheEdge`; `ShipStateText_*`, `ShipRowText_JoinsNameAndState` (`"Aurore — à quai"`); `FormatQueued_*`; `FormatState_*` for the 5 states; `PortDefinition.TryValidate_SpeedNotPositive_Fails`.
- [ ] **Step 2: Run, expect failure.** - [ ] **Step 3: Implement.** - [ ] **Step 4: Run the whole suite, expect PASS.**
- [ ] **Step 5: Play-mode check** (unity-mcp): run `Build World Map Scene`, enter Play, fast-forward a few days; check the console is clean, AI ships dock and leave (debug ships table shows "Arrive"/"Repart"), and the player's ship ordered to a city shows in its panel as "arrive" then "à quai". Exit Play.
- [ ] **Step 6: Rules**: `ships.md`, `ai-merchants.md`, `time.md` (fast forward and harbours), `world-map.md` (ships tab), `debug-tools.md`, `player.md` (queue notification).
- [ ] **Step 7: Commit** — `"Navires : file d'attente et traversée du port dans la partie"`

---

### Task 7: Ships in the city view

**Files:**
- Modify: `Assets/Scripts/Core/CityView/IsoProjection.cs`, `Assets/Scripts/Core/CityView/BuildingDrawOrder.cs`, `Assets/Scripts/Game/CityView/CityGridView.cs`, `Assets/Scripts/Game/CityView/CityPlacementController.cs`, `Assets/Scripts/Game/CityView/CityViewController.cs`, `Assets/Scripts/Editor/CityView/CityViewSetup.cs`
- Create: `Assets/Scripts/Game/CityView/HarbourView.cs`, `Assets/Art/CityView/ShipCylinder.png` (made by the setup)
- Test: `IsoProjectionTests.cs`, `BuildingDrawOrderTests.cs`, `HarbourViewTests.cs` (static picking)

**Interfaces:**
- Consumes: Tasks 4–6 (`WorldEconomy.HarbourOf`, `Harbour.GetShips/StateOf/TryGetPosition`, `ShipsView.Selection/IsPlayerShip`, `ShipDocking.CityStateChanged`).
- Produces:
  - `IsoProjection.PointOf(Vector2 gridPoint) : Vector2` (continuous `CenterOf`; equal to it on integer points) and `IsoProjection.GridPointAt(Vector2 world) : Vector2` (its exact inverse).
  - `BuildingDrawOrder.RankOf(long key) : int` (how many ranked keys are smaller).
  - `CityGridView`: buildings at `FirstBuildingSortingOrder + 2 × rank`; `int ShipSortingOrderAt(Vector2Int cell)` = `FirstBuildingSortingOrder + 2 × RankOf(DrawKeyOf(cell, 1×1)) − 1` (check `FirstBuildingSortingOrder − 1` stays above the roads' order, raise it if not, and that the ghost stays above all).
  - `HarbourView` (MonoBehaviour, CityView scene): `[SerializeField]` grid view, `Sprite shipSprite`, `float shipScale` (like `buildingScale`), `Color playerShipColor`, `Color aiShipColor`. `Show(CityDefinition city)`; `LateUpdate` draws one renderer per ship on the grid (pooled), at `PointOf` of its position, sortingOrder `ShipSortingOrderAt` of its rounded cell, on the `CityView` layer with the unlit sprite material. `static bool TryPick(IReadOnlyList<Ship> ships, IReadOnlyList<Vector2> gridPoints, Vector2 clickedGridPoint, out Ship ship)`: the nearest ship within 0.5 cell.
  - `CityPlacementController.ClickedWithoutPlacing : event Action<Vector2>` (screen position), raised from its `Clicked` handler when not placing.
  - `CityViewController`: on `ClickedWithoutPlacing`, picks a ship (`HarbourView`) and selects it through `SimulationServices.Current.ShipsView.Selection` (the map's ship panel shows it), or clears the ship selection; clears it when the selected ship's `CityStateChanged` goes to `AtSea`.
  - `CityViewSetup`: makes `ShipCylinder.png` (a cylinder seen in the view's projection, pivot at the ground centre of a cell, like the other placeholders), the `HarbourView` object and its references.

- [ ] **Step 1: Write the failing tests**: `PointOf_OnIntegerPoints_IsCenterOf`; `GridPointAt_IsTheInverseOfPointOf` (several points incl. fractions); `RankOf_CountsSmallerKeys`; `TryPick_NearestWithinHalfACell`, `TryPick_TooFar_IsFalse`.
- [ ] **Step 2: Run, expect failure.** - [ ] **Step 3: Implement.** - [ ] **Step 4: Run the whole suite, expect PASS.**
- [ ] **Step 5: Play-mode check** (unity-mcp): build the scenes, Play, open a city's view while ships arrive (fast-forward then open); capture the camera: cylinders on the water, smaller than a cell, moving to the berths, drawn in front of / behind the port correctly; drive `CityViewController`'s click on a ship (public method or the placement controller's) and check the map's ship panel shows it over the view (raise the panel's `sortingOrder` if hidden); check berths are not drawn. Exit Play.
- [ ] **Step 6: Rules**: `city-view.md` (`HarbourView`, ship sortingOrders and the stride of 2, picking, ship panel through the map's selection, ships re-placed every frame), `CLAUDE.md` "Runtime UI" exception list if needed, and the project state paragraph of `CLAUDE.md` (ships in the city view).
- [ ] **Step 7: Commit** — `"Vue de la ville : navires du port, sélection"`
