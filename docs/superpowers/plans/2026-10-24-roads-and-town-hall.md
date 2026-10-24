# Roads and Town Hall Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every city a town hall and roads; a house or workshop works only when it touches a road joined to the town hall; the player traces roads with the mouse, the cities lay them with their houses.

**Architecture:** The grid (`CityGrid`, Core) learns two new kinds of building and keeps the network itself: which roads are joined to the town hall and which buildings touch them, raising `BuildingConnected` when one becomes active. `CityMarket` gives a house its room and lets a player's workshop hire only on that event. A Core `RoadPlanner` finds road paths for the cities, the new placement strategy and the layout tool; the city view gets a road trace mode.

**Tech Stack:** Unity 6000.6.4f1, C#, UI Toolkit, Input System, NUnit EditMode tests (`run-tests` skill), `unity-mcp` for compiling and play-mode checks.

**Spec:** `docs/superpowers/specs/2026-10-24-roads-and-town-hall-design.md`

## Global Constraints

- All code, identifiers, comments, asset names in English; everything the player reads in French ("Route", "Hôtel de ville", "Routes", "Construction achevée à {ville} : Route.").
- No mention of Patrician III anywhere in the repository, commit messages included.
- Touching is by side or corner (8 neighbours). Only a road of the network connects a building; the town hall alone does not.
- Town hall: 3×2, one per city, placed only in the layout, no cost. Road: 1×1, gold only (materials through `BuildingDefinition.MaterialsOf` if ever added).
- No demolition: a connected building stays connected.
- Ghost colours in the city view: green (buildable and connected), yellow (buildable, unconnected), red (not buildable). Existing green `#6FBF7399` and red `#D0505099` stay; yellow is `#E0C04099`.
- New runtime UI on the shared `PanelSettings`; lists through `NameOrder`; striped lists through `ListStripes.Apply`.
- Every new scene reference or data asset is created by `WorldMapSetup.Build` / `CityViewSetup` when missing.
- Assets created, moved or deleted with their `.meta`.
- Commit at the end of each task on `main` (French commit message, no push).

## Review Focus

- A city whose layout lacks a town hall after migration: it must get a market without grid and one warning, never a market whose every house is inactive. Test in Task 5.
- A starting house that is unconnected at start and gets joined later by the player's road: its room must be added once, with the room of its class's house. Test in Task 4.
- The city building a house when no connectable place exists (network walled in): nothing taken, no house, no road. Test in Task 4.
- A road trace that crosses existing roads only: costs 0 new cells; must be refused as "nothing to build" rather than announced. Test in Task 4.
- A trace dragged off the grid or released over the UI: nothing built, ghost hidden, input back to its normal state after Cancel. Covered by the play-mode check in Task 8.

---

### Task 1: Roads, town hall and the network on the grid

**Files:**
- Modify: `Assets/Scripts/Core/CityView/BuildingKind.cs`, `Assets/Scripts/Core/CityView/PlacedBuilding.cs`, `Assets/Scripts/Core/CityView/CityGrid.cs`
- Test: `Assets/Tests/EditMode/CityView/CityGridNetworkTests.cs` (new), `CityGridTests.cs` (existing tests keep passing)

**Interfaces:**
- Produces:
  - `BuildingKind.Road()`, `BuildingKind.TownHall()`; `bool IsHouse`, `IsWorkshop`, `IsRoad`, `IsTownHall`. `HousedClass` throws unless a house, `Good` unless a workshop. Replace the private `isHouse` bool by a private enum.
  - `bool PlacedBuilding.IsConnected { get; internal set; }` — always false for roads and the town hall (they are the network, not its users).
  - `CityGrid.TownHall` (`PlacedBuilding`, null when none). `Place` of a second town hall throws `InvalidOperationException`; a road needs a 1×1 footprint (`ArgumentException` otherwise).
  - `bool CityGrid.IsNetworkRoad(Vector2Int cell)` — a road joined to the town hall.
  - `bool CityGrid.TouchesNetwork(IEnumerable<Vector2Int> cells)` — one of the cells (not counting them as roads) has a network road among its 8 neighbours or is one.
  - `bool CityGrid.JoinsNetwork(Vector2Int cell)` — a road placed there would be a network road: the cell touches the town hall or a network road.
  - `bool CityGrid.WouldConnect(Footprint footprint, GridPlace place)` — `TouchesNetwork` of the cells the footprint would cover.
  - `int CityGrid.ConnectedHousesOf(SocialClass)`; `HousesOf` stays (all houses).
  - `event Action<PlacedBuilding> CityGrid.BuildingConnected` — raised once per house or workshop when it becomes connected, after `BuildingPlaced` of the building that caused it (itself or a road/town hall), in the order the buildings were placed.

