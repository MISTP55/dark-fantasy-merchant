# Social Classes and Satisfaction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A city's housed inhabitants split into rich, wealthy and poor, each with its houses, its consumption and a satisfaction that, as in Patrician III, moves the poor up and down the classes, gates the hiring of beggars and makes the poor leave.

**Architecture:** `Core` gains `SocialClass` and the generic `PerClass<T>` (one value per housed class). `CityPopulation` holds the three classes, their room and their satisfaction. A new stateless `SocialRule` holds P3's satisfaction and class formulas. `CityMarket` takes an optional `SocialRule`, as it takes `LabourRule`. Without it, nothing of the classes moves, so existing tests keep their behaviour. `MarketGood` carries consumption and satisfaction weights per class. `CityHousingRule` becomes one rule per class. In `Game`, the goods, economy and buildings assets hold the per-class values and the city panel shows them.

**Tech Stack:** Unity 6000.6.4f1, C#, UI Toolkit, Unity Test Framework (NUnit, EditMode).

**Spec:** `docs/superpowers/specs/2026-10-18-social-classes-design.md`

## Global Constraints

- Code, comments and asset names are in English; everything the player reads is in French, numbers written with a no-break space between thousands (`TreasuryHudController.FormatNumber`).
- Every list the player reads is sorted with `NameOrder`; the four population counters keep the fixed order rich, wealthy, poor, beggars.
- Satisfaction level fixed at P3's Intermédiaire: bases rich −13, wealthy −18, poor −27; rise 2, fall 1 per day; common term 9; bounds −40 and +80; start 50.
- Houses: poor 140 places, 5 000 gold, 10 timber, 10 metal; wealthy 70 places, 10 000 gold, 15 timber, 20 metal; rich 40 places, 25 000 gold, 25 timber, 30 metal.
- City house occupancies: rich 0.97, wealthy 0.96, poor 0.95.
- Beggars consume nothing.
- Every construction goes through `BuildingsDefinition.MaterialsOf` (switched-off materials left out).
- Formulas are P3's, in integer arithmetic where P3 uses integers (C# `/` on `int` truncates toward zero like P3's `IDIV`). The P3 source of each is named; the notes are `patrician3-re\resultats\Population des villes.md` and `Production et consommation des villes.md`.
- Run the tests with the project's `run-tests` skill. Commit at the end of every task, message in French as the repository's history, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- A class of 0 or 1 inhabitant: the share of a leaving family (`SendToBeggars`) must neither divide by zero nor take a class below 1; covered in Task 1.
- A city with no house of a class: its target is floored at 10, so up to 10 rich or wealthy live there without room (P3); covered in Task 3.
- Very few poor: the leaving poor never exceed poor − 1 and no worker is taken for fewer than 4; covered in Task 3.
- An existing scene and existing assets: renamed serialized fields (`house`, `houseRoom`, `cityHouseOccupancy`) keep their values through `FormerlySerializedAs`; covered in Task 5.
- A good the city does not consume (pitch, pig iron, a weight on a good with no consumption) leaves the satisfaction unchanged; covered in Task 4.

---

### Task 1: Classes in `CityPopulation`

**Files:**
- Create: `Assets/Scripts/Core/Population/SocialClass.cs` (enum and `PerClass<T>`)
- Modify: `Assets/Scripts/Core/Population/CityPopulation.cs`
- Modify: `Assets/Scripts/Core/Economy/CityMarket.cs` (callers of `FreeRoom` and `AddRoom(int)` only)
- Test: `Assets/Tests/EditMode/Population/CityPopulationTests.cs`, and the existing callers in the tests

