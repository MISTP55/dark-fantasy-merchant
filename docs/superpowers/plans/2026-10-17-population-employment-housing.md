# Population, Employment and Housing — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The population of a city follows its jobs and its houses as in Patrician III: beggars come by themselves, workshops hire them within the free room of the houses, laid-off workers wait in a reserve; the player builds workshops (30 jobs) and houses (100 room) from the city panel; food no longer changes the population.

**Architecture:** A stateless `LabourRule` (Core) holds Patrician III's formulas (beggars, hiring limits, vacancies, reserve leak). `CityPopulation` becomes whole numbers (housed, beggars, room). `CityMarket` takes an optional `LabourRule`: without it hiring stays immediate and the population fixed (the tests' markets), with it the day follows Patrician III's order and the market holds reserves and the player's workshops and houses. `Construction` (Core, static like `Trade`) quotes and pays a building from a hold, the market and the treasury. In `Game`, a `BuildingsDefinition` asset holds the two buildings, `CityBuildingsSection` is the panel's new section, and the debug table shows beggars, housing and reserves.

**Tech Stack:** Unity 6000.6.4f1, C#, UI Toolkit, Unity Test Framework (NUnit, EditMode).

**Spec:** `docs/superpowers/specs/2026-10-17-population-employment-housing-design.md`

## Global Constraints

- All code, comments, log messages, asset and folder names are in English. Everything the player reads is in French, exactly: "Habitants : 1 008 · Mendiants : 59", "Vos bâtiments", "Logements : 2 (ville : 1 008 / 1 300 places)", "Céréales : 2 ateliers (45 / 60 ouvriers)", "Bière : 0 atelier", "Construire (12 340 or)", "Logement : 5 000 or, 10 bois, 10 métal · Atelier : 10 000 or, 10 bois, 30 métal", "Atelier construit à Lübeck : céréales.", "Logement construit à Lübeck.", debug headers "Mendiants", "Logements". Numbers through `TreasuryHudController.FormatNumber` / `FormatGold` (no-break space).
- `Core` contains no `MonoBehaviour`, nothing that needs a scene, and does not reference `Game`.
- Values pinned by the spec: 4 inhabitants per worker; starting beggars 4 ÷ 17 per worker, rounded down; city houses ⌈housed ÷ 100⌉; beggars' satisfaction 50, target `√(P × S ÷ 2) ÷ 3 + 8`, arrivals `⌈√P ÷ 5⌉`, departures `⌈√P ÷ 10⌉`; vacancies need ≥ 5 beggars and > 3 free room, fill `min(beggars − 1, free room) ÷ 4` workers; reserve leak 0.02, ceiling 30; workshop 10 000 gold + 10 timber + 30 metal, 30 jobs; house 5 000 gold + 10 timber + 10 metal, 100 room.
- Integer arithmetic as Patrician III does it: `√` truncated to an int before dividing, divisions of ints truncate. `⌈s ÷ 5⌉` is `(s + 4) / 5` with `s = (int)Math.Sqrt(P)`.
- Never hand-write `.meta` files: Unity generates them. Delete a file together with its `.meta`.
- Never edit the generated `.sln` / `.csproj`. Exclude `Library/` from searches.
- Match the surrounding code: Allman braces, a blank line before `return` and after blocks, XML `<summary>` on public types and members, "The negated comparison also rejects NaN." where a negated comparison validates, `ArgumentOutOfRangeException` for bad numbers.
- Commit directly on `main`. Commit messages in French, "Population : …", ending with:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
- Intentional differences from the spec's wording:
  - Housed inhabitants are a count in `CityPopulation`, changed by 4 per worker at each hire and each leak, rather than derived from the workers: a market's test can then be given any population. The start sets them to 4 × workers, and only the labour moves them, so the spec's invariant holds in the game.
  - A market created without a `LabourRule` keeps the old behaviour (immediate hiring, layoffs that vanish, a fixed population, no beggar step): the existing tests of workshops and prices keep their figures. `WorldEconomy` always gives one.

## Review Focus