- [ ] **Step 1: Write the failing tests** in `CityGridNetworkTests`:
  - `RoadNextToTownHall_IsNetworkRoad`, `RoadTouchingTownHallOnlyByCorner_IsNetworkRoad`, `RoadChainByCorners_IsNetworkRoad`, `RoadWithoutTownHall_IsNotNetworkRoad`.
  - `HouseTouchingOnlyTownHall_IsNotConnected`.
  - `HouseTouchingNetworkRoadByCorner_IsConnectedWhenPlaced` (event raised once, after `BuildingPlaced`).
  - `HousePlacedFirst_GetsConnectedByTheLastRoadOfAChain` (event raised on the road that closes the chain, not before).
  - `TownHallPlacedAfterRoads_ConnectsEverythingAlreadyTouchingThem` (layouts may place the town hall first or the setup roads first: both work).
  - `SecondTownHall_Throws`, `RoadOfTwoCells_Throws`.
  - `ConnectedHousesOf_CountsOnlyConnectedHouses`.
  - `WouldConnect_TrueOnlyWhenTheFootprintTouchesANetworkRoad`, `JoinsNetwork_TrueNextToTownHallOrNetworkRoad`.
- [ ] **Step 2: Run** with the `run-tests` skill, filter `CityGridNetworkTests`. Expected: compile failure / FAIL.
- [ ] **Step 3: Implement.** On each road or town hall placed, flood the network from the town hall over roads (8 neighbours; a `bool[]` of network cells), then mark every not-yet-connected house/workshop touching a network road, raising `BuildingConnected` in `buildings` order. On a house/workshop placed, check it alone. Grids are at most 1000×1000 but in practice 40×40: a full flood per road is fine; a player trace of n roads must not flood n times — add `internal void PlaceRoads(IReadOnlyList<Vector2Int> cells, BuildingOwner owner)` that places them all then floods once (used by Task 4), and make the flood incremental only if the tests are slow.
- [ ] **Step 4: Run** `CityGridNetworkTests` and `CityGridTests`. Expected: PASS.
- [ ] **Step 5: Commit** — `Grille : routes, hôtel de ville et réseau`.

### Task 2: Road paths and the player's trace

**Files:**
- Create: `Assets/Scripts/Core/CityView/RoadPlanner.cs`, `Assets/Scripts/Core/CityView/RoadTrace.cs`
- Test: `Assets/Tests/EditMode/CityView/RoadPlannerTests.cs`, `RoadTraceTests.cs`

**Interfaces:**
- Consumes: Task 1's `IsNetworkRoad`, `JoinsNetwork`, `TouchesNetwork`, `TownHall`.
- Produces:
  - `static bool RoadPlanner.TryFindRoads(CityGrid grid, IReadOnlyCollection<Vector2Int> buildingCells, List<Vector2Int> roads)` — fills `roads` (cleared first) with the fewest free cells that, laid as roads, make a building covering `buildingCells` touch the network. `buildingCells` may be cells of a building already placed or of one being planned (then they count as blocked). Empty `roads` and true when it already touches a network road. False when the grid has no town hall or no path exists. Path cells are free, not forbidden, not in `buildingCells`; moves are 8-neighbour. Roads are listed from the network outwards. Ties broken by the order of a BFS that visits neighbours in a fixed order (x−1..x+1, y−1..y+1), so results are deterministic.
  - `static void RoadPlanner.DistancesFromNetwork(CityGrid grid, int[] distances)` — `distances` of length `Width*Height`: for each free cell, how many road cells (itself included) join it to the network (1 when `JoinsNetwork`), `int.MaxValue` when unreachable or not free. For ranking places cheaply (Task 3).
  - `static void RoadTrace.Of(Vector2Int start, Vector2Int end, List<Vector2Int> cells)` — the L: from `start` along x to `end.x`, then along y to `end.y`; `start` first, `end` last, no cell twice; one cell when start equals end.

