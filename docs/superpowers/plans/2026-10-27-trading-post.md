# Trading Post Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the player a trading post ("Comptoir") per city, on a reserved site of every city's layout, with an inventory that trades with the market, exchanges goods with the player's docked ships, supplies construction materials and costs a weekly rent above its capacity.

**Architecture:** The grid (`CityGrid`, Core) keeps the reserved site and refuses it to every other building through its existing placement tests; the trading post is placed on it by a dedicated method, like the port. The inventory is a new Core store, `Warehouse`, sharing with `CargoHold` two small interfaces (`IGoodSource`, `IGoodStore`) that `Trade`, `Transfer` and `Construction` take. `WorldEconomy` owns the player's trading posts per city; `PlayerBuilder` builds them; `PlayerExpenses` bills their rent; the city panel gets a "Comptoir" entry in its market drop-down and a "Transfert" tab.

**Tech Stack:** Unity 6000.6.4f1, C#, UI Toolkit, NUnit EditMode tests (run with the `run-tests` skill), unity-mcp for play-mode checks.

**Spec:** `docs/superpowers/specs/2026-10-27-trading-post-design.md`

## Global Constraints

- Core (`Assets/Scripts/Core`) has no `MonoBehaviour` and nothing that needs a scene; Game holds assets and thin adapters. New scripts go in their subsystem's subfolder (`CityView`, `Economy`, `Population`, `Player`, `WorldMap`).
- Everything the player reads is in French; code, comments, logs and asset names in English. Numbers with a no-break space for thousands (`TreasuryHudController.FormatNumber` / `FormatGold`).
- Lists the player reads sorted with `NameOrder`, striped with `ListStripes.Apply`; a good's name never followed by its unit.
- UI Toolkit only, on the shared `PanelSettings`.
- Every new scene object, asset and reference is created by `WorldMapSetup.Build` / `CityViewSetup` when missing.
- Nothing in the repository names the game this one draws inspiration from.
- Values from the spec, in assets: trading post footprint **2×3**, cost **20 000 gold, 50 Metal, 100 Timber**, capacity **2 000 barrels**, rent **1 gold per barrel per week** above capacity.
- Commit messages in French, as the repository's history. Commit at the end of each task, directly on `main`, no push.
- Each task that changes a subsystem's rules updates its `.claude/rules/<subsystem>.md` in the same commit.

## Review Focus

1. **A reserved cell taken by another path** — the city's own houses (`CityMarket.BuildCityHouse` through `RoadPlanner`), the player's road trace (`Construction.NewRoadCellsOf` / `TryCollectNewRoads`), a wall or the port in the layout: each must be refused. Tests in Task 1 (grid) and Task 4 (layout tools).
2. **Buying into a trading post far above capacity** — must never be blocked by room, and `Used`/`Excess` must not overflow `int`. Test in Task 5.
3. **Building a second trading post, or one where the grid has no free site** — the quote is impossible, nothing paid. Test in Task 6.
4. **Materials split across the trading post and the hold** — 30 Timber in the post, 50 in the hold, 100 needed: 30 from the post, 50 from the hold, 20 bought. Test in Task 6.
5. **The shown city's trading post changing while the panel is open** (built in the view, goods moved) — the drop-down and the Transfer tab appear and refresh without reopening. Play-mode check in Task 12 (and Tasks 10, 11).

---

### Task 1: The reserved site and the trading post on the grid

**Files:**
- Modify: `Assets/Scripts/Core/CityView/BuildingKind.cs`
- Modify: `Assets/Scripts/Core/CityView/CityGrid.cs`
- Test: `Assets/Tests/EditMode/CityView/CityGridTradingPostTests.cs` (create)
- Modify: `.claude/rules/city-view.md` (new section "The trading post site")

**Interfaces:**
- Produces:
  - `BuildingKind.TradingPost()`, `bool BuildingKind.IsTradingPost`
  - `bool CityGrid.CanReserveTradingPostSite(Footprint footprint, GridPlace place)` — inside, land, no building, no site yet, inside the walls while `KeepsInsideWalls`.
  - `void CityGrid.ReserveTradingPostSite(Footprint footprint, GridPlace place)` — throws `InvalidOperationException` where `CanReserve…` refuses.
  - `GridPlace? CityGrid.TradingPostSite`, `Footprint CityGrid.TradingPostFootprint`, `bool CityGrid.IsReserved(Vector2Int cell)`
  - `PlacedBuilding CityGrid.TradingPost` (null until built), `bool CityGrid.CanPlaceTradingPost` (a site and no trading post yet)
  - `PlacedBuilding CityGrid.PlaceTradingPost()` — owner `BuildingOwner.Player`, on the site in its orientation, raises `BuildingPlaced`, not part of the network, never `BuildingConnected`; throws when `!CanPlaceTradingPost`.

