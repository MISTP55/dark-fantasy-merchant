# Workshop Production Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cities produce in workshops as Patrician III does — workers up to a maximum per good, hired and laid off every day by where the stock stands against the price thresholds, no production above T3 — and a new game starts every city with the same workforce, its population derived from it.

**Architecture:** A stateless `WorkforceRule` (Core) gives a workshop's next workers and splits the starting workforce; `MarketGood` carries a workshop's maximum of workers and output per worker in place of a production rate and a ceiling; `CityMarket` holds the workers and runs the day in Patrician III's order (consume, then regulate and produce). In `Game`, `GoodDefinition` holds the two production figures, `EconomyDefinition` the workshop and start-of-game settings, and `CityDefinition` loses its population. The cities debug tab gains a workers column.

**Tech Stack:** Unity 6000.6.4f1, C#, UI Toolkit (USS), Unity Test Framework (NUnit, EditMode).

**Spec:** `docs/superpowers/specs/2026-10-14-workshop-production-design.md`

## Global Constraints

- All code, comments, log messages, asset and folder names are in English. Everything the player reads is in French.
- `Core` contains no `MonoBehaviour` and nothing that needs a scene, and does not reference `Game`.
- Game content and its numbers live in ScriptableObject assets; `Core` hard-codes no step, budget or output.
- Defaults: inefficient output 0.75; workers hired 16 at or under T0, 8 above T0, 4 above T1, 1 above T2; 1 worker above T3; starting workforce 250; inhabitants per worker 72 ÷ 17.
- Good figures: fish 65 workers, 0.0611 per worker; beer 68, 0.3337; grain 68, 0.2669; iron 78, 0.1455; meat 60, 0.0529; vegetables 60, 0.1869.
- Hiring is immediate and unlimited; no social classes, inputs, seasons, famine or hidden supply.
- A stock equal to a threshold counts as under it (Patrician III's comparisons).
- Never hand-write `.meta` files: Unity generates them on import; commit each with its file.
- Never edit the generated `.sln` / `.csproj`. Exclude `Library/` from searches.
- Match the surrounding code: Allman braces, a blank line before `return` and after blocks as in the existing files, XML `<summary>` on public types and members, "The negated comparison also rejects NaN." where a negated comparison validates.
- Commit directly on `main`. Commit messages are in French and end with:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
- Existing tests change only where a task says so; their expected figures are the ones given in this plan.
- Differences from the spec's wording, intentional:
  - `WorkforceRule.ProducesAt(stock, thresholds)` is a static helper so that `CityMarket` and the rule agree on "above T3".
  - The workers column of the debug tab is the fifth cell of a good, after the net, so the other cells keep their indices.
  - `CityMarket.MaxWorkersOf` is added for the debug tab, which has no other way to read a workshop's maximum.
  - Tests keep their figures by giving their economies a workforce of 6 000 and one inhabitant per worker (`TestEconomy.SetStartingPopulation`), and their markets a budget large enough that every workshop starts full (`TestMarkets.Workforce`).

## Review Focus

Conditions the spec implies and that are most likely to bite:

1. A delivery that pushes a producer's stock above T3: the workshop drops to one worker and stops producing, then rehires slowly; no exception, no production that day. Tests in Task 2 (`AboveT3_AProducerStops_AndKeepsOneWorker`, `AStockAboveT3_IsOnlyConsumed`).
2. A stock exactly at T3 after consumption: the workshop still produces. Tests in Task 1 (`AtT3_AWorkshopStillProduces`) and Task 2 (`AtT3Itself_TheWorkshopStillProduces`).
3. A city that produces nothing, or an economy whose starting workforce is 0: no worker, no division by zero, and the population is still the budget's (or 0). Tests in Task 1 (`WithoutWorkshops_NoOneStarts`) and Task 3 (`CreateMarket_ForACityThatProducesNothing_HasNoWorkers_AndTheSamePopulation`, `CreateMarket_WithoutStartingWorkforce_StartsEmpty`).
4. A huge starting workforce typed in the Inspector: the split does not overflow. Test in Task 1 (`AHugeStartingWorkforce_DoesNotOverflow`).
5. Negative workforce settings in the asset file: refused, and named, in the Inspector warning and in `WorldEconomy`'s error. Test in Task 2 (`TryValidateSettings_SaysWhenTheWorkforceIsWrong`).

## Commands

The Unity Editor is normally **open** on this project. Then use the `unity-mcp` tools: `Unity_RunCommand` to refresh the asset database and to run the EditMode tests, `Unity_GetConsoleLogs` to read compile errors.

Refresh and compile (run after every file change, before the tests):

```csharp
UnityEditor.AssetDatabase.Refresh();
```

Run EditMode tests through `Unity_RunCommand` (a filter is optional); check `EditorApplication.isCompiling` and `EditorUtility.scriptCompilationFailed` first:

```csharp
var api = UnityEngine.ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
api.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(
    new UnityEditor.TestTools.TestRunner.Api.Filter
    {
        testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,
        testNames = null, // or groupNames = new[] { "DarkFantasyMerchant\\.Tests\\.EditMode\\.WorkforceRuleTests" }
    }));
```

The run writes `C:\Users\Spellman\AppData\LocalLow\DefaultCompany\Dark Fantasy Merchant\TestResults.xml`: read its `<test-run>` line (`result`, `passed`, `failed`) and the `<test-case ... result="Failed">` entries. `Unity_RunCommand` refuses code that writes files or needs interaction; log to the console instead.

When the Editor is **closed**, use batch mode. `Unity.exe` is a GUI executable, so start it with `-Wait`. Do **not** add `-nographics`.

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe"
Remove-Item Logs\TestResults.xml -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList '-batchmode','-projectPath','.','-runTests','-testPlatform','EditMode','-testResults','Logs\TestResults.xml','-logFile','Logs\test.log'
"exit code: $($p.ExitCode)"
Select-String -Path Logs\TestResults.xml -Pattern '<test-run ' | ForEach-Object { $_.Line }
```

## File Structure

| File | Responsibility |
|---|---|
| `Assets/Scripts/Core/WorkforceRule.cs` (new) | Next workers of a workshop; split of the starting workforce. |
| `Assets/Scripts/Core/MarketGood.cs` (modify) | A workshop's maximum of workers and output per worker. |
| `Assets/Scripts/Core/CityMarket.cs` (modify) | Workers per good; the day in Patrician III's order; thresholds from capacity. |
| `Assets/Scripts/Game/GoodDefinition.cs` (modify) | Maximum of workers and output per worker of a good. |
| `Assets/Scripts/Game/EconomyDefinition.cs` (modify) | Workshop and start-of-game settings; validation. |
| `Assets/Scripts/Game/CityDefinition.cs` (modify) | Loses its population. |
| `Assets/Scripts/Game/WorldEconomy.cs` (modify) | Validation name; warning for a city that produces nothing. |
| `Assets/Scripts/Game/CityInfoPanelController.cs` (modify) | "Population : —" without a market. |
| `Assets/Scripts/Game/DebugCitiesTable.cs`, `Assets/UI/Debug/DebugPanel.uss` (modify) | The workers column. |
| `Assets/Scripts/Editor/WorldMapSetup.cs` (modify) | Sample goods' production figures; sample cities without population. |
| `Assets/Tests/EditMode/WorkforceRuleTests.cs` (new) and the existing test files (modify) | Tests. |
| `Assets/Data/Goods/*.asset`, `Assets/Data/Cities/*.asset`, `Assets/Data/Economy/Economy.asset` (modify) | New figures; no city population. |
| `CLAUDE.md` (modify) | The new model. |

---

### Task 1: Workforce rule

**Files:**
- Create: `Assets/Scripts/Core/WorkforceRule.cs`
- Test: `Assets/Tests/EditMode/WorkforceRuleTests.cs`

**Interfaces:**
- Consumes: `StockThresholds` (existing: `T0`..`T3`).
- Produces: `public sealed class WorkforceRule` with `WorkforceRule(int hiredUpToT0, int hiredUpToT1, int hiredUpToT2, int hiredUpToT3, int workersAboveT3, int startingWorkforce)` (throws `ArgumentOutOfRangeException` for a negative value), the six values as properties of the same names in PascalCase, `int NextWorkers(int workers, int maxWorkers, double stock, StockThresholds thresholds)`, `int StartingWorkersOf(int maxWorkers, int workshops)`, and `static bool ProducesAt(double stock, StockThresholds thresholds)`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/WorkforceRuleTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class WorkforceRuleTests
    {
        // Patrician III's: 16, 8, 4 and 1 workers a day, one above T3; 250 to start with.
        private static WorkforceRule Rule => new WorkforceRule(16, 8, 4, 1, 1, 250);

        private static readonly StockThresholds Thresholds = new StockThresholds(20, 60, 90, 110);

        [TestCase(0, 26)]
        [TestCase(20, 26)]
        [TestCase(20.5, 18)]
        [TestCase(60, 18)]
        [TestCase(61, 14)]
        [TestCase(90, 14)]
        [TestCase(91, 11)]
        [TestCase(110, 11)]
        public void TheWorkersHiredInADay_FollowTheStock(double stock, int expected)
        {
            // Ten workers to start with; a stock equal to a threshold counts as under it.
            Assert.AreEqual(expected, Rule.NextWorkers(10, 60, stock, Thresholds));
        }

        [Test]
        public void Hiring_StopsAtTheMaximum()
        {
            Assert.AreEqual(60, Rule.NextWorkers(55, 60, 0, Thresholds));
            Assert.AreEqual(60, Rule.NextWorkers(60, 60, 100, Thresholds));
        }

        [TestCase(110.01)]
        [TestCase(500)]
        public void AboveT3_AWorkshopKeepsOneWorker_AndDoesNotProduce(double stock)
        {
            Assert.AreEqual(1, Rule.NextWorkers(60, 60, stock, Thresholds));
            Assert.IsFalse(WorkforceRule.ProducesAt(stock, Thresholds));
        }

        [Test]
        public void AtT3_AWorkshopStillProduces()
        {
            Assert.IsTrue(WorkforceRule.ProducesAt(110, Thresholds));
            Assert.IsTrue(WorkforceRule.ProducesAt(0, Thresholds));
        }

        [Test]
        public void AboveT3_TheWorkersKept_AreNeverAboveTheMaximum()
        {
            var rule = new WorkforceRule(16, 8, 4, 1, 5, 250);

            Assert.AreEqual(3, rule.NextWorkers(3, 3, 500, Thresholds));
        }

        [Test]
        public void AWorkshopWithoutRoomForWorkers_StaysEmpty()
        {
            Assert.AreEqual(0, Rule.NextWorkers(0, 0, 0, Thresholds));
            Assert.AreEqual(0, Rule.NextWorkers(0, 0, 500, Thresholds));
        }

        [TestCase(1, 300, 250)]
        [TestCase(2, 200, 125)]
        [TestCase(3, 60, 60)]
        [TestCase(4, 78, 63)]
        [TestCase(5, 65, 50)]
        public void TheStartingWorkforce_IsSpreadEvenly_RoundedUp_AndCutToTheMaximum(int workshops, int maxWorkers, int expected)
        {
            // 250 over 3 is 84, cut to 60; 250 over 4 is 62.5, rounded up.
            Assert.AreEqual(expected, Rule.StartingWorkersOf(maxWorkers, workshops));
        }

        [Test]
        public void WithoutWorkshops_NoOneStarts()
        {
            Assert.AreEqual(0, Rule.StartingWorkersOf(60, 0));
            Assert.AreEqual(0, Rule.StartingWorkersOf(0, 3));
        }

        [Test]
        public void AHugeStartingWorkforce_DoesNotOverflow()
        {
            var rule = new WorkforceRule(16, 8, 4, 1, 1, int.MaxValue);

            Assert.AreEqual(60, rule.StartingWorkersOf(60, 2));
            Assert.AreEqual(60, new WorkforceRule(int.MaxValue, 8, 4, 1, 1, 250).NextWorkers(50, 60, 0, Thresholds));
        }

        [Test]
        public void ARule_KeepsItsSettings()
        {
            WorkforceRule rule = Rule;

            Assert.AreEqual(16, rule.HiredUpToT0);
            Assert.AreEqual(8, rule.HiredUpToT1);
            Assert.AreEqual(4, rule.HiredUpToT2);
            Assert.AreEqual(1, rule.HiredUpToT3);
            Assert.AreEqual(1, rule.WorkersAboveT3);
            Assert.AreEqual(250, rule.StartingWorkforce);
        }

        [TestCase(-1, 8, 4, 1, 1, 250, "hiredUpToT0")]
        [TestCase(16, -1, 4, 1, 1, 250, "hiredUpToT1")]
        [TestCase(16, 8, -1, 1, 1, 250, "hiredUpToT2")]
        [TestCase(16, 8, 4, -1, 1, 250, "hiredUpToT3")]
        [TestCase(16, 8, 4, 1, -1, 250, "workersAboveT3")]
        [TestCase(16, 8, 4, 1, 1, -1, "startingWorkforce")]
        public void RejectsANegativeSetting_AndNamesIt(
            int hiredUpToT0, int hiredUpToT1, int hiredUpToT2, int hiredUpToT3, int workersAboveT3, int startingWorkforce, string setting)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new WorkforceRule(hiredUpToT0, hiredUpToT1, hiredUpToT2, hiredUpToT3, workersAboveT3, startingWorkforce));

            Assert.AreEqual(setting, exception.ParamName);
        }

        [Test]
        public void RejectsNegativeWorkers()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Rule.NextWorkers(-1, 60, 0, Thresholds));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rule.NextWorkers(10, -1, 0, Thresholds));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rule.StartingWorkersOf(-1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rule.StartingWorkersOf(60, -1));
        }
    }
}
```

- [ ] **Step 2: Refresh and run `WorkforceRuleTests`.** Expected: compile error, `WorkforceRule` does not exist.

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/Core/WorkforceRule.cs`:

```csharp
using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// How the workshops of a city hire and lay off, as in Patrician III: every day a
    /// workshop hires more workers the lower the stock of its good stands against the
    /// thresholds, up to its maximum, and keeps a few workers above T3, where it produces
    /// nothing. A stock equal to a threshold counts as under it. It also spreads the
    /// workers every city starts with over its workshops.
    /// </summary>
    public sealed class WorkforceRule
    {
        /// <param name="hiredUpToT0">Workers a workshop hires in a day while the stock is at T0 or under.</param>
        /// <param name="hiredUpToT1">Workers hired in a day while the stock is above T0, up to T1.</param>
        /// <param name="hiredUpToT2">Workers hired in a day while the stock is above T1, up to T2.</param>
        /// <param name="hiredUpToT3">Workers hired in a day while the stock is above T2, up to T3.</param>
        /// <param name="workersAboveT3">Workers a workshop keeps above T3, where it produces nothing.</param>
        /// <param name="startingWorkforce">Workers every city starts with, spread evenly over its workshops.</param>
        public WorkforceRule(
            int hiredUpToT0,
            int hiredUpToT1,
            int hiredUpToT2,
            int hiredUpToT3,
            int workersAboveT3,
            int startingWorkforce)
        {
            RequireNotNegative(hiredUpToT0, nameof(hiredUpToT0));
            RequireNotNegative(hiredUpToT1, nameof(hiredUpToT1));
            RequireNotNegative(hiredUpToT2, nameof(hiredUpToT2));
            RequireNotNegative(hiredUpToT3, nameof(hiredUpToT3));
            RequireNotNegative(workersAboveT3, nameof(workersAboveT3));
            RequireNotNegative(startingWorkforce, nameof(startingWorkforce));

            HiredUpToT0 = hiredUpToT0;
            HiredUpToT1 = hiredUpToT1;
            HiredUpToT2 = hiredUpToT2;
            HiredUpToT3 = hiredUpToT3;
            WorkersAboveT3 = workersAboveT3;
            StartingWorkforce = startingWorkforce;
        }

        public int HiredUpToT0 { get; }

        public int HiredUpToT1 { get; }

        public int HiredUpToT2 { get; }

        public int HiredUpToT3 { get; }

        public int WorkersAboveT3 { get; }

        public int StartingWorkforce { get; }

        /// <summary>Whether a workshop produces at this stock: up to T3, not above.</summary>
        public static bool ProducesAt(double stock, StockThresholds thresholds)
        {
            return !(stock > thresholds.T3);
        }

        /// <summary>The workers of a workshop for the day, from the stock its good has left.</summary>
        public int NextWorkers(int workers, int maxWorkers, double stock, StockThresholds thresholds)
        {
            RequireNotNegative(workers, nameof(workers));
            RequireNotNegative(maxWorkers, nameof(maxWorkers));

            if (!ProducesAt(stock, thresholds))
            {
                return Math.Min(WorkersAboveT3, maxWorkers);
            }

            int hired = stock > thresholds.T2 ? HiredUpToT3
                : stock > thresholds.T1 ? HiredUpToT2
                : stock > thresholds.T0 ? HiredUpToT1
                : HiredUpToT0;

            return (int)Math.Min(maxWorkers, (long)workers + hired);
        }

        /// <summary>
        /// The workers a workshop starts with: the starting workforce over the city's
        /// workshops, rounded up as in Patrician III, and never above the workshop's maximum.
        /// </summary>
        public int StartingWorkersOf(int maxWorkers, int workshops)
        {
            RequireNotNegative(maxWorkers, nameof(maxWorkers));
            RequireNotNegative(workshops, nameof(workshops));

            if (workshops == 0)
            {
                return 0;
            }

            long share = ((long)StartingWorkforce + workshops - 1) / workshops;

            return (int)Math.Min(share, maxWorkers);
        }

        private static void RequireNotNegative(int value, string name)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(name, "Workers are not negative.");
            }
        }
    }
}
```

- [ ] **Step 4: Refresh and run `WorkforceRuleTests`.** Expected: all pass. Then all EditMode tests: all pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/WorkforceRule.cs Assets/Scripts/Core/WorkforceRule.cs.meta Assets/Tests/EditMode/WorkforceRuleTests.cs Assets/Tests/EditMode/WorkforceRuleTests.cs.meta
git commit -m "Production : règle d'embauche des exploitations

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Workshops in the markets

`MarketGood`'s constructor changes, which `EconomyDefinition` and the tests call: everything in this task compiles together.

**Files:**
- Modify: `Assets/Scripts/Core/MarketGood.cs`, `Assets/Scripts/Core/CityMarket.cs`
- Modify: `Assets/Scripts/Game/GoodDefinition.cs`, `Assets/Scripts/Game/EconomyDefinition.cs`, `Assets/Scripts/Game/WorldEconomy.cs`
- Modify: `Assets/Scripts/Editor/WorldMapSetup.cs`
- Modify: `Assets/Tests/EditMode/TestMarkets.cs`, `TestEconomy.cs`, `CityMarketTests.cs`, `EconomyDefinitionTests.cs`, `EconomyContentTests.cs`, `DebugCitiesTableTests.cs`, `TradeTests.cs`, `TradeRoutePlannerTests.cs`, `AiMerchantFleetTests.cs`, `CityMarketSectionTests.cs`

**Interfaces:**
- Consumes: `WorkforceRule` (Task 1).
- Produces:
  - `MarketGood(long basePrice, double dailyConsumptionPerThousand, int maxWorkers, double dailyOutputPerWorker, bool isFood = false)`; properties `MaxWorkers`, `DailyOutputPerWorker`, `DailyCapacity`, `IsProduced`; `ProductionRate` and `CeilingDays` are gone.
  - `CityMarket(IReadOnlyList<MarketGood> goods, ThresholdRule thresholdRule, WorkforceRule workforce, double startingDays, MarketPricing pricing, int daysPerWeek, CityPopulation population, FoodGrowth foodGrowth = null)`; `int WorkersOf(int good)`, `int MaxWorkersOf(int good)`.
  - `GoodDefinition.MaxWorkers` (int), `GoodDefinition.DailyOutputPerWorker` (double).
  - `EconomyDefinition`: `InefficientEfficiency`, `HiredUpToT0`..`HiredUpToT3`, `WorkersAboveT3`, `StartingWorkforce`, `CreateWorkforceRule()`, `TryValidateSettings(out string problem)` (replaces `TryValidatePrices`); `EfficientProductionRate`, `InefficientProductionRate`, `EfficientCeilingDays`, `InefficientCeilingDays` are gone.
  - Tests: `TestMarkets.Workforce`; `TestEconomy.CreateGood(string displayName, long basePrice, double weeklyConsumptionPerThousand, GoodCategory category = GoodCategory.Food, int maxWorkers = 60, double dailyOutputPerWorker = 0.1)`.

- [ ] **Step 1: Test helpers**

In `Assets/Tests/EditMode/TestMarkets.cs`, after `Thresholds`, add:

```csharp
        /// <summary>
        /// Patrician III's hiring, and a starting workforce large enough that every workshop
        /// of a test market starts with its maximum of workers.
        /// </summary>
        public static WorkforceRule Workforce => new WorkforceRule(16, 8, 4, 1, 1, 1000);
```

In `Assets/Tests/EditMode/TestEconomy.cs`, `CreateGood` becomes:

```csharp
        /// <summary>A good priced from its base price by the economy's curves.</summary>
        public static GoodDefinition CreateGood(
            string displayName,
            long basePrice,
            double weeklyConsumptionPerThousand,
            GoodCategory category = GoodCategory.Food,
            int maxWorkers = 60,
            double dailyOutputPerWorker = 0.1)
        {
            var good = ScriptableObject.CreateInstance<GoodDefinition>();
            good.name = displayName;

            var serialized = new SerializedObject(good);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("category").enumValueIndex = (int)category;
            serialized.FindProperty("basePrice").longValue = basePrice;
            serialized.FindProperty("weeklyConsumptionPerThousand").doubleValue = weeklyConsumptionPerThousand;
            serialized.FindProperty("maxWorkers").intValue = maxWorkers;
            serialized.FindProperty("dailyOutputPerWorker").doubleValue = dailyOutputPerWorker;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return good;
        }
```

- [ ] **Step 2: Adapt `CityMarketTests` and write its new tests**

In `Assets/Tests/EditMode/CityMarketTests.cs`:

1. Replace the two produced goods and add the rule:

```csharp
        private static ThresholdRule Thresholds => TestMarkets.Thresholds;

        private static WorkforceRule Workforce => TestMarkets.Workforce;

        private static MarketPricing Pricing => TestMarkets.Pricing;

        // Two barrels a day, base price 10.
        private static MarketGood NotProduced => new MarketGood(10, 2, 0, 0);

        // Workshops of 30 and 22 workers making a tenth of a barrel each: 3 and 2.2 a day.
        private static MarketGood Efficient => new MarketGood(10, 2, 30, 0.1);
        private static MarketGood Inefficient => new MarketGood(10, 2, 22, 0.1);
```

   (the `Thresholds` and `Pricing` lines already exist; keep one of each.)
2. In every `new CityMarket(` of the file, insert `Workforce, ` right after the threshold rule argument (`Thresholds` or, in `RejectsMissingArguments`, `null`).
3. The thresholds of `Efficient` are T0 20, T1 60, T2 90 (60 + 10 days of 3), T3 110. Replace these tests:

   - `StartDay_KeepsAnEfficientGoodAtItsCeiling` by:

```csharp
        [Test]
        public void AboveT3_AProducerStops_AndKeepsOneWorker()
        {
            CityMarket market = MarketOf(Efficient);
            market.Put(0, 60);

            market.StartDay();

            // 120, then 118 once consumed: above T3, nothing produced.
            Assert.AreEqual(118, market.StockOf(0), Tolerance);
            Assert.AreEqual(1, market.WorkersOf(0));
            Assert.AreEqual(0, market.RecentProductionOf(0), Tolerance);
        }
```

   - `StartDay_LeavesAloneAStockAboveItsCeiling` by:

```csharp
        [Test]
        public void AStockAboveT3_IsOnlyConsumed()
        {
            CityMarket market = MarketOf(Efficient);
            market.Put(0, 160);

            market.StartDay();

            Assert.AreEqual(218, market.StockOf(0), Tolerance);
        }
```

   - `AnInefficientGood_SettlesJustUnderItsCeiling` by:

```csharp
        [Test]
        public void AProducerLeftAlone_StaysBetweenT2AndT3()
        {
            CityMarket market = MarketOf(Efficient);

            for (int day = 0; day < 200; day++)
            {
                market.StartDay();
            }

            // It drops to one worker above T3 (110) and hires again under it; T2 is 90.
            for (int day = 0; day < 200; day++)
            {
                market.StartDay();
                Assert.GreaterOrEqual(market.StockOf(0), 90d, $"day {day}");
                Assert.LessOrEqual(market.StockOf(0), 113d, $"day {day}");
            }
        }
```

   - `RecentProduction_StopsAtTheCeiling` by:

```csharp
        [Test]
        public void RecentProduction_IsNothingWhileAboveT3()
        {
            // 119 in stock: 117, 115 and 113 after the days' consumption, all above T3.
            CityMarket market = MarketOf(Efficient);
            market.Put(0, 59);

            for (int day = 0; day < 3; day++)
            {
                market.StartDay();
            }

            Assert.AreEqual(0, market.RecentProductionOf(0), Tolerance);
            Assert.AreEqual(6, market.RecentConsumptionOf(0), Tolerance);
        }
```

   - `RecentProduction_IsNothingAboveTheCeiling`: rename to `RecentProduction_IsNothingAboveT3` (body unchanged).
   - `RecentProduction_IsNothingAtTheCeilingItself` by:

```csharp
        [Test]
        public void AtT3Itself_TheWorkshopStillProduces()
        {
            // 112 in stock, 110 once consumed: T3 itself.
            CityMarket market = MarketOf(Efficient);
            market.Put(0, 52);

            market.StartDay();

            Assert.AreEqual(113, market.StockOf(0), Tolerance);
            Assert.AreEqual(3, market.RecentProductionOf(0), Tolerance);
        }
```

4. In `WhatACityConsumes_FollowsItsInhabitants`, the workshop does not follow the inhabitants: replace its comment and the asserts after the stock by:

```csharp
            // Two barrels a day per thousand: five for 2 500. The workshop makes 3 whatever the size.
            Assert.AreEqual(5, market.DailyConsumptionOf(0), Tolerance);
            Assert.AreEqual(150, market.StockOf(0), Tolerance);
            Assert.AreEqual(150, market.ThresholdsOf(0).T1, Tolerance);
            Assert.AreEqual(35, market.NominalWeeklyConsumptionOf(0), Tolerance);
            Assert.AreEqual(21, market.NominalWeeklyProductionOf(0), Tolerance);

            market.StartDay();

            // 150 - 5 consumed + 3 produced.
            Assert.AreEqual(148, market.StockOf(0), Tolerance);
```

5. In `MarketGood_RejectsFiguresThatMakeNoSense`, replace the last two lines by:

```csharp
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, 2, -1, 0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, 2, 30, -0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, 2, 30, double.NaN));
```

6. In `RejectsMissingArguments`, add:

```csharp
            Assert.Throws<ArgumentNullException>(() => new CityMarket(new[] { NotProduced }, Thresholds, null, StartingDays, Pricing, DaysPerWeek, new CityPopulation(1000)));
```

7. Add these tests:

```csharp
        [Test]
        public void StartDay_ConsumesBeforeItProduces()
        {
            // 111 in stock: above T3 before the day's consumption, 109 after it, so it produces.
            CityMarket market = MarketOf(Efficient);
            market.Put(0, 51);

            market.StartDay();

            Assert.AreEqual(112, market.StockOf(0), Tolerance);
            Assert.AreEqual(30, market.WorkersOf(0));
        }

        [Test]
        public void AWorkshop_HiresByTheStock_AndProducesWithTheWorkersOfTheDay()
        {
            // Two workers to start with; 58 once consumed is above T0 (20), so 8 more are hired.
            var market = new CityMarket(
                new[] { Efficient }, Thresholds, new WorkforceRule(16, 8, 4, 1, 1, 2), StartingDays, Pricing, DaysPerWeek,
                new CityPopulation(1000));

            Assert.AreEqual(2, market.WorkersOf(0));

            market.StartDay();

            Assert.AreEqual(10, market.WorkersOf(0));
            Assert.AreEqual(59, market.StockOf(0), Tolerance);
        }

        [Test]
        public void TheStartingWorkforce_IsSpreadOverTheWorkshops()
        {
            // 50 workers over two workshops: 25 each, cut to 22 in the smaller one.
            var market = new CityMarket(
                new[] { Efficient, Inefficient, NotProduced }, Thresholds, new WorkforceRule(16, 8, 4, 1, 1, 50), StartingDays,
                Pricing, DaysPerWeek, new CityPopulation(1000));

            Assert.AreEqual(25, market.WorkersOf(0));
            Assert.AreEqual(22, market.WorkersOf(1));
            Assert.AreEqual(0, market.WorkersOf(2));
            Assert.AreEqual(30, market.MaxWorkersOf(0));
            Assert.AreEqual(22, market.MaxWorkersOf(1));
            Assert.AreEqual(0, market.MaxWorkersOf(2));
        }

        [Test]
        public void AGoodNotProduced_HasNoWorkers_AndKeepsThem()
        {
            CityMarket market = MarketOf(NotProduced);

            market.StartDay();

            Assert.AreEqual(0, market.WorkersOf(0));
            Assert.AreEqual(0, market.NominalWeeklyProductionOf(0), Tolerance);
        }
```

   The tests `StartDay_RefillsAProducedGoodByItsSurplus` (71), `ThresholdsOf_FollowTheProduction` (T2 90 and 82, T3 110 and 102), `NominalWeeklyFigures_*`, `RecentFigures_*` and `Changed_*` keep their figures: a full workshop of 30 makes 3 a day, of 22 makes 2.2.

- [ ] **Step 3: Adapt the other test files**

- Every `new CityMarket(` in `TradeTests.cs`, `TradeRoutePlannerTests.cs`, `AiMerchantFleetTests.cs`, `CityMarketSectionTests.cs` and `DebugCitiesTableTests.cs`: insert `TestMarkets.Workforce,` right after `TestMarkets.Thresholds,`.
- `DebugCitiesTableTests.cs:43`: `new MarketGood(50, 2, 1.5, 60)` becomes `new MarketGood(50, 2, 30, 0.1)`, and its comment `// Iron: 2 barrels a day and 3 produced, 60 in stock under a ceiling of 120.` becomes `// Iron: 2 barrels a day, and a full workshop of 30 workers making 3; 60 in stock.` Its figures stay: day one, 60 - 2 + 3 = 61.
- `EconomyDefinitionTests.cs`:
  - In `SetUp`, the goods get workshops: wheat makes 24 a day in a city that produces it efficiently, fish 13.5 inefficiently:

```csharp
            // Per week for 1 000 inhabitants: 2, 1.5 and 0.5 barrels a day. Workshops of 60 workers
            // making 0.4, 0.3 and 0.1 barrels each.
            wheat = Track(TestEconomy.CreateGood("Blé", 10, 14, dailyOutputPerWorker: 0.4));
            fish = Track(TestEconomy.CreateGood("Poisson", 12, 10.5, dailyOutputPerWorker: 0.3));
            salt = Track(TestEconomy.CreateGood("Sel", 18, 3.5));
```

  - In `ANewDefinition_HasTheGamesSettings`, replace the four production asserts (`EfficientProductionRate` … `InefficientCeilingDays`) by:

```csharp
            Assert.AreEqual(0.75, fresh.InefficientEfficiency);
            Assert.AreEqual(16, fresh.HiredUpToT0);
            Assert.AreEqual(8, fresh.HiredUpToT1);
            Assert.AreEqual(4, fresh.HiredUpToT2);
            Assert.AreEqual(1, fresh.HiredUpToT3);
            Assert.AreEqual(1, fresh.WorkersAboveT3);
            Assert.AreEqual(250, fresh.StartingWorkforce);
```

  - In `ANewGood_AndANewCity_HaveUsableDefaults`, after the `BasePrice` assert, add:

```csharp
            Assert.Greater(good.MaxWorkers, 0);
            Assert.Greater(good.DailyOutputPerWorker, 0d);
```

  - `CreateMarket_ProducesAtTheDefinitionsRates` keeps its figures (wheat 84 - 12 + 24 = 96, fish 63 - 9 + 13.5 = 67.5): the 250 workers over two workshops are cut to 60 each. Rename it `CreateMarket_ProducesAtTheDefinitionsOutputs`.
  - `CreateMarket_ThresholdsFollowTheProductionOfTheCity` keeps its figures (capacities 24 and 13.5).
  - Rename every `TryValidatePrices` to `TryValidateSettings`.
  - Add:

```csharp
        [Test]
        public void CreateMarket_SpreadsTheStartingWorkforce_OverTheCitysWorkshops()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("startingWorkforce").intValue = 50;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            CityDefinition city = City(6000, new[] { wheat }, new[] { fish });

            CityMarket market = economy.CreateMarket(city, economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(25, market.WorkersOf(0));
            Assert.AreEqual(25, market.WorkersOf(1));
            Assert.AreEqual(0, market.WorkersOf(2));
            Assert.AreEqual(60, market.MaxWorkersOf(1));
        }

        [Test]
        public void CreateWorkforceRule_UsesTheDefinitionsSettings()
        {
            WorkforceRule rule = economy.CreateWorkforceRule();

            Assert.AreEqual(16, rule.HiredUpToT0);
            Assert.AreEqual(1, rule.WorkersAboveT3);
            Assert.AreEqual(250, rule.StartingWorkforce);
        }

        [Test]
        public void TryValidateSettings_SaysWhenTheWorkforceIsWrong()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("workersAboveT3").intValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(economy.TryValidateSettings(out string problem));
            StringAssert.Contains("the workforce", problem);
            StringAssert.Contains("workersAboveT3", problem);
        }
```

- `EconomyContentTests.cs`:
  - In `TheCatalogue_HasItsGoods_InOrder_WithPricesThatMakeSense`, after the `BasePrice` assert, add `Assert.Greater(good.MaxWorkers, 0);` and `Assert.Greater(good.DailyOutputPerWorker, 0d);`.
  - In `TheEconomy_IsUsable`, add `Assert.DoesNotThrow(() => economy.CreateWorkforceRule());` and `Assert.IsTrue(economy.TryValidateSettings(out string settingsProblem), settingsProblem);`.

- [ ] **Step 4: Refresh.** Expected: compile errors, `WorkforceRule` is not a parameter of `CityMarket`, `MarketGood` has no `maxWorkers`, `GoodDefinition` has no `MaxWorkers`, `EconomyDefinition` has no `TryValidateSettings`. Go on: the code follows.

- [ ] **Step 5: `MarketGood`**

Replace the summary, the constructor and the production properties of `Assets/Scripts/Core/MarketGood.cs` so that it reads:

```csharp
using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The figures of one good in one city: its base price, what the city consumes of it
    /// for the inhabitants it has, and the workshop that produces it there, if any.
    /// </summary>
    public readonly struct MarketGood
    {
        /// <param name="basePrice">Gold per barrel that the factors of the price curves multiply, at least 1.</param>
        /// <param name="dailyConsumptionPerThousand">Barrels 1 000 inhabitants consume in a day; it can be a fraction.</param>
        /// <param name="maxWorkers">Most workers of the city's workshop for the good; 0 for a good the city does not produce.</param>
        /// <param name="dailyOutputPerWorker">Barrels a worker of that workshop makes in a day, its efficiency included.</param>
        /// <param name="isFood">Whether the stock of the good feeds the city: only food makes its population change.</param>
        public MarketGood(
            long basePrice,
            double dailyConsumptionPerThousand,
            int maxWorkers,
            double dailyOutputPerWorker,
            bool isFood = false)
        {
            if (basePrice < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(basePrice));
            }

            if (maxWorkers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWorkers));
            }

            RequireNotNegative(dailyConsumptionPerThousand, nameof(dailyConsumptionPerThousand));
            RequireNotNegative(dailyOutputPerWorker, nameof(dailyOutputPerWorker));

            BasePrice = basePrice;
            DailyConsumptionPerThousand = dailyConsumptionPerThousand;
            MaxWorkers = maxWorkers;
            DailyOutputPerWorker = dailyOutputPerWorker;
            IsFood = isFood;
        }

        public long BasePrice { get; }

        public double DailyConsumptionPerThousand { get; }

        public int MaxWorkers { get; }

        public double DailyOutputPerWorker { get; }

        public bool IsFood { get; }

        public bool IsProduced => MaxWorkers > 0 && DailyOutputPerWorker > 0d;

        /// <summary>Barrels the workshop makes in a day with all its workers: it does not depend on the city's size.</summary>
        public double DailyCapacity => MaxWorkers * DailyOutputPerWorker;

        private static void RequireNotNegative(double value, string name)
        {
            // The negated comparison also rejects NaN.
            if (!(value >= 0d) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
```

- [ ] **Step 6: `CityMarket`**

In `Assets/Scripts/Core/CityMarket.cs`:

1. Class summary:

```csharp
    /// <summary>
    /// The stocks of goods of one city. Every day the city consumes its goods, then its
    /// workshops hire or lay off by where each stock stands against its thresholds, and
    /// produce; the price of a good follows its stock against the same thresholds. There is
    /// no limit to what a city can store. Goods are indices into the catalogue. It also
    /// remembers what the days of the last week actually produced and consumed. What it
    /// consumes follows its population, which the food it holds makes grow or decline every day.
    /// </summary>
```

2. Fields: add `private readonly WorkforceRule workforce;` after `thresholdRule`, and `private readonly int[] workers;` after `stocks`.
3. Constructor: after the `thresholdRule` parameter and its doc, add

```csharp
        /// <param name="workforce">How the workshops hire, lay off and start.</param>
```

   and the parameter `WorkforceRule workforce,` right after `ThresholdRule thresholdRule,`. After the `thresholdRule` assignment, add

```csharp
            this.workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));
```

   After `stocks = new double[goods.Count];`, add `workers = new int[goods.Count];`. After the loop that fills the stocks, add:

```csharp
            int workshops = Array.FindAll(this.goods, good => good.IsProduced).Length;

            for (int i = 0; i < this.goods.Length; i++)
            {
                workers[i] = this.goods[i].IsProduced
                    ? workforce.StartingWorkersOf(this.goods[i].MaxWorkers, workshops)
                    : 0;
            }
```

4. `ThresholdsOf` passes the capacity:

```csharp
        /// <summary>The stocks at which the prices of a good are given, for the population of the moment.</summary>
        public StockThresholds ThresholdsOf(int good)
        {
            RequireGood(good);

            return thresholdRule.ThresholdsFor(DailyConsumptionOf(good), goods[good].DailyCapacity);
        }

        /// <summary>Workers of the city's workshop for a good: 0 for a good it does not produce.</summary>
        public int WorkersOf(int good)
        {
            RequireGood(good);

            return workers[good];
        }

        /// <summary>The most workers the city's workshop for a good can have: 0 for a good it does not produce.</summary>
        public int MaxWorkersOf(int good)
        {
            RequireGood(good);

            return goods[good].IsProduced ? goods[good].MaxWorkers : 0;
        }
```

5. `NominalWeeklyProductionOf`:

```csharp
        /// <summary>Barrels the workshop makes in a week with the workers of the moment, while the stock is not above T3.</summary>
        public double NominalWeeklyProductionOf(int good)
        {
            return workers[good] * goods[good].DailyOutputPerWorker * daysPerWeek;
        }
```

6. In the summary of `RecentProductionOf`, replace `less than the nominal figure when the ceiling stopped the production.` by `less than the nominal figure on the days the stock was above T3.`
7. `StartDay` and its summary:

```csharp
        /// <summary>
        /// One day of the city, as in Patrician III: the inhabitants consume every good, down
        /// to nothing; then every workshop hires or lays off by where the stock left stands
        /// against the thresholds, and produces, unless that stock is above T3. The food that
        /// is left then changes the population.
        /// </summary>
        public void StartDay()
        {
            weekLog.StartDay();

            for (int i = 0; i < goods.Length; i++)
            {
                MarketGood good = goods[i];
                double consumed = Math.Min(stocks[i], DailyConsumptionOf(i));
                double produced = 0d;
                stocks[i] -= consumed;

                if (good.IsProduced)
                {
                    StockThresholds thresholds = ThresholdsOf(i);
                    workers[i] = workforce.NextWorkers(workers[i], good.MaxWorkers, stocks[i], thresholds);

                    if (WorkforceRule.ProducesAt(stocks[i], thresholds))
                    {
                        produced = workers[i] * good.DailyOutputPerWorker;
                        stocks[i] += produced;
                    }
                }

                weekLog.Record(i, produced, consumed);
            }
```

   (the part from `if (foodGrowth != null)` on stays.)

- [ ] **Step 7: `GoodDefinition`**

In `Assets/Scripts/Game/GoodDefinition.cs`, after the `weeklyConsumptionPerThousand` field add:

```csharp
        [Tooltip("Most workers a city's workshop for the good can have. The same in every city.")]
        [SerializeField, Min(0)] private int maxWorkers = 60;

        [Tooltip("Barrels a worker makes in a day in a city that produces the good efficiently.")]
        [SerializeField, Min(0f)] private double dailyOutputPerWorker = 0.1d;
```

and after the `WeeklyConsumptionPerThousand` property:

```csharp
        public int MaxWorkers => maxWorkers;

        public double DailyOutputPerWorker => dailyOutputPerWorker;
```

- [ ] **Step 8: `EconomyDefinition`**

In `Assets/Scripts/Game/EconomyDefinition.cs`:

1. Replace the `Production` header and its four fields (`efficientProductionRate` to `inefficientCeilingDays`) by:

```csharp
        [Header("Workshops (a good's maximum of workers and output per worker are the good's own)")]
        [Tooltip("Share of a good's output per worker in a city that produces it inefficiently: 0.75 is Patrician III's 768 / 1024.")]
        [SerializeField, Range(0f, 1f)] private double inefficientEfficiency = 0.75d;

        [Tooltip("Workers a workshop hires in a day while the stock of its good is at T0 or under.")]
        [SerializeField, Min(0)] private int hiredUpToT0 = 16;

        [Tooltip("Workers hired in a day while the stock is above T0, up to T1.")]
        [SerializeField, Min(0)] private int hiredUpToT1 = 8;

        [Tooltip("Workers hired in a day while the stock is above T1, up to T2.")]
        [SerializeField, Min(0)] private int hiredUpToT2 = 4;

        [Tooltip("Workers hired in a day while the stock is above T2, up to T3.")]
        [SerializeField, Min(0)] private int hiredUpToT3 = 1;

        [Tooltip("Workers a workshop keeps above T3, where it produces nothing.")]
        [SerializeField, Min(0)] private int workersAboveT3 = 1;

        [Header("Start of a game")]
        [Tooltip("Workers every city starts with, spread evenly over its workshops: 250 is Patrician III's.")]
        [SerializeField, Min(0)] private int startingWorkforce = 250;
```

2. Replace the four production properties by:

```csharp
        public double InefficientEfficiency => inefficientEfficiency;

        public int HiredUpToT0 => hiredUpToT0;

        public int HiredUpToT1 => hiredUpToT1;

        public int HiredUpToT2 => hiredUpToT2;

        public int HiredUpToT3 => hiredUpToT3;

        public int WorkersAboveT3 => workersAboveT3;

        public int StartingWorkforce => startingWorkforce;
```

3. Replace `TryValidatePrices` and the `OnValidate` call:

```csharp
        /// <summary>
        /// Whether the price factors, the stock thresholds and the workforce can be used: the
        /// Inspector lets through values that the Create methods refuse.
        /// </summary>
        /// <param name="problem">Which setting is wrong and why, for the console; null when none is.</param>
        public bool TryValidateSettings(out string problem)
        {
            problem = ProblemOf(() => buyFactors.ToCurve(), "the buy factors")
                ?? ProblemOf(() => sellFactors.ToCurve(), "the sell factors")
                ?? ProblemOf(() => CreatePricing(), "the sell factors against the buy factors")
                ?? ProblemOf(() => CreateThresholdRule(), "the stock thresholds")
                ?? ProblemOf(() => CreateWorkforceRule(), "the workforce");

            return problem == null;
        }
```

   and in `OnValidate`, `TryValidatePrices` becomes `TryValidateSettings`.
4. After `CreateThresholdRule`, add:

```csharp
        /// <summary>How the workshops hire, lay off and start, from these settings. Throws when one of them is unusable.</summary>
        public WorkforceRule CreateWorkforceRule()
        {
            return new WorkforceRule(hiredUpToT0, hiredUpToT1, hiredUpToT2, hiredUpToT3, workersAboveT3, startingWorkforce);
        }
```

5. In `CreateMarket`, the loop body becomes:

```csharp
                GoodDefinition good = goods[i];
                int maxWorkers = 0;
                double outputPerWorker = 0d;

                switch (city.ProductionOf(good))
                {
                    case GoodProduction.Efficient:
                        maxWorkers = good.MaxWorkers;
                        outputPerWorker = good.DailyOutputPerWorker;
                        break;
                    case GoodProduction.Inefficient:
                        maxWorkers = good.MaxWorkers;
                        outputPerWorker = good.DailyOutputPerWorker * inefficientEfficiency;
                        break;
                }

                marketGoods[i] = new MarketGood(
                    good.BasePrice,
                    good.WeeklyConsumptionPerThousand / daysPerWeek,
                    maxWorkers,
                    outputPerWorker,
                    good.Category == GoodCategory.Food);
```

   and the market gets `CreateWorkforceRule(),` right after `CreateThresholdRule(),`.

In `Assets/Scripts/Game/WorldEconomy.cs`, `economy.TryValidatePrices(out string pricesProblem)` becomes `economy.TryValidateSettings(out string settingsProblem)`, and `: pricesProblem;` becomes `: settingsProblem;`.

- [ ] **Step 9: `WorldMapSetup` sample goods**

In `Assets/Scripts/Editor/WorldMapSetup.cs`:

1. `SampleGood` gets two more parameters, `int maxWorkers, double dailyOutputPerWorker` after `weeklyConsumptionPerThousand`, their assignments and the properties `public int MaxWorkers { get; }` and `public double DailyOutputPerWorker { get; }`.
2. The comment and table:

```csharp
        // In the order of the catalogue, which is their order on screen. Category, base price,
        // what 1 000 inhabitants consume in a week, then the workshop's maximum of workers and
        // a worker's daily output: the values a good that is missing is created with, which are
        // then balanced by hand in the asset.
        private static readonly SampleGood[] SampleGoods =
        {
            new SampleGood("Fish", "Poisson", GoodCategory.Food, 12, 10.5, 65, 0.0611),
            new SampleGood("Iron", "Fer", GoodCategory.RawMaterial, 50, 2.8, 78, 0.1455),
            new SampleGood("Beer", "Bière", GoodCategory.Food, 50, 50, 68, 0.3337),
            new SampleGood("Grain", "Grain", GoodCategory.Food, 80, 40, 68, 0.2669),
            new SampleGood("Meat", "Viande", GoodCategory.Food, 600, 8, 60, 0.0529),
            new SampleGood("Vegetables", "Légumes", GoodCategory.Food, 100, 30, 60, 0.1869),
        };
```

3. In `CreateGood`, after the `weeklyConsumptionPerThousand` line:

```csharp
            serialized.FindProperty("maxWorkers").intValue = sample.MaxWorkers;
            serialized.FindProperty("dailyOutputPerWorker").doubleValue = sample.DailyOutputPerWorker;
```

- [ ] **Step 10: Refresh, read the console for compile errors, run all EditMode tests.** Expected: no error, all pass. If a test of the AI merchants or of the planner fails, its goods are not produced (rate 0) and its figures cannot have changed: look for a `new CityMarket(` that got the workforce in the wrong place.

  Then check that nothing of the old model is left; expected: no match.

```bash
grep -rn "ProductionRate\|CeilingDays\|ceilingDays\|productionRate\|TryValidatePrices" Assets/Scripts Assets/Tests
```

- [ ] **Step 11: Commit**

```bash
git add Assets/Scripts Assets/Tests
git commit -m "Production : exploitations régulées par les seuils de stock, à la place des taux et des plafonds

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Population from the workforce

**Files:**
- Modify: `Assets/Scripts/Game/EconomyDefinition.cs`, `CityDefinition.cs`, `WorldEconomy.cs`, `CityInfoPanelController.cs`
- Modify: `Assets/Scripts/Editor/WorldMapSetup.cs`
- Modify: `Assets/Tests/EditMode/TestEconomy.cs`, `EconomyDefinitionTests.cs`, `EconomyContentTests.cs`

**Interfaces:**
- Consumes: `EconomyDefinition.StartingWorkforce`, `CreateWorkforceRule`, `TryValidateSettings` (Task 2).
- Produces: `EconomyDefinition.InhabitantsPerWorker` (double), `EconomyDefinition.StartingPopulation` (double); `CityDefinition.Population` is gone; `CityInfoPanelController.NoPopulation` (const string); tests: `TestEconomy.CreateCity(GoodDefinition[] efficient, GoodDefinition[] inefficient)`, `TestEconomy.SetStartingPopulation(EconomyDefinition economy, int inhabitants)`.

- [ ] **Step 1: Write the failing tests**

In `Assets/Tests/EditMode/TestEconomy.cs`:

1. `CreateCity` loses its population:

```csharp
        public static CityDefinition CreateCity(GoodDefinition[] efficient, GoodDefinition[] inefficient)
        {
            var city = ScriptableObject.CreateInstance<CityDefinition>();

            var serialized = new SerializedObject(city);
            SetGoods(serialized.FindProperty("efficientGoods"), efficient);
            SetGoods(serialized.FindProperty("inefficientGoods"), inefficient);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return city;
        }
```

2. `CreateEconomy` starts its cities with 6 000 inhabitants, the figure the tests were written with:

```csharp
        /// <summary>An economy of these goods whose cities start with 6 000 inhabitants.</summary>
        public static EconomyDefinition CreateEconomy(params GoodDefinition[] goods)
        {
            var economy = ScriptableObject.CreateInstance<EconomyDefinition>();

            var serialized = new SerializedObject(economy);
            SetGoods(serialized.FindProperty("goods"), goods);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SetStartingPopulation(economy, 6000);
            return economy;
        }

        /// <summary>
        /// Makes every city of the economy start with these inhabitants: as many workers, one
        /// inhabitant each. The workshops then start full.
        /// </summary>
        public static void SetStartingPopulation(EconomyDefinition economy, int inhabitants)
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("startingWorkforce").intValue = inhabitants;
            serialized.FindProperty("inhabitantsPerWorker").doubleValue = 1d;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
```

In `Assets/Tests/EditMode/EconomyDefinitionTests.cs`:

1. The helper: `private CityDefinition City(GoodDefinition[] efficient, GoodDefinition[] inefficient)` returning `Track(TestEconomy.CreateCity(efficient, inefficient))`; every `City(6000, ` and `City(1000, ` becomes `City(`.
2. In `ANewDefinition_HasTheGamesSettings`, after the `StartingWorkforce` assert add `Assert.AreEqual(72d / 17d, fresh.InhabitantsPerWorker, Tolerance);`.
3. In `ANewGood_AndANewCity_HaveUsableDefaults`, delete `Assert.AreEqual(0, city.Population);`.
4. In `CreateMarket_FeedsThePopulationWithFoodOnly`, after the line that creates `mixed`, add `TestEconomy.SetStartingPopulation(mixed, 1000);`.
5. In `CreateMarket_SpreadsTheStartingWorkforce_OverTheCitysWorkshops` (Task 2), the starting workforce of 50 also gives 50 inhabitants now; nothing to change.
6. Replace `CreateMarket_ForACityWithoutInhabitants_HasEmptyStocks` and `CreateMarket_StartsWithThePopulationOfTheCity` by:

```csharp
        [Test]
        public void CreateMarket_WithoutStartingWorkforce_StartsEmpty()
        {
            TestEconomy.SetStartingPopulation(economy, 0);
            CityDefinition city = City(new[] { wheat }, new GoodDefinition[0]);

            CityMarket market = economy.CreateMarket(city, economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(0, market.Population.Inhabitants);
            Assert.AreEqual(0, market.WorkersOf(0));
            Assert.AreEqual(0, market.AvailableOf(0));
            Assert.AreEqual(0, market.AvailableOf(2));
        }

        [Test]
        public void CreateMarket_StartsWithThePopulationOfTheWorkforce()
        {
            // Patrician III's: 250 workers and 72 / 17 inhabitants each.
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("startingWorkforce").intValue = 250;
            serialized.FindProperty("inhabitantsPerWorker").doubleValue = 72d / 17d;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            CityMarket market = economy.CreateMarket(City(new[] { wheat }, new[] { fish }), economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(250d * 72d / 17d, economy.StartingPopulation, Tolerance);
            Assert.AreEqual(250d * 72d / 17d, market.Population.Count, Tolerance);
            Assert.AreEqual(1059, market.Population.Inhabitants);
        }

        [Test]
        public void CreateMarket_ForACityThatProducesNothing_HasNoWorkers_AndTheSamePopulation()
        {
            CityMarket producer = economy.CreateMarket(City(new[] { wheat }, new GoodDefinition[0]), economy.CreatePricing(), DaysPerWeek);
            CityMarket idle = economy.CreateMarket(City(new GoodDefinition[0], new GoodDefinition[0]), economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(0, idle.WorkersOf(0));
            Assert.AreEqual(producer.Population.Count, idle.Population.Count, Tolerance);
        }

        [Test]
        public void TryValidateSettings_SaysWhenThePopulationPerWorkerIsWrong()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("inhabitantsPerWorker").doubleValue = -1;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(economy.TryValidateSettings(out string problem));
            StringAssert.Contains("starting population", problem);
        }
```

In `Assets/Tests/EditMode/EconomyContentTests.cs`:

1. `EveryCity_HasInhabitants_AndProducesOnlyGoodsOfTheCatalogue_Once` becomes `EveryCity_ProducesSomething_OnlyGoodsOfTheCatalogue_Once`: replace `Assert.Greater(city.Population, 0, city.name);` by `Assert.Greater(city.EfficientGoods.Count + city.InefficientGoods.Count, 0, city.name);`.
2. Replace the seven `TestCase`s and `ACity_HasItsPopulation` by:

```csharp
        [Test]
        public void EveryCity_StartsWithPatricianIIIsWorkforce()
        {
            EconomyDefinition economy = LoadEconomy();

            Assert.AreEqual(250, economy.StartingWorkforce);
            Assert.AreEqual(72d / 17d, economy.InhabitantsPerWorker, 1e-9);
        }
```

- [ ] **Step 2: Refresh.** Expected: compile errors, `CreateCity` and `SetStartingPopulation` call fields and members that do not exist yet (`inhabitantsPerWorker`, `StartingPopulation`, `InhabitantsPerWorker`), and `CityDefinition.Population` is still used by the tests' old calls. Go on.

- [ ] **Step 3: `EconomyDefinition`**

1. After the `startingWorkforce` field add:

```csharp
        [Tooltip("Inhabitants per starting worker: 72 / 17 is Patrician III's 4, plus a seventeenth of beggars.")]
        [SerializeField, Min(0f)] private double inhabitantsPerWorker = 72d / 17d;
```

2. After the `StartingWorkforce` property add:

```csharp
        public double InhabitantsPerWorker => inhabitantsPerWorker;

        /// <summary>The inhabitants of every city when the game starts: the starting workforce, times the inhabitants per worker.</summary>
        public double StartingPopulation => startingWorkforce * inhabitantsPerWorker;
```

3. In `TryValidateSettings`, add a last line to the chain:

```csharp
                ?? ProblemOf(() => new CityPopulation(StartingPopulation), "the starting population (workforce times inhabitants per worker)");
```

4. In `CreateMarket`, its summary's `with the population of its definition` becomes `with the starting population, the same in every city`, and `new CityPopulation(Math.Max(0, city.Population)),` becomes `new CityPopulation(StartingPopulation),`.

- [ ] **Step 4: `CityDefinition`, `WorldEconomy`, `CityInfoPanelController`**

In `Assets/Scripts/Game/CityDefinition.cs`, delete the `population` field with its tooltip and the `Population` property with its summary, and change the two tooltips of the goods lists:

```csharp
        [Tooltip("Goods the city has a workshop for, at the full output of a worker.")]
        [SerializeField] private List<GoodDefinition> efficientGoods = new List<GoodDefinition>();

        [Tooltip("Goods the city has a workshop for, at the economy's inefficient share of the output.")]
        [SerializeField] private List<GoodDefinition> inefficientGoods = new List<GoodDefinition>();
```

In `Assets/Scripts/Game/WorldEconomy.cs`, in `WarnAbout`, replace the population warning by:

```csharp
            if (!ProducesAnyGood(city))
            {
                Debug.LogWarning(
                    $"CityDefinition '{city.name}' produces no good of the economy; it still starts with the economy's population.",
                    city);
            }
```

and add after `WarnAboutGoods`:

```csharp
        private bool ProducesAnyGood(CityDefinition city)
        {
            foreach (GoodDefinition good in economy.Goods)
            {
                if (city.ProductionOf(good) != GoodProduction.None)
                {
                    return true;
                }
            }

            return false;
        }
```

In `Assets/Scripts/Game/CityInfoPanelController.cs`, add next to `FormatPopulation`:

```csharp
        /// <summary>What the panel shows of a city that has no market: its population is the market's.</summary>
        public const string NoPopulation = "Population : —";
```

and in `ShowPopulationOf`, `populationLabel.text = FormatPopulation(city.Population);` becomes `populationLabel.text = NoPopulation;`; the comment above the method becomes `// The inhabitants of the game are in the market; a city without one has none to show.`

- [ ] **Step 5: `WorldMapSetup` sample cities**

In `Assets/Scripts/Editor/WorldMapSetup.cs`:

1. Class summary: `Cities of the map that have no inhabitant are given the population and the productions of the sample content.` becomes `Cities of the map that produce nothing are given the productions of the sample content.`
2. `SampleCity`: delete the `population` parameter, its assignment and the `Population` property; the summaries of `EfficientGoods` and `InefficientGoods` become `Asset names of the goods the city has a workshop for, at the full output.` and `Asset names of the goods the city has a workshop for, at the inefficient share of the output.`
3. In `SampleCities`, delete the population argument of every city (`2100`, `1500`, `2000`, `1800`, `1500`, `2500`, `2100`).
4. `FillCityEconomies`: its comment becomes `// A city that produces nothing has no economy yet: one made before the economy existed, or just created. A city that produces something is the user's and is left untouched.` and its test `city.Population != 0` becomes `city.EfficientGoods.Count + city.InefficientGoods.Count > 0`.
5. `FillCityEconomy`: delete the `population` line.

- [ ] **Step 6: Refresh, read the console, run all EditMode tests.** Expected: no error, all pass. Then check that no city population is left; expected: no match outside `CityPopulation`.

```bash
grep -rn "city.Population\b\|\.Population;\|\"population\"\|sample.Population" Assets/Scripts Assets/Tests
```

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts Assets/Tests
git commit -m "Production : population de départ déduite des ouvriers, la même dans toutes les villes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Workers in the debug tool

**Files:**
- Modify: `Assets/Scripts/Game/DebugCitiesTable.cs`, `Assets/UI/Debug/DebugPanel.uss`
- Test: `Assets/Tests/EditMode/DebugCitiesTableTests.cs`

**Interfaces:**
- Consumes: `CityMarket.WorkersOf`, `CityMarket.MaxWorkersOf` (Task 2).
- Produces: a fifth cell per good, after the net: "Ouv." in the header, "30/30" or `DebugCitiesTable.NoFigure` in a row, the sums in the total row.

- [ ] **Step 1: Write the failing tests**

In `Assets/Tests/EditMode/DebugCitiesTableTests.cs`:

1. The cell constants: `CellsPerGood = 5`, and after `Net = 3` add `private const int Workers = 4;`; the comment above becomes `// ... then five cells per good.`
2. In `TheHeader_NamesTheGoods_AndTheirFigures`, after the `Net` assert add:

```csharp
            Assert.AreEqual("Ouv.", ((Label)header[1][FirstGoodCell + Workers]).text);
```

3. Add:

```csharp
        [Test]
        public void ARow_WritesTheWorkersOfEveryWorkshop()
        {
            table.AddCity("Sparia", market);

            // Fish is not produced; iron has a full workshop of 30.
            Assert.AreEqual(DebugCitiesTable.NoFigure, Text(0, Fish, Workers));
            Assert.AreEqual("30/30", Text(0, Iron, Workers));
        }

        [Test]
        public void WhileShown_TheWorkersFollowTheMarket()
        {
            table.AddCity("Sparia", market);
            table.Show();
            market.Put(Iron, 100);

            market.StartDay();

            // 158 iron once consumed, above T3: the workshop keeps one worker.
            Assert.AreEqual("1/30", Text(0, Iron, Workers));
        }

        [Test]
        public void TheTotals_SumTheWorkersAndTheirMaxima()
        {
            var other = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(50, 2, 40, 0.1) },
                TestMarkets.Thresholds,
                new WorkforceRule(16, 8, 4, 1, 1, 10),
                30,
                TestMarkets.Pricing,
                7,
                new CityPopulation(1000));

            table.AddCity("Sparia", market);
            table.AddCity("Elforth", other);

            Assert.AreEqual("40/70", Total(Iron, Workers));
            Assert.AreEqual(DebugCitiesTable.NoFigure, Total(Fish, Workers));
        }
```

4. In `AMarketWithFewerGoodsThanColumns_LeavesTheExtraCellsEmpty`, add `Assert.AreEqual(DebugCitiesTable.NoFigure, Text(0, Iron, Workers));`. In `TheTotalsOfAnEmptyTable_AreZero`, add `Assert.AreEqual(DebugCitiesTable.NoFigure, Total(Fish, Workers));`.

- [ ] **Step 2: Refresh and run `DebugCitiesTableTests`.** Expected: the new and changed tests fail (a row has four cells per good, the header no "Ouv."); the others fail too where they index cells of the second good (`Iron`), since `CellsPerGood` is now 5. That is the expected red.

- [ ] **Step 3: Write the implementation**

In `Assets/Scripts/Game/DebugCitiesTable.cs`:

1. Class summary: after `and gains in a week, the nominal figure first and the one of the last days in brackets,` add `the workers of its workshop over their maximum,`.
2. Add `private const string WorkersCellClass = "debug-cities__cell--workers";` after `FigureCellClass`.
3. `GoodCells` gets `public Label Workers;` after `Net`.
4. In `AddGoodCells`, add `Workers = AddLabel(row, WorkersCellClass),` after `Net`.
5. In `BuildHeader`, after the `Net` label add `AddLabel(columns, WorkersCellClass).text = "Ouv.";`.
6. Add the format:

```csharp
        /// <summary>Workers as the table writes them: "45/60", or a dash where no workshop can have any.</summary>
        public static string FormatWorkers(long workers, long maxWorkers)
        {
            return maxWorkers > 0 ? $"{workers}/{maxWorkers}" : NoFigure;
        }
```

7. In `Write(Row row)`, the branch of a good the market does not have also sets `cells.Workers.text = NoFigure;`, and the call to `Write(cells, ...)` gets two more arguments, `market.WorkersOf(i), market.MaxWorkersOf(i)`.
8. In `WriteTotals`, sum per good `long workers` and `long maxWorkers` alongside the stock (`workers += market.WorkersOf(i); maxWorkers += market.MaxWorkersOf(i);`) and pass them to `Write`.
9. `Write(GoodCells cells, ...)` gets the parameters `long workers, long maxWorkers` after `recentConsumption`, and the line `cells.Workers.text = FormatWorkers(workers, maxWorkers);`.

In `Assets/UI/Debug/DebugPanel.uss`, after `.debug-cities__cell--figure`:

```css
.debug-cities__cell--workers {
    width: 70px;
}
```

and the good group, which spans the stock, the three figures and the workers, becomes `width: 570px;` with the comment `/* As wide as the stock, the three figures and the workers of a good. */`.

- [ ] **Step 4: Refresh and run `DebugCitiesTableTests`, then all EditMode tests.** Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Game/DebugCitiesTable.cs Assets/UI/Debug/DebugPanel.uss Assets/Tests/EditMode/DebugCitiesTableTests.cs
git commit -m "Outils de débogage : ouvriers de chaque exploitation dans l'onglet Villes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Assets, documentation and a game run

**Files:**
- Modify: `Assets/Data/Goods/*.asset`, `Assets/Data/Cities/*.asset`, `Assets/Data/Economy/Economy.asset`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: the serialized names of Tasks 2 and 3 (`maxWorkers`, `dailyOutputPerWorker`, `inefficientEfficiency`, `hiredUpToT0`..`hiredUpToT3`, `workersAboveT3`, `startingWorkforce`, `inhabitantsPerWorker`).
- Produces: nothing new.

- [ ] **Step 1: Good assets**

At the end of each file of `Assets/Data/Goods/`, after `weeklyConsumptionPerThousand:`, add the two lines:

| File | `maxWorkers` | `dailyOutputPerWorker` |
|---|---|---|
| `Fish.asset` | 65 | 0.0611 |
| `Beer.asset` | 68 | 0.3337 |
| `Grain.asset` | 68 | 0.2669 |
| `Iron.asset` | 78 | 0.1455 |
| `Meat.asset` | 60 | 0.0529 |
| `Vegetables.asset` | 60 | 0.1869 |

for example in `Fish.asset`:

```yaml
  weeklyConsumptionPerThousand: 10
  maxWorkers: 65
  dailyOutputPerWorker: 0.0611
```

- [ ] **Step 2: City and economy assets**

In each of the seven files of `Assets/Data/Cities/`, delete the `population:` line.

In `Assets/Data/Economy/Economy.asset`, delete the four lines `efficientProductionRate`, `efficientCeilingDays`, `inefficientProductionRate`, `inefficientCeilingDays`, and after `startingStockDays: 7` insert:

```yaml
  inefficientEfficiency: 0.75
  hiredUpToT0: 16
  hiredUpToT1: 8
  hiredUpToT2: 4
  hiredUpToT3: 1
  workersAboveT3: 1
  startingWorkforce: 250
  inhabitantsPerWorker: 4.235294117647059
```

Refresh, then log through `Unity_RunCommand` what `Economy.asset` loads: the new settings, and for every good its `MaxWorkers` and `DailyOutputPerWorker`; they must match the tables above. Run all EditMode tests: all pass.

- [ ] **Step 3: `CLAUDE.md`**

1. In the *World map* bullet of `WorldMapSetup`, replace `Cities of the map with a population of 0 are given the population and the productions of the sample content, whether the tool has just created them or not.` by `Cities of the map that produce nothing are given the productions of the sample content, whether the tool has just created them or not.`
2. In the *Economy* section, the first bullet: replace `its base price, and what 1 000 inhabitants consume of it in a week (spread over the days of the calendar's week).` by `its base price, what 1 000 inhabitants consume of it in a week (spread over the days of the calendar's week), and the maximum of workers and the daily output of a worker of the workshop that produces it.`
3. The `CityDefinition` bullet: replace `holds the population the city starts with (see Population) and the goods the city produces efficiently and inefficiently` by `holds the goods the city has a workshop for, efficiently and inefficiently`.
4. Replace the bullet that starts `- A day of a city, per good:` by:

```markdown
- Workshops follow Patrician III (`patrician3-re\resultats\Production et consommation des villes.md`). A city that produces a good has a workshop for it: workers up to the good's maximum (`GoodDefinition.maxWorkers`, the same in every city), each making the good's daily output (`dailyOutputPerWorker`), times 0.75 in a city that produces it inefficiently (`EconomyDefinition.inefficientEfficiency`). Its capacity does not depend on the size of the city. A day of a city, per good (`CityMarket.StartDay`): the inhabitants consume, down to nothing; then the workshop hires by where the stock left stands against the thresholds (`WorkforceRule`): 16 workers at T0 or under, 8 above T0, 4 above T1, 1 above T2, never above its maximum, and produces with the workers of the day; above T3 it keeps a single worker and produces nothing that day. A stock equal to a threshold counts as under it. A producer left alone therefore settles between T2 and T3. There is no limit to what a city stores: what the player delivers always adds, and a stock above T3 is only consumed. Stocks are fractional; what is shown and can be bought is the whole part.
- A new game starts as in Patrician III: every city gets the same workforce (`EconomyDefinition.startingWorkforce`, 250), spread evenly over its workshops, rounded up and cut to their maximum (`WorkforceRule.StartingWorkersOf`), and the same population, the workforce times the inhabitants per worker (`inhabitantsPerWorker`, 72 ÷ 17: Patrician III's 4 inhabitants plus a seventeenth of beggars), about 1 059. Workers are a count per workshop, not taken from the inhabitants, and hiring is immediate. A city holds at the start the same days of its consumption of every good, produced or not (`startingStockDays`, 7): T0, so prices open at 1.5 and 1.4 times the base price, producers fill up and the others run out within the first week.
```

5. In the prices bullet, replace `T2 = T1 + 10 days of the city's production (T1 itself for a good it does not produce)` by `T2 = T1 + 10 days of the capacity of the city's workshop (T1 itself for a good it does not produce)`.
6. Replace the bullet that starts `- What a city consumes of a good is` by `- What a city consumes of a good is the good's consumption per 1 000 inhabitants times its population of the moment (`CityMarket.DailyConsumptionOf`), so consumption and the thresholds that are given in days of it follow the population; workshops and stocks do not.`
7. Replace the bullet that starts `- Production is a share of a city's own consumption` by `- The output per worker of each good is set so that, every workshop at its maximum, the world can produce 1.5 times what it consumes at the start: capacity absorbs about eight months of the fastest growth, and the regulation keeps the surplus from piling up. A city that does not produce a good depends on the player or on the AI merchants (see AI merchants) to bring it, and pays high prices for it until then. Every number is a field of an asset, meant to be tuned by playing.`
8. In the *Population* section, replace `; `CityDefinition.population` is only what it starts with.` by `; every city starts with the economy's starting population (see Economy).`
9. In the *Debug tools* bullet of the cities tab, replace `the weekly production, consumption and net gain, written "nominal (recent)" with one decimal, the net in green or red (rich text).` by `the weekly production, consumption and net gain, written "nominal (recent)" with one decimal, the nominal production at the workers of the moment, the net in green or red (rich text), and the workers of its workshop over their maximum ("Ouv.", "45/60", a dash for a good the city does not produce).`
10. In *Project state*, replace `cities hold stocks of goods (six for now: fish, iron, beer, grain, meat and vegetables) that they consume and partly produce` by `cities hold stocks of goods (six for now: fish, iron, beer, grain, meat and vegetables) that they consume and produce in workshops regulated by their stocks`.

- [ ] **Step 4: Run the game in the Editor**

Enter Play mode on `Assets/Scenes/WorldMap.unity` (the scene must not have unsaved changes) and check, through `Unity_RunCommand` reading `WorldEconomy`, `WorldMapView` and `AiMerchants`, and the console, writing down what is seen:
- The console shows no error and no warning from the economy.
- Day 0: every city has 1 059 inhabitants; Hazer Empire (5 goods) has 50 workers in each workshop; Elforth (3 goods) has 65 in fish and 60 in meat and in vegetables.
- After about 60 days of fast forward (`WorldClock.Clock.SetFastForward(true)`): the stock of every produced good of a city with no trade is between T2 and T3 plus a day's capacity, or rising towards it; workers of a workshop above T3 are 1; the AI ships have traded (`AiMerchants.TryGetProfit`).

Leave Play mode. If the AI ships never trade, or a city's population collapses, report it with the figures seen rather than changing the settings: balancing is out of scope.

- [ ] **Step 5: Commit**

```bash
git add Assets/Data CLAUDE.md
git commit -m "Production : réglages des exploitations et du départ, et CLAUDE.md

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