- [ ] **Step 1: Write the failing tests**:
  - `RoadTraceTests`: `SameCell_IsOneCell`; `StraightAlongX`; `StraightAlongY`; `LShape_GoesAlongXThenY` — `Of((1,1),(3,4))` gives `(1,1),(2,1),(3,1),(3,2),(3,3),(3,4)`; `Backwards_WorksWithNegativeSteps`.
  - `RoadPlannerTests`: `AlreadyTouchingNetworkRoad_NeedsNoRoad`; `TouchingOnlyTownHall_NeedsOneRoad`; `ShortestPathAroundAWall` (expected length given); `UsesDiagonalSteps` (a diagonal gap of 3 needs 2 roads, not 4); `PlannedCellsAreBlocked`; `WalledIn_ReturnsFalse`; `NoTownHall_ReturnsFalse`; `DistancesFromNetwork_OneNextToTownHall_MaxValueOnBuildings`.
- [ ] **Step 2: Run** filter `RoadTraceTests|RoadPlannerTests`. Expected: FAIL.
- [ ] **Step 3: Implement** both: a multi-source BFS whose sources are the free cells for which `JoinsNetwork` is true (distance 1), stopping at the first cell touching `buildingCells`, then walking back the parents.
- [ ] **Step 4: Run** the same filter. Expected: PASS.
- [ ] **Step 5: Commit** — `Grille : chemins de routes et tracé en L`.

### Task 3: Placement with roads

**Files:**
- Modify: `Assets/Scripts/Core/CityView/IPlacementStrategy.cs`, `RandomPlacement.cs`, `StartingHouses.cs`
- Create: `Assets/Scripts/Core/CityView/RoadsidePlacement.cs`
- Test: `Assets/Tests/EditMode/CityView/RoadsidePlacementTests.cs`, update `RandomPlacementTests.cs`, `StartingHousesTests.cs`

**Interfaces:**
- Consumes: Task 2's `RoadPlanner`.
- Produces:
  - `bool IPlacementStrategy.TryChoose(CityGrid grid, Footprint footprint, out GridPlace place, List<Vector2Int> roads)` — `roads` cleared and filled with the roads to lay with the building. `RandomPlacement` leaves it empty.
  - `RoadsidePlacement(System.Random random)` — ranks every place of `grid.GetPlaces` by the least `DistancesFromNetwork` over the free cells touching it (0 when `WouldConnect`), keeps the minimal ones, and draws among them; it confirms the drawn place with `RoadPlanner.TryFindRoads` on its cells (which the distances ignore) and, when the exact path is longer or missing, drops that place and draws again among the remaining minimal ones, then the next rank. False when no place can be connected (or the grid has no town hall).
  - `StartingHouses.TryPlace(...)` lays the strategy's roads (owner City) right after each house.

- [ ] **Step 1: Write the failing tests**: `RoadsidePlacementTests`: `PrefersAPlaceTouchingTheNetwork_NoRoad`; `WhenNoneTouches_ReturnsTheShortestRoads`; `SameSeed_SamePlace`; `NoTownHall_ReturnsFalse`; `NetworkWalledIn_ReturnsFalse`; `ReturnedRoadsConnectTheHouse` (lay them, place the house, `IsConnected` true). `StartingHousesTests`: `WithRoadsidePlacement_EveryHouseIsConnected`. Existing `RandomPlacementTests`: adapt calls, assert `roads` empty.
- [ ] **Step 2: Run** filter `RoadsidePlacementTests|RandomPlacementTests|StartingHousesTests`. Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the same filter. Expected: PASS.
- [ ] **Step 5: Commit** — `Placement : le long des routes`.