- [ ] **Step 1: Write the failing tests** in `CityGridTradingPostTests` (build grids as `CityGridPortTests` does):
  - `ReserveTradingPostSite_SetsSiteAndReservesItsCells`: 2×3 `Across` at (4, 4) → `TradingPostSite == place`, `IsReserved` true for the 6 cells (x 4..6, y 4..5), false at (7, 4).
  - `CanReserveTradingPostSite_RefusesWaterBuildingOffGridAndSecondSite`.
  - `CanReserveTradingPostSite_RefusesOutsideWallsWhileKeepingInsideWalls`.
  - `IsBuildable_IsFalseOnReservedCells`.
  - `CanPlace_RefusesAnyFootprintCoveringAReservedCell` (house 2×1, road 1×1) and `Place_ThrowsForAWallOnAReservedCell`.
  - `CanPlacePort_RefusesASiteCoveringAReservedCell`.
  - `GetPlaces_ListsNoPlaceOverlappingTheSite`.
  - `RoadPlanner_NeverRoutesThroughReservedCells`: town hall and a house separated by a 3-wide reserved strip with a detour → `TryFindRoads` result contains no reserved cell.
  - `PlaceTradingPost_PlacesThePlayersBuildingOnTheSiteAndRaisesBuildingPlaced`: kind `IsTradingPost`, owner Player, `Place == site`, `TradingPost` set, `CanPlaceTradingPost` false afterwards; `PlaceTradingPost` again throws.
  - `PlaceTradingPost_TouchingANetworkRoad_IsNeverConnected`: no `BuildingConnected` raised; `IsNetworkRoad` of a road touching only the post stays false.
  - `TryValidateWalls_ReportsASiteOutsideTheWalls`: reserve with `KeepsInsideWalls` off outside closed walls → false, problem mentions "trading post".

- [ ] **Step 2: Run the tests** (`run-tests`, filter `CityGridTradingPostTests`). Expected: compile errors / FAIL.

- [ ] **Step 3: Implement** — a `bool[] reserved` beside `forbidden`; `IsBuildable` and the private `Fits` (walls included, whatever `insideWalls`) refuse reserved cells; `CanPlacePort` too; `TryValidateWalls` checks every site cell `IsEnclosed` after its existing checks (`"the trading post site at (x, y) is outside its walls"`); `PlaceTradingPost` goes through `PlaceWithoutNetwork`, then raises `BuildingPlaced` as `Place` does, without `UpdateNetwork`. The reserved cells stay reserved once the post stands (they are under it anyway).

- [ ] **Step 4: Run all CityView tests.** Expected: PASS, the existing ones included.

- [ ] **Step 5: Update `city-view.md`** with the site's invariants (the reservation goes through `IsBuildable` and `Fits`, so every strategy, `RoadPlanner` and the player's roads respect it; only `PlaceTradingPost` covers it; not part of the network, like the port).

- [ ] **Step 6: Commit** `git commit -m "Comptoir : emplacement réservé et comptoir sur la grille"`

---

### Task 2: The trading post's definition in the catalogue

**Files:**
- Create: `Assets/Scripts/Game/Population/TradingPostDefinition.cs`
- Modify: `Assets/Scripts/Game/Population/BuildingsDefinition.cs`
- Modify: `Assets/Scripts/Editor/WorldMap/WorldMapSetup.cs` (asset `Assets/Data/Buildings/TradingPost.asset` when missing, linked to the catalogue)
- Modify: the test helpers that build a catalogue (search `ScriptableObject.CreateInstance<PortDefinition>` under `Assets/Tests/EditMode` and `Assets/Scripts/*/Common`) so every test catalogue has a valid trading post.
- Test: `Assets/Tests/EditMode/Population/TradingPostDefinitionTests.cs` (create); content check added to the existing buildings content test (search `BuildingsDefinition` in `*ContentTests.cs`).
- Modify: `.claude/rules/population.md`

