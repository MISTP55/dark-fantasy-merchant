# City walls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every city walls that close in the space where buildings stand, generated around the town hall and painted by hand in the City Layout window.

**Architecture:** The wall is a new `BuildingKind`, placed as a 1×1 city building. `CityGrid` computes its enclosure (a side-only flood from the town hall, stopped by walls and water) and, once a grid is in play (`KeepsInsideWalls`), refuses every cell outside it through one test (`IsBuildable`) that `CanPlace`, `RoadPlanner` and `CityMarket.TryCollectNewRoads` share. `TryCreateGrid` and the editor place without the restriction, then the game grid is validated (`TryValidateWalls`) and switched to it.

**Tech Stack:** Unity 6000.6.4f1, C#, NUnit EditMode tests, UI Toolkit (none here), IMGUI editor window.

**Spec:** `docs/superpowers/specs/2026-10-25-city-walls-design.md`

## Global Constraints

- Code, comments, asset and folder names in English; what the player reads in French: the wall's name is **"Mur"**.
- No mention of Patrician III anywhere (code, comments, rules, commits).
- Values in assets or named constants: `GeneratedWallSide = 45`.
- Walls: footprint **1×1**, owner `BuildingOwner.City`, never in the build bar, never darkened, never part of the road network.
- Enclosure flood: by **sides only**, from every cell of the town hall, never onto a wall nor onto water (`IsForbidden`); cells under other buildings are crossed. Reaching any cell on the grid's edge makes it **open**.
- A usable grid: closed enclosure, and every **land** cell of every building other than a wall enclosed (the port's water rows are not checked).
- Create assets with their `.meta`; never edit `.sln`/`.csproj`; tests through the `run-tests` skill.
- Commit messages in French, on `main`, at the end of each task.

## Review Focus

1. **A town hall touching the grid's edge** — its own cell is on the edge, so the enclosure is open, whatever the walls. Test in Task 1 (`Enclosure_IsOpen_WhenTheTownHallTouchesTheEdge`).
2. **Two walls meeting only by a corner** — they close the way (side-only flood). Test in Task 1 (`Enclosure_DiagonalWalls_Close`).
3. **A road traced by the player across the wall** — `QuoteRoads`/`TryBuildRoads` go through `CityMarket.TryCollectNewRoads`, which must refuse a cell outside. Test in Task 1 (`TryCollectNewRoads_RefusesACellOutsideTheWalls`, in the economy tests next to the existing ones of that method).
4. **A generated outline crossing the port's land row or another building** — the wall is skipped there and the enclosure leaks; generation must fail with a problem naming the walls, never return an open grid. Test in Task 2 (`TryBuild_Fails_WhenTheWallsDoNotClose`).
5. **Painting water over a wall, or a wall over a berth / water / building** — refused in the window, so a cell is one thing at a time. Tests in Task 4.

---

### Task 1: The wall and the enclosure in `CityGrid`

**Files:**
- Modify: `Assets/Scripts/Core/CityView/BuildingKind.cs`
- Modify: `Assets/Scripts/Core/CityView/CityGrid.cs`
- Modify: `Assets/Scripts/Core/CityView/RoadPlanner.cs:211-217` (`IsOpen`)
- Modify: `Assets/Scripts/Core/Economy/CityMarket.cs:499-525` (`TryCollectNewRoads`)
- Create: `Assets/Tests/EditMode/CityView/CityGridWallTests.cs`
- Test: the economy test file that already covers `TryCollectNewRoads` (find it with a search for that name)

**Interfaces:**
- Produces:
  - `BuildingKind.Wall()`, `bool BuildingKind.IsWall`
  - `bool CityGrid.HasClosedWalls { get; }` — a town hall is placed and the flood reaches no edge cell
  - `bool CityGrid.IsEnclosed(Vector2Int cell)` — `HasClosedWalls` and the cell was reached
  - `bool CityGrid.KeepsInsideWalls { get; set; }` — off at creation; when on, `IsBuildable` also requires `IsEnclosed`
  - `bool CityGrid.IsBuildable(Vector2Int cell)` — inside, not water, and (`!KeepsInsideWalls` or enclosed); says nothing about occupancy
  - `bool CityGrid.TryValidateWalls(out string problem)` — false with "its walls do not close: the town hall reaches the edge at (x, y)" or "the {kind} at (x, y) stands outside the walls" (kind in lowercase English: house, workshop, road, town hall, port)

- [ ] **Step 1: Write the failing tests** in `CityGridWallTests` (a 10×10 grid, `SidesOnly` unless said; a helper `Ring(grid, rect)` placing walls on a rect's outline):
  - `AWall_IsPlacedAsAOneByOneCityBuilding` — `Place(BuildingKind.Wall(), City, 1×1, …)` succeeds, `BuildingAt(cell).Kind.IsWall`; a 2×1 wall throws `ArgumentException`.
  - `AWall_IsNeitherRoadNorConnected` — a wall touching the town hall: `IsNetworkRoad` false, `IsConnected` false, no `BuildingConnected` raised.
  - `Enclosure_IsClosed_InsideARing` — town hall 3×2 at (4,4), ring on rect (1,1,8,8): `HasClosedWalls`, `IsEnclosed((2,2))`, `!IsEnclosed((0,0))`, `!IsEnclosed` of a wall cell.
  - `Enclosure_IsOpen_WithAGap` — same ring with one wall missing: `!HasClosedWalls`, `!IsEnclosed((5,5))`.
  - `Enclosure_IsOpen_WithoutTownHall` — ring alone: `!HasClosedWalls`.
  - `Enclosure_IsOpen_WhenTheTownHallTouchesTheEdge` — town hall at (0,4), walls elsewhere: `!HasClosedWalls`.
  - `Enclosure_WaterCloses` — water rows y 0..1, walls on x=1, x=8 (y 2..8) and y=8: closed; `!IsEnclosed` of a water cell.
  - `Enclosure_DiagonalWalls_Close` — a ring whose corner cell is missing but whose two neighbours along the diagonal are walls still closes (side-only flood); e.g. ring (1,1,8,8) without (1,1) and with (2,2) a wall: closed.
  - `Enclosure_CrossesOtherBuildings` — a house inside the ring between the town hall and a cell: that cell stays enclosed.
  - `Enclosure_FollowsWallsPlacedLater` — open before the last wall, closed after it.
  - `KeepsInsideWalls_RefusesOutside` — closed ring, flag on: `CanPlace` of a 2×1 house outside false, inside true, straddling the wall false; `GetPlaces` lists no place outside; a wall can still be `Place`d outside (walls are never refused for the enclosure).
  - `KeepsInsideWalls_Off_PlacesAnywhere` — same grid, flag off: outside `CanPlace` true.
  - `TryValidateWalls_NamesTheBuildingOutside` — closed ring, a house placed outside with the flag off: false, problem contains "house" and its origin.
  - `TryValidateWalls_ChecksOnlyThePortsLandRows` — a port whose water rows lie outside the ring and land row inside: true.
  - `TryValidateWalls_Open` — false, problem contains "do not close".
  - In `RoadPlannerTests`: `TryFindRoads_StaysInsideTheWalls` — flag on, a house whose shortest way to the town hall runs outside: the roads found are all enclosed (or none).
  - In the economy tests of `TryCollectNewRoads`: `TryCollectNewRoads_RefusesACellOutsideTheWalls`.

- [ ] **Step 2: Run them; expect compile failure (`Wall`, `IsEnclosed`… missing).**

- [ ] **Step 3: Implement.** `PlaceWithoutNetwork` throws for a wall that is not 1×1 (as for a road). The enclosure is a `bool[]` recomputed lazily: placing a wall or the town hall marks it stale; `RemoveHouse` does not. `CanPlace` tests each cell with `IsBuildable` instead of `forbidden[index]`; `Place` for a wall skips the enclosure part (a private `Fits(footprint, place, insideWalls)` shared by both). `RoadPlanner.IsOpen` and `TryCollectNewRoads` replace their `Contains && !IsForbidden` by `IsBuildable`. Update `CityGrid`'s doc comments and the `BuildingKind` summary.

- [ ] **Step 4: Run the CityView and Economy tests; expect all PASS** (existing grids are made with the flag off, so nothing else changes).

- [ ] **Step 5: Commit** — `Murs : le mur et l'enceinte de la grille`.

### Task 2: Walls in the generation

**Files:**
- Modify: `Assets/Scripts/Core/CityView/StartingLayout.cs`
- Test: `Assets/Tests/EditMode/CityView/StartingLayoutTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces: `StartingLayout.TryBuild(int width, int height, PortFacing shore, int waterRows, int townHallDistance, int houseArea, int wallSide, PortFootprint port, …)` — `wallSide` inserted right after `houseArea`; the returned grid has `KeepsInsideWalls` on.

- [ ] **Step 1: Write the failing tests** (the helper `TryBuild` passes `wallSide` 25 on its 40×40 grids, which keeps the square inside the grid on the land sides for every shore):
  - `TryBuild_WallsTheOutlineOfTheSquare` (4 shores) — every cell of the outline of the 25-side square centred on the town hall (`(size − side) / 2` as `HouseArea`) that is inside the grid and land is a wall; no wall elsewhere; `HasClosedWalls`.
  - `TryBuild_PutsEveryBuildingInsideTheWalls` (4 shores) — `TryValidateWalls` true, and `KeepsInsideWalls` on.
  - `TryBuild_Fails_WhenTheWallsDoNotClose` — a `wallSide` so large the square leaves the grid on a land side (e.g. 41 on 40×40): false, no grid, problem contains "walls".
  - `TryBuild_Fails_WhenTheHousesDoNotFitInsideTheWalls` — `wallSide` 7 with 10 houses: false, problem contains "houses".
  - Update the existing calls to the new signature.

- [ ] **Step 2: Run them; expect FAIL.**

- [ ] **Step 3: Implement.** After placing the town hall: walls on the outline cells where `CanPlace(1×1)` holds; if `!HasClosedWalls`, fail with "its walls do not close on a {w} by {h} grid"; turn `KeepsInsideWalls` on; then the houses. Share the square's computation with `HouseArea` (one private `SquareAround(townHall, side)`). Update the doc comment.

- [ ] **Step 4: Run StartingLayout, StartingHouses and DefaultLayout tests; expect PASS** (the last may still fail until Task 3 passes `wallSide`: then fix only the call).

- [ ] **Step 5: Commit** — `Murs : la génération entoure l'hôtel de ville`.

### Task 3: The wall's asset and the layout's walls

**Files:**
- Create: `Assets/Scripts/Game/Population/WallDefinition.cs` (`[CreateAssetMenu(fileName = "Wall", menuName = "Dark Fantasy Merchant/Wall")]`, empty sealed subclass of `BuildingDefinition`, like `TownHallDefinition`)
- Modify: `Assets/Scripts/Game/Population/BuildingsDefinition.cs` (field `wall`, property `Wall`, `TryValidate` "it has no wall" / its problem, `DefinitionOf` returns it for `IsWall`)
- Modify: `Assets/Scripts/Game/CityView/CityLayoutDefinition.cs`
- Modify: `Assets/Scripts/Editor/WorldMap/WorldMapSetup.cs` (`LoadOrCreateWall` at `Assets/Data/Buildings/Wall.asset`, name "Mur", footprint 1×1, no cost; filled in the catalogue like the town hall; `CompleteFootprints` 1×1)
- Modify: `Assets/Tests/EditMode/Common/TestBuildings.cs` (`CreateWall`, in `CreateCatalogue`)
- Test: `Assets/Tests/EditMode/CityView/CityLayoutDefinitionTests.cs`, and every test whose layout must stay usable (`CityLayoutEditing*Tests`, `WorldMapViewUsableCityTests`, …)

**Interfaces:**
- Consumes: Tasks 1–2.
- Produces: `IReadOnlyList<Vector2Int> CityLayoutDefinition.Walls`; `SetContent(width, height, forbidden, houses, townHall, roads, port, berths, IEnumerable<Vector2Int> walls)` (walls last); `SetContent(CityGrid)` keeps the grid's walls; `BuildingsDefinition.Wall`.

- [ ] **Step 1: Write the failing tests** in `CityLayoutDefinitionTests` (give the fixture's layouts walls with a helper that rings them; move the fixture's town hall off the edge, since a town hall on the edge is open):
  - `TheWalls_AreOnTheGrid` — walls of the layout are walls of the grid.
  - `NoWalls_IsUnusable` — problem contains "do not close".
  - `ABuildingOutsideTheWalls_IsUnusable` — a starting house outside: problem contains "outside the walls".
  - `AWallThatCannotStand_IsUnusable` — on water, on the town hall, or outside the grid: problem names the wall's cell.
  - `TheGrid_KeepsInsideTheWalls` — the grid returned has `KeepsInsideWalls` on.
  - `SetContent_KeepsTheGridsWalls`.
  - `BuildingsDefinitionTests` (or where the catalogue is validated): `NoWall_IsInvalid`, `DefinitionOf_TheWall`.

- [ ] **Step 2: Run them; expect FAIL.**

- [ ] **Step 3: Implement.** Serialized `List<Vector2Int> walls` with a tooltip. `TryCreateGrid` places the walls right after the town hall (refusing one that cannot stand: "the wall at (x, y) cannot stand: …"), then port, berths, roads and houses unchanged, then `TryValidateWalls`, then `HarbourPaths.TryValidate`, then turns `KeepsInsideWalls` on. Update its doc and `problem` comment. Fix every test fixture that builds a usable layout so it has closed walls.

- [ ] **Step 4: Run all EditMode tests; expect PASS except `CityViewContentTests` (the layout assets have no walls yet: Task 5).**

- [ ] **Step 5: Commit** — `Murs : l'asset du mur et les murs des grilles`.

### Task 4: Walls in the City Layout window

**Files:**
- Modify: `Assets/Scripts/Editor/CityView/CityLayoutEditing.cs`
- Modify: `Assets/Scripts/Editor/CityView/CityLayoutWindow.cs`
- Create: `Assets/Tests/EditMode/CityView/CityLayoutEditingWallTests.cs`

**Interfaces:**
- Consumes: Tasks 1–3.
- Produces: `CityLayoutEditing.SetWalls(CityLayoutDefinition layout, BuildingsDefinition buildings, IEnumerable<Vector2Int> cells, bool add)`; `CityLayoutEditing.GeneratedWallSide = 45`; `TryMakeGrid` places the walls (after the town hall) and never turns `KeepsInsideWalls` on.

- [ ] **Step 1: Write the failing tests:**
  - `SetWalls_AddsAndRemoves` — one undo step each, nothing recorded when nothing changes.
  - `SetWalls_SkipsWaterBuildingsPortAndBerths` — cells on water, under the town hall, the port, a house, a road or a berth are skipped.
  - `SetForbidden_SkipsWalls` — water is not painted over a wall.
  - `SetRoads_TryAddHouse_SetTownHall_SetPort_RefuseWalls` — a wall's cell takes none of them.
  - `TryAddHouse_OutsideTheWalls_IsAllowed` — the editor places outside the enclosure.
  - `Resize_DropsTheWallsOutside`.
  - `Erase_RemovesWalls` — through the same calls the Erase tool makes.
  - `TryGenerate_UsesGeneratedWallSide` (in `CityLayoutRegenerationTests` or `DefaultLayoutTests`): a generated 80×80 layout is usable and has walls.

- [ ] **Step 2: Run them; expect FAIL.**

- [ ] **Step 3: Implement.** `Apply` carries the walls (an optional `walls` argument, the layout's when null, filtered to the new size); `SetForbidden(forbid: true)` skips wall cells; `CanPlaceTownHall` and `SetRoads` skip wall cells; `TryMakeGrid` places walls. `TryGenerate` passes `GeneratedWallSide`. In the window: tool `Wall` after `Water` (label "Wall"), a brush (`BrushCells`), left click `SetWalls(add: true)`, right click `add: false`; Erase also calls `SetWalls(…, false)`; walls drawn `WallColor` dark grey (e.g. `#3A3A3A`); the existing unusable warning shows `TryCreateGrid`'s problem unchanged.

- [ ] **Step 4: Run the CityView tests; expect PASS (except `CityViewContentTests`).**

- [ ] **Step 5: Commit** — `Murs : l'outil Mur de la fenêtre des grilles`.

### Task 5: Drawing, regeneration and rules

**Files:**
- Modify: `Assets/Scripts/Editor/CityView/CityViewSetup.cs` (`WallSpritePath = ArtFolder + "/Wall.png"`, `CreateSprite(WallSpritePath, 1, 1, WallHeightPixels)` with `WallHeightPixels = 2 * PixelsPerUnit / 2` = 64, i.e. two cells of 32 px; fill `wallSprite`)
- Modify: `Assets/Scripts/Game/CityView/CityGridView.cs` (`wallSprite`, `wallColor = #3A3A3A`, kind name "Wall", `ColorOf` returns `wallColor`, never shaded; drawn and ranked like any block, scaled by `buildingScale`)
- Modify: `Assets/Data/CityLayouts/*.asset` (by the menu), `Assets/Art/CityView/Wall.png(.meta)`, `Assets/Data/Buildings/Wall.asset(.meta)`, the scenes (by the setup menu)
- Modify: `.claude/rules/city-view.md`, `.claude/rules/population.md` if it lists the catalogue's buildings, `CLAUDE.md` (Project state: walls)

- [ ] **Step 1: Write the failing test** — in `CityGridView`'s tests (or a new `WallSpriteTests`): a wall's block takes `wallSprite` and `wallColor`, and stays that colour after `BuildingConnected` of its neighbours.

- [ ] **Step 2: Implement and run it; expect PASS.**

- [ ] **Step 3: In the open Editor, run `Tools > Dark Fantasy Merchant > Build World Map Scene`, then `CityLayoutRegeneration.Run()`.** Expected: no error in the console; `Assets/Data/Buildings/Wall.asset` and `Assets/Art/CityView/Wall.png` exist; every layout has walls.

- [ ] **Step 4: Run all EditMode tests; expect all PASS, `CityViewContentTests` included.**

- [ ] **Step 5: Play-mode check** — open a city view, capture the camera: a dark grey ring of tall blocks around the town hall, closed by the water; the player's house ghost is red beyond the wall.

- [ ] **Step 6: Update the rules** — `city-view.md`: the wall and the enclosure (grid section), `KeepsInsideWalls` and its one test `IsBuildable`, `TryCreateGrid`'s new order and checks, generation's square (`GeneratedWallSide`), the Wall tool, the wall's sprite. `CLAUDE.md` Project state mentions walls.

- [ ] **Step 7: Commit** — `Murs : dessin, régénération des grilles et règles`.