### Task 4: The market and construction follow the network

**Files:**
- Modify: `Assets/Scripts/Core/Economy/CityMarket.cs`, `Assets/Scripts/Core/Population/Construction.cs`, `Assets/Scripts/Game/Economy/EconomyDefinition.cs` (starting room: `ConnectedHousesOf`)
- Test: `Assets/Tests/EditMode/Population/CityMarketRoadsTests.cs` (new), `ConstructionTests.cs`, existing `CityMarketHousingTests`/`CityMarketLabourTests` adapted (their grids need a town hall and roads for houses to give room)

**Interfaces:**
- Consumes: Tasks 1–3.
- Produces:
  - `CityMarket` with a grid subscribes to `Grid.BuildingConnected`: a house adds the room it was built with (player: the `room` passed; city: `rule.Room`; a house already on the grid at construction: the `Room` of the housing rule of its class — a grid holding an unconnected house of a class without housing rule is an `ArgumentException`). Houses already connected at construction add nothing: their room is in the starting population. `AddPlayerHouse` with a grid no longer adds room directly.
  - A player's workshop on the grid hires (`FillVacancies`) only while its `PlacedBuilding.IsConnected`; one without grid always.
  - `BuildCityHouse`: the placement's place and roads; before taking anything it checks `Grid.WouldConnect` after the roads (i.e. the roads join the network and touch the footprint — use `RoadPlanner.TryFindRoads` on the footprint's cells returning no more roads than given, or an equivalent check) and returns otherwise; then takes the materials, places the house, then the roads through `PlaceRoads` (owner City), counts `CityHouses`. Its room comes through the event.
  - `void CityMarket.AddPlayerRoads(IReadOnlyList<Vector2Int> cells)` — places, owner Player, every cell that is not already a road; throws `InvalidOperationException` (nothing placed) when a cell is off the grid, forbidden or under another building, or when no cell is new; raises `Changed` once.
  - `int Construction.NewRoadCellsOf(CityMarket market, IReadOnlyList<Vector2Int> cells)` — cells not already roads; −1 when one cannot hold a road.
  - `ConstructionQuote Construction.QuoteRoads(CityMarket market, CargoHold hold, Treasury treasury, BuildingCost costPerCell, IReadOnlyList<Vector2Int> cells, bool isFree = false)` — impossible when `NewRoadCellsOf` is −1 or 0; otherwise the quote of `costPerCell.Times(n)`.
  - `bool Construction.TryBuildRoads(CityMarket market, CargoHold hold, Treasury treasury, BuildingCost costPerCell, IReadOnlyList<Vector2Int> cells, Action<int, int, long> materialBought = null, bool isFree = false)` — all or nothing.
  - `BuildingCost BuildingCost.Times(int count)` — gold and every material's units × count; `count` < 1 throws.

- [ ] **Step 1: Write the failing tests** in `CityMarketRoadsTests`:
  - `PlayerHouseUnconnected_GivesNoRoom`; `PlayerHouseConnectedLaterByPlayerRoads_GivesItsRoomOnce`.
  - `StartingHouseUnconnected_GetsTheRoomOfItsClassRuleWhenConnected`.
  - `PlayerWorkshopUnconnected_DoesNotHire`; `PlayerWorkshopConnected_HiresOnTheNextDay`.
  - `CityHouse_IsBuiltWithItsRoadsAndIsConnected`; `CityHouse_NetworkWalledIn_NothingTakenNoHouseNoRoad`.
  - `AddPlayerRoads_OnlyExistingRoads_Throws`; `AddPlayerRoads_ACellUnderAHouse_ThrowsAndPlacesNothing`.
  - In `ConstructionTests`: `QuoteRoads_ChargesOnlyNewCells` (cost 50 gold, trace of 5 with 2 existing roads → 150); `QuoteRoads_OnlyExistingRoads_IsImpossible`; `TryBuildRoads_GoldShort_TakesNothing`; `TryBuildRoads_Free_TakesNothingAndBuilds`; `Times_MultipliesGoldAndMaterials`.