**Interfaces:**
- Produces:
  - `sealed class TradingPostDefinition : BuildingDefinition` with `[SerializeField, Min(0)] int capacity` → `int Capacity`, `[SerializeField, Min(0)] long rentPerBarrel` → `long RentPerBarrel`; `TryValidate` adds "capacity under 0"/"rent under 0" to the base checks.
  - `TradingPostDefinition BuildingsDefinition.TradingPost`, required by `BuildingsDefinition.TryValidate` ("it has no trading post" / the definition's problem), like `Port`.
- Setup defaults: display name "Comptoir", footprint (2, 3), gold 20000, materials Metal ×50 and Timber ×100 (the goods `Assets/Data/Goods/Metal.asset`, `Timber.asset`), capacity 2000, rentPerBarrel 1.

- [ ] **Step 1: Write the failing tests**: `TryValidate_RefusesNegativeCapacityOrRent`, `TryValidate_AcceptsTheDefaults`; `BuildingsDefinition_TryValidate_RefusesACatalogueWithoutTradingPost`; content test `TradingPostAsset_Is2By3AndCosts20000Gold50Metal100Timber` with capacity 2000 and rent 1.
- [ ] **Step 2: Run them.** Expected: FAIL.
- [ ] **Step 3: Implement** the definition, the catalogue field and the setup; update the test helpers.
- [ ] **Step 4: Run `WorldMapSetup.Build` in the open Editor** (`Unity_RunCommand`), check the asset exists with the defaults, then run all tests. Expected: PASS.
- [ ] **Step 5: Commit** `"Comptoir : définition du bâtiment dans le catalogue"` (scripts, asset, `.meta`, catalogue, rule).

---

### Task 3: The site in the layouts, required, generated, and every layout regenerated

**Files:**
- Modify: `Assets/Scripts/Game/CityView/CityLayoutDefinition.cs`
- Modify: `Assets/Scripts/Core/CityView/StartingLayout.cs`
- Create: `Assets/Scripts/Core/CityView/TradingPostPlacement.cs`
- Modify: `Assets/Scripts/Editor/CityView/CityLayoutEditing.cs` (`TryGenerate` only, in this task)
- Modify: every caller of the long `SetContent` (tests listed by `grep -rn "SetContent(" Assets --include=*.cs`)
- Test: `CityLayoutDefinitionTests.cs`, `StartingLayoutTests.cs` (or the existing generation tests), `Assets/Tests/EditMode/CityView/TradingPostPlacementTests.cs` (create)
- Assets: every `Assets/Data/CityLayouts/*Layout.asset` (regenerated)
- Modify: `.claude/rules/city-view.md`

**Interfaces:**
- Consumes: Task 1's grid API; Task 2's `BuildingsDefinition.TradingPost`.
- Produces:
  - `CityLayoutDefinition`: serialized `bool hasTradingPostSite`, `TownHallSite`-like struct `tradingPostSite` (origin, orientation); `bool HasTradingPostSite`, `GridPlace TradingPostSite`.
  - The long `SetContent(...)` gains a last optional parameter `GridPlace? tradingPostSite = null`; `SetContent(CityGrid grid)` copies `grid.TradingPostSite`.
  - `TryCreateGrid` reserves the site right after the walls ("it has no trading post site" / "the trading post site at (x, y) cannot stand: …" / "the buildings have no trading post with a footprint"); the existing `TryValidateWalls` call then reports a site outside the walls.
  - `static bool TradingPostPlacement.TryNear(CityGrid grid, Footprint footprint, Vector2 centre, out GridPlace place)` — among `grid.GetPlaces(footprint)`, those whose footprint grown by one cell on every side lies inside the grid with every ring cell buildable (`IsBuildable`) and free; the nearest to `centre` by the middle of the footprint, ties by `GetPlaces` order. The ring keeps the town hall's sides open for the roads.
  - `StartingLayout.TryBuild` gains a `Footprint tradingPost` parameter (after `townHall`); after the walls and `KeepsInsideWalls = true`, it reserves `TryNear(grid, tradingPost, centre of the town hall)`, failing with "its trading post site does not fit inside its walls" when none.
  - `CityLayoutEditing.TryGenerate` passes `buildings.TradingPost.Footprint`.

- [ ] **Step 1: Write the failing tests**:
  - `TryCreateGrid_FailsWithoutATradingPostSite`, `TryCreateGrid_FailsWhenTheSiteIsOnWaterOrABuilding`, `TryCreateGrid_FailsWhenTheSiteIsOutsideTheWalls`, `TryCreateGrid_ReservesTheSite`.
  - `SetContent_FromAGrid_KeepsItsTradingPostSite`.
  - `TryNear_PicksTheNearestPlaceWithAFreeRing`, `TryNear_IsFalseWhenNoPlaceHasAFreeRing`.
  - `TryBuild_ReservesASiteInsideTheWallsThatNoHouseOrRoadCovers` (generate with a seed: every site cell `IsEnclosed`, `BuildingAt` null).
- [ ] **Step 2: Run them.** Expected: FAIL.
- [ ] **Step 3: Implement**, and update the other test fixtures that build a usable layout so that they place a site.
- [ ] **Step 4: Regenerate every layout** in the open Editor: `Unity_RunCommand` calling `CityLayoutRegeneration.Run()`; check the console has no error, then save the assets.
- [ ] **Step 5: Run all tests**, `CityViewContentTests` and `WorldMapViewUsableCityTests` included. Expected: PASS.
- [ ] **Step 6: Update `city-view.md`** (layout content, `TryCreateGrid` order, generation order "water, port, berths, town hall, walls, trading post site, houses and roads").
- [ ] **Step 7: Commit** `"Comptoir : emplacement obligatoire et généré dans les dispositions des villes"` (with the regenerated layout assets).

---

### Task 4: The site tool of the City Layout window

**Files:**
- Modify: `Assets/Scripts/Editor/CityView/CityLayoutEditing.cs`
- Modify: `Assets/Scripts/Editor/CityView/CityLayoutWindow.cs`
- Test: `Assets/Tests/EditMode/CityView/CityLayoutEditingTradingPostTests.cs` (create)
- Modify: `.claude/rules/city-view.md`

**Interfaces:**
- Consumes: Task 3's layout fields.
- Produces:
  - `static Vector2Int CityLayoutEditing.TradingPostSize(BuildingsDefinition buildings, GridPlace place)`
  - `static bool CityLayoutEditing.CanPlaceTradingPostSite(CityLayoutDefinition layout, BuildingsDefinition buildings, GridPlace place)` — grid of `TryMakeGrid` without the current site; walls not enforced.
  - `static bool CityLayoutEditing.SetTradingPostSite(...)` (one undo step "Place trading post site"; false and nothing recorded when it cannot stand), `static void RemoveTradingPostSite(CityLayoutDefinition layout)`, `static bool IsUnderTradingPostSite(CityLayoutDefinition layout, BuildingsDefinition buildings, Vector2Int cell)`.
  - `TryMakeGrid` reserves the layout's site right after creating the grid (so every other tool's placement is refused there through the grid); the private `Apply` carries the layout's site and drops it when it no longer fits inside the new size or covers a water cell.
  - `SetForbidden`, `SetRoads`, `SetWalls` skip cells under the site, as they skip the port.