**Interfaces:**
- Produces:
  - `public enum SocialClass { Rich, Wealthy, Poor }` (P3's order).
  - `public readonly struct PerClass<T>`: `PerClass(T rich, T wealthy, T poor)`, `T Rich`, `T Wealthy`, `T Poor`, indexer `T this[SocialClass c]`, `static PerClass<T> All(T value)`.
  - `CityPopulation`:
    - `CityPopulation(int housed)` and `CityPopulation(int housed, int beggars, int room)` keep their meaning: every housed inhabitant is poor, and the room is the poor's.
    - New constructor `CityPopulation(PerClass<int> housed, int beggars, PerClass<int> room)`.
    - `int CountOf(SocialClass)`, `int RoomOf(SocialClass)`, `int FreeRoomOf(SocialClass)` (never negative).
    - `int SatisfactionOf(SocialClass)`, which starts at 0, and `void SetSatisfaction(SocialClass, int)`.
    - `Housed` and `Room` are the sums over the three classes.
    - `FreeRoom` and `AddRoom(int)` are removed. Use `FreeRoomOf(SocialClass.Poor)` and `AddRoom(SocialClass, int)` instead.
    - `void Hire(int workers, int inhabitantsPerWorker)` turns beggars into poor.
    - `int SendToBeggars(int workers, int inhabitantsPerWorker)` returns the workers who stay.
    - `void Move(SocialClass from, SocialClass to, int inhabitants)` and `void Leave(SocialClass c, int inhabitants)` (they leave the city).
    - `static CityPopulation AtStart(int workers, int inhabitantsPerWorker, double beggarsPerWorker, PerClass<int> roomPerHouse)` replaces the four-argument overload.

- [ ] **Step 1: Write the failing tests** in `CityPopulationTests`:

```csharp
[Test] public void AtStart_SplitsTheHousedAsPatricianIII()
{
    var p = CityPopulation.AtStart(275, 4, 4d / 17d, new PerClass<int>(40, 70, 140));
    Assert.AreEqual(110, p.CountOf(SocialClass.Rich));
    Assert.AreEqual(330, p.CountOf(SocialClass.Wealthy));
    Assert.AreEqual(660, p.CountOf(SocialClass.Poor));
    Assert.AreEqual(64, p.Beggars);
    Assert.AreEqual(120, p.RoomOf(SocialClass.Rich));
    Assert.AreEqual(350, p.RoomOf(SocialClass.Wealthy));
    Assert.AreEqual(700, p.RoomOf(SocialClass.Poor));
    Assert.AreEqual(1100, p.Housed);
    Assert.AreEqual(1170, p.Room);
}

[Test] public void Hire_TurnsBeggarsIntoPoor()  // 20 beggars, Hire(2, 4): beggars 12, poor +8, rich and wealthy unchanged

[Test] public void SendToBeggars_SharesTheFamiliesByCountAndSatisfaction()
{
    var p = new CityPopulation(new PerClass<int>(11, 31, 61), 0, new PerClass<int>(40, 70, 140));
    foreach (SocialClass c in new[] { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor }) p.SetSatisfaction(c, 50);
    Assert.AreEqual(0, p.SendToBeggars(1, 4));
    Assert.AreEqual(11, p.CountOf(SocialClass.Rich));
    Assert.AreEqual(30, p.CountOf(SocialClass.Wealthy));
    Assert.AreEqual(58, p.CountOf(SocialClass.Poor));
    Assert.AreEqual(4, p.Beggars);
}

[Test] public void SendToBeggars_TakesMoreFromAnUnhappyClass()
// same population, poor satisfaction -10, others 50: rich 11, wealthy 31, poor 57, beggars 4

[Test] public void SendToBeggars_KeepsTheWorkersWhoseFamiliesCannotBeTaken()
{
    var p = new CityPopulation(new PerClass<int>(1, 1, 1), 0, new PerClass<int>(40, 70, 140));
    Assert.AreEqual(1, p.SendToBeggars(1, 4));
    Assert.AreEqual(3, p.Housed);
    Assert.AreEqual(0, p.Beggars);
}

[Test] public void Move_AndLeave_ChangeTheClasses()  // Move(Poor, Rich, 5): rich +5, poor -5, housed same; Leave(Poor, 8): poor -8, inhabitants -8, beggars same
[Test] public void Move_MoreThanTheClassHas_Throws()  // ArgumentOutOfRangeException
[Test] public void AddRoom_AddsToTheClassOnly()
```

Update the existing tests and callers that used `FreeRoom` or `AddRoom(int)` so that they use the poor's room. `CityMarket.Hire` and `FillVacancies` read `FreeRoomOf(SocialClass.Poor)`. `AddPlayerHouse` and `BuildCityHouse` call `AddRoom(SocialClass.Poor, …)` until Task 4. `EconomyDefinition.CreateMarket` calls the new `AtStart` with its single room for the three classes (`PerClass<int>.All(roomPerHouse)`) until Task 5. Every task leaves the project compiling.

- [ ] **Step 2: Run the tests; the new ones fail to compile or fail.**

- [ ] **Step 3: Implement.** `SendToBeggars` follows P3's `0x50E6D0`, with n = workers × inhabitantsPerWorker:

```text
for c in Rich, Wealthy, Poor: w[c] = max(0, count[c] − 1) × (satisfaction[c] < 0 ? 10 − satisfaction[c] : 10)
W = Σ w; while W > 4096: W >>= 4, every w[c] >>= 4          // P3's overflow guard, kept for its rounding
remaining = n
for c in Rich, Wealthy, Poor:
    t = W > 0 ? w[c] × n / W : 0;  t = min(t, count[c] − 1, remaining)  (never below 0)
    count[c] −= t; beggars += t; remaining −= t
kept = 0
if remaining ≥ inhabitantsPerWorker: kept = remaining / inhabitantsPerWorker; remaining %= inhabitantsPerWorker
cycle c = Poor, Wealthy, Rich, Poor, …: if count[c] > 1 take 1 into the beggars; stop when remaining is 0 or no class is above 1
   (the inhabitants still missing then also become a kept worker's family: add 1 to kept if remaining > 0)
return kept
```

P3 gives a class of 0 a weight of −10. DFM clamps it to 0, which differs from P3 only for an empty class, a state P3 never reaches. Throw when workers is negative or when `inhabitantsPerWorker < 1`, as now. `AtStart`: housed = workers × inhabitantsPerWorker, rich = housed / 10, wealthy = 3 × rich, poor = the rest; each room rounded up to whole houses of its class.

- [ ] **Step 4: Run all the EditMode tests; they pass.**
- [ ] **Step 5: Commit** « Population : trois classes logées dans CityPopulation ».

---

### Task 2: Consumption per class

**Files:**
- Modify: `Assets/Scripts/Core/Economy/MarketGood.cs`, `Assets/Scripts/Core/Economy/CityMarket.cs` (`DailyConsumptionOf`)
- Test: `Assets/Tests/EditMode/Economy/CityMarketTests.cs`

**Interfaces:**
- Consumes: `SocialClass`, `PerClass<T>`, `CityPopulation.CountOf` (Task 1).
- Produces:
  - New constructor `MarketGood(double basePrice, PerClass<double> dailyConsumptionPerThousand, int maxWorkers, double dailyOutputPerWorker, double merchantDailyOutputPerWorker, PerClass<int> satisfactionWeights)`.
  - `PerClass<double> DailyConsumptionPerThousand` and `PerClass<int> SatisfactionWeights`.
  - The existing constructor stays, with the same rate for every class and weights of 0.
  - Every value is checked as now: not negative, finite. The weights may not be negative either.

- [ ] **Step 1: Write the failing tests** in `CityMarketTests`:

```csharp
[Test] public void DailyConsumptionOf_AddsUpTheClasses_AndLeavesOutTheBeggars()
{
    // wine, per thousand and per day: rich 7.5, wealthy 1.9, poor 0
    var good = new MarketGood(220, new PerClass<double>(7.5, 1.9, 0), 0, 0, 0, PerClass<int>.All(0));
    var population = new CityPopulation(new PerClass<int>(110, 330, 660), 64, new PerClass<int>(120, 350, 700));
    CityMarket market = /* a market of that good and population, as the fixture builds others */;
    Assert.AreEqual(1.452, market.DailyConsumptionOf(0), 1e-9);
}

[Test] public void MarketGood_RefusesANegativeWeight()  // ArgumentOutOfRangeException
```

An existing test of a market with beggars that expected them to consume now expects them not to. Update its figure.

- [ ] **Step 2: Run them; they fail.**
- [ ] **Step 3: Implement.** `DailyConsumptionOf(good)` = Σ over the classes of rate[c] × CountOf(c) / 1000.
- [ ] **Step 4: Run all the tests; they pass.**
- [ ] **Step 5: Commit** « Économie : consommation par classe ; les mendiants ne consomment plus ».

---

### Task 3: `SocialRule`, P3's formulas

**Files:**
- Create: `Assets/Scripts/Core/Population/SocialRule.cs`
- Test: `Assets/Tests/EditMode/Population/SocialRuleTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces: `public sealed class SocialRule`, stateless, constructed as
  `SocialRule(int startingSatisfaction, PerClass<int> satisfactionBases, int satisfactionRise, int satisfactionFall, int commonSatisfaction, int lowestSatisfaction, int highestSatisfaction, int classTargetOffset, int richTargetDivisor, int wealthyTargetDivisor, int classRoomFloor, int poorLeavingDivisor, int protectedWorkers)`.
  Each argument is exposed as a property of the same name. The constructor throws `ArgumentOutOfRangeException` in four cases: lowest > highest, the start outside [lowest, highest], a negative rise, fall, room floor or protected workers, or a divisor under 1. Its methods:
  - `int GoodTerm(int weight, double stock, double t0)`, with t0 > 0.
  - `int TargetOf(SocialClass c, int goodsTerm)`.
  - `int NextSatisfaction(int current, int target)`.
  - `int ClassTargetOf(SocialClass c, int satisfaction, int poor, int room)`, for c = `Rich` or `Wealthy` only.
  - `void RiseAndFall(CityPopulation population)`.
  - `int LeavingPoorOf(CityPopulation population)`: the poor who want to leave today.

The test fixture builds the rule with the values of the Global Constraints, and with offset 40, divisors 320 and 160, room floor 10, poor leaving divisor 100 and 30 protected workers.

- [ ] **Step 1: Write the failing tests:**

```csharp
[Test] public void GoodTerm_FollowsTheStockAgainstT0()
{
    Assert.AreEqual(-8, rule.GoodTerm(8, 0, 10));
    Assert.AreEqual(-4, rule.GoodTerm(8, 5, 10));
    Assert.AreEqual(0, rule.GoodTerm(8, 10, 10));
    Assert.AreEqual(2, rule.GoodTerm(8, 13, 10));   // 2.4 truncated
    Assert.AreEqual(4, rule.GoodTerm(8, 15, 10));
    Assert.AreEqual(8, rule.GoodTerm(8, 20, 10));
    Assert.AreEqual(8, rule.GoodTerm(8, 25, 10));
}
[Test] public void TargetOf_AddsTheBaseAndTheCommonTerm()  // TargetOf(Rich, -5) == -9; TargetOf(Poor, 50) == 32
[Test] public void NextSatisfaction_MovesTowardsTwiceTheTarget()
{
    Assert.AreEqual(49, rule.NextSatisfaction(50, -9));
    Assert.AreEqual(52, rule.NextSatisfaction(50, 30));
    Assert.AreEqual(60, rule.NextSatisfaction(60, 30));
    Assert.AreEqual(80, rule.NextSatisfaction(79, 45));
    Assert.AreEqual(-40, rule.NextSatisfaction(-40, -30));
}
[Test] public void ClassTargetOf_IsPatricianIIIs()
{
    Assert.AreEqual(168, rule.ClassTargetOf(SocialClass.Rich, 50, 600, 200));
    Assert.AreEqual(337, rule.ClassTargetOf(SocialClass.Wealthy, 50, 600, 400));
    Assert.AreEqual(40, rule.ClassTargetOf(SocialClass.Rich, 50, 600, 40));    // the room caps it
    Assert.AreEqual(10, rule.ClassTargetOf(SocialClass.Rich, 50, 600, 0));     // floored at 10
    Assert.AreEqual(1, rule.ClassTargetOf(SocialClass.Rich, -40, 600, 200));   // at least 1
}
[Test] public void RiseAndFall_MovesPoorUpTowardsTheTargets()
{
    var p = new CityPopulation(new PerClass<int>(60, 180, 600), 0, new PerClass<int>(200, 400, 700));
    foreach (SocialClass c in new[] { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor }) p.SetSatisfaction(c, 50);
    rule.RiseAndFall(p);
    Assert.AreEqual(66, p.CountOf(SocialClass.Rich));
    Assert.AreEqual(184, p.CountOf(SocialClass.Wealthy));
    Assert.AreEqual(590, p.CountOf(SocialClass.Poor));
}
[Test] public void RiseAndFall_SendsAClassAboveItsRoomBackToThePoor()  // rich 50, room 40, poor 600 → rich 47; Housed unchanged
[Test] public void RiseAndFall_FillsAnEmptyClassOneAtATime()          // rich 0, room 200, poor 600 → rich 1; Housed unchanged
[Test] public void LeavingPoorOf_IsTheExcessOverTheRoom()             // poor 1000, room 980, satisfaction 10 → 6
[Test] public void LeavingPoorOf_IsOnePercentOfUnhappyPoor()          // poor 1000, room 1400, satisfaction -5 → 10
[Test] public void LeavingPoorOf_NeverTakesTheLastPoor()               // poor 1, room 0, satisfaction 10 → 0
[Test] public void LeavingPoorOf_IsZeroForHousedHappyPoor()           // poor 600, room 700, satisfaction 10 → 0
[Test] public void Constructor_RefusesAStartOutsideTheBounds()
```

- [ ] **Step 2: Run them; they fail.**
- [ ] **Step 3: Implement**, as P3's `0x51CB80` and `0x51C9A0`:

```text
GoodTerm: stock < t0: −(int)(w × (t0 − stock) / t0); stock < 2 × t0: (int)(w × (stock − t0) / t0); else w
TargetOf: base[c] + goodsTerm + common
NextSatisfaction: twice = 2 × target
    current < twice: min(current + rise, highest); current > twice: max(current − fall, lowest); else current
ClassTargetOf: t = (satisfaction + offset) × poor / divisor[c]; t = max(t, 1); if room < t: t = max(room, floor)
RiseAndFall: both targets from the poor before any move; then for c = Rich, then Wealthy:
    count = CountOf(c), target as above
    target < count: m = 2 × count / target + 1; if m ≥ count: m = count − 1; Move(c, Poor, m)
    else: m = count == 0 ? 1 : 2 × target / count + 1; if m < poor (current): Move(Poor, c, m)
LeavingPoorOf: l = satisfaction(Poor) < 0 ? poor / poorLeavingDivisor : 0
    excess = poor − RoomOf(Poor); if excess > 0: l = max(l, excess / 4 + 1)
    if l ≥ poor: l = poor − 1 (never below 0)
```

The `excess / 4` is P3's `>> 2`. It stays a literal 4, apart from the inhabitants per worker.

- [ ] **Step 4: Run them; they pass.**
- [ ] **Step 5: Commit** « Population : règles de satisfaction et de passage entre classes de Patrician III ».

---

### Task 4: The day of a city with classes

**Files:**
- Modify: `Assets/Scripts/Core/Economy/CityMarket.cs`, `Assets/Scripts/Core/Population/CityHousingRule.cs`, `Assets/Scripts/Core/Population/Construction.cs`
- Test: create `Assets/Tests/EditMode/Population/CityMarketSocialTests.cs`; update `CityMarketHousingTests.cs`, `CityMarketLabourTests.cs`, `ConstructionTests.cs`

**Interfaces:**
- Consumes: Tasks 1 to 3.
- Produces:
  - `CityHousingRule(SocialClass housedClass, double occupancy, int room, IReadOnlyList<MaterialCost> materials)`, with `SocialClass HousedClass`. `IsCrowded` compares `CountOf(HousedClass)` with occupancy × `RoomOf(HousedClass)`.
  - `CityMarket` constructor: `…, CityPopulation population, LabourRule labour = null, IReadOnlyList<CityHousingRule> housing = null, SocialRule social = null`.
    - It throws when a class appears twice in `housing`, or when `social` is given without `labour`.
    - With `social`, it sets each class's satisfaction to `social.StartingSatisfaction`.
  - `SocialRule Social`, `IReadOnlyList<CityHousingRule> Housing` (empty when null).
  - `int PlayerHousesOf(SocialClass)` replaces `PlayerHouses`. `CityHouses` stays the total.
  - `void AddPlayerHouse(SocialClass housedClass, int room)`.
  - `Construction.TryBuildHouse(CityMarket market, CargoHold hold, Treasury treasury, BuildingCost cost, SocialClass housedClass, int room)`.

- [ ] **Step 1: Write the failing tests** in `CityMarketSocialTests`, on the fixture of `CityMarketLabourTests` (good `MarketGood(10, 2, 60, 0.1)`, `Workforce` starting with 10 workers, `TestMarkets.Thresholds`, `TestMarkets.Labour`, 7-day week), with `social:` the rule of Task 3. Unless a test says otherwise the population is `new CityPopulation(new PerClass<int>(rich, wealthy, poor), beggars, new PerClass<int>(…))` built by the test, and the satisfactions are the rule's 50 set by the constructor. The figures below follow from P3's formulas of Tasks 1 and 3; work out any you change from them, do not tune them to the code.

```csharp
[Test] public void StartDay_LowersTheSatisfactionOfACityWithoutTheGood()
// one good not produced, MarketGood(10, PerClass<double>.All(2), 0, 0, 0, new PerClass<int>(5, 4, 8)), starting days 0:
// targets -9, -13, -26 → satisfactions 49, 49, 49
[Test] public void StartDay_RaisesTheSatisfactionOfAWellSuppliedCity()
// the same good with weights (40, 40, 40) and starting days 100 (stock above 2 x T0):
// targets 36, 31, 22 → satisfactions 52, 52, 49
[Test] public void StartDay_IgnoresAGoodTheCityDoesNotConsume()
// the first test plus a good with consumption PerClass<double>.All(0) and weights (9, 9, 9): satisfactions 49, 49, 49
[Test] public void StartDay_DoesNotHireBeggarsWhileThePoorAreUnhappy()
// starting days 0, poor 1000, room (0, 0, 1100), 100 beggars, SetSatisfaction(Poor, 0) after construction:
// WorkersOf(0) stays 10
[Test] public void StartDay_MovesPoorUpAfterTheSatisfaction()
// rich 60, wealthy 180, poor 600, room (200, 400, 700), no beggar: rich 66, wealthy 184
[Test] public void WithoutASocialRule_TheClassesNeverMove()
// the same city with no social rule: rich 60, wealthy 180, satisfactions 0
[Test] public void StartDay_ThePoorWithoutRoomLeaveWithTheirJobs()
// starting days 100 (the workshop lays off to 1, reserve 9), poor 1000, room (0, 0, 980), no beggar:
// rich 1, wealthy 1, then 5 poor want to leave, 1 worker: ReserveOf(0) == 8, CountOf(Poor) == 994, Housed == 996
[Test] public void LeavingPoor_TakeTheWorkersAbove30_WhenThereIsNoReserve()
// workforce starting with 50 workers, starting days 0, otherwise as above: WorkersOf(0) == 49, CountOf(Poor) == 994
[Test] public void LeavingPoor_TakeThePlayersWorkshopsLast()
// a city workshop at 30 workers or fewer, no reserve, a player workshop filled to 10 workers by an earlier day:
// PlayerWorkersOf(good) == 9 after the day the poor leave
[Test] public void StartDay_BuildsAHouseForEachCrowdedClass()
// housing rules for the three classes (rich 0.97 / 40, wealthy 0.96 / 70, poor 0.95 / 140) with one material in stock,
// rich 117, wealthy 100, poor 600, room (120, 350, 700), no beggar: the rich rise to 120 and get a house:
// RoomOf(Rich) == 160, CityHouses == 1, RoomOf(Wealthy) == 350, RoomOf(Poor) == 700
```

To keep `Game` compiling until Tasks 5 and 6: `EconomyDefinition.CreateHousingRule` makes the poor's rule and `CreateMarket` passes it as a one-element list; `CityBuildingsSection` reads `PlayerHousesOf(SocialClass.Poor)` and builds poor houses.

In `CityMarketHousingTests`, `ConstructionTests` and `CityBuildingsSectionTests`, existing houses become poor houses: `AddPlayerHouse(SocialClass.Poor, …)`, `TryBuildHouse(…, SocialClass.Poor, 100)`, and a one-element housing list for the poor.

- [ ] **Step 2: Run them; they fail.**
- [ ] **Step 3: Implement.** The order of `StartDay` becomes:

1. consumption;
2. the city's workshops. In `Hire`, the beggars are hired only when `Social == null` or the satisfaction of the poor is above 0 (P3's `0x510C30`). The reserves are still taken.
3. with `Social`, the satisfactions: for each class, `TargetOf(c, Σ GoodTerm(weight[c], StockOf(g), ThresholdsOf(g).T0))` over the goods with `DailyConsumptionOf(g) > 0`, then `NextSatisfaction`;
4. `FillVacancies`;
5. with `Social`: `RiseAndFall`, then `LeavingPoorOf` / `Labour.InhabitantsPerWorker` workers taken away. If that is at least 1, `Leave(Poor, removed × InhabitantsPerWorker)`.
6. the beggars;
7. `BuildCityHouses`: each rule in its order, at most one house per class a day.

`LayOff`: `int kept = Population.SendToBeggars(leak.Leaving, …)`. The reserve loses only `leak.Leaving − kept`.

The workers are taken away as in P3's `0x51C6B0`, with `wanted` the workers to take away and `protected` = `Social.ProtectedWorkers`:

```text
1. reserves, in the catalogue's order: take from each until wanted is met (return when met)
2. step = (Σ over city workshops of max(0, workers − protected) + Σ over player workshops of workers) / wanted; step = max(step, 2)
   acc = 0; for each city workshop in the catalogue's order with workers > protected:
       acc += workers − protected
       if acc ≥ step: n = min(acc / step, wanted); if n ≥ workers: n = workers − 1
                      workers −= n; wanted −= n; acc −= n × step; removed += n
3. while wanted > 0, for each player workshop in the order built, with workers > 0:
       acc += workers
       if acc ≥ step: n = min(acc / step, wanted, workers); workers −= n; acc −= n × step; wanted −= n; removed += n
return removed (reserves included)
```

P3 caps its slot 9 at 60. DFM has no such slot and applies 30 everywhere. Update the class's summary, and `.claude/rules/population.md` (the order of the day, `SocialRule`, the classes).

- [ ] **Step 4: Run all the tests; they pass.**
- [ ] **Step 5: Commit** « Population : satisfaction, passage entre classes et départ des pauvres dans la journée d'une ville ».

---

### Task 5: Assets and the economy's wiring

**Files:**
- Modify: `Assets/Scripts/Game/Economy/GoodDefinition.cs`, `Assets/Scripts/Game/Economy/EconomyDefinition.cs`, `Assets/Scripts/Game/Population/BuildingsDefinition.cs`, `Assets/Scripts/Game/Economy/WorldEconomy.cs`, `Assets/Scripts/Editor/WorldMap/WorldMapSetup.cs`
- Modify: the 20 assets of `Assets/Data/Goods/`, `Assets/Data/Economy/Economy.asset`, `Assets/Data/Economy/Buildings.asset`
- Test: `EconomyDefinitionTests`, `BuildingsDefinitionTests`, `EconomyContentTests`, `Tests/EditMode/Common/TestEconomy.cs`

**Interfaces:**
- Consumes: Tasks 1 to 4.
- Produces:
  - `GoodDefinition`:
    - Fields `richWeeklyConsumptionPerThousand`, `wealthyWeeklyConsumptionPerThousand` and `poorWeeklyConsumptionPerThousand`, which replace `weeklyConsumptionPerThousand`.
    - Fields `richSatisfactionWeight`, `wealthySatisfactionWeight` and `poorSatisfactionWeight`, each `Min(0)`.
    - Properties `PerClass<double> WeeklyConsumptionPerThousand` and `PerClass<int> SatisfactionWeights`.
  - `EconomyDefinition`:
    - A header "Satisfaction and classes, as in Patrician III (« Besoins des citoyens » : Intermédiaire)" with the thirteen settings of `SocialRule`, named as its parameters, at the values of Task 3.
    - `cityHouseOccupancy` becomes `cityPoorHouseOccupancy` (`FormerlySerializedAs("cityHouseOccupancy")`), next to `cityWealthyHouseOccupancy` (0.96) and `cityRichHouseOccupancy` (0.97).
    - `SocialRule CreateSocialRule()`.
    - `CityHousingRule CreateHousingRule(SocialClass housedClass, int room, IReadOnlyList<MaterialCost> materials)`.
    - `CreateMarket(CityDefinition city, MarketPricing pricing, int daysPerWeek, LabourRule labour = null, PerClass<int> roomPerHouse = default, IReadOnlyList<CityHousingRule> housing = null, SocialRule social = null)`. With labour, each room per house must be at least 1.
    - `TryValidateSettings` also checks `CreateSocialRule()`.
  - `BuildingsDefinition`:
    - `house` becomes `poorHouse` (`FormerlySerializedAs("house")`) and `houseRoom` becomes `poorHouseRoom` (`FormerlySerializedAs("houseRoom")`), next to `wealthyHouse`, `wealthyHouseRoom`, `richHouse` and `richHouseRoom`.
    - `BuildingType HouseOf(SocialClass)` and `PerClass<int> HouseRooms`. `House` and `HouseRoom` are removed.
    - `TryValidate` refuses a room under 1 for any class.
  - `WorldEconomy`:
    - `PerClass<BuildingCost> HouseCosts` replaces `HouseCost`. A null cost means that house is unavailable.
    - It builds the three housing rules (rich, wealthy, poor), each from `MaterialsOf(HouseOf(c), economy)`, and the social rule, and passes them to `CreateMarket`.

- [ ] **Step 1: Write the failing tests:**
  - `EconomyDefinitionTests.CreateMarket_StartsTheCityWithItsThreeClasses`: rooms (40, 70, 140) with labour, counts as in Task 1.
  - `EconomyDefinitionTests.CreateMarket_ConvertsTheWeeklyConsumptionOfEachClass`: weekly (52.5, 13.3, 0) over a 7-day week gives daily (7.5, 1.9, 0) per thousand.
  - `EconomyDefinitionTests.TryValidateSettings_RefusesAStartingSatisfactionAboveTheHighest`.
  - `BuildingsDefinitionTests.TryValidate_RefusesAHouseOfAnyClassWithoutRoom`.
  - `BuildingsDefinitionTests.HouseOf_KeepsThePoorHouseOfAnOldAsset`: a YAML with the old `house` and `houseRoom` keys, loaded through `FormerlySerializedAs`. If an in-memory object cannot reproduce a renamed key, check instead that `Buildings.asset`, once refreshed, has 140 for the poor and its old gold.
  - `EconomyContentTests`: every good of `Economy.asset` has consumptions and weights at least 0, and the values listed below.
  - `TestEconomy.CreateGood` keeps its signature and sets the three consumptions to the same rate. An optional `PerClass<int>? satisfactionWeights` sets the weights.

- [ ] **Step 2: Run them; they fail.**
- [ ] **Step 3: Implement**, then write the values into the assets. Edit the YAML directly, then call `AssetDatabase.Refresh()`. Weekly consumption per 1 000 inhabitants (rich / wealthy / poor) and satisfaction weights (rich / wealthy / poor), P3's:

| Asset | Consumption | Weights |
|---|---|---|
| Grain | 3.15 / 4.2 / 5.25 | 2 / 4 / 8 |
| Meat | 3.85 / 3.045 / 0.42 | 5 / 4 / 4 |
| Fish | 1.4 / 2.8 / 3.5 | 2 / 6 / 6 |
| Beer | 22.75 / 45.5 / 22.75 | 2 / 6 / 6 |
| Salt | 0.35 / 0.35 / 0.35 | 2 / 2 / 4 |
| Honey | 17.5 / 8.75 / 1.75 | 3 / 2 / 0 |
| Spices | 1.4 / 0.7 / 0.7 | 3 / 0 / 0 |
| Wine | 52.5 / 13.3 / 0 | 5 / 2 / 0 |
| Cloth | 17.5 / 12.25 / 5.25 | 5 / 4 / 0 |
| Skins | 21 / 10.5 / 0 | 3 / 2 / 0 |
| Oil | 17.5 / 12.25 / 3.5 | 3 / 4 / 4 |
| Timber | 2.8 / 2.8 / 1.4 | 3 / 4 / 6 |
| Metal | 35 / 26.25 / 8.75 | 2 / 2 / 0 |
| Leather | 15.4 / 12.25 / 1.75 | 2 / 2 / 4 |
| Wool | 0.35 / 1.4 / 0.7 | 2 / 6 / 4 |
| Pitch | 0 / 0 / 0 | 0 / 0 / 0 |
| PigIron | 0 / 0 / 0 | 0 / 0 / 0 |
| Hemp | 0.175 / 0.105 / 0.07 | 0 / 0 / 0 |
| Pottery | 10.5 / 6.3 / 4.2 | 3 / 2 / 4 |
| Tiles | 0.035 / 0.035 / 0 | 0 / 0 / 0 |

`Buildings.asset` gets the three houses of the Global Constraints. Timber is guid `c1aee0a6d45237f4f98a31437e2602e4`, metal `4aa4ecf3894e5d34b8ade5228cde2e48`. In `WorldMapSetup`, `SampleGood` takes the three consumptions and the three weights of this table, and the houses are filled with the same three costs and rooms. Update `.claude/rules/economy.md` where it speaks of the consumption. Until Task 6, `CityInfoPanelController` passes `HouseCosts.Poor` and `HouseRooms.Poor` to the buildings section.

- [ ] **Step 4: Refresh, check that the console has no compile error, and run all the tests; they pass.**
- [ ] **Step 5: Commit** « Données : consommation et poids de satisfaction par classe, trois maisons ».

---

### Task 6: What the player sees

**Files:**
- Create: `Assets/Scripts/Game/Population/SocialClassNames.cs`
- Modify: `Assets/Scripts/Game/Population/CityBuildingsSection.cs`, `Assets/Scripts/Game/WorldMap/CityInfoPanelController.cs`, `Assets/Scripts/Game/Population/BuildingsDefinition.cs` (`CostsText`)
- Test: `CityMarketSectionTests`, `CityBuildingsSectionTests`, `BuildingsDefinitionTests`

**Interfaces:**
- Consumes: Tasks 4 and 5.
- Produces:
  - `SocialClassNames.HousesOf(SocialClass)` returns "Maisons de riches", "Maisons d'aisés" or "Maisons de pauvres", and `HouseOf(SocialClass)` returns "Maison de riches", "Maison d'aisés" or "Maison de pauvres".
  - `CityInfoPanelController.FormatPopulation(int rich, int wealthy, int poor, int beggars)` and `HouseBuiltText(string city, SocialClass housedClass)`.
  - `CityBuildingsSection`:
    - Constructor `(…, IReadOnlyList<string> goodNames, PerClass<BuildingCost> houseCosts, PerClass<int> houseRooms, BuildingCost workshopCost, …)`.
    - `bool BuildHouse(SocialClass)`, and `event Action<SocialClass> HouseBuilt`.
    - `static string HousesText(SocialClass housedClass, int playerHouses, int inhabitants, int room)`.

- [ ] **Step 1: Write the failing tests:**

```csharp
Assert.AreEqual("Riches : 110 · Aisés : 330 · Pauvres : 1 008 · Mendiants : 64",
    CityInfoPanelController.FormatPopulation(110, 330, 1008, 64));
Assert.AreEqual("Maison de pauvres construite à Lübeck.",
    CityInfoPanelController.HouseBuiltText("Lübeck", SocialClass.Poor));
Assert.AreEqual("Maisons de pauvres : 2 (ville : 1 008 / 1 300 places)",
    CityBuildingsSection.HousesText(SocialClass.Poor, 2, 1008, 1300));
// CityBuildingsSectionTests.Show_ListsTheThreeHousesInAlphabeticalOrder:
//   the house rows read "Maisons d'aisés…", "Maisons de pauvres…", "Maisons de riches…"
// CityBuildingsSectionTests.BuildHouse_BuildsAHouseOfThatClass:
//   BuildHouse(SocialClass.Rich) → PlayerHousesOf(Rich) == 1, RoomOf(Rich) +40, HouseBuilt raised with Rich
// BuildingsDefinitionTests.CostsText_WritesTheThreeHousesThenTheWorkshop:
//   "Maison d'aisés : 10 000 or, 15 bois, 20 métal · Maison de pauvres : 5 000 or, 10 bois, 10 métal · "
//   + "Maison de riches : 25 000 or, 25 bois, 30 métal · Atelier : 10 000 or, 10 bois, 30 métal"
```

Adapt the existing tests of the "Logement" texts and of the single house row.

- [ ] **Step 2: Run them; they fail.**
- [ ] **Step 3: Implement.**
  - The house rows are sorted with `NameOrder` on `HousesOf`. Each row's text reads the player's houses, the inhabitants and the room of its class, and its button builds a house of that class.
  - `CostsText` names the houses with `HouseOf`, sorted with `NameOrder`, then the workshop.
  - `CityInfoPanelController` passes `worldEconomy.HouseCosts` and `buildings.HouseRooms`, and posts `HouseBuiltText` with the class it receives.
- [ ] **Step 4: Run all the tests; they pass.**
- [ ] **Step 5: Commit** « Panneau de ville : population par classe et trois maisons ».

---

### Task 7: Debug table and final check

**Files:**
- Modify: `Assets/Scripts/Game/Debug/DebugCitiesTable.cs`, its USS in `Assets/UI/Debug/`
- Test: `Assets/Tests/EditMode/Debug/DebugCitiesTableTests.cs`

**Interfaces:**
- Consumes: Tasks 1 and 4.
- Produces:
  - Three columns, "Riches", "Aisés" and "Pauvres", after "Mendiants", written by `static string FormatClass(long count, long room, int? satisfaction)`.
  - The total row passes no satisfaction.
  - "Logements" stays the total of the three classes.

- [ ] **Step 1: Write the failing tests:** `FormatClass(110, 120, 50)` is `"110 / 120 (50)"`, `FormatClass(1008, 1300, -5)` is `"1 008 / 1 300 (-5)"`, and `FormatClass(1108, 1400, null)` is `"1 108 / 1 400"`. The header row has the three columns in that order.
- [ ] **Step 2: Run them; they fail.**
- [ ] **Step 3: Implement.** Use a cell class `debug-cities__cell--class`, as wide as the housing cell, and widen the city group's header to match (see the comment over the group's width in the USS). Update `.claude/rules/debug-tools.md` if it lists the columns.
- [ ] **Step 4: Run all the tests; they pass.** Then, with the Editor open, enter play mode on `WorldMap`. Open a city panel and check that the four counters and the three house rows are shown. Let ten days pass in fast forward and check in the debug table that the classes move. Exit play mode, and check the console for errors.
- [ ] **Step 5: Commit** « Débogage : classes et satisfaction dans le tableau des villes ».