- [ ] **Step 2: Run** filter `CityMarketRoadsTests|ConstructionTests`. Expected: FAIL.
- [ ] **Step 3: Implement**, then update `EconomyDefinition.CreateMarket` to count `ConnectedHousesOf` and adapt the test helpers (`TestMarkets`, `TestBuildings`) so grids in existing tests get a town hall and roads where those tests expect room.
- [ ] **Step 4: Run** the whole EditMode suite. Expected: PASS (the content tests on the real layouts may fail until Task 6 — note which, they must be exactly the room/connection ones).
- [ ] **Step 5: Commit** — `Marché : les bâtiments ne comptent que raccordés`.

### Task 5: Data — definitions, catalogue, layouts, setup

**Files:**
- Create: `Assets/Scripts/Game/Population/RoadDefinition.cs`, `TownHallDefinition.cs`; `Assets/Scripts/Game/CityView/RoadsidePlacementDefinition.cs`
- Modify: `Assets/Scripts/Game/Population/BuildingsDefinition.cs`, `BuildingOffers.cs` (add `RoadOffer`), `Assets/Scripts/Game/Economy/WorldEconomy.cs`, `Assets/Scripts/Game/CityView/CityLayoutDefinition.cs`, `Assets/Scripts/Editor/WorldMap/WorldMapSetup.cs`
- Assets (made by running the setup): `Assets/Data/Buildings/Road.asset`, `Assets/Data/Buildings/TownHall.asset`, `Assets/Data/Buildings/Placement/RoadsidePlacement.asset`; `Buildings.asset` references them and uses the roadside strategy.
- Test: `BuildingsDefinitionTests`, `BuildingDefinitionTests`, `CityLayoutDefinitionTests`, `BuildingsSetupTests`

**Interfaces:**
- Consumes: Tasks 1–4.
- Produces:
  - `RoadDefinition : BuildingDefinition` (`TryValidate` also requires a 1×1 footprint), `TownHallDefinition : BuildingDefinition` (nothing more). Update `BuildingDefinition`'s summary: not every building is the player's.
  - `BuildingsDefinition`: fields `road`, `townHall`; `Road`, `TownHall` properties; `TryValidate` fails with "it has no road" / "it has no town hall"; `RoadOffer CreateRoadOffer(EconomyDefinition economy, Action<BuildingDefinition, string> unavailable)`.
  - `RoadOffer(string name, BuildingCost costPerCell, string costText)` with `Name`, `CostPerCell`, `CostText`.
  - `WorldEconomy.RoadOffer`.
  - `CityLayoutDefinition`: `[Serializable] TownHallPlace { Vector2Int origin; BuildingOrientation orientation; }`, field `bool hasTownHall` + `townHall`, `List<Vector2Int> roads`; `HasTownHall`, `TownHallPlace`, `Roads`. `TryCreateGrid` places the town hall (catalogue's footprint), then the roads, then the houses; problems: "it has no town hall", "the town hall cannot stand…", "the road at (x, y) cannot stand…". `SetContent(int width, int height, IEnumerable<Vector2Int> forbidden, IEnumerable<StartingHouse> houses, GridPlace? townHall, IEnumerable<Vector2Int> roads)` replaces the old overload.
  - `WorldEconomy.WarnAboutRoom` counts `ConnectedHousesOf`.
  - `WorldMapSetup.CompleteBuildings` creates and adds when missing `Road.asset` (display name "Route", gold 50, no materials, footprint 1×1) and `TownHall.asset` ("Hôtel de ville", gold 0, footprint 3×2), by the same rules as the houses (an entry removed by hand stays out only for lists; these are single fields: fill them when null). `CompletePlacement` gives a catalogue whose strategy is null or the random asset the roadside one (made when missing) — the random strategy stays as an asset but is no longer used.
  - `WorldMapSetup.TryCreateLayout`: the town hall first, at the free place nearest the grid's centre (`CityLayoutEditing.TownHallPlaceNear` from Task 6 lives in Editor — put the Core part, `static bool TownHallPlacement.TryNear(CityGrid grid, Footprint footprint, Vector2 centre, out GridPlace place)`, in `Assets/Scripts/Core/CityView/TownHallPlacement.cs` with tests `TownHallPlacementTests`: nearest by distance of the footprint's centre, ties by `GetPlaces` order), then `StartingHouses.TryPlace` with `RoadsidePlacement` seeded by the city's index; saves town hall and roads.