- [ ] **Step 1: Write the failing tests**: `SetTradingPostSite_PlacesAndMovesTheSite`, `SetTradingPostSite_RefusesWaterAndBuildings`, `RemoveTradingPostSite_ClearsIt`, `SetRoads_SkipsCellsUnderTheSite`, `SetWalls_SkipsCellsUnderTheSite`, `SetForbidden_SkipsCellsUnderTheSite`, `TryAddHouse_RefusesAPlaceOnTheSite`, `SetTownHall_RefusesAPlaceOnTheSite`, `Resize_DropsASiteThatFallsOutside`, `Resize_KeepsASiteStillInside`.
- [ ] **Step 2: Run them.** Expected: FAIL.
- [ ] **Step 3: Implement** the editing functions, then the window: a `Tool.TradingPostSite` after `Tool.Berth`, labelled "Trading post site"; ghost of `TradingPostSize` green/red from `CanPlaceTradingPostSite`, R toggles `Along`/`Across`, left click `SetTradingPostSite`, right click on it `RemoveTradingPostSite`; the site drawn on the grid in its own colour; a warning "No trading post site: the city is not usable." when `!HasTradingPostSite`, next to the missing-port warning.
- [ ] **Step 4: Run all tests.** Expected: PASS. Open the window in the Editor on one city, check the tool draws (scene capture or screenshot).
- [ ] **Step 5: Commit** `"Comptoir : outil d'emplacement dans la fenêtre City Layout"`

---

### Task 5: The inventory store

**Files:**
- Create: `Assets/Scripts/Core/Economy/IGoodStore.cs` (both interfaces), `Assets/Scripts/Core/Economy/Warehouse.cs`, `Assets/Scripts/Core/Economy/Transfer.cs`
- Modify: `Assets/Scripts/Core/Economy/CargoHold.cs` (implements `IGoodStore`), `Assets/Scripts/Core/Economy/Trade.cs` (its four public methods take `IGoodStore` instead of `CargoHold`)
- Test: `Assets/Tests/EditMode/Economy/WarehouseTests.cs`, `TransferTests.cs` (create); add to the existing `TradeTests.cs`
- Modify: `.claude/rules/economy.md`

**Interfaces:**
- Produces:
  - `interface IGoodSource { int UnitsOf(int good); bool TryRemove(int good, int count); }`
  - `interface IGoodStore : IGoodSource { int RoomFor(int good); bool TryAdd(int good, int count); event Action Changed; }`
  - `sealed class Warehouse : IGoodStore` — `Warehouse(int capacity, GoodUnits units = null)` (throws for capacity < 0); `int Capacity`; `long Used` (barrels, bushels weighted as in a hold); `long Excess => Math.Max(0, Used - Capacity)`; `RoomFor` returns `int.MaxValue`; `TryAdd` always succeeds; `TryRemove` false when fewer are stored; `Changed` only on an actual change.
  - `static long Warehouse.WeeklyRentOf(IEnumerable<Warehouse> warehouses, long rentPerBarrel)` — Σ `Excess × rentPerBarrel`.
  - `static int Transfer.Move(IGoodStore from, IGoodStore to, int good, int units)` — moves `min(units, from.UnitsOf(good), to.RoomFor(good))`, returns it; 0 moves nothing and raises nothing.

- [ ] **Step 1: Write the failing tests**:
  - `TryAdd_AboveCapacity_Succeeds_AndCountsExcess`: capacity 2000, add 2150 barrels → `Used` 2150, `Excess` 150.
  - `Used_CountsBushelsAsSeveralBarrels`.
  - `TryAdd_FarAboveIntRange_DoesNotOverflow`: add `int.MaxValue` twice → `Used == 2L * int.MaxValue`.
  - `WeeklyRentOf_SumsExcessTimesRent`: two warehouses with excess 150 and 0, rent 1 → 150.
  - `TradeBuy_IntoAWarehouse_IsLimitedByStockAndGoldOnly`.
  - `Move_HoldToWarehouse_IgnoresCapacity`, `Move_WarehouseToHold_StopsWhenTheHoldIsFull`, `Move_MovesNoMoreThanTheSourceHolds`, `Move_NothingToMove_RaisesNoChanged`.
