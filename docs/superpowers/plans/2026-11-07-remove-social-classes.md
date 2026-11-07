# Removing the social classes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A city's population becomes the housed workers' families ("Ouvriers") and the beggars, with no social class and no satisfaction.

**Architecture:** Three code tasks, each leaving the whole project compiling and its tests green: first the satisfaction and the class moves go (the classes stay, frozen); then each good gets one consumption rate; then the three classes collapse into one housed group everywhere (Core, Game, Editor, data, debug), after a one-off trim of the city layouts that still needs the classes. A last task checks the whole game.

**Tech Stack:** Unity 6000.6.4f1, C#, UI Toolkit, Unity Test Framework (EditMode), `unity-mcp` (`Unity_RunCommand`, `Unity_GetConsoleLogs`).

**Spec:** `docs/superpowers/specs/2026-11-07-remove-social-classes-design.md`

## Global Constraints

- Code, comments, logs, asset names in English; everything the player or the debug tools show in French ("Ouvriers", "Place des ouvriers", "Maison d'ouvriers"). Numbers through `NumberText`.
- Nothing kept for later: at the end no `SocialClass`, `PerClass<T>`, rich, wealthy or class satisfaction remains in code, assets, UXML/USS, rules. Older specs and plans in `docs/superpowers/` are not touched.
- `LabourRule.BeggarSatisfaction` and its setting `beggarSatisfaction` stay as they are.
- Data assets are edited by a one-off script through `Unity_RunCommand` (not kept) or by hand; assets are moved/deleted with their `.meta` (`AssetDatabase.MoveAsset` / `DeleteAsset`). After removing serialized fields, rewrite the affected assets with `AssetDatabase.ForceReserializeAssets(paths)` so no stale key stays.
- Run the tests with the `run-tests` skill; after scene or asset changes, run `SceneCheck.Run` (Tools > Dark Fantasy Merchant > Check Scenes) and expect no report.
- No mention of the game the old rules came from, anywhere (CLAUDE.md rule).
- Each task ends with a commit (French message, like the repo's) and a push.

## Review Focus

1. **Shortage with no satisfaction:** a city whose consumed goods all sit at 0 for months keeps its housed and keeps hiring from the beggars — pinned by `CityLeavingTests.EmptyStocks_NeitherStopTheHiringNorMakeTheHousedLeave` (Task 1).
2. **Families sent back when the city is almost empty:** `SendToBeggars` never leaves 0 housed and returns the workers whose family could not be taken — pinned by `SendToBeggars_NeverTakesTheLastHousedInhabitant` (Task 3).
3. **A city that needs more houses than its trimmed layout has:** the trim keeps all and reports it; at run time `WorldEconomy` logs its warning — pinned by `CityViewContentTests.EveryCity_StartsWithTheHousesItsPopulationNeeds` (Task 3), which fails on such a city.
4. **Goods the poor never consumed (Wine, Skins, Tiles) now consumed:** their T0 and prices are those of a consumed good in every city — pinned by `EconomyContentTests.EveryActiveGood_HasTheWeightedAverageOfTheOldRates` (Task 2) together with the existing threshold tests.
5. **Kept rich or wealthy houses after the trim:** they stand where they were and stay connected to the town hall — pinned by the connectivity assertion kept in `CityViewContentTests` (Task 3).

---

### Task 1: The satisfaction and the class moves go

The classes stay but never move; nothing has a satisfaction; the housed beyond their room still leave.

**Files:**
- Delete: `Assets/Scripts/Core/City/SocialRule.cs` (+ `.meta`), `Assets/Tests/EditMode/City/SocialRuleTests.cs` (+ `.meta`)
- Create: `Assets/Scripts/Core/City/LeavingRule.cs`, `Assets/Tests/EditMode/City/LeavingRuleTests.cs`
- Rename: `Assets/Tests/EditMode/City/CitySocialTests.cs` → `CityLeavingTests.cs` (class `CityLeavingTests`, `git mv` with its `.meta`)
- Modify: `Core/City/City.cs`, `Core/City/CityPopulation.cs`, `Core/City/CityWorkforce.cs`, `Core/Economy/MarketGood.cs`, `Core/Debug/CityIndicator.cs`, `Core/Debug/CitySnapshot.cs`, `Game/Economy/GoodDefinition.cs`, `Game/Economy/EconomyDefinition.cs`, `Game/Economy/WorldEconomy.cs`, `Game/Debug/DebugIndicators.cs`, `Game/Debug/DebugCityCurvesView.cs`, `Game/Debug/DebugCitiesTable.cs`, `Game/Debug/DebugPopulationTable.cs`
- Data: `Assets/Data/Economy/Economy.asset`, every `Assets/Data/Goods/*.asset` (reserialized)
- Tests: `Common/TestMarkets.cs`, `Common/TestEconomy.cs`, `City/CityPopulationTests.cs`, `City/CityLabourTests.cs`, `Economy/EconomyDefinitionTests.cs`, `Debug/DebugCitiesTableTests.cs`, `Debug/DebugIndicatorsTests.cs`, and whatever else stops compiling
- Rules: `.claude/rules/city.md`, `.claude/rules/debug-tools.md`

**Interfaces:**
- Produces:
  - `public sealed class LeavingRule { public LeavingRule(int protectedWorkers); public int ProtectedWorkers { get; } public int LeavingOf(CityPopulation population); }` — `protectedWorkers < 0` throws `ArgumentOutOfRangeException`; null population throws `ArgumentNullException`.
  - `City(..., LabourRule labour = null, IReadOnlyList<CityHousingRule> housing = null, LeavingRule leaving = null, CityGrid grid = null, IPlacementStrategy placement = null, MerchantOutputRule merchantOutput = null)`; `public LeavingRule Leaving { get; }` replaces `Social`. A leaving rule without labour throws `ArgumentException`.
  - `EconomyDefinition.CreateLeavingRule()` replaces `CreateSocialRule()`; `CreateCity(..., LeavingRule leaving = null, ...)` in place of `social`.
  - `MarketGood(double basePrice, PerClass<double> dailyConsumptionPerThousand, int maxWorkers, double dailyOutputPerWorker, double merchantDailyOutputPerWorker)` (the weights parameter goes); `SatisfactionWeights` gone. `GoodDefinition.SatisfactionWeights` gone.
  - `TestMarkets.Leaving => new LeavingRule(30)` replaces `TestMarkets.Social`.
  - `DebugCitiesTable.FormatClass(long count, long room)` → `"110 / 120"`.

- [ ] **Step 1: Write the failing tests**

`LeavingRuleTests` (with the classes still there, it reads the poor):

```csharp
[Test] public void LeavingOf_TheExcessPoor_AQuarterPlusOne()
{ Assert.AreEqual(26, new LeavingRule(30).LeavingOf(new CityPopulation(1000, 0, 900))); }

[Test] public void LeavingOf_NobodyWithinTheRoom()
{ Assert.AreEqual(0, new LeavingRule(30).LeavingOf(new CityPopulation(900, 0, 900))); }

[Test] public void LeavingOf_NeverTheLastOne()
{ Assert.AreEqual(2, new LeavingRule(30).LeavingOf(new CityPopulation(5, 0, 0)));
  Assert.AreEqual(0, new LeavingRule(30).LeavingOf(new CityPopulation(1, 0, 0))); }

[Test] public void Constructor_RefusesNegativeProtectedWorkers()
{ Assert.Throws<ArgumentOutOfRangeException>(() => new LeavingRule(-1)); }
```

`CityLeavingTests` (rewrite of `CitySocialTests`; keep its city-building helpers, with `TestMarkets.Leaving`):
- `EmptyStocks_NeitherStopTheHiringNorMakeTheHousedLeave`: a city with labour and the leaving rule, ample room, every consumed good's stock at 0, run 120 days; assert `Population.Housed` never drops below its starting value and a workshop that wants workers gets them from the beggars.
- `HousedBeyondTheirRoom_LeaveWithTheirJobs`: the existing test of the poor without room, kept (reserves first, then above 30, then the player's).
- `TheClassesNeverMove`: after 60 days, `CountOf(Rich)` and `CountOf(Wealthy)` equal their starting values.
- `Constructor_RefusesALeavingRuleWithoutLabour`.

Delete the tests about satisfaction, targets, rising and falling.

- [ ] **Step 2: Run them to see them fail**

Run the `run-tests` skill, filter `DarkFantasyMerchant.Tests.EditMode.LeavingRuleTests` and `CityLeavingTests`.
Expected: compile errors (`LeavingRule` is not defined).

- [ ] **Step 3: Implement**

- `LeavingRule.LeavingOf`: today's `SocialRule.LeavingPoorOf` without the unhappy term: `excess = poor - roomOf(poor)`; `excess > 0 ? excess / 4 + 1 : 0`, clamped to `[0, poor - 1]`. The divisor 4 stays a private constant.
- `CityPopulation`: remove `satisfactions`, `SatisfactionOf`, `SetSatisfaction`, `Move`. In `SendToBeggars`, every class weighs `max(0, count - 1)` (the satisfaction factor goes; keep the rest of the algorithm).
- `City`: drop `UpdateSatisfactions`, the `RiseAndFall` call and the starting satisfaction loop; `LetThePoorLeave` uses `Leaving.LeavingOf(Population)`; `TakeWorkersAway` uses `Leaving.ProtectedWorkers`. Update the class summary.
- `CityWorkforce`: drop the `social` parameter and the satisfaction gate on hiring from the beggars.
- `GoodDefinition`: remove the three `*SatisfactionWeight` fields, their header and `SatisfactionWeights`.
- `EconomyDefinition`: remove `startingSatisfaction`, the three `*SatisfactionBase`, `satisfactionRise`, `satisfactionFall`, `commonSatisfaction`, `lowestSatisfaction`, `highestSatisfaction`, `classTargetOffset`, `richTargetDivisor`, `wealthyTargetDivisor`, `classRoomFloor`, `poorLeavingDivisor`. Keep `protectedWorkers` (30) under `[Header("Departures")]`, tooltip "Workers of a city's workshop the housed who leave spare while other jobs can be taken: 30." `TryValidateSettings` checks `CreateLeavingRule()` as "the departures".
- `WorldEconomy`: `economy.CreateLeavingRule()` passed as `leaving`.
- Debug: remove `CityIndicator.RichSatisfaction/WealthySatisfaction/PoorSatisfaction` and their lines in `CitySnapshot` and `DebugIndicators`; remove the satisfaction chart of `DebugCityCurvesView` (the two charts left in the population row share it; builder's call on their size class); `FormatClass(count, room)` everywhere it is called.

- [ ] **Step 4: Clean the data**

In the Editor, after the refresh compiles: `AssetDatabase.ForceReserializeAssets(new[] { "Assets/Data/Economy/Economy.asset" }.Concat(goods paths))`. Check with `git diff` that only the removed keys disappear from `Economy.asset` (lines `startingSatisfaction` … `poorLeavingDivisor`, `protectedWorkers` kept) and from the goods (`*SatisfactionWeight`).

- [ ] **Step 5: Update the rules**

`city.md`: the population has no satisfaction; the day's order loses steps 3 and the class moves of step 5 (the classes still exist until Task 3: say they never move); hiring from the beggars has no condition; `LeavingRule` replaces `SocialRule` in the list of tests and in the text. `debug-tools.md`: no satisfaction indicator nor chart.

- [ ] **Step 6: Run all the EditMode tests**

Run the `run-tests` skill, all EditMode tests. Expected: all pass, console free of errors.

- [ ] **Step 7: Commit and push**

```bash
git add -A
git commit -m "Population : satisfaction et mobilité des classes retirées, LeavingRule pour les départs faute de place"
git push
```

---

### Task 2: One consumption rate per good

**Files:**
- Modify: `Core/Economy/MarketGood.cs`, `Core/City/City.cs` (`DailyConsumptionOf`), `Game/Economy/GoodDefinition.cs`, `Game/Economy/EconomyDefinition.cs` (`CreateMarketGoods`)
- Data: every `Assets/Data/Goods/*.asset`
- Tests: `Common/TestEconomy.cs` (its consumption helper takes one rate), `Economy/EconomyContentTests.cs`, `Economy/EconomyDefinitionTests.cs`, `Economy/CityMarketTests.cs`, `City/CityTests.cs`, and whatever else stops compiling
- Rules: `.claude/rules/economy.md` (and `city.md` where it says "each class at its rate")

**Interfaces:**
- Consumes: Task 1's `MarketGood` constructor.
- Produces: a single constructor `MarketGood(double basePrice, double dailyConsumptionPerThousand, int maxWorkers, double dailyOutputPerWorker, double merchantDailyOutputPerWorker = 0d)`; `public double DailyConsumptionPerThousand { get; }`. `GoodDefinition.WeeklyConsumptionPerThousand` is a `double`, serialized field `weeklyConsumptionPerThousand` (default 7, `Min(0f)`, tooltip "Units of the good that 1 000 housed inhabitants consume in a week; the beggars consume nothing.").

- [ ] **Step 1: Write the failing tests**

```csharp
// CityTests
[Test] public void DailyConsumption_IsTheHousedAtTheGoodsRate_TheBeggarsConsumingNothing()
{
    // One good at 7 per thousand a day; 1 000 housed (whatever their class) and 500 beggars.
    City city = /* TestMarkets city with new MarketGood(10d, 7d, 0, 0d) and new CityPopulation(new PerClass<int>(100, 300, 600), 500, PerClass<int>.All(1000)) */;
    Assert.AreEqual(7d, city.DailyConsumptionOf(0), 1e-9);
}

// EconomyContentTests
[TestCase("Beer", 29.575)] [TestCase("Cloth", 8.575)] [TestCase("Fish", 3.08)] [TestCase("Grain", 4.725)]
[TestCase("Hemp", 0.091)] [TestCase("Honey", 5.425)] [TestCase("Leather", 6.265)] [TestCase("Meat", 1.5505)]
[TestCase("Metal", 16.625)] [TestCase("Oil", 7.525)] [TestCase("PigIron", 0d)] [TestCase("Pitch", 0d)]
[TestCase("Pottery", 5.46)] [TestCase("Salt", 0.35)] [TestCase("Skins", 5.25)] [TestCase("Spices", 0.77)]
[TestCase("Tiles", 0.014)] [TestCase("Timber", 1.96)] [TestCase("Wine", 9.24)] [TestCase("Wool", 0.875)]
public void EveryActiveGood_HasTheWeightedAverageOfTheOldRates(string asset, double weekly)
{
    // Loads Assets/Data/Goods/{asset}.asset.
    Assert.AreEqual(weekly, good.WeeklyConsumptionPerThousand, 1e-9);
}
```

(The values are 0.1 × rich + 0.3 × wealthy + 0.6 × poor of the assets before this task.)

- [ ] **Step 2: Run them to see them fail**

`run-tests`, filter `CityTests` and `EconomyContentTests`. Expected: compile errors (`WeeklyConsumptionPerThousand` is not a double).

- [ ] **Step 3: Implement**

`City.DailyConsumptionOf(good) = goods[good].DailyConsumptionPerThousand * Population.Housed / 1000d`. `CreateMarketGoods` passes `good.WeeklyConsumptionPerThousand / daysPerWeek`. Remove the three per-class fields of `GoodDefinition` and their header.

- [ ] **Step 4: Write the data**

In each good asset, replace the three lines `richWeeklyConsumptionPerThousand` / `wealthyWeeklyConsumptionPerThousand` / `poorWeeklyConsumptionPerThousand` by one line `  weeklyConsumptionPerThousand: <value>` with the values of the test above (a text edit of the YAML is enough), then `AssetDatabase.Refresh()`.

- [ ] **Step 5: Update the rules**

`economy.md` and `city.md`: one consumption rate per good, for the housed; the beggars consume nothing.

- [ ] **Step 6: Run all the EditMode tests**

Expected: all pass, console free of errors.

- [ ] **Step 7: Commit and push**

```bash
git add -A
git commit -m "Biens : un seul taux de consommation, moyenne pondérée des trois classes"
git push
```

---

### Task 3: One housed group

The classes collapse into one group. Atomic: the project compiles only once every caller has followed.

**Files:**
- Delete: `Core/City/SocialClass.cs` (+ `.meta`), `Assets/Data/Buildings/Houses/RichHouse.asset`, `WealthyHouse.asset` (+ `.meta`, by `AssetDatabase.DeleteAsset`)
- Move: `Assets/Data/Buildings/Houses/PoorHouse.asset` → `WorkersHouse.asset` (`AssetDatabase.MoveAsset`, GUID kept)
- Modify (Core): `City/CityPopulation.cs`, `City/City.cs`, `City/CityHousing.cs`, `City/CityHousingRule.cs`, `City/CityWorkforce.cs`, `City/PlayerWorkshops.cs`, `City/Construction.cs`, `City/LeavingRule.cs`, `CityView/BuildingKind.cs`, `CityView/CityGrid.cs`, `CityView/StartingHouses.cs`, `CityView/StartingLayout.cs`, `Debug/CityIndicator.cs`, `Debug/CitySnapshot.cs`
- Modify (Game): `City/HouseDefinition.cs`, `City/BuildingsDefinition.cs`, `City/BuildingOffers.cs`, `Economy/EconomyDefinition.cs`, `Economy/WorldEconomy.cs`, `CityView/CityLayoutDefinition.cs`, `CityView/CityGridView.cs`, `CityView/HouseColors.cs`, `CityView/PlayerBuilder.cs`, `CityView/BuildBarController.cs`, `WorldMap/CityInfoPanelController.cs`, `Debug/DebugChartColors.cs`, `Debug/DebugCitiesTable.cs`, `Debug/DebugCityCurvesView.cs`, `Debug/DebugIndicators.cs`, `Debug/DebugPopulationTable.cs`
- Modify (Editor): `CityView/CityLayoutEditing.cs`, `CityView/CityLayoutWindow.cs`
- Data: every `Assets/Data/CityLayouts/*.asset`, `Assets/Data/Buildings/Buildings.asset`, `WorkersHouse.asset`, `Assets/Data/Economy/Economy.asset`
- Tests: every test listed by `grep -rlE "SocialClass|PerClass|HousedClass|HouseOf|HouseRooms|ConnectedHousesOf|HousesOf" Assets/Tests`, with `Common/TestBuildings.cs`, `Common/TestMarkets.cs`
- Rules: `.claude/rules/city.md`, `city-view.md`, `debug-tools.md`, `economy.md`; `CLAUDE.md` only if it names classes

**Interfaces:**
- Consumes: `LeavingRule` (Task 1), the single consumption rate (Task 2).
- Produces:
  - `CityPopulation(int housed)`, `CityPopulation(int housed, int beggars, int room)`; `Housed`, `Room`, `int FreeRoom { get; }`, `Beggars`, `Inhabitants`, `StartingInhabitants`, `GrowthSinceStart`; `static CityPopulation AtStart(int workers, int inhabitantsPerWorker, double beggarsPerWorker, int roomPerHouse)` and `AtStart(..., int roomPerHouse, int room)`; `static int RoomAtStart(int workers, int inhabitantsPerWorker, int roomPerHouse)`; `void AddRoom(int places)`; `void Hire(int workers, int inhabitantsPerWorker)`; `int SendToBeggars(int workers, int inhabitantsPerWorker)`; `void Leave(int inhabitants)`; `void SetBeggars(int beggars)`. `CountOf`, `RoomOf`, `FreeRoomOf` go.
  - `CityHousingRule(double occupancy, int room, IReadOnlyList<MaterialCost> materials, Footprint? footprint = null)`; `bool IsCrowded(CityPopulation)` is `Housed > Occupancy * Room`.
  - `City(..., LabourRule labour = null, CityHousingRule housing = null, LeavingRule leaving = null, ...)`; `CityHousingRule Housing { get; }` (null when the city never builds); `int PlayerHouses { get; }`; `void AddPlayerHouse(int room, Footprint footprint, GridPlace place)`.
  - `BuildingKind.House()`; `HousedClass` goes. `CityGrid.CountHouses()` and `CityGrid.CountConnectedHouses()` replace `HousesOf` / `ConnectedHousesOf`.
  - `StartingHouses.TryPlace(CityGrid grid, int houses, Footprint footprint, System.Random random)` and the `RectInt area` overload; `StartingLayout.TryBuild(..., Footprint tradingPost, int houses, Footprint houseFootprint, RoadContact contact, ...)`.
  - `Construction.TryBuildHouse(...)` without its `SocialClass` parameter.
  - `HouseDefinition`: no class; room tooltip "Inhabitants the house shelters: 140." `BuildingsDefinition.House` (`[SerializeField] private HouseDefinition house`, replacing the `houses` list); `HouseOf`, `HouseRooms`, `Houses` go; `TryValidate` says "there is no house" when it is missing; `DefinitionOf(BuildingKind.House())` returns it; `CreateHouseOffers` still returns a list (of one).
  - `HouseOffer(string name, int room, BuildingCost cost, string costText, Footprint footprint = default)`.
  - `EconomyDefinition.CreateHousingRule(int room, IReadOnlyList<MaterialCost> materials, Footprint? footprint = null)`; `int StartingRoomOf(CityDefinition city, int roomPerHouse)`; `CreateCity(..., LabourRule labour = null, int roomPerHouse = 0, CityHousingRule housing = null, LeavingRule leaving = null, CityGrid grid = null, IPlacementStrategy placement = null)`. Field `cityHouseOccupancy` (0.95) with `[FormerlySerializedAs("cityPoorHouseOccupancy")]`, tooltip "A city builds a house of its own, with the materials of the player's, once its housed are above this share of their room."
  - `CityLayoutDefinition.StartingHouse(Vector2Int origin, BuildingOrientation orientation)`.
  - `HouseColors.House = new Color32(0x9E, 0x94, 0x6D, 0xFF)`; `HouseColors.Of` goes.
  - `CityIndicator.Housed` and `CityIndicator.HousedRoom` replace the six class indicators; read "Ouvriers" and "Place des ouvriers". `DebugChartColors.Housed = Hex(0xE0705A)` replaces the three class colours.
  - `CityInfoPanelController.FormatPopulation(int housed, int beggars)` → `"Ouvriers : 660 · Mendiants : 64"`.
  - `TestBuildings.CreateHouse(string name, long gold, int room, params (GoodDefinition good, int units)[] materials)`; the test catalogue holds one house of 140 places, 2 × 1. `TestMarkets.AddPlayerHouse(City city, int room)`.

- [ ] **Step 1: Trim the city layouts (old code, before any change)**

With the Editor on the current commit, run a one-off `Unity_RunCommand` script (not kept) over every `CityLayoutDefinition` in `Assets/Data/CityLayouts`:
- find its `CityDefinition` (the one whose `Layout` is it) and the `EconomyDefinition` asset;
- `housed` = the sum over the three classes of `economy.StartingRoomOf(city, PerClass<int>.All(1))` (room 1 per house gives each class's exact count); `needed = (housed + 139) / 140`;
- through a `SerializedObject` on `startingHouses`: keep the poor houses, then the wealthy, then the rich, each in list order, until `needed` are kept; set every kept entry's `socialClass` to `Poor` (2); remove the others; `ApplyModifiedPropertiesWithoutUndo`, `SetDirty`, then `AssetDatabase.SaveAssets()`;
- log per city `kept / needed` and, for a city with fewer houses than `needed`, keep them all and log it as a warning.

Expected log: 8 kept in most cities, 7 in Oslo (the counts are each city's), no warning. Report any warning to the user. Do not commit yet: the old tests fail on the trimmed layouts until this task ends.

- [ ] **Step 2: Write the failing tests**

`CityPopulationTests` (replace the per-class tests):

```csharp
[Test] public void AtStart_HousesEveryFamily_InWholeHouses()
{
    CityPopulation population = CityPopulation.AtStart(250, 4, 4d / 17d, 140);
    Assert.AreEqual(1000, population.Housed);
    Assert.AreEqual(58, population.Beggars);
    Assert.AreEqual(1120, population.Room);
}

[Test] public void SendToBeggars_TakesEveryFamilyAsked_WhenItCan()
{
    var population = new CityPopulation(100, 0, 200);
    Assert.AreEqual(0, population.SendToBeggars(5, 4));
    Assert.AreEqual(80, population.Housed);
    Assert.AreEqual(20, population.Beggars);
}

[Test] public void SendToBeggars_NeverTakesTheLastHousedInhabitant()
{
    var population = new CityPopulation(9, 0, 200);
    Assert.AreEqual(1, population.SendToBeggars(3, 4));
    Assert.AreEqual(1, population.Housed);
    Assert.AreEqual(8, population.Beggars);

    var whole = new CityPopulation(8, 0, 200);
    Assert.AreEqual(1, whole.SendToBeggars(2, 4));
    Assert.AreEqual(4, whole.Housed);
}
```

`CityHousingRuleTests` (or in `CityHousingTests`): `IsCrowded_AboveTheOccupancyOfTheRoom` — occupancy 0.95, room 100: 96 housed → true, 95 → false.

`BuildingsDefinitionTests`: `TryValidate_RefusesACatalogueWithoutHouse` (problem contains "no house").

`CityViewContentTests`: `EveryCity_StartsWithTheHousesItsPopulationNeeds` — for every usable city, `grid.CountConnectedHouses() == economy.StartingRoomOf(city, 140) / 140` and every house is connected.

`CityMarketSectionTests`: the population line reads `"Ouvriers : 1 008 · Mendiants : 64"` and `"Ouvriers : 0 · Mendiants : 0"`.

`DebugCitiesTableTests` / `DebugPopulationTableTests`: one column "Ouvriers" after "Mendiants", written `FormatClass(housed, room)`; the population table's titles are `{ "Ville", "Population", "Mendiants", "Ouvriers", "Croiss. totale", "Maisons de la ville" }`.

`LeavingRuleTests`: the same values as in Task 1, through `new CityPopulation(housed, 0, room)`.

- [ ] **Step 3: Implement**

Follow the Interfaces block. Notes on what the signatures leave open:
- `SendToBeggars`: throws as today when `workers * inhabitantsPerWorker > Housed`; `taken = Math.Min(workers, (Housed - 1) / inhabitantsPerWorker)` families move to the beggars; returns `workers - taken`.
- `LeavingRule.LeavingOf` reads `Housed` and `Room`. `City.LetThePoorLeave` becomes `LetTheHousedLeave`, with `Population.Leave(removed * InhabitantsPerWorker)`.
- `CityHousing` holds one rule; on a grid, a house not connected yet needs the rule to give its room (same exception as today, without "its class").
- `StartingHouses` / `StartingLayout`: the same three steps with one footprint.
- `CityLayoutWindow`: one tool `House` (label "House") in place of the three; `DrawRooms` writes one line `"Rooms {placed} / {need} ({need / perHouse} houses needed)"`, red when short.
- `CityLayoutEditing.TryGenerate`: `houses = economy.StartingRoomOf(city, room) / room` with `room = buildings.House.Room`.
- `WorldEconomy`: one housing rule from `buildings.House` (`MaterialsOf(economy)`, `Footprint`); `WarnAboutRoom` compares `grid.CountConnectedHouses() * room` with `StartingRoomOf(city, room)`.
- Debug curves: the population chart is titled "Ouvriers et mendiants", series "Ouvriers" (`DebugChartColors.Housed`, stacked) and "Mendiants".
- `BuildBarController`: the doc comment's example becomes "Maison d'ouvriers\n5 000 or".

- [ ] **Step 4: Fix the data**

After the refresh compiles, in one one-off `Unity_RunCommand` script:
- `AssetDatabase.DeleteAsset` on `RichHouse.asset` and `WealthyHouse.asset`; `AssetDatabase.MoveAsset("…/PoorHouse.asset", "…/WorkersHouse.asset")`; set its `displayName` to "Maison d'ouvriers";
- set `Buildings.asset`'s `house` to `WorkersHouse.asset` (`SerializedObject`);
- `AssetDatabase.ForceReserializeAssets` on every city layout, `Buildings.asset`, `WorkersHouse.asset` and `Economy.asset`; then `SaveAssets`.

Check with `git diff`: the layouts lose only their `socialClass` lines (and the removed houses, from Step 1); `Economy.asset` loses `cityRichHouseOccupancy` and `cityWealthyHouseOccupancy` and has `cityHouseOccupancy: 0.95`; `Buildings.asset` has one `house` reference. Then run `SceneCheck.Run`: expect no report.

- [ ] **Step 5: Update the rules**

`city.md`: one housed group and the beggars, no class; `CityPopulation`, `LeavingRule`, the one `CityHousingRule`, the day's order (consumption, the city's workshops, the player's workshops, the housed beyond their room leave, the beggars, at most one house a day), the catalogue's one house. `city-view.md`: one house kind, one colour, one layout tool, the layout's houses without class. `debug-tools.md`: the "Ouvriers" column, indicators and series. `economy.md`: anything that names a class.

- [ ] **Step 6: Check nothing is left**

Run `grep -rnE "SocialClass|PerClass|HousedClass|Wealthy|\bRich\b|riches|aisés|pauvres|Satisfaction des" Assets/Scripts Assets/Tests Assets/UI .claude CLAUDE.md` (Library excluded).
Expected: no hit, except `BeggarSatisfaction` / `beggarSatisfaction` and the rich-text colour comments of `DebugCitiesTable`.

- [ ] **Step 7: Run all the EditMode tests**

Expected: all pass, console free of errors.

- [ ] **Step 8: Commit and push**

```bash
git add -A
git commit -m "Population : un seul groupe d'ouvriers logés, maisons et dispositions de ville sans classe"
git push
```

---

### Task 4: The whole game

**Files:** none expected; a fix found here goes in the task it belongs to, with its test.

- [ ] **Step 1: Play**

Enter Play mode in `WorldMap.unity`, run about eight weeks at the fastest speed, open a city panel, a city view (build bar: one house "Maison d'ouvriers"; houses in one colour), and the debug panel (cities table, population table, curves).
Expected: `Unity_GetConsoleLogs` shows no error and no `WorldEconomy` room warning; the panel reads "Ouvriers : … · Mendiants : …"; the cities keep hiring and their populations stay near their starting size.

- [ ] **Step 2: Exit Play mode and report**

Exit Play mode. Tell the user what was seen, with any number that looks off (a city emptying or growing fast, a good whose price stays at an extreme).