- [ ] **Step 1: Write the failing tests**: `BuildingsDefinitionTests`: `NoRoad_IsInvalid`, `NoTownHall_IsInvalid`, `RoadOfTwoCells_IsInvalid`, `CreateRoadOffer_CostIsGoldOnly`. `CityLayoutDefinitionTests`: `NoTownHall_IsUnusable`, `TownHallAndRoads_AreOnTheGrid`, `StartingHouseOnARoad_IsUnusable`, `StartingHouseTouchingRoads_IsConnected`. `TownHallPlacementTests` as above. `BuildingsSetupTests`: `CompleteBuildings_AddsRoadAndTownHall`, `CompletePlacement_ReplacesRandomByRoadside`.
- [ ] **Step 2: Run** filter on those fixtures. Expected: FAIL.
- [ ] **Step 3: Implement**, then run `Tools > Dark Fantasy Merchant > Build World Map Scene` through `Unity_RunCommand` (`WorldMapSetup.Build`) and check the three new assets and `Buildings.asset`'s references exist; console clean (`Unity_GetConsoleLogs`), apart from the expected "has no town hall … no grid" warnings of the existing layouts, fixed in Task 6.
- [ ] **Step 4: Run** the filtered tests. Expected: PASS.
- [ ] **Step 5: Commit** (assets with their `.meta`) — `Données : routes, hôtel de ville, placement le long des routes`.

### Task 6: City Layout window and migration of the layouts

**Files:**
- Modify: `Assets/Scripts/Editor/CityView/CityLayoutEditing.cs`, `CityLayoutWindow.cs`
- Assets: every `Assets/Data/CityLayouts/*Layout.asset`
- Test: `CityLayoutEditingTests.cs`, `CityViewContentTests.cs`

**Interfaces:**
- Consumes: Task 5's layout fields, Task 2's `RoadPlanner`, `TownHallPlacement`.
- Produces (`CityLayoutEditing`, each records an undo and marks dirty, nothing recorded when nothing changes):
  - `bool SetTownHall(CityLayoutDefinition layout, BuildingsDefinition buildings, GridPlace place)` — places or moves it; false when it cannot stand there (its old place not counting).
  - `void RemoveTownHall(CityLayoutDefinition layout)`.
  - `void SetRoads(CityLayoutDefinition layout, BuildingsDefinition buildings, IEnumerable<Vector2Int> cells, bool add)` — skips cells off the grid, forbidden, or under the town hall or a house.
  - `int AutoConnect(CityLayoutDefinition layout, BuildingsDefinition buildings)` — town hall when missing (`TownHallPlacement.TryNear` the mean of the starting houses' centres, the grid's centre without houses), then for each starting house in list order not connected, `RoadPlanner.TryFindRoads` and add them; returns the houses left unconnected.
  - `Resize`, `SetForbidden`, `TryAddHouse`, `RemoveHouses` take the town hall and roads into account (dropped when outside; a forbidden cell or a house never covers them).
  - Window: tools "Town hall" and "Road" (brush) added to `Tool`/`ToolLabels`, right click erases what the tool paints; a "Town hall and roads" button calling `AutoConnect` and showing in the window how many houses stay unconnected; the town hall drawn in a colour of its own (`#C9A227`), roads in `#8A7B62`, unconnected starting houses outlined in `ShortageColor`; the room lines count connected houses only.