- [ ] **Step 2: Run them.** Expected: FAIL.
- [ ] **Step 3: Implement.** `CargoHold` keeps every current member; callers of `Trade` with a `CargoHold` compile unchanged.
- [ ] **Step 4: Run all tests.** Expected: PASS.
- [ ] **Step 5: Commit** `"Comptoir : inventaire, transferts et commerce avec la ville"`

---

### Task 6: Construction materials from the trading post, and building the trading post

**Files:**
- Create: `Assets/Scripts/Core/Population/MaterialSources.cs`
- Modify: `Assets/Scripts/Core/Population/Construction.cs`
- Test: add to the existing `ConstructionTests.cs` (search under `Assets/Tests/EditMode/Population`)
- Modify: `.claude/rules/population.md`

**Interfaces:**
- Consumes: Task 1 (`CanPlaceTradingPost`, `PlaceTradingPost`), Task 5 (`IGoodSource`).
- Produces:
  - `sealed class MaterialSources : IGoodSource` — `MaterialSources(IGoodSource first, IGoodSource second)`, either may be null; `UnitsOf` sums both; `TryRemove` takes from `first` then `second`, all or nothing.
  - Every public method of `Construction` takes `IGoodSource materials` where it took `CargoHold hold` (same position, still nullable).
  - `static ConstructionQuote Construction.QuoteTradingPost(CityMarket market, IGoodSource materials, Treasury treasury, BuildingCost cost, bool isFree = false)` — impossible when `market.Grid == null || !market.Grid.CanPlaceTradingPost`.
  - `static bool Construction.TryBuildTradingPost(CityMarket market, IGoodSource materials, Treasury treasury, BuildingCost cost, Action<int, int, long> materialBought = null, bool isFree = false)` — pays like the others, then `market.Grid.PlaceTradingPost()`.

- [ ] **Step 1: Write the failing tests**:
  - `MaterialSources_TakesFromTheFirstThenTheSecond`: post 30 Timber, hold 50, remove 60 → post 0, hold 20.
  - `TryBuildHouse_TakesMaterialsFromThePostThenTheHoldThenBuys`: cost 100 Timber, post 30, hold 50 → post 0, hold 0, market stock down 20, gold = cost gold + price of 20.
  - `QuoteTradingPost_IsImpossibleWithoutASiteOrWhenBuilt`.
  - `TryBuildTradingPost_PaysAndPlacesThePost`; `TryBuildTradingPost_Twice_PaysNothingTheSecondTime`.
  - `TryBuildTradingPost_Free_TakesNothing`.
  - `TryBuildTradingPost_SkipsSwitchedOffMaterials` (a cost made from `BuildingDefinition.MaterialsOf` with Metal inactive asks only Timber and gold — use the existing test pattern for switched-off materials).
- [ ] **Step 2: Run them.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run all tests.** Expected: PASS.
- [ ] **Step 5: Commit** `"Comptoir : matériaux pris au comptoir et construction du comptoir"`

---

### Task 7: The player's trading posts in the world, built and given

**Files:**
- Modify: `Assets/Scripts/Game/Economy/WorldEconomy.cs`
- Modify: `Assets/Scripts/Game/Population/BuildingOffers.cs` (new `TradingPostOffer`)
- Modify: `Assets/Scripts/Game/CityView/PlayerBuilder.cs`
- Modify: `Assets/Scripts/Game/Player/HomeCityStart.cs` (+ serialized `WorldEconomy`, wired in `WorldMapSetup`)
- Test: `PlayerBuilderTests.cs`, a new `Assets/Tests/EditMode/Economy/WorldEconomyTradingPostTests.cs` if `WorldEconomy` is testable as the existing tests do it (otherwise in `PlayerBuilderTests`)
- Modify: `.claude/rules/economy.md`, `.claude/rules/player.md`, `.claude/rules/city-view.md`