1. A test market whose housed inhabitants exceed its room (not enforced without labour): free room must read 0, never a negative hire. Test in Task 1 (`WorkersHireableFrom_ANegativeFreeRoom_HiresNobody`) and Task 2 (`FreeRoom_IsNeverNegative`).
2. A city of 0 inhabitants: no NaN, no beggar comes, nothing throws. Test in Task 1 (`AnEmptyCity_GetsNoBeggar`).
3. A selected ship holding more of a material than the building needs: only what is needed is taken from the hold. Test in Task 4 (`AHoldWithMoreThanNeeded_GivesOnlyWhatIsNeeded`).
4. The gold quoted on the button is the gold actually paid (the market's price moves with every unit bought). Test in Task 4 (`TryBuild_PaysExactlyTheQuote`).
5. Two player workshops of different goods: vacancies go to the first built up to its 30 jobs before the second. Test in Task 3 (`Vacancies_FillThePlayersWorkshopsInTheOrderTheyWereBuilt`).

## Commands

The Unity Editor is normally **open** on this project. Use the `unity-mcp` tools: `Unity_RunCommand` to refresh the asset database and to run the EditMode tests, `Unity_GetConsoleLogs` to read compile errors.

Refresh and compile after every file change, before the tests:

```csharp
UnityEditor.AssetDatabase.Refresh();
```

Run EditMode tests through `Unity_RunCommand`, after checking `EditorApplication.isCompiling` and `EditorUtility.scriptCompilationFailed`:

```csharp
var api = UnityEngine.ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
api.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(
    new UnityEditor.TestTools.TestRunner.Api.Filter
    {
        testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,
        groupNames = null, // or new[] { "DarkFantasyMerchant\\.Tests\\.EditMode\\.LabourRuleTests" }
    }));
```

The run writes `C:\Users\Spellman\AppData\LocalLow\DefaultCompany\Dark Fantasy Merchant\TestResults.xml`: read its `<test-run>` line and the `<test-case ... result="Failed">` entries. `Unity_RunCommand` refuses code that writes files with `System.IO` or needs interaction; log to the console instead.

When the Editor is closed, use the batch-mode commands of `CLAUDE.md` ("Commands").

## File Structure

| File | Responsibility |
|---|---|
| `Assets/Scripts/Core/LabourRule.cs` (new) | Patrician III's formulas: beggars, hiring limits, vacancies, reserve leak. `ReserveLeak` struct. |
| `Assets/Scripts/Core/CityPopulation.cs` (rewrite) | Housed, beggars, room, in whole numbers; the start. |
| `Assets/Scripts/Core/FoodGrowth.cs` (delete) | Gone with food growth. |
| `Assets/Scripts/Core/MarketGood.cs` (modify) | `isFood` removed. |
| `Assets/Scripts/Core/CityMarket.cs` (modify) | Labour in the day, reserves, player's workshops and houses. |
| `Assets/Scripts/Core/Construction.cs` (new) | `MaterialCost`, `BuildingCost`, `ConstructionQuote`, `Construction`. |
| `Assets/Scripts/Game/EconomyDefinition.cs`, `Assets/Data/Economy/Economy.asset` (modify) | Food settings out, labour settings in, `CreateLabourRule`, `CreateMarket` with labour. |
| `Assets/Scripts/Game/BuildingsDefinition.cs`, `Assets/Data/Economy/Buildings.asset` (new) | The two buildings, their costs as `BuildingCost`, the costs line. |
| `Assets/Scripts/Game/WorldEconomy.cs` (modify) | Labour and room to every market; the buildings. |
| `Assets/Scripts/Game/CityBuildingsSection.cs` (new), `CityInfoPanelController.cs`, `Assets/UI/WorldMap/CityInfoPanel.uxml`, `CityInfoPanel.uss` (modify) | The panel's population line and "Vos bâtiments". |
| `Assets/Scripts/Game/DebugCitiesTable.cs` (modify) | Beggars, housing, reserves. |
| `Assets/Scripts/Editor/WorldMapSetup.cs` (modify) | Creates the buildings asset, fills the new references. |
| `CLAUDE.md` (modify) | Population, Economy, panel, debug tools. |

---

### Task 0: Baseline

- [ ] **Step 1: Run every EditMode test before any change**

Run all EditMode tests (Commands). Note every test that already fails: those are not this plan's to fix, and no other test may fail at the end of any task.

- [ ] **Step 2: Commit the spec**

```bash
git add .gitignore docs/superpowers/specs/2026-10-17-population-employment-housing-design.md docs/superpowers/plans/2026-10-17-population-employment-housing.md
git commit -m "Population : spécification et plan de l'emploi et des logements"
```

---

### Task 1: `LabourRule`

**Files:**
- Create: `Assets/Scripts/Core/LabourRule.cs`
- Test: `Assets/Tests/EditMode/LabourRuleTests.cs`, `Assets/Tests/EditMode/TestMarkets.cs`

**Interfaces:**
- Produces:
  - `public sealed class LabourRule` with constructor `LabourRule(int inhabitantsPerWorker, double beggarSatisfaction, int beggarTargetDivisor, int beggarTargetMinimum, int beggarArrivalDivisor, int beggarDepartureDivisor, int vacancyMinimumBeggars, int vacancyMinimumFreeRoom, double reserveLeakShare, int reserveLeakCeiling)` and a get-only property per parameter (same names, PascalCase).
  - `int BeggarTargetFor(int population)`, `int NextBeggars(int beggars, int population)`, `int WorkersHireableFrom(int beggars, int freeRoom)`, `int VacanciesFillable(int beggars, int freeRoom)`, `ReserveLeak LeakAfterLayoff(double counter, int workers, int reserve)`.
  - `public readonly struct ReserveLeak { public ReserveLeak(int leaving, double counter); int Leaving; double Counter; }`.
  - `TestMarkets.Labour => new LabourRule(4, 50, 3, 8, 5, 10, 5, 3, 0.02, 30)`.

- [ ] **Step 1: Write the failing tests** in `LabourRuleTests.cs` (rule `TestMarkets.Labour` unless stated):

```csharp
[TestCase(1067, 62)]   // √26 675 = 163 → 54 + 8
[TestCase(10000, 174)] // √250 000 = 500 → 166 + 8
[TestCase(0, 8)]
public void BeggarTarget_IsPatricianIIIs(int population, int target) =>
    Assert.AreEqual(target, TestMarkets.Labour.BeggarTargetFor(population));

[Test]
public void WithoutSatisfaction_TheTargetIsTheMinimum() =>
    Assert.AreEqual(8, new LabourRule(4, 0, 3, 8, 5, 10, 5, 3, 0.02, 30).BeggarTargetFor(1067));

[TestCase(0, 1067, 7)]    // √1 067 = 32: (32 + 4) / 5 = 7 come
[TestCase(59, 1067, 62)]  // never above the target
[TestCase(100, 1067, 96)] // (32 + 9) / 10 = 4 go
[TestCase(63, 1067, 62)]  // never under the target
[TestCase(62, 1067, 62)]
public void NextBeggars_ComeOrGo_TowardsTheTarget(int beggars, int population, int next) =>
    Assert.AreEqual(next, TestMarkets.Labour.NextBeggars(beggars, population));

[Test]
public void AnEmptyCity_GetsNoBeggar() => Assert.AreEqual(0, TestMarkets.Labour.NextBeggars(0, 0));

[TestCase(40, 24, 6)]  // the room holds 6 workers' families
[TestCase(10, 100, 2)] // 10 beggars make 2 workers
[TestCase(3, 100, 0)]
public void WorkersHireableFrom_TheBeggars_AndTheFreeRoom(int beggars, int freeRoom, int workers) =>
    Assert.AreEqual(workers, TestMarkets.Labour.WorkersHireableFrom(beggars, freeRoom));

[Test]
public void WorkersHireableFrom_ANegativeFreeRoom_HiresNobody() =>
    Assert.AreEqual(0, TestMarkets.Labour.WorkersHireableFrom(100, -50));

[TestCase(50, 100, 12)] // min(49, 100) / 4
[TestCase(200, 20, 5)]  // min(199, 20) / 4
[TestCase(4, 100, 0)]   // under 5 beggars
[TestCase(50, 3, 0)]    // 3 free room is not more than 3
[TestCase(5, 4, 1)]     // min(4, 4) / 4
public void VacanciesFillable_FollowPatricianIII(int beggars, int freeRoom, int workers) =>
    Assert.AreEqual(workers, TestMarkets.Labour.VacanciesFillable(beggars, freeRoom));

[TestCase(0, 5, 10, 0, 0.2)]    // 10 × 0.02: nothing whole yet
[TestCase(0.9, 5, 10, 1, -0.9)] // 1.1 → 1 leaves; the counter loses it twice
[TestCase(0.9, 20, 10, 0, 0.1)] // 30 workers and reserve: nothing leaves, it loses it once
[TestCase(2.5, 19, 10, 1, -0.3)]// 2.7 → 2, cut to 30 − 29 = 1; 0.7 − 1
[TestCase(-2, 5, 10, 0, -1.8)]  // a negative whole part counts as 0
public void LeakAfterLayoff_IsPatricianIIIs(double counter, int workers, int reserve, int leaving, double next)
{
    ReserveLeak leak = TestMarkets.Labour.LeakAfterLayoff(counter, workers, reserve);
    Assert.AreEqual(leaving, leak.Leaving);
    Assert.AreEqual(next, leak.Counter, 1e-9);
}

[Test]
public void TheConstructor_RejectsUnusableSettings()
{
    Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(0, 50, 3, 8, 5, 10, 5, 3, 0.02, 30));
    Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, double.NaN, 3, 8, 5, 10, 5, 3, 0.02, 30));
    Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 0, 8, 5, 10, 5, 3, 0.02, 30));
    Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 3, -1, 5, 10, 5, 3, 0.02, 30));
    Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 3, 8, 0, 10, 5, 3, 0.02, 30));
    Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 3, 8, 5, 0, 5, 3, 0.02, 30));
    Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 3, 8, 5, 10, -1, 3, 0.02, 30));
    Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 3, 8, 5, 10, 5, -1, 0.02, 30));
    Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 3, 8, 5, 10, 5, 3, 1.5, 30));
    Assert.Throws<ArgumentOutOfRangeException>(() => new LabourRule(4, 50, 3, 8, 5, 10, 5, 3, 0.02, -1));
}
```

Add `TestMarkets.Labour` with a `<summary>` ("Patrician III's labour, as the economy's defaults").

- [ ] **Step 2: Run them to verify they fail** — compile error: `LabourRule` does not exist.

- [ ] **Step 3: Implement `LabourRule` and `ReserveLeak` in `LabourRule.cs`**

Ranges: inhabitants per worker ≥ 1; satisfaction finite (any sign); the four divisors ≥ 1; target minimum, vacancy minimums and leak ceiling ≥ 0; leak share in [0, 1]. Formulas, exactly:

```csharp
// BeggarTargetFor
if (!(BeggarSatisfaction > 0d)) return BeggarTargetMinimum;
int root = (int)Math.Sqrt(Math.Floor(population * BeggarSatisfaction / 2d));
return root / BeggarTargetDivisor + BeggarTargetMinimum;

// NextBeggars: s = (int)Math.Sqrt(population)
//   under the target: beggars + Math.Min(target - beggars, (s + BeggarArrivalDivisor - 1) / BeggarArrivalDivisor)
//   above it:         beggars - Math.Min(beggars - target, (s + BeggarDepartureDivisor - 1) / BeggarDepartureDivisor)

// LeakAfterLayoff
double next = counter + reserve * ReserveLeakShare;
int whole = Math.Max(0, (int)next);   // truncation, as ftol
next -= whole;
int total = workers + reserve;
if (whole == 0 || total >= ReserveLeakCeiling) return new ReserveLeak(0, next);
int leaving = Math.Min(whole, ReserveLeakCeiling - total);
return new ReserveLeak(leaving, next - leaving);  // Patrician III subtracts it a second time
```

`WorkersHireableFrom` = `Math.Min(beggars / InhabitantsPerWorker, Math.Max(0, freeRoom) / InhabitantsPerWorker)`. `VacanciesFillable` = 0 when `beggars < VacancyMinimumBeggars || freeRoom <= VacancyMinimumFreeRoom`, else `Math.Min(beggars - 1, freeRoom) / InhabitantsPerWorker`. The class `<summary>` cites `patrician3-re\resultats\Population des villes.md`.

- [ ] **Step 4: Run the tests** — `LabourRuleTests` pass, nothing else fails.

- [ ] **Step 5: Commit** — "Population : règles de Patrician III pour les mendiants, l'embauche et la réserve".

---

### Task 2: Whole population, no more food growth

**Files:**
- Rewrite: `Assets/Scripts/Core/CityPopulation.cs`
- Delete: `Assets/Scripts/Core/FoodGrowth.cs` (+ `.meta`), `Assets/Tests/EditMode/FoodGrowthTests.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Core/MarketGood.cs`, `Assets/Scripts/Core/CityMarket.cs`, `Assets/Scripts/Game/EconomyDefinition.cs`, `Assets/Scripts/Game/WorldEconomy.cs`, `Assets/Scripts/Game/DebugCitiesTable.cs`, `Assets/Data/Economy/Economy.asset`, `CLAUDE.md`
- Test: `CityPopulationTests.cs` (rewrite), `CityMarketTests.cs`, `DebugCitiesTableTests.cs`, `EconomyDefinitionTests.cs`, `EconomyContentTests.cs`, `TestEconomy.cs`

**Interfaces:**
- Produces:
  - `CityPopulation(int housed)` (no beggar, room = housed) and `CityPopulation(int housed, int beggars, int room)`; `static CityPopulation AtStart(int workers, int inhabitantsPerWorker, double beggarsPerWorker, int roomPerHouse)`.
  - `int Housed`, `int Beggars`, `int Room`, `int FreeRoom` (`Math.Max(0, Room - Housed)`), `int Inhabitants` (housed + beggars), `int StartingInhabitants`, `double GrowthSinceStart`.
  - `void AddRoom(int places)` (places ≥ 1), `void Hire(int workers, int inhabitantsPerWorker)` (throws when the beggars are short), `void SendToBeggars(int workers, int inhabitantsPerWorker)` (throws when the housed are short), `void SetBeggars(int beggars)`.
  - `CityMarket` constructor without its `FoodGrowth` parameter; `MonthlyGrowthRate`, `TryGetFoodDays`, `MarketGood.IsFood` and its `isFood` parameter removed. `DailyConsumptionOf` reads `Population.Inhabitants`.
  - `EconomyDefinition.InhabitantsPerWorker` becomes `int` (field `inhabitantsPerWorker`, default 4, `[Min(0)]`), `StartingPopulation` becomes `int` (workforce × inhabitants per worker); the food settings and `CreateFoodGrowth` are removed; `CreateMarket(CityDefinition city, MarketPricing pricing, int daysPerWeek)`.
  - `DebugCitiesTable` without the "Croiss. / mois" column (Task 7 adds the new ones).

- [ ] **Step 1: Write the failing tests** — rewrite `CityPopulationTests.cs`:

```csharp
[TestCase(252, 1008, 59, 1100)] // Patrician III's start: 4 × W, 4 × W ÷ 17, houses rounded up
[TestCase(250, 1000, 58, 1000)]
[TestCase(17, 68, 4, 100)]
[TestCase(0, 0, 0, 0)]
public void AtStart_SplitsTheWorkersFamilies(int workers, int housed, int beggars, int room)
{
    CityPopulation population = CityPopulation.AtStart(workers, 4, 4d / 17d, 100);
    Assert.AreEqual(housed, population.Housed);
    Assert.AreEqual(beggars, population.Beggars);
    Assert.AreEqual(room, population.Room);
    Assert.AreEqual(housed + beggars, population.Inhabitants);
    Assert.AreEqual(housed + beggars, population.StartingInhabitants);
}

[Test] public void FreeRoom_IsNeverNegative() => Assert.AreEqual(0, new CityPopulation(1000, 0, 800).FreeRoom);

[Test]
public void Hire_TurnsFourBeggarsPerWorkerIntoHousedInhabitants()
{
    var population = new CityPopulation(1000, 40, 1100);
    population.Hire(6, 4);
    Assert.AreEqual(1024, population.Housed);
    Assert.AreEqual(16, population.Beggars);
    Assert.AreEqual(76, population.FreeRoom);
    Assert.Throws<ArgumentOutOfRangeException>(() => population.Hire(5, 4));
}

[Test]
public void SendToBeggars_GivesFourHousedInhabitantsPerWorkerBack()
{
    var population = new CityPopulation(1000, 0, 1000);
    population.SendToBeggars(4, 4);
    Assert.AreEqual(984, population.Housed);
    Assert.AreEqual(16, population.Beggars);
}

[Test]
public void GrowthSinceStart_FollowsTheInhabitants()
{
    var population = new CityPopulation(1000, 0, 1100);
    population.SetBeggars(100);
    Assert.AreEqual(0.1, population.GrowthSinceStart, 1e-9);
    Assert.AreEqual(0, new CityPopulation(0).GrowthSinceStart);
}

[Test]
public void AddRoom_AddsPlaces()
{
    var population = new CityPopulation(1000);
    population.AddRoom(100);
    Assert.AreEqual(1100, population.Room);
    Assert.Throws<ArgumentOutOfRangeException>(() => population.AddRoom(0));
}
```

plus `TheConstructors_RejectNegativeCounts` (negative housed, beggars or room, `AtStart` with negative workers, inhabitants per worker < 1, room per house < 1, NaN or negative beggars per worker → `ArgumentOutOfRangeException`).

Elsewhere:
- `CityMarketTests.cs`: delete `Food`, `Growth`, `FedMarket` and the six food tests (`WithoutAFoodGrowth_…` to `MonthlyGrowthRate_FollowsTheFoodInStock_AtOnce`); keep the others with `MarketOf`.
- `EconomyDefinitionTests.cs`: delete `CreateFoodGrowth_UsesTheDefinitionsSettings` and `CreateMarket_FeedsThePopulationWithFoodOnly`; `CreateMarket_StartsWithThePopulationOfTheWorkforce` sets `inhabitantsPerWorker` to 4 (`intValue`) and asserts `economy.StartingPopulation == 1000` and `market.Population.Inhabitants == 1000`; `CreateMarket_ForACityThatProducesNothing_…` compares `Inhabitants`; `TryValidateSettings_SaysWhenThePopulationPerWorkerIsWrong` writes `intValue = -1`.
- `EconomyContentTests.cs`: remove the `CreateFoodGrowth(30)` assertion; assert `economy.InhabitantsPerWorker == 4`.
- `TestEconomy.SetStartingPopulation` writes `inhabitantsPerWorker` with `intValue = 1`.
- `DebugCitiesTableTests.cs`: delete `ARow_WritesTheMonthlyGrowth_OfAPopulationThatFoodChanges`; `FedMarket(double startingDays, CityPopulation population)` builds `new MarketGood(10, 2, 0, 0)` without food growth. A population now changes through `market.Population.SetBeggars(…)` followed by `market.StartDay()` (which raises `Changed`; a market without labour keeps the beggars it is given):
  - `WhileShown_ADayThatStarts_RewritesThePopulation_AndTheTotals`: one city of `new CityPopulation(900, 100, 1000)`, `SetBeggars(0)`, `StartDay()` → population cell "900", total "900".
  - `ARow_WritesTheGrowthSinceTheStart_AndTheTotalTheOneOfAllTheInhabitants`: cities of `(900, 100, 1000)` set to 0 beggars and `(1000, 0, 1100)` set to 50 → "-10,0 %", "+5,0 %", total "-2,5 %" (1 950 ÷ 2 000 − 1).
  - The header asserts become `Population` then `Croiss. totale`, at cells 1 and 2.

- [ ] **Step 2: Run them to verify they fail** — compile errors on the new `CityPopulation` members.

- [ ] **Step 3: Implement** the new `CityPopulation` (`AtStart`: `housed = workers × inhabitantsPerWorker`, `beggars = (int)Math.Floor(workers × beggarsPerWorker + 1e-9)`, `room = ((housed + roomPerHouse - 1) / roomPerHouse) × roomPerHouse`); delete `FoodGrowth.cs`, its tests and their `.meta`; remove the food parts of `CityMarket`, `MarketGood`, `EconomyDefinition` (four fields, four properties, `CreateFoodGrowth`, the `foodGrowth` parameter; the `StartingPopulation` validation stays, on the int), `WorldEconomy` (no `FoodGrowth`), `DebugCitiesTable` (the growth column, its total and `monthlyChange`; `GrowthSinceStart` and the totals read `Inhabitants` and `StartingInhabitants`). In `Economy.asset`, delete the four lines `stableFoodDays` … `maxMonthlyDecline` and write `inhabitantsPerWorker: 4`. Update the `<summary>` of `CityMarket` and `StartDay` (no more food).

- [ ] **Step 4: Run all EditMode tests** — all pass (Task 0's failures excepted).

- [ ] **Step 5: Update `CLAUDE.md`** — Population section: whole inhabitants, nothing changes them yet but the labour to come (Task 3 completes it); Economy: 4 inhabitants per worker, the starting population 1 000; remove `FoodGrowth` and the "Croiss. / mois" column from Debug tools; the Project state sentence "the population of a city grows or declines with the food it holds" becomes "the population of a city follows its jobs and houses" (true at the end of Task 5).

- [ ] **Step 6: Commit** — "Population : habitants en nombres entiers, fin de la croissance par la nourriture".

---

### Task 3: Labour in the city's day

**Files:**
- Modify: `Assets/Scripts/Core/CityMarket.cs`, `CLAUDE.md`
- Test: `Assets/Tests/EditMode/CityMarketLabourTests.cs` (new)

**Interfaces:**
- Consumes: `LabourRule`, `ReserveLeak` (Task 1); `CityPopulation.Hire`, `SendToBeggars`, `SetBeggars`, `AddRoom`, `FreeRoom` (Task 2).
- Produces on `CityMarket`:
  - constructor gains a last parameter `LabourRule labour = null`; `LabourRule Labour { get; }`.
  - `static int StartingWorkforceOf(IReadOnlyList<MarketGood> goods, WorkforceRule workforce)` — the sum of the workers the constructor gives the workshops (the constructor uses it).
  - `int ReserveOf(int good)`; `int BeggarTarget` (`Labour?.BeggarTargetFor(Population.Inhabitants) ?? 0`).
  - `void AddPlayerWorkshop(int good, int jobs)` (`ArgumentOutOfRangeException` for a good the city does not produce or jobs < 1); `int PlayerWorkshopsOf(int good)`, `int PlayerWorkersOf(int good)`, `int PlayerJobsOf(int good)`.
  - `void AddPlayerHouse(int room)`; `int PlayerHouses { get; }`.
  - Each `Add…` raises `Changed` once.

- [ ] **Step 1: Write the failing tests** in `CityMarketLabourTests.cs`. Fixture:

```csharp
// Two barrels a day for 1 000 inhabitants; a workshop of up to 60 workers making a tenth each.
// Near 1 000 inhabitants: T0 20, T1 60, T2 120, T3 140.
private static MarketGood Good => new MarketGood(10, 2, 60, 0.1);
// Every workshop of these markets starts with 10 workers (or 5 each for two).
private static WorkforceRule Workforce => new WorkforceRule(16, 8, 4, 1, 1, 10);
private static CityMarket Market(double startingDays, CityPopulation population, LabourRule labour = null, int goods = 1) =>
    new CityMarket(Enumerable.Repeat(Good, goods).ToArray(), TestMarkets.Thresholds, Workforce, startingDays,
        TestMarkets.Pricing, 7, population, labour ?? TestMarkets.Labour);
```

Tests (two-goods markets use `new WorkforceRule(16, 8, 4, 1, 1, 20)` so each starts with 10):

| Test | Setup | Assertions after `StartDay()` |
|---|---|---|
| `AWorkshop_HiresBeggars_FourPerWorker_WithinTheFreeRoom` | `Market(0, new CityPopulation(1000, 40, 1024))` (stock 0: wants 26) | `WorkersOf(0) == 16`, `Housed == 1024`, `Beggars == 23` (16 left, then 7 come: target 61 at 1 040) |
| `WithoutFreeRoom_NoBeggarIsHired` | `Market(0, new CityPopulation(1000, 100, 1000))` | `WorkersOf(0) == 10`, `Housed == 1000`, `Beggars == 96` |
| `ALayoff_GoesToTheReserve_AndStaysHoused` | `Market(100, new CityPopulation(1000))` (stock 200 > T3) | `WorkersOf(0) == 1`, `ReserveOf(0) == 9`, `Housed == 1000` |
| `AWorkshop_HiresFromItsReserveFirst` | as above, then `Take(0, AvailableOf(0))`, second `StartDay()` | `WorkersOf(0) == 10`, `ReserveOf(0) == 0`, `Housed == 1000` |
| `AWorkshop_TakesOtherReserves_AfterTheBeggars` | two goods, `startingDays` 100, `Take(1, AvailableOf(1))` before the day, `new CityPopulation(1000, 8, 1008)` | `WorkersOf(0) == 1`, `ReserveOf(0) == 0`, `WorkersOf(1) == 21` (2 from beggars, 9 from good 0's reserve), `Housed == 1008` |
| `ALeak_SendsFourInhabitantsPerWorkerToTheBeggars` | `Market(100, new CityPopulation(1000), new LabourRule(4, 50, 3, 8, 5, 10, 5, 3, 0.5, 30))` | `ReserveOf(0) == 5` (9 × 0.5 = 4.5 → 4 leave), `Housed == 984`, `Beggars == 23` (16, then 7 come) |
| `TheCity_HiresBeforeThePlayer` | `Market(0, new CityPopulation(1000, 40, 1100))`, `AddPlayerWorkshop(0, 30)` | `WorkersOf(0) == 20`, `PlayerWorkersOf(0) == 0` |
| `Vacancies_AreFilledFromTheBeggars` | `Market(100, new CityPopulation(1000, 50, 1100))`, `AddPlayerWorkshop(0, 30)` | `PlayerWorkersOf(0) == 12`, `Housed == 1048`, `Beggars == 9`; second day `PlayerWorkersOf(0) == 14` |
| `Vacancies_FillThePlayersWorkshopsInTheOrderTheyWereBuilt` | two goods, `startingDays` 100, `new CityPopulation(1000, 200, 1200)`, `AddPlayerWorkshop(1, 30)` then `AddPlayerWorkshop(0, 30)` | `PlayerWorkersOf(1) == 30`, `PlayerWorkersOf(0) == 19`, `PlayerJobsOf(0) == 30`, `PlayerWorkshopsOf(0) == 1` |
| `Vacancies_NeedFiveBeggars` | `Market(100, new CityPopulation(1000, 4, 1100))`, `AddPlayerWorkshop(0, 30)` | `PlayerWorkersOf(0) == 0` |
| `AddPlayerHouse_AddsItsRoom_AndRaisesChangedOnce` | `Market(30, new CityPopulation(1000))`, count `Changed` | after `AddPlayerHouse(100)`: `Population.Room == 1100`, `PlayerHouses == 1`, one `Changed` |
| `AddPlayerWorkshop_RefusesAGoodNotProduced` | a market of `new MarketGood(10, 2, 0, 0)` | `AddPlayerWorkshop(0, 30)` and `AddPlayerWorkshop` with jobs 0 on a produced good throw `ArgumentOutOfRangeException` |
| `WithoutLabour_HiringIsImmediate_AndThePopulationStays` | `Market(0, new CityPopulation(1000, 40, 1000), labour: null)` → pass `null` explicitly through a second helper | `WorkersOf(0) == 26`, `ReserveOf(0) == 0`, `Beggars == 40`, `BeggarTarget == 0` |
| `StartingWorkforceOf_SumsTheStartingWorkers` | | `CityMarket.StartingWorkforceOf(new[] { Good, Good, new MarketGood(10, 2, 0, 0) }, new WorkforceRule(16, 8, 4, 1, 1, 25)) == 26` (13 + 13) |

(The Market helper's `labour ?? TestMarkets.Labour` means the no-labour test needs its own constructor call.)

- [ ] **Step 2: Run them to verify they fail** — compile errors.

- [ ] **Step 3: Implement in `CityMarket`.** New state: `int[] reserves`, `double[] leakCounters`, a `List` of player workshops in build order (good, jobs, workers), `PlayerHouses`. `StartDay` per good keeps consumption, then `target = workforce.NextWorkers(...)`:
  - without labour: `workers[i] = target` (as now);
  - with labour, `target > workers[i]`: hire `target - workers[i]` from, in order, its own reserve; the beggars, `Labour.WorkersHireableFrom(Population.Beggars, Population.FreeRoom)` at most, through `Population.Hire(n, Labour.InhabitantsPerWorker)`; the other goods' reserves in catalogue order;
  - with labour, `target < workers[i]`: move the difference to `reserves[i]`, then `ReserveLeak leak = Labour.LeakAfterLayoff(leakCounters[i], workers[i], reserves[i])`, store `leak.Counter`, take `leak.Leaving` from the reserve and `Population.SendToBeggars(leak.Leaving, …)`;
  - production as now, with `workers[i]`.
  After every good, with labour: `n = Labour.VacanciesFillable(Population.Beggars, Population.FreeRoom)` shared over the player's workshops in build order up to their jobs, each through `Population.Hire`; then `Population.SetBeggars(Labour.NextBeggars(Population.Beggars, Population.Inhabitants))`. `Changed` once at the end, as now.

- [ ] **Step 4: Run all EditMode tests** — all pass.

- [ ] **Step 5: Update `CLAUDE.md`** — Population: the day's labour (hiring sources and order, the reserve and its leak, the vacancies, the beggars), a market without `LabourRule` keeps its population; Economy: workers are no longer free once the economy gives the labour (Task 5).

- [ ] **Step 6: Commit** — "Population : embauche parmi les mendiants, réserve et ateliers du joueur".

---

### Task 4: Construction

**Files:**
- Create: `Assets/Scripts/Core/Construction.cs`
- Test: `Assets/Tests/EditMode/ConstructionTests.cs`

**Interfaces:**
- Consumes: `CityMarket.AvailableOf`, `BuyPriceOf`, `Take`, `MaxWorkersOf`, `AddPlayerWorkshop`, `AddPlayerHouse` (Task 3); `CargoHold.UnitsOf`, `TryRemove`; `Treasury.CanAfford`, `TryWithdraw`.
- Produces:
  - `public readonly struct MaterialCost { MaterialCost(int good, int units); int Good; int Units; }` (good ≥ 0, units ≥ 1).
  - `public sealed class BuildingCost { BuildingCost(long gold, IReadOnlyList<MaterialCost> materials); long Gold; IReadOnlyList<MaterialCost> Materials; }` (gold ≥ 0; a good twice → `ArgumentException`; the list is copied).
  - `public readonly struct ConstructionQuote { bool IsPossible; long Gold; }` — `Gold` is the building's gold plus the price of what would be bought.
  - `public static class Construction` with `ConstructionQuote QuoteHouse(CityMarket market, CargoHold hold, Treasury treasury, BuildingCost cost)`, `ConstructionQuote QuoteWorkshop(CityMarket market, CargoHold hold, Treasury treasury, BuildingCost cost, int good)`, `bool TryBuildHouse(CityMarket market, CargoHold hold, Treasury treasury, BuildingCost cost, int room)`, `bool TryBuildWorkshop(CityMarket market, CargoHold hold, Treasury treasury, BuildingCost cost, int good, int jobs)`. `hold` may be null (no ship): everything is bought.

- [ ] **Step 1: Write the failing tests.** Fixture: a market (no labour) of two goods, `new MarketGood(10, 2, 0, 0)` (good 0, the material, stock 60 with 30 starting days) and `new MarketGood(10, 2, 30, 0.1)` (good 1, produced); `new CityPopulation(1000)`; a `CargoHold(200)`; `new Treasury(10000)`; `cost = new BuildingCost(100, new[] { new MaterialCost(0, 3) })`.

| Test | Assertions |
|---|---|
| `Quote_TakesTheHoldFirst_ThenPricesWhatIsBought` | hold has 1 of good 0: `Gold == 100 + BuyPriceOf(0, 0) + BuyPriceOf(0, 1)`, `IsPossible` |
| `TryBuild_PaysExactlyTheQuote` | after `TryBuildHouse(…, 100)`: true; treasury lost exactly the quote's `Gold`; hold has 0; `AvailableOf(0)` is 2 less; `PlayerHouses == 1`; `Room == 1100` |
| `AHoldWithMoreThanNeeded_GivesOnlyWhatIsNeeded` | hold has 5: quote `Gold == 100`; after building the hold has 2 and the stock is unchanged |
| `WithoutAShip_EverythingIsBought` | `hold` null: `Gold == 100 + BuyPriceOf(0,0) + BuyPriceOf(0,1) + BuyPriceOf(0,2)` |
| `WhenHoldAndMarketLack_NothingIsTaken` | material cost 70 units, hold 5: not possible; `TryBuildHouse` false; hold, stock, gold, room unchanged |
| `WhenTheGoldLacks_NothingIsTaken` | `new Treasury(150)`: not possible, nothing changes |
| `ATreasuryInDebt_BuildsNothing` | `new Treasury(-10)`, a cost of 0 gold and no material: not possible |
| `AWorkshop_AddsItsJobs` | `TryBuildWorkshop(…, 1, 30)` true; `PlayerWorkshopsOf(1) == 1`, `PlayerJobsOf(1) == 30` |
| `AWorkshopForAGoodNotProduced_IsImpossible` | `QuoteWorkshop(…, 0)` not possible; `TryBuildWorkshop(…, 0, 30)` false, nothing taken |
| `BuildingCost_RejectsAGoodTwice` | `ArgumentException` |

- [ ] **Step 2: Run them to verify they fail** — compile errors.

- [ ] **Step 3: Implement `Construction.cs`.** A quote takes from the hold `min(UnitsOf, Units)` of each material, prices the rest unit by unit with `BuyPriceOf(good, i)` for `i` from 0, is impossible when the rest exceeds `AvailableOf`, and is possible only when `treasury.CanAfford(total)` (and, for a workshop, `MaxWorkersOf(good) > 0`). Building quotes first and returns false on an impossible quote; otherwise `hold.TryRemove`, `market.Take`, `treasury.TryWithdraw(quote.Gold)`, then `AddPlayerHouse` / `AddPlayerWorkshop`. `<summary>`s say that what is bought does not pass through a hold.

- [ ] **Step 4: Run all EditMode tests** — all pass.

- [ ] **Step 5: Commit** — "Population : construction d'ateliers et de logements".

---

### Task 5: The economy gives the labour, the buildings asset

**Files:**
- Create: `Assets/Scripts/Game/BuildingsDefinition.cs`, `Assets/Data/Economy/Buildings.asset` (through the setup tool)
- Modify: `Assets/Scripts/Game/EconomyDefinition.cs`, `Assets/Data/Economy/Economy.asset`, `Assets/Scripts/Game/WorldEconomy.cs`, `Assets/Scripts/Editor/WorldMapSetup.cs`, `CLAUDE.md`
- Test: `EconomyDefinitionTests.cs`, `EconomyContentTests.cs`, `Assets/Tests/EditMode/BuildingsDefinitionTests.cs` (new)

**Interfaces:**
- Consumes: `LabourRule` (Task 1), `CityPopulation.AtStart` (Task 2), `CityMarket.StartingWorkforceOf` and the `labour` parameter (Task 3), `BuildingCost`, `MaterialCost` (Task 4).
- Produces:
  - `EconomyDefinition` fields under a header "Labour, as in Patrician III": `startingBeggarsPerWorker` (4 ÷ 17), `beggarSatisfaction` (50), `beggarTargetDivisor` (3), `beggarTargetMinimum` (8), `beggarArrivalDivisor` (5), `beggarDepartureDivisor` (10), `vacancyMinimumBeggars` (5), `vacancyMinimumFreeRoom` (3), `reserveLeakShare` (0.02), `reserveLeakCeiling` (30), each with a `[Tooltip]`; property `StartingBeggarsPerWorker`; `LabourRule CreateLabourRule()`; `TryValidateSettings` adds `ProblemOf(() => CreateLabourRule(), "the labour")` and `ProblemOf(() => CityPopulation.AtStart(0, inhabitantsPerWorker, startingBeggarsPerWorker, 1), "the starting beggars per worker")`.
  - `CityMarket CreateMarket(CityDefinition city, MarketPricing pricing, int daysPerWeek, LabourRule labour = null, int roomPerHouse = 0)`: without labour the population is `new CityPopulation(StartingPopulation)`; with it, `roomPerHouse` ≥ 1 (else `ArgumentOutOfRangeException`) and `CityPopulation.AtStart(CityMarket.StartingWorkforceOf(goods, workforceRule), InhabitantsPerWorker, StartingBeggarsPerWorker, roomPerHouse)`.
  - `BuildingsDefinition` (`[CreateAssetMenu(fileName = "Buildings", menuName = "Dark Fantasy Merchant/Buildings")]`) with nested `[Serializable] public sealed class MaterialAmount` (`GoodDefinition good`, `int units`; properties `Good`, `Units`) and `[Serializable] public sealed class BuildingType` (`long gold`, `List<MaterialAmount> materials`; properties `Gold`, `Materials`); fields `workshop`, `workshopJobs` (30), `house`, `houseRoom` (100); properties `Workshop`, `WorkshopJobs`, `House`, `HouseRoom`; `bool TryCreateCost(BuildingType type, EconomyDefinition economy, out BuildingCost cost, out string problem)` (a material null, outside the active goods or under 1 unit, or a negative gold, is a problem naming the building and the good's asset); `bool TryValidate(EconomyDefinition economy, out string problem)` (both costs, jobs ≥ 1, room ≥ 1); `string CostsText(EconomyDefinition economy)` → "Logement : 5 000 or, 10 bois, 10 métal · Atelier : 10 000 or, 10 bois, 30 métal" (a material's name is its `DisplayName` in lower case).
  - `WorldEconomy`: `[SerializeField] private BuildingsDefinition buildings;`, `public BuildingsDefinition Buildings => buildings;`; markets created with `economy.CreateLabourRule()` and `buildings.HouseRoom`; without buildings or with `TryValidate` false it logs an error naming the problem and the cities have no market; the warning for a city producing nothing ends "; it starts with no inhabitant.".
  - `WorldMapSetup.CreateBuildings()` (`public static`, idempotent like `CreateExpenses`, `BuildingsPath = EconomyFolder + "/Buildings.asset"`, materials loaded from `GoodsFolder + "/Timber.asset"` and `"/Metal.asset"`), called after `CreateEconomy()`; `AddEconomyToScene` fills `worldEconomy`'s `buildings`.

- [ ] **Step 1: Write the failing tests.**
  - `EconomyDefinitionTests.CreateMarket_WithLabour_SplitsThePopulationAsPatricianIII`: workforce 250, `inhabitantsPerWorker` 4, city with wheat efficient and fish inefficient (the fixture's goods, 60 workers at most) → W = 120 (125 cut to 60 each): `Housed == 480`, `Beggars == 28`, `Room == 500`, `market.Labour != null`.
  - `CreateMarket_WithLabour_NeedsTheRoomOfAHouse`: `roomPerHouse` 0 → `ArgumentOutOfRangeException`.
  - `TryValidateSettings_SaysWhenTheLabourIsWrong`: `beggarArrivalDivisor` 0 → false, problem contains "labour".
  - `CreateLabourRule_UsesTheDefinitionsSettings`: a new `EconomyDefinition`'s rule has the ten defaults.
  - `BuildingsDefinitionTests`: `TryCreateCost` maps timber and metal to their indices among the active goods; an inactive material, a null one, 0 units, a negative gold each give a problem naming them; `CostsText` gives the exact line above.
  - `EconomyContentTests`: `Assets/Data/Economy/Buildings.asset` has the workshop at 10 000 gold, Timber 10, Metal 30, 30 jobs, the house at 5 000 gold, Timber 10, Metal 10, 100 room, and `TryValidate` against `Economy.asset` is true; `Economy.asset` has the ten labour defaults.

- [ ] **Step 2: Run them to verify they fail.**

- [ ] **Step 3: Implement** `EconomyDefinition`, `BuildingsDefinition`, `WorldEconomy`, `WorldMapSetup`; add the ten labour lines to `Economy.asset` after `inhabitantsPerWorker` (YAML keys as the field names).

- [ ] **Step 4: Create the asset and wire the open scene** with `Unity_RunCommand` (scene `Assets/Scenes/WorldMap.unity` open, no unsaved changes): call `WorldMapSetup.CreateBuildings()`, `AssetDatabase.SaveAssets()`, then set `WorldEconomy`'s `buildings` through a `SerializedObject` and `EditorSceneManager.SaveScene`. Check that `Buildings.asset` and its `.meta` exist.

- [ ] **Step 5: Run all EditMode tests** — all pass.

- [ ] **Step 6: Update `CLAUDE.md`** — Economy: start of a game (4 × W housed, 4 × W ÷ 17 beggars, houses rounded up; workers taken from the beggars); the buildings asset; the setup tool creates it and fills `buildings` of `WorldEconomy`.

- [ ] **Step 7: Commit** — "Population : l'économie donne l'emploi et les logements à chaque ville".

---

### Task 6: The city panel

**Files:**
- Create: `Assets/Scripts/Game/CityBuildingsSection.cs`
- Modify: `Assets/Scripts/Game/CityInfoPanelController.cs`, `Assets/UI/WorldMap/CityInfoPanel.uxml`, `Assets/UI/WorldMap/CityInfoPanel.uss`, `Assets/Scripts/Editor/WorldMapSetup.cs`, `CLAUDE.md`
- Test: `Assets/Tests/EditMode/CityBuildingsSectionTests.cs` (new), `CityMarketSectionTests.cs`

**Interfaces:**
- Consumes: `Construction`, `BuildingCost` (Task 4); `BuildingsDefinition`, `WorldEconomy.Buildings` (Task 5); the market's player members (Task 3).
- Produces:
  - `CityInfoPanelController.FormatPopulation(int housed, int beggars)` → "Habitants : 1 008 · Mendiants : 59"; `NoPopulation = "Habitants : —"`; `static string WorkshopBuiltText(string city, string good)` → "Atelier construit à Lübeck : céréales." (good in lower case); `static string HouseBuiltText(string city)` → "Logement construit à Lübeck."; new `[SerializeField] private PlayerNotifications playerNotifications;` (optional, like `PlayerExpenses`'s).
  - `public sealed class CityBuildingsSection` with constructor `(VisualElement section, VisualElement rowContainer, Label costsLabel, IReadOnlyList<string> goodNames, BuildingCost houseCost, int houseRoom, BuildingCost workshopCost, int workshopJobs, string costsText, Treasury treasury)`; `void Show(CityMarket market, CargoHold hold)`, `void Hide()`; `bool BuildHouse()`, `bool BuildWorkshop(int good)` (what the buttons call); `event Action HouseBuilt`, `event Action<int> WorkshopBuilt`; statics `HousesText(int playerHouses, int housed, int room)`, `WorkshopsText(string goodName, int workshops, int workers, int jobs)`, `BuildButtonText(long gold)` → "Construire (12 340 or)".
  - UXML, before `market-section`: `<ui:VisualElement name="buildings-section" class="buildings">` with a `Label` "Vos bâtiments" (class `city-panel__section`), `<ui:VisualElement name="buildings-rows" />` and `<ui:Label name="buildings-costs" class="buildings__costs" />`. Rows: class `buildings__row` (row, space-between, centered), a `Label` `buildings__text`, a `Button` `buildings__button`.

- [ ] **Step 1: Write the failing tests** in `CityBuildingsSectionTests.cs` (a section built on plain `VisualElement`s as `CityMarketSectionTests` does; a market of good 0 material and good 1 produced as in Task 4, plus a third good not produced; `Treasury(100000)`):
  - `HousesText(2, 1008, 1300) == "Logements : 2 (ville : 1\u00A0008 / 1\u00A0300 places)"`.
  - `WorkshopsText("Céréales", 2, 45, 60) == "Céréales : 2 ateliers (45 / 60 ouvriers)"`, `WorkshopsText("Bière", 0, 0, 0) == "Bière : 0 atelier"`, `WorkshopsText("Bois", 1, 30, 30) == "Bois : 1 atelier (30 / 30 ouvriers)"`.
  - `BuildButtonText(12340) == "Construire (12\u00A0340 or)"`.
  - `Show_BuildsAHousesRow_AndOneRowPerGoodTheCityProduces`: 2 rows (houses, good 1), the costs label holds `costsText`.
  - `AButton_ShowsTheQuote_AndIsDisabledWhenImpossible`: house button text is `BuildButtonText(quote.Gold)`; with `Treasury(0)` both buttons are disabled.
  - `BuildHouse_Builds_RaisesHouseBuilt_AndRewritesTheRows`; `BuildWorkshop_RaisesWorkshopBuiltWithTheGood`.
  - `AfterHide_ATreasuryChangeRewritesNothing` (the section stops listening).
  - In `CityMarketSectionTests`, `FormatPopulation_WritesThousandsSeparators` becomes `FormatPopulation(1008, 59) == "Habitants : 1\u00A0008 · Mendiants : 59"`; add asserts for `WorkshopBuiltText("Lübeck", "Céréales")` and `HouseBuiltText("Lübeck")`.

- [ ] **Step 2: Run them to verify they fail.**

- [ ] **Step 3: Implement** `CityBuildingsSection` (rows rebuilt by `Show`, texts and buttons rewritten on the market's, the hold's and the treasury's `Changed` while shown; no treasury → buttons disabled) and the controller: create the section lazily like the market section, from `worldEconomy.Buildings` (`TryCreateCost` for both, `CostsText`); show it with the market and the hold of `HoldOfSelectedShipInPort()`, hide it with the panel; on `HouseBuilt` / `WorkshopBuilt` post the texts to `playerNotifications.Feed` when set; write the population with `FormatPopulation(Housed, Beggars)`. USS for the three classes in `CityInfoPanel.uss`, matching the panel's existing sizes and colours. `WorldMapSetup` fills `playerNotifications` of `CityInfoPanelController`; set it in the open scene as in Task 5 Step 4.

- [ ] **Step 4: Run all EditMode tests** — all pass.

- [ ] **Step 5: Check in Play mode** — enter Play mode with `Application.runInBackground = true`, select Bruges through `WorldMapView.Selection`, capture the Game view (`Unity_Camera_Capture` or a scene capture): the population line and "Vos bâtiments" with the houses row, one row per produced good and the costs line are shown; read the console for errors; leave Play mode.

- [ ] **Step 6: Update `CLAUDE.md`** — the city panel's population line and section, the build rules (hold of the selected ship, then the market; all or nothing; notification).

- [ ] **Step 7: Commit** — "Population : panneau de ville, mendiants, logements et constructions".

---

### Task 7: Debug table

**Files:**
- Modify: `Assets/Scripts/Game/DebugCitiesTable.cs`, `Assets/UI/Debug/*.uss` (the cities table's column widths), `CLAUDE.md`
- Test: `DebugCitiesTableTests.cs`

**Interfaces:**
- Consumes: `CityMarket.BeggarTarget`, `ReserveOf` (Task 3).
- Produces: columns in order Name, "Population", "Mendiants", "Logements", "Croiss. totale", then the goods; `static string FormatBeggars(int beggars, int target)` → "59 (62)"; `static string FormatHousing(int housed, int room)` → "1 008 / 1 100"; `FormatWorkers(long workers, long maxWorkers, long reserve = 0)` → "45/60", "45/60 +10" with a reserve, the dash unchanged.

- [ ] **Step 1: Write the failing tests** — header texts at cells 1 to 4; `FormatBeggars(59, 62) == "59 (62)"`; `FormatHousing(1008, 1100) == "1\u00A0008 / 1\u00A0100"`; `FormatWorkers(45, 60, 10) == "45/60 +10"`, `FormatWorkers(45, 60) == "45/60"`; a row of a labour market (Task 3 fixture) writes its beggars, target, housing and a reserve after a layoff; the total row sums beggars ("(targets)" summed), housed and room.

- [ ] **Step 2: Run them to verify they fail.**

- [ ] **Step 3: Implement**, widths in the debug USS for the two new cell classes `debug-cities__cell--beggars` and `debug-cities__cell--housing`.

- [ ] **Step 4: Run all EditMode tests** — all pass.

- [ ] **Step 5: Update `CLAUDE.md`** — Debug tools: the new columns and the reserve in the workers cell.

- [ ] **Step 6: Commit** — "Population : mendiants, logements et réserves dans l'outil de débogage".

---

### Task 8: Whole check

- [ ] **Step 1: Run every EditMode test** — no failure beyond Task 0's.
- [ ] **Step 2: Play a month in fast forward** (Play mode, `runInBackground`, fast forward through `WorldMapInteraction` or by advancing the `GameClock`): read in the debug table that beggars sit near their target, that cities whose free room is used up stop hiring beggars, and that no error is logged. Report what the cities' totals look like after 30 days (population, beggars, room) to the user.
- [ ] **Step 3: Read `CLAUDE.md`'s Population, Economy, city panel and Debug tools parts once more** against the code, and fix what disagrees; commit if anything changed ("Population : documentation").