- [ ] **Step 1: Write the failing tests** in `CityLayoutEditingTests`: `SetTownHall_MovesIt`, `SetTownHall_OnAHouse_ReturnsFalse`, `SetRoads_SkipsHousesAndForbidden`, `AutoConnect_PlacesTownHallAndConnectsEveryHouse`, `AutoConnect_WalledInHouse_IsCounted`, `AutoConnect_Twice_ChangesNothing`, `Resize_DropsRoadsAndTownHallOutside`. In `CityViewContentTests`: `EveryLayout_HasATownHall`, `EveryStartingHouse_IsConnected`, and the existing starting-room test counts connected houses.
- [ ] **Step 2: Run** filter `CityLayoutEditingTests|CityViewContentTests`. Expected: FAIL.
- [ ] **Step 3: Implement** the editing operations and the window.
- [ ] **Step 4: Migrate**: through `Unity_RunCommand`, call `AutoConnect` on every `CityLayoutDefinition` under `Assets/Data/CityLayouts`, log those with unconnected houses, `AssetDatabase.SaveAssets()`. Any unconnected house is reported to the user rather than moved.
- [ ] **Step 5: Run** the whole EditMode suite. Expected: PASS.
- [ ] **Step 6: Commit** — `City Layout : hôtel de ville et routes, layouts raccordés`.

### Task 7: Drawing roads, the town hall and inactive buildings

**Files:**
- Modify: `Assets/Scripts/Game/CityView/CityGridView.cs`, `Assets/Scripts/Editor/CityView/CityViewSetup.cs`
- Assets (setup-made): `Assets/Art/CityView/Block3x2.png`, `Block2x3.png`
- Test: `BuildingDrawOrderTests.cs` / `IsoProjectionTests.cs` (`DrawKeyOf_OrdersEveryFrontBlockAfterTheOneBehindIt_*` extended to 3×2 and 2×3)

**Interfaces:**
- Consumes: Task 1's kinds and `BuildingConnected`.
- Produces:
  - `enum GhostState { Connected, Unconnected, Impossible }` (Game/CityView), green/yellow/red.
  - `CityGridView.ShowGhost(Vector2Int origin, Vector2Int size, GhostState state)` replaces the `bool` overload.
  - `CityGridView.ShowRoadGhost(IReadOnlyList<Vector2Int> cells, GhostState state)` — one flat cell tile per cell at `GhostSortingOrder`, pooled; `HideGhost` hides them too.
  - Roads: cell-sprite tiles tinted `roadColor` (`#8A7B62`) at sortingOrder 1; buildings start at 2 (`FirstBuildingSortingOrder`). Roads are not in `BuildingDrawOrder`.
  - Town hall: `block3x2Sprite`/`block2x3Sprite`, colour `townHallColor` (`#C9A227`), in the draw order like other blocks.
  - An unconnected house or workshop: its colour × `inactiveShade` (0.45 on RGB, alpha kept); on `BuildingConnected` its renderer gets its normal colour.
  - `CityViewSetup` makes the two block sprites and fills the two references.

- [ ] **Step 1: Write the failing tests**: extend the brute-force draw-order tests to 3×2 and 2×3 blocks (with 1×1, 2×1, 1×2, 2×2 neighbours).
- [ ] **Step 2: Run** filter `BuildingDrawOrderTests|IsoProjectionTests`. Expected: FAIL only if `DrawKeyOf` mis-orders them; if PASS straight away, keep the tests (they pin the size).
- [ ] **Step 3: Implement** `CityGridView` and `CityViewSetup`; run the setup through `Unity_RunCommand`; console clean.
- [ ] **Step 4: Check in play mode**: open a city view (`CityViewLoader.Open`), capture the camera: town hall, roads under the houses, no darkened starting house. Exit play mode.
- [ ] **Step 5: Commit** — `Vue de ville : routes, hôtel de ville et bâtiments inactifs`.

### Task 8: Tracing roads in the city view