**Interfaces:**
- Consumes: Tasks 2, 5, 6.
- Produces:
  - `sealed class TradingPostOffer` — `TradingPostOffer(string name, BuildingCost cost, string costText, Footprint footprint)`; `Name`, `Cost`, `CostText`, `Footprint`.
  - `WorldEconomy.TradingPostOffer` (from `buildings.TradingPost`, like the other offers), `Warehouse WorldEconomy.TradingPostOf(CityDefinition city)` (null when the player has none there), `IEnumerable<Warehouse> WorldEconomy.TradingPosts`, `event Action<CityDefinition> TradingPostOpened`.
  - `Warehouse WorldEconomy.OpenTradingPost(CityDefinition city)` — after the grid has its trading post: makes the city's `Warehouse(buildings.TradingPost.Capacity, economy's GoodUnits)`, raises `TradingPostOpened`; throws when the city already has one.
  - `bool WorldEconomy.TryGiveTradingPost(CityDefinition city)` — `grid.PlaceTradingPost()` then `OpenTradingPost`; false when the city has no grid or `!CanPlaceTradingPost`.
  - `IGoodSource PlayerBuilder.MaterialsIn(CityDefinition city)` — `new MaterialSources(TradingPostOf(city), HoldIn(city))`; every quote and build of `PlayerBuilder` goes through it.
  - `ConstructionQuote PlayerBuilder.QuoteTradingPost(CityDefinition city, TradingPostOffer offer)`, `bool PlayerBuilder.TryBuildTradingPost(CityDefinition city, TradingPostOffer offer)` — on success `OpenTradingPost`, then the usual notification `BuildingBuiltText(city, "Comptoir")`, `MaterialBought` per material.
  - `HomeCityStart.Start` calls `TryGiveTradingPost(homeCity)` before opening the view; logs a warning when it fails.

- [ ] **Step 1: Write the failing tests**: `QuoteTradingPost_CostsTheOffersGoldAndMaterials`, `TryBuildTradingPost_OpensTheCitysTradingPostAndAnnouncesIt`, `TryBuildTradingPost_WhenOneStands_IsRefused`, `QuoteHouse_CountsMaterialsInTheTradingPost`, `TryGiveTradingPost_PaysNothingAndStartsEmpty`.
- [ ] **Step 2: Run them.** Expected: FAIL.
- [ ] **Step 3: Implement**, the setup wiring included.
- [ ] **Step 4: Run all tests**, then enter Play mode: the home city's grid has a trading post (`Unity_RunCommand` reading `GridOf(home).TradingPost`), its warehouse is empty, the console has no error; exit Play mode.
- [ ] **Step 5: Commit** `"Comptoir : comptoirs du joueur, construction et comptoir de la ville natale"`

---

### Task 8: The weekly rent

**Files:**
- Modify: `Assets/Scripts/Core/Player/WeeklyBill.cs`, `Assets/Scripts/Core/Player/WeeklyBilling.cs`
- Modify: `Assets/Scripts/Game/Player/ExpensesDefinition.cs`, `Assets/Scripts/Game/Player/PlayerExpenses.cs` (+ serialized `WorldEconomy`, wired in `WorldMapSetup`)
- Test: existing `WeeklyBillingTests.cs` / `WeeklyBillTests.cs`
- Modify: `.claude/rules/player.md`

**Interfaces:**
- Consumes: Task 5 (`Warehouse.WeeklyRentOf`), Task 7 (`WorldEconomy.TradingPosts`).
- Produces:
  - `WeeklyBill(int sailorCount, long sailorWeeklyWage, long warehouseRent = 0)` (throws for a negative rent), `long WarehouseRent`, `Total = SailorWages + WarehouseRent`.
  - `WeeklyBilling(Treasury treasury, int daysPerWeek, long sailorWeeklyWage, Func<int> sailorCount, Func<long> warehouseRent = null)` — read on the billing day.
  - `ExpensesDefinition.CreateBilling(Treasury, int daysPerWeek, Func<int> sailorCount, Func<long> warehouseRent = null)`.
  - `PlayerExpenses` passes `() => Warehouse.WeeklyRentOf(worldEconomy.TradingPosts, buildings.TradingPost.RentPerBarrel)`.

- [ ] **Step 1: Write the failing tests**: `Total_AddsWarehouseRent`, `Constructor_RefusesNegativeRent`, `StartDay_OnBillingDay_WithdrawsTheRentReadThatDay` (rent function changes between days: only the billing day's value is taken), `StartDay_RentIsTakenEvenInDebt`.
- [ ] **Step 2: Run them.** Expected: FAIL.
- [ ] **Step 3: Implement**, the wiring included.
- [ ] **Step 4: Run all tests.** Expected: PASS.
- [ ] **Step 5: Commit** `"Comptoir : loyer hebdomadaire au-delà de la capacité"`

---

### Task 9: Building the trading post in the city view

**Files:**
- Modify: `Assets/UI/CityView/*.uxml` (the build bar's category buttons: a fourth one, name `category-trading-post`, text "Comptoir")
- Modify: `Assets/Scripts/Game/CityView/BuildBarController.cs`, `CityPlacementController.cs`, `CityGridView.cs`, `CityViewController.cs`
- Modify: `Assets/Scripts/Editor/CityView/CityViewSetup.cs` (`tradingPostColor`, `reservedColor` defaults)
- Test: `BuildBarTests.cs` (static texts and offer state), `GroundMeshTests.cs` if the highlight goes through it
- Modify: `.claude/rules/city-view.md`

**Interfaces:**
- Consumes: Task 7 (`TradingPostOffer`, `QuoteTradingPost`, `TryBuildTradingPost`, `TradingPostOpened`), Task 1 (`TradingPostSite`, `IsReserved`).
- Produces:
  - `BuildBarController.TradingPostText = "Comptoir"`, `Category.TradingPost`, `event Action<TradingPostOffer> TradingPostChosen`; the offer's button disabled when the city's grid already has its trading post or the quote is impossible; its tip `BuildBarController.TradingPostBuiltText = "Votre comptoir est déjà construit dans cette ville."` when it stands *(builder's text)*, the cost tip otherwise. Refreshes also on the city's warehouse `Changed` and on `TradingPostOpened`.
  - `CityPlacementController.StartTradingPost(TradingPostOffer offer)`: turns `TracesWithLeftButton` off, shows the reserved cells highlighted (`CityGridView.ShowReservedCells(bool)`, ground tinted `reservedColor`), the ghost fixed on the site (`ShowGhost` at the site's place, green when the quote is possible, red otherwise), R does nothing; a click on a reserved cell builds through `PlayerBuilder.TryBuildTradingPost` and stops placing; Cancel stops placing; stopping always removes the highlight.
  - `CityGridView` draws a building whose kind `IsTradingPost` with the 2×3 / 3×2 block tinted `tradingPostColor`, ranked like any building, never darkened.

- [ ] **Step 1: Write the failing tests** for the testable parts: `OfferState_TradingPostBuilt_IsDisabledWithItsTip`, and, if the highlight is a `GroundMesh` tint, `ShowReservedCells_TintsOnlyReservedCellsAndRestoresThem`.
- [ ] **Step 2: Run them.** Expected: FAIL.
- [ ] **Step 3: Implement**, the setup defaults included; run `WorldMapSetup.Build`.
- [ ] **Step 4: Play-mode check** in the Editor: in a city without a post (move there or use another city's view through `CityViewLoader.Open`), with the debug tools' free building on, choose "Comptoir": camera capture shows the highlighted cells and the ghost; drive `CityPlacementController.Click` on a site cell: the post is drawn, the highlight gone, the offer disabled; the home city shows its post. Exit Play mode.
- [ ] **Step 5: Run all tests.** Expected: PASS.
- [ ] **Step 6: Commit** `"Comptoir : construction dans la vue de ville"`

---

### Task 10: The Market tab trades with the trading post

**Files:**
- Modify: `Assets/UI/WorldMap/CityInfoPanel.uxml` ("Navire :" → "Avec :"; the hold column header label gets a name, `market-store-header`), `CityInfoPanel.uss` (`market__hold--over`)
- Modify: `Assets/Scripts/Game/Economy/CityMarketSection.cs`, `Assets/Scripts/Game/WorldMap/CityInfoPanelController.cs`
- Test: the existing tests of `CityInfoPanelController`'s and `CityMarketSection`'s static texts (search `TradingShipLabels` under `Assets/Tests`)
- Modify: `.claude/rules/economy.md`, `.claude/rules/world-map.md`

**Interfaces:**
- Consumes: Task 5 (`IGoodStore`, `Warehouse`), Task 7 (`TradingPostOf`, `TradingPostOpened`, through `SimulationServices.Current.WorldEconomy` or the panel's serialized economy).
- Produces:
  - `CityMarketSection.Show(CityMarket market, IGoodStore store, string storeHeader)` — `storeHeader` "Cale" for a ship, "Comptoir" for the post; the fill label is `ShipInfoPanelController.FormatCargo` for a `CargoHold`, `FormatWarehouseFill` for a `Warehouse`, with `market__hold--over` and the tooltip while `Excess > 0`.
  - `static string CityMarketSection.FormatWarehouseFill(long used, int capacity)` → `"1 850 / 2 000 tonneaux"`.
  - `static string CityMarketSection.RentTipText(long excess, long weeklyRent)` → `"150 tonneaux au-delà de la capacité : loyer de 150 or par semaine."` *(builder's text)*.
  - `CityInfoPanelController.TradingPostChoice = "Comptoir"`; `static List<string> TradingPartnerLabels(IReadOnlyList<string> shipNames, bool hasTradingPost)` — the ship labels as `TradingShipLabels` makes them, then "Comptoir"; `NoTradingShip` only when both are empty.
  - Choice: the kept partner while still valid, otherwise the first docked ship, otherwise the post. The panel follows `TradingPostOpened` for the shown city.
  - `PlayerTraded` is raised for the post's trades as for a ship's (the debug recorder records them unchanged).

- [ ] **Step 1: Write the failing tests**: `FormatWarehouseFill_WritesUsedAndCapacityTheFrenchWay` (1850, 2000 → "1 850 / 2 000 tonneaux", no-break spaces), `RentTipText_…`, `TradingPartnerLabels_AppendsComptoirAfterTheShips`, `TradingPartnerLabels_NoShipNoPost_IsAucunNavire`, `TradingPartnerLabels_NoShipWithPost_IsComptoirOnly`.
- [ ] **Step 2: Run them.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Play-mode check**: home city panel, "Avec :" lists the ship then "Comptoir"; choosing "Comptoir" shows the column "Comptoir" and "0 / 2 000 tonneaux"; a buy price click with Ctrl buys 100 units into the post; with the ship sent away, the panel opens on "Comptoir". Exit Play mode.
- [ ] **Step 5: Run all tests.** Expected: PASS.
- [ ] **Step 6: Commit** `"Comptoir : commerce avec la ville depuis l'onglet Marché"`

---

### Task 11: The Transfer tab

**Files:**
- Modify: `Assets/UI/WorldMap/CityInfoPanel.uxml` (button `tab-transfer` "Transfert" between `tab-market` and `tab-ships`; page `transfer-page` with a line "Navire :" + `DropdownField` `transfer-ship`, a header row Marchandise / <ship name> / — / Comptoir, a `ScrollView` with `transfer-rows`, a `Label` `no-transfer-ship` "Aucun navire"), `CityInfoPanel.uss`
- Create: `Assets/Scripts/Game/Economy/TradingPostTransferSection.cs`
- Modify: `Assets/Scripts/Game/WorldMap/CityInfoPanelController.cs`
- Test: `Assets/Tests/EditMode/Economy/TradingPostTransferSectionTests.cs` (create) for its static helper
- Modify: `.claude/rules/economy.md`, `.claude/rules/world-map.md`

**Interfaces:**
- Consumes: Task 5 (`Transfer.Move`, `Warehouse`), Task 10 (the panel's docked-ship list and `TradingPostOpened` following), `CityMarketSection.UnitsFor`.
- Produces:
  - `TradingPostTransferSection(VisualElement section, VisualElement rowContainer, Label holdHeader, IReadOnlyList<string> goodNames, IReadOnlyList<Sprite> goodIcons = null)`; `Show(CargoHold hold, string shipName, Warehouse post)` (hold may be null: no rows, "Aucun navire"), `Hide()`; rows rebuilt on the hold's and the post's `Changed`, then `ListStripes.Apply`.
  - `static List<int> TradingPostTransferSection.GoodsShown(IGoodSource hold, IGoodSource post, IReadOnlyList<string> goodNames)` — the goods with at least one unit in either, in `NameOrder`.
  - Row: icon, name, hold units, button "→" (`Transfer.Move(hold, post, good, UnitsFor(shift, ctrl))`), button "←" (`Transfer.Move(post, hold, …)`), post units; "←" disabled when the hold has no room for one unit or the post has none, "→" disabled when the hold has none.
  - The tab button and page are shown only while the shown city has the player's trading post; choosing the tab while it disappears falls back to "Marché".

- [ ] **Step 1: Write the failing tests**: `GoodsShown_ListsGoodsInEitherStoreByName` (Fer in hold, Bière in post, Bois in neither → [Bière, Fer]), `GoodsShown_Empty_WhenBothEmpty`.
- [ ] **Step 2: Run them.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Play-mode check**: home city, tab "Transfert" present; buy 10 goods into the ship, "→" with Shift moves 10 to the post, "←" moves them back; a city without a post has no tab; capture the panel. Exit Play mode.
- [ ] **Step 5: Run all tests.** Expected: PASS.
- [ ] **Step 6: Commit** `"Comptoir : onglet Transfert entre navire et comptoir"`

---

### Task 12: Whole-feature check and project documentation

**Files:**
- Modify: `CLAUDE.md` ("Project state": the trading post exists), `.claude/rules/*.md` touched above (check they are consistent)

- [ ] **Step 1: Play-mode walk-through** of the Review Focus: build a post in a second city, its tab and "Comptoir" entry appear in the open panel without reopening it; buy 2 150 barrels into a post, advance a week with fast forward: the weekly notification's total includes 150 gold of rent; build a house in a city with Timber in its post: the post's Timber goes down first.
- [ ] **Step 2: Run all tests.** Expected: PASS.
- [ ] **Step 3: Commit** `"Comptoir : état du projet"`

## Self-review notes

- Spec coverage: building and catalogue (Tasks 2, 9), one per city / player only (Tasks 1, 6, 7), home city (Task 7), reserved site required, inside walls, invisible except while placing (Tasks 1, 3, 4, 9), generation and regeneration (Task 3), inventory, capacity and rent (Tasks 5, 8), Market tab (Task 10), Transfer tab (Task 11), materials from the post (Tasks 6, 7), debug recorder (Task 10), free building (Tasks 6, 9).
- Types used across tasks: `IGoodSource`/`IGoodStore`/`Warehouse`/`Transfer.Move` (Task 5), `MaterialSources` (Task 6), `TradingPostOffer`/`TradingPostOf`/`TradingPostOpened`/`OpenTradingPost`/`TryGiveTradingPost` (Task 7), `CityGrid.TradingPostSite`/`IsReserved`/`PlaceTradingPost`/`CanPlaceTradingPost` (Task 1).