**Files:**
- Modify: `Assets/InputSystem_Actions.inputactions` (CityView map: new action `MiddlePan`, button, `<Mouse>/middleButton`), `Assets/Scripts/Game/CityView/CityViewInput.cs`, `CityPlacementController.cs`, `PlayerBuilder.cs`, `BuildBarController.cs`, `Assets/UI/CityView/CityView.uxml` (`roads-button`)
- Test: `BuildBarTests.cs`, `PlayerBuilderTests.cs`, `WorldMapInputActionsTests.cs` (the CityView map has `MiddlePan` on the middle button)

**Interfaces:**
- Consumes: Tasks 4, 5, 7.
- Produces:
  - `CityViewInput.TracesWithLeftButton` (bool): when true, the left press/move/release raise `TraceStarted(Vector2)`, `TraceMoved(Vector2)`, `TraceEnded(Vector2)` (screen positions; a press over the UI starts nothing) instead of `Clicked`/`Dragged`. `MiddlePan` always pans (`Dragged`). Setting it cancels the gestures in progress.
  - `PlayerBuilder.QuoteRoads(CityDefinition city, RoadOffer offer, IReadOnlyList<Vector2Int> cells)` and `TryBuildRoads(...)` → `bool`, through `Construction`, one notification `BuildingBuiltText(city, offer.Name)` per trace, `MaterialBought` as for the others.
  - `CityPlacementController.StartPlacing(RoadOffer offer)`; public `TraceStart(Vector2)`, `TraceMove(Vector2)`, `TraceEnd(Vector2)` for play-mode checks. Without a press the ghost is one cell; while dragging it is `RoadTrace.Of(startCell, cell)`; state: `Impossible` if `QuoteRoads` is impossible (blocked cell, gold short, nothing new), `Connected` if any trace cell `JoinsNetwork` or is a network road, else `Unconnected`. Release builds and keeps the mode; a trace that leaves the grid is clamped to the last cell on the grid. `Cancel` and `StartPlacing` of a house/workshop set `TracesWithLeftButton` false.
  - House/workshop ghost: `Impossible` as today, else `Connected` when `grid.WouldConnect`, else `Unconnected`.
  - Build bar: third category button "Routes" (`RoadsText`), one offer, its button reading `OfferText(name, gold)` of one cell; its cost tip `CostText` + " par case".

- [ ] **Step 1: Write the failing tests**: `BuildBarTests`: `RoadsCategory_ShowsTheRoadOffer`; `PlayerBuilderTests`: `TryBuildRoads_PostsOneNotification`, `QuoteRoads_NoMarket_IsImpossible`; `WorldMapInputActionsTests`: `CityViewMap_HasMiddlePanOnMiddleButton`.
- [ ] **Step 2: Run** filter on those fixtures. Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the whole EditMode suite. Expected: PASS.
- [ ] **Step 5: Check in play mode** through `Unity_RunCommand`: open a city, `StartPlacing(roadOffer)`, `TraceStart`/`TraceMove`/`TraceEnd` from a cell touching the town hall: treasury down by 50 × new cells, one notification, roads drawn; a trace over a house builds nothing; a house placed against the new road: ghost green, built, `IsConnected` true; one placed far away: ghost yellow, drawn darkened. `Cancel` restores `TracesWithLeftButton` false. Exit play mode.
- [ ] **Step 6: Commit** — `Vue de ville : tracer des routes`.

### Task 9: Rules and project description

**Files:**
- Modify: `.claude/rules/city-view.md`, `.claude/rules/population.md`, `CLAUDE.md` (project state: roads and town hall; input: `MiddlePan`)

- [ ] **Step 1:** Record what the code cannot say: the network is kept by the grid and `BuildingConnected` is the only way room and hiring start; the town hall alone connects nothing; `PlaceRoads` floods once; the room of a starting house connected later is its class's housing rule's; a layout without town hall is unusable; the random strategy is kept but unused; roads at sortingOrder 1; a new footprint size needs the draw-order tests extended (3×2 now covered).
- [ ] **Step 2:** Run the whole EditMode suite one last time. Expected: PASS.
- [ ] **Step 3: Commit** — `Règles : routes et hôtel de ville`.
