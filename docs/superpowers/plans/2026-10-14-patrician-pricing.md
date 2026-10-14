# Patrician III Pricing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cities price their goods as Patrician III does: one base price per good times a factor that follows the stock against four thresholds, on a curve for what the player buys and another for what the player sells, every barrel priced at the average factor over the stock it crosses.

**Architecture:** Three small `Core` types do the arithmetic: `StockThresholds` (T0 to T3 of one good in one city), `ThresholdRule` (thresholds from daily consumption and production) and `PriceCurve` (five factors, the factor at a stock and the average over a range). `MarketPricing` composes a buy curve and a sell curve; `CityMarket` works out the thresholds of a good when a price is asked. `BuyPriceOf` / `SellPriceOf` keep their signatures, so `Trade`, the AI merchants and the UI do not change. In `Game`, `EconomyDefinition` holds the settings, with a serializable `PriceFactors` per curve.

**Tech Stack:** Unity 6000.6.4f1, C#, Unity Test Framework (NUnit, EditMode).

**Spec:** `docs/superpowers/specs/2026-10-14-patrician-pricing-design.md`

## Global Constraints

- All code, comments, log messages, asset and folder names are in English. Everything the player reads is in French.
- `Core` contains no `MonoBehaviour` and nothing that needs a scene, and does not reference `Game`.
- Game content and its numbers live in ScriptableObject assets; `Core` hard-codes no factor, day or floor.
- Defaults: thresholds 7 days (T0), 14 days added (T1), 10 days of production added (T2), T3 = T2 + T0, floors 5, 10, 15 and 20 barrels; factors when the player buys 4.0, 1.5, 1.0, 0.8, 0.6; when the player sells 2.0, 1.4, 1.0, 0.7, 0.5.
- Base prices kept: beer 50, fish 500, grain 80, iron 300, meat 600, vegetables 100.
- Out of scope: the game option on the empty-stock sell factor, the 15 % surcharged good, any change to production, consumption, population or the AI merchants' rules, showing thresholds in the debug tools.
- Never hand-write `.meta` files: Unity generates them on import; commit each with its file.
- Never edit the generated `.sln` / `.csproj`. Exclude `Library/` from searches.
- Match the surrounding code: Allman braces, a blank line before `return` and after blocks as in the existing files, XML `<summary>` on public types and members, comments only where the reason is not obvious, "The negated comparisons also reject NaN." where a negated comparison validates.
- Commit directly on `main`. Commit messages are in French and end with:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
- Existing tests change only where a task says so. Their expected prices are the ones given in this plan, worked out from the new model.
- Differences from the spec's wording, intentional:
  - `EconomyDefinition.CreateMarket` builds the `ThresholdRule` itself through `CreateThresholdRule()` rather than taking it as a parameter: like the starting days, it is the definition's own setting.
  - The tests share one `ThresholdRule` (`TestMarkets.Thresholds`) whose T1 is 30 days of consumption for a good not produced, the target of the old tests, so that their scenarios (empty, at target, surplus) keep their meaning.

## Review Focus

Conditions the spec implies and that are most likely to bite:

1. A stock far above T3 (a player dumping thousands of barrels): the price stays the T3 factor exactly, with no floating-point drift that rounds a whole price up or down. Test in Task 3 (`Prices_ThatAreWholeNumbers_AreNotRoundedAway`, up to 100 000 barrels).
2. A barrel priced past an empty stock (`barrelsAlreadyBought` larger than the stock): it costs the empty-stock factor, no exception. Test in Task 3 (`ABarrelPastAnEmptyStock_CostsTheEmptyFactor`).
3. A city with no inhabitant, or a good it does not consume: the thresholds are the floors and the price still follows the stock. Tests in Task 2 (`WithoutConsumption_TheThresholdsAreTheFloors`) and Task 3 (`AGoodTheCityDoesNotConsume_IsPricedAgainstTheFloors`).
4. Settings typed in the Inspector that break the rules (a sell factor above the buy factor, a rising curve, a floor at 0): the pricing is refused with an exception, as the margins were. Tests in Task 1, Task 2 and Task 3 (`CreatePricing_RejectsSellFactorsAboveTheBuyFactors`).
5. A trade crossing the step of a good not produced (T2 = T1): buying and selling back still never gains. Test in Task 3 (`BuyingABarrelAndSellingItBack_NeverGainsGold`, with the `Step` thresholds).

## Commands

The Unity Editor is normally **open** on this project. Then use the `unity-mcp` tools: `Unity_RunCommand` to refresh the asset database and to run the EditMode tests, `Unity_GetConsoleLogs` to read compile errors.

Refresh and compile (run after every file change, before the tests):

```csharp
UnityEditor.AssetDatabase.Refresh();
```

Run EditMode tests through `Unity_RunCommand` (a filter is optional):

```csharp
var api = UnityEngine.ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
api.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(
    new UnityEditor.TestTools.TestRunner.Api.Filter
    {
        testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,
        testNames = null, // or groupNames = new[] { "DarkFantasyMerchant\\.Tests\\.EditMode\\.PriceCurveTests" }
    }));
```

and read the outcome in the Test Runner results or the console.

When the Editor is **closed** (`Get-Process Unity` shows no window titled with this project), use batch mode. `Unity.exe` is a GUI executable, so start it with `-Wait`. Do **not** add `-nographics`.

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe"
Remove-Item Logs\TestResults.xml -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList '-batchmode','-projectPath','.','-runTests','-testPlatform','EditMode','-testResults','Logs\TestResults.xml','-logFile','Logs\test.log'
"exit code: $($p.ExitCode)"
Select-String -Path Logs\TestResults.xml -Pattern '<test-run ' | ForEach-Object { $_.Line }
```

Append `'-testFilter','DarkFantasyMerchant.Tests.EditMode.PriceCurveTests'` to run one fixture. Exit code `0` passed, `2` test failures, `1` compile error (read `Logs\test.log`).

## File Structure

| File | Responsibility |
|---|---|
| `Assets/Scripts/Core/StockThresholds.cs` (new) | T0 to T3 of one good in one city, in barrels. |
| `Assets/Scripts/Core/PriceCurve.cs` (new) | Five factors; the factor at a stock and the average over a range. |
| `Assets/Scripts/Core/ThresholdRule.cs` (new) | Thresholds from daily consumption and production, with floors. |
| `Assets/Scripts/Core/MarketPricing.cs` (rewrite) | The price of one barrel bought or sold, from the two curves. |
| `Assets/Scripts/Core/MarketGood.cs` (modify) | Loses the minimum and maximum prices. |
| `Assets/Scripts/Core/CityMarket.cs` (modify) | Takes a `ThresholdRule`; `ThresholdsOf` replaces `TargetOf`. |
| `Assets/Scripts/Game/PriceFactors.cs` (new) | The five factors of a curve as an Inspector setting. |
| `Assets/Scripts/Game/EconomyDefinition.cs` (modify) | Threshold and factor settings in place of the target and margins. |
| `Assets/Scripts/Game/GoodDefinition.cs` (modify) | Only the base price is left. |
| `Assets/Scripts/Editor/WorldMapSetup.cs` (modify) | Sample goods give only their base price. |
| `Assets/Tests/EditMode/TestMarkets.cs` (new) | The threshold rule and pricing the tests' markets share. |
| `Assets/Tests/EditMode/PriceCurveTests.cs`, `ThresholdRuleTests.cs` (new) | Tests of the new types. |
| `Assets/Tests/EditMode/MarketPricingTests.cs` (rewrite) and six other test files (modify) | Adapted to the new model. |
| `Assets/Data/Economy/Economy.asset`, `Assets/Data/Goods/*.asset` (modify) | Obsolete fields removed, new defaults set. |
| `CLAUDE.md` (modify) | The *Economy* section describes the new model. |

---

### Task 1: Stock thresholds and price curves

**Files:**
- Create: `Assets/Scripts/Core/StockThresholds.cs`
- Create: `Assets/Scripts/Core/PriceCurve.cs`
- Test: `Assets/Tests/EditMode/PriceCurveTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `public readonly struct StockThresholds` with `StockThresholds(double t0, double t1, double t2, double t3)` and properties `double T0`, `T1`, `T2`, `T3`. Throws `ArgumentOutOfRangeException` unless `0 <= t0 <= t1 <= t2 <= t3` and all are finite.
  - `public sealed class PriceCurve` with `PriceCurve(double empty, double atT0, double atT1, double atT2, double atT3)`, properties `double Empty`, `AtT0`, `AtT1`, `AtT2`, `AtT3`, and methods `double FactorAt(double stock, StockThresholds thresholds)` and `double AverageFactor(double from, double to, StockThresholds thresholds)`. The constructor throws `ArgumentOutOfRangeException` for a factor that is not above 0 and finite, or a curve that rises with the stock. `AverageFactor` throws `ArgumentOutOfRangeException` unless `from < to`, both finite.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/PriceCurveTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class PriceCurveTests
    {
        private const double Tolerance = 1e-9;

        // The factors at which the player buys in Patrician III.
        private static PriceCurve Curve => new PriceCurve(4, 1.5, 1, 0.8, 0.6);

        private static readonly StockThresholds Thresholds = new StockThresholds(20, 40, 60, 80);

        // A good the city does not produce: T2 is T1.
        private static readonly StockThresholds Step = new StockThresholds(20, 60, 60, 80);

        [TestCase(0, 4)]
        [TestCase(10, 2.75)]
        [TestCase(20, 1.5)]
        [TestCase(30, 1.25)]
        [TestCase(40, 1)]
        [TestCase(50, 0.9)]
        [TestCase(60, 0.8)]
        [TestCase(70, 0.7)]
        [TestCase(80, 0.6)]
        public void FactorAt_IsAStraightLineBetweenThePoints(double stock, double expected)
        {
            Assert.AreEqual(expected, Curve.FactorAt(stock, Thresholds), Tolerance);
        }

        [Test]
        public void FactorAt_ANegativeStock_IsThatOfAnEmptyOne()
        {
            Assert.AreEqual(4, Curve.FactorAt(-5, Thresholds), Tolerance);
        }

        [TestCase(80.5)]
        [TestCase(500)]
        [TestCase(1e6)]
        public void FactorAt_AboveT3_StaysAtTheLastFactor(double stock)
        {
            Assert.AreEqual(0.6, Curve.FactorAt(stock, Thresholds), Tolerance);
        }

        [Test]
        public void AverageFactor_InsideOneSegment_IsTheMiddleOfItsEnds()
        {
            Assert.AreEqual(2.75, Curve.AverageFactor(0, 20, Thresholds), Tolerance);
            Assert.AreEqual(0.7, Curve.AverageFactor(60, 80, Thresholds), Tolerance);
        }

        [Test]
        public void AverageFactor_AcrossAThreshold_AddsTheSegmentsItCrosses()
        {
            // Ten barrels averaging 2.125 under T0, then ten averaging 1.375 above it.
            Assert.AreEqual(1.75, Curve.AverageFactor(10, 30, Thresholds), Tolerance);
        }

        [Test]
        public void AverageFactor_BelowAnEmptyStock_CountsTheEmptyFactor()
        {
            Assert.AreEqual(4, Curve.AverageFactor(-1, 0, Thresholds), Tolerance);

            // Half at 4, half averaging 3.9375.
            Assert.AreEqual(3.96875, Curve.AverageFactor(-1, 1, Thresholds), Tolerance);
        }

        [Test]
        public void AverageFactor_AboveT3_IsTheLastFactor()
        {
            Assert.AreEqual(0.6, Curve.AverageFactor(90, 95, Thresholds), Tolerance);
        }

        [Test]
        public void TwoEqualThresholds_MakeAStep()
        {
            // At T1 itself, the factor of T1; just above, already on the segment from T2 to T3.
            Assert.AreEqual(1, Curve.FactorAt(60, Step), Tolerance);
            Assert.AreEqual(0.795, Curve.FactorAt(60.5, Step), Tolerance);

            // One barrel averaging 1.00625 below the step, one averaging 0.795 above it.
            Assert.AreEqual(0.900625, Curve.AverageFactor(59, 61, Step), Tolerance);
        }

        [TestCase(5, 5)]
        [TestCase(6, 5)]
        [TestCase(double.NaN, 5)]
        [TestCase(5, double.PositiveInfinity)]
        public void AverageFactor_RejectsARangeThatIsNotOne(double from, double to)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Curve.AverageFactor(from, to, Thresholds));
        }

        [Test]
        public void AFlatCurve_IsOneFactorEverywhere()
        {
            var flat = new PriceCurve(1, 1, 1, 1, 1);

            Assert.AreEqual(1, flat.FactorAt(0, Thresholds), Tolerance);
            Assert.AreEqual(1, flat.FactorAt(55, Step), Tolerance);
            Assert.AreEqual(1, flat.AverageFactor(-3, 300, Thresholds), Tolerance);
        }

        [Test]
        public void ACurve_KeepsItsFactors()
        {
            PriceCurve curve = Curve;

            Assert.AreEqual(4, curve.Empty);
            Assert.AreEqual(1.5, curve.AtT0);
            Assert.AreEqual(1, curve.AtT1);
            Assert.AreEqual(0.8, curve.AtT2);
            Assert.AreEqual(0.6, curve.AtT3);
        }

        [TestCase(0, 1.5, 1, 0.8, 0.6)]
        [TestCase(4, 1.5, 1, 0.8, -0.1)]
        [TestCase(double.NaN, 1.5, 1, 0.8, 0.6)]
        [TestCase(double.PositiveInfinity, 1.5, 1, 0.8, 0.6)]
        [TestCase(4, 1.5, 1.6, 0.8, 0.6)]
        [TestCase(4, 1.5, 1, 0.8, 0.9)]
        [TestCase(1, 1.5, 1, 0.8, 0.6)]
        public void RejectsFactorsThatAreNotPositive_OrThatRise(double empty, double atT0, double atT1, double atT2, double atT3)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PriceCurve(empty, atT0, atT1, atT2, atT3));
        }

        [Test]
        public void Thresholds_CanBeEqual()
        {
            var thresholds = new StockThresholds(10, 10, 10, 10);

            Assert.AreEqual(10, thresholds.T0);
            Assert.AreEqual(10, thresholds.T3);
        }

        [TestCase(-1, 10, 15, 20)]
        [TestCase(20, 10, 30, 40)]
        [TestCase(5, 10, 15, double.PositiveInfinity)]
        [TestCase(double.NaN, 10, 15, 20)]
        public void Thresholds_RejectANegativeStock_OrAWrongOrder(double t0, double t1, double t2, double t3)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StockThresholds(t0, t1, t2, t3));
        }
    }
}
```

- [ ] **Step 2: Refresh and run `PriceCurveTests`.** Expected: compile error, `PriceCurve` and `StockThresholds` do not exist.

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/Core/StockThresholds.cs`:

```csharp
using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The four stocks of a good in a city at which its price factors are given, in barrels,
    /// from the lowest to the highest. Two of them can be equal.
    /// </summary>
    public readonly struct StockThresholds
    {
        public StockThresholds(double t0, double t1, double t2, double t3)
        {
            // The negated comparisons also reject NaN.
            if (!(t0 >= 0d) || !(t1 >= t0) || !(t2 >= t1) || !(t3 >= t2) || double.IsInfinity(t3))
            {
                throw new ArgumentOutOfRangeException(nameof(t0), "Thresholds are finite, not negative and in order.");
            }

            T0 = t0;
            T1 = t1;
            T2 = t2;
            T3 = t3;
        }

        public double T0 { get; }

        public double T1 { get; }

        public double T2 { get; }

        public double T3 { get; }
    }
}
```

Create `Assets/Scripts/Core/PriceCurve.cs`:

```csharp
using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The factors of a good's base price at an empty stock and at the four thresholds of a
    /// city's stock, in straight lines between them and constant above the last. A curve never
    /// rises as the stock rises. Two equal thresholds make a step.
    /// </summary>
    public sealed class PriceCurve
    {
        public PriceCurve(double empty, double atT0, double atT1, double atT2, double atT3)
        {
            RequireFactor(empty, nameof(empty));
            RequireFactor(atT0, nameof(atT0));
            RequireFactor(atT1, nameof(atT1));
            RequireFactor(atT2, nameof(atT2));
            RequireFactor(atT3, nameof(atT3));

            if (atT0 > empty || atT1 > atT0 || atT2 > atT1 || atT3 > atT2)
            {
                throw new ArgumentOutOfRangeException(nameof(atT0), "A price curve never rises with the stock.");
            }

            Empty = empty;
            AtT0 = atT0;
            AtT1 = atT1;
            AtT2 = atT2;
            AtT3 = atT3;
        }

        /// <summary>The factor at an empty stock, and below.</summary>
        public double Empty { get; }

        public double AtT0 { get; }

        public double AtT1 { get; }

        public double AtT2 { get; }

        /// <summary>The factor at T3, and above.</summary>
        public double AtT3 { get; }

        /// <summary>
        /// The factor at a stock. A stock of 0 or below is an empty one; at a step, the factor
        /// is the one before it.
        /// </summary>
        public double FactorAt(double stock, StockThresholds thresholds)
        {
            if (stock <= 0d)
            {
                return Empty;
            }

            if (stock <= thresholds.T0)
            {
                return Interpolate(stock, 0d, Empty, thresholds.T0, AtT0);
            }

            if (stock <= thresholds.T1)
            {
                return Interpolate(stock, thresholds.T0, AtT0, thresholds.T1, AtT1);
            }

            if (stock <= thresholds.T2)
            {
                return Interpolate(stock, thresholds.T1, AtT1, thresholds.T2, AtT2);
            }

            if (stock <= thresholds.T3)
            {
                return Interpolate(stock, thresholds.T2, AtT2, thresholds.T3, AtT3);
            }

            return AtT3;
        }

        /// <summary>
        /// The average factor over a range of stock: the integral of the curve over it,
        /// divided by its width. Below an empty stock the factor is the empty one.
        /// </summary>
        public double AverageFactor(double from, double to, StockThresholds thresholds)
        {
            // The negated comparison also rejects NaN.
            if (!(to > from) || double.IsInfinity(from) || double.IsInfinity(to))
            {
                throw new ArgumentOutOfRangeException(nameof(to));
            }

            // Segment by segment rather than as a difference of integrals from 0: a range far
            // above T3 then gives the last factor exactly.
            double area = Overlap(from, to, double.NegativeInfinity, 0d) * Empty
                + AreaOfSegment(from, to, 0d, Empty, thresholds.T0, AtT0)
                + AreaOfSegment(from, to, thresholds.T0, AtT0, thresholds.T1, AtT1)
                + AreaOfSegment(from, to, thresholds.T1, AtT1, thresholds.T2, AtT2)
                + AreaOfSegment(from, to, thresholds.T2, AtT2, thresholds.T3, AtT3)
                + Overlap(from, to, thresholds.T3, double.PositiveInfinity) * AtT3;

            return area / (to - from);
        }

        private static double Overlap(double from, double to, double start, double end)
        {
            return Math.Max(0d, Math.Min(to, end) - Math.Max(from, start));
        }

        /// <summary>The area under one straight segment, over the part of the range it covers; none for a step.</summary>
        private static double AreaOfSegment(
            double from, double to, double start, double startFactor, double end, double endFactor)
        {
            double low = Math.Max(from, start);
            double high = Math.Min(to, end);

            if (!(high > low))
            {
                return 0d;
            }

            double lowFactor = Interpolate(low, start, startFactor, end, endFactor);
            double highFactor = Interpolate(high, start, startFactor, end, endFactor);

            return (high - low) * (lowFactor + highFactor) / 2d;
        }

        private static double Interpolate(double stock, double start, double startFactor, double end, double endFactor)
        {
            return startFactor + (endFactor - startFactor) * (stock - start) / (end - start);
        }

        private static void RequireFactor(double value, string name)
        {
            // The negated comparison also rejects NaN.
            if (!(value > 0d) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
```

- [ ] **Step 4: Refresh and run `PriceCurveTests`.** Expected: all pass. Then run all EditMode tests: expected all pass (nothing else changed).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/StockThresholds.cs Assets/Scripts/Core/StockThresholds.cs.meta Assets/Scripts/Core/PriceCurve.cs Assets/Scripts/Core/PriceCurve.cs.meta Assets/Tests/EditMode/PriceCurveTests.cs Assets/Tests/EditMode/PriceCurveTests.cs.meta
git commit -m "Prix : seuils de stock et courbes de facteurs

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Threshold rule

**Files:**
- Create: `Assets/Scripts/Core/ThresholdRule.cs`
- Test: `Assets/Tests/EditMode/ThresholdRuleTests.cs`

**Interfaces:**
- Consumes: `StockThresholds` (Task 1).
- Produces: `public sealed class ThresholdRule` with `ThresholdRule(double t0Days, double t1AddedDays, double t2ProductionDays, double t0Floor, double t1Floor, double t2Floor, double t3Floor)`, the same seven values as properties (`T0Days`, `T1AddedDays`, `T2ProductionDays`, `T0Floor`, `T1Floor`, `T2Floor`, `T3Floor`), and `StockThresholds ThresholdsFor(double dailyConsumption, double dailyProduction)`. The constructor throws `ArgumentOutOfRangeException` for days that are negative or not finite, a T0 floor not above 0, floors out of order or not finite; `ThresholdsFor` for a negative or non-finite consumption or production.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/ThresholdRuleTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ThresholdRuleTests
    {
        private const double Tolerance = 1e-9;

        // Patrician III's: 7 days, 14 more, 10 days of production more; floors of 5 to 20 barrels.
        private static ThresholdRule Rule => new ThresholdRule(7, 14, 10, 5, 10, 15, 20);

        private static void AssertThresholds(StockThresholds actual, double t0, double t1, double t2, double t3)
        {
            Assert.AreEqual(t0, actual.T0, Tolerance, "T0");
            Assert.AreEqual(t1, actual.T1, Tolerance, "T1");
            Assert.AreEqual(t2, actual.T2, Tolerance, "T2");
            Assert.AreEqual(t3, actual.T3, Tolerance, "T3");
        }

        [Test]
        public void AGoodTheCityDoesNotProduce_HasT2AtT1()
        {
            // Two barrels a day: 14, then 28 more, then nothing produced, then T0 again.
            AssertThresholds(Rule.ThresholdsFor(2, 0), 14, 42, 42, 56);
        }

        [Test]
        public void T2_AddsDaysOfProduction()
        {
            // Four barrels produced a day: 40 more barrels from T1 to T2.
            AssertThresholds(Rule.ThresholdsFor(2, 4), 14, 42, 82, 96);
        }

        [Test]
        public void TheThresholds_FollowTheConsumption()
        {
            AssertThresholds(Rule.ThresholdsFor(20, 0), 140, 420, 420, 560);
        }

        [Test]
        public void AThreshold_IsAtLeastItsFloor()
        {
            // Half a barrel a day: 3.5 for T0, raised to 5; T1 is 5 + 7, above its floor of 10;
            // T2 is raised to 15, T3 is 15 + 5, which is its floor.
            AssertThresholds(Rule.ThresholdsFor(0.5, 0), 5, 12, 15, 20);
        }

        [Test]
        public void WithoutConsumption_TheThresholdsAreTheFloors()
        {
            AssertThresholds(Rule.ThresholdsFor(0, 0), 5, 10, 15, 20);
        }

        [Test]
        public void WithoutDays_TheThresholdsAreTheFloors()
        {
            var rule = new ThresholdRule(0, 0, 0, 5, 10, 15, 20);

            AssertThresholds(rule.ThresholdsFor(100, 100), 5, 10, 15, 20);
        }

        [Test]
        public void ARule_KeepsItsSettings()
        {
            ThresholdRule rule = Rule;

            Assert.AreEqual(7, rule.T0Days);
            Assert.AreEqual(14, rule.T1AddedDays);
            Assert.AreEqual(10, rule.T2ProductionDays);
            Assert.AreEqual(5, rule.T0Floor);
            Assert.AreEqual(10, rule.T1Floor);
            Assert.AreEqual(15, rule.T2Floor);
            Assert.AreEqual(20, rule.T3Floor);
        }

        [TestCase(-1, 14, 10, 5, 10, 15, 20)]
        [TestCase(7, -1, 10, 5, 10, 15, 20)]
        [TestCase(7, 14, double.NaN, 5, 10, 15, 20)]
        [TestCase(double.PositiveInfinity, 14, 10, 5, 10, 15, 20)]
        [TestCase(7, 14, 10, 0, 10, 15, 20)]
        [TestCase(7, 14, 10, 5, 10, 8, 20)]
        [TestCase(7, 14, 10, 5, 10, 15, double.PositiveInfinity)]
        [TestCase(7, 14, 10, double.NaN, 10, 15, 20)]
        public void RejectsSettingsThatMakeNoSense(
            double t0Days, double t1AddedDays, double t2ProductionDays,
            double t0Floor, double t1Floor, double t2Floor, double t3Floor)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ThresholdRule(t0Days, t1AddedDays, t2ProductionDays, t0Floor, t1Floor, t2Floor, t3Floor));
        }

        [TestCase(-1, 0)]
        [TestCase(2, -1)]
        [TestCase(double.NaN, 0)]
        [TestCase(2, double.PositiveInfinity)]
        public void ThresholdsFor_RejectsFiguresThatMakeNoSense(double dailyConsumption, double dailyProduction)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Rule.ThresholdsFor(dailyConsumption, dailyProduction));
        }
    }
}
```

- [ ] **Step 2: Refresh and run `ThresholdRuleTests`.** Expected: compile error, `ThresholdRule` does not exist.

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/Core/ThresholdRule.cs`:

```csharp
using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// How the four stock thresholds of a good in a city follow what the city consumes and
    /// produces of it, as in Patrician III: T0 is days of consumption, T1 adds days of
    /// consumption, T2 adds days of production, T3 adds T0 again. Each is at least its floor,
    /// in barrels, so that a village or a good little consumed keeps a range of prices.
    /// </summary>
    public sealed class ThresholdRule
    {
        /// <param name="t0Days">Days of consumption up to T0.</param>
        /// <param name="t1AddedDays">Days of consumption from T0 to T1.</param>
        /// <param name="t2ProductionDays">Days of production from T1 to T2: none for a good the city does not produce.</param>
        /// <param name="t0Floor">The lowest T0, in barrels, above 0.</param>
        /// <param name="t1Floor">The lowest T1, at least the T0 floor.</param>
        /// <param name="t2Floor">The lowest T2, at least the T1 floor.</param>
        /// <param name="t3Floor">The lowest T3, at least the T2 floor.</param>
        public ThresholdRule(
            double t0Days,
            double t1AddedDays,
            double t2ProductionDays,
            double t0Floor,
            double t1Floor,
            double t2Floor,
            double t3Floor)
        {
            RequireNotNegative(t0Days, nameof(t0Days));
            RequireNotNegative(t1AddedDays, nameof(t1AddedDays));
            RequireNotNegative(t2ProductionDays, nameof(t2ProductionDays));

            // The negated comparisons also reject NaN.
            if (!(t0Floor > 0d) || !(t1Floor >= t0Floor) || !(t2Floor >= t1Floor) || !(t3Floor >= t2Floor)
                || double.IsInfinity(t3Floor))
            {
                throw new ArgumentOutOfRangeException(nameof(t0Floor), "Floors are above 0, finite and in order.");
            }

            T0Days = t0Days;
            T1AddedDays = t1AddedDays;
            T2ProductionDays = t2ProductionDays;
            T0Floor = t0Floor;
            T1Floor = t1Floor;
            T2Floor = t2Floor;
            T3Floor = t3Floor;
        }

        public double T0Days { get; }

        public double T1AddedDays { get; }

        public double T2ProductionDays { get; }

        public double T0Floor { get; }

        public double T1Floor { get; }

        public double T2Floor { get; }

        public double T3Floor { get; }

        /// <summary>The thresholds of a good the city consumes and produces at these daily figures, in barrels.</summary>
        public StockThresholds ThresholdsFor(double dailyConsumption, double dailyProduction)
        {
            RequireNotNegative(dailyConsumption, nameof(dailyConsumption));
            RequireNotNegative(dailyProduction, nameof(dailyProduction));

            double t0 = Math.Max(T0Days * dailyConsumption, T0Floor);
            double t1 = Math.Max(t0 + T1AddedDays * dailyConsumption, T1Floor);
            double t2 = Math.Max(t1 + T2ProductionDays * dailyProduction, T2Floor);
            double t3 = Math.Max(t2 + t0, T3Floor);

            return new StockThresholds(t0, t1, t2, t3);
        }

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

- [ ] **Step 4: Refresh and run `ThresholdRuleTests`.** Expected: all pass. Then all EditMode tests: all pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/ThresholdRule.cs Assets/Scripts/Core/ThresholdRule.cs.meta Assets/Tests/EditMode/ThresholdRuleTests.cs Assets/Tests/EditMode/ThresholdRuleTests.cs.meta
git commit -m "Prix : seuils de stock d'après la consommation et la production

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Markets priced by the curves

This task changes constructors that `Core`, `Game`, `Editor` and the tests all call: nothing compiles between its first edit and its last, so it is one task. Its steps go test helpers and new tests first, then the code, then the remaining tests.

**Files:**
- Rewrite: `Assets/Scripts/Core/MarketPricing.cs`
- Modify: `Assets/Scripts/Core/MarketGood.cs`
- Modify: `Assets/Scripts/Core/CityMarket.cs`
- Create: `Assets/Scripts/Game/PriceFactors.cs`
- Modify: `Assets/Scripts/Game/EconomyDefinition.cs`
- Modify: `Assets/Scripts/Game/GoodDefinition.cs`
- Modify: `Assets/Scripts/Editor/WorldMapSetup.cs:85-125` and `:541-543`
- Create: `Assets/Tests/EditMode/TestMarkets.cs`
- Rewrite: `Assets/Tests/EditMode/MarketPricingTests.cs`
- Modify: `Assets/Tests/EditMode/TestEconomy.cs`, `CityMarketTests.cs`, `EconomyDefinitionTests.cs`, `TradeRoutePlannerTests.cs`, `TradeTests.cs`, `AiMerchantFleetTests.cs`, `CityMarketSectionTests.cs`, `DebugCitiesTableTests.cs`

**Interfaces:**
- Consumes: `StockThresholds`, `PriceCurve` (Task 1), `ThresholdRule` (Task 2).
- Produces:
  - `MarketPricing(PriceCurve buyCurve, PriceCurve sellCurve)`, properties `PriceCurve BuyCurve`, `PriceCurve SellCurve`, `long BuyPrice(long basePrice, double stock, StockThresholds thresholds)`, `long SellPrice(long basePrice, double stock, StockThresholds thresholds)`. `Price(...)` and the margin constructor are gone.
  - `MarketGood(long basePrice, double dailyConsumptionPerThousand, double productionRate, double ceilingDays, bool isFood = false)`; `MinPrice` and `MaxPrice` are gone.
  - `CityMarket(IReadOnlyList<MarketGood> goods, ThresholdRule thresholds, double startingDays, MarketPricing pricing, int daysPerWeek, CityPopulation population, FoodGrowth foodGrowth = null)`; `StockThresholds ThresholdsOf(int good)` replaces `double TargetOf(int good)`.
  - `PriceFactors` (Game, `[Serializable] struct`): `PriceFactors(double empty, double atT0, double atT1, double atT2, double atT3)`, properties `Empty`, `AtT0`, `AtT1`, `AtT2`, `AtT3`, `PriceCurve ToCurve()`.
  - `EconomyDefinition`: properties `T0Days`, `T1AddedDays`, `T2ProductionDays`, `T0Floor`, `T1Floor`, `T2Floor`, `T3Floor` (`double`), `BuyFactors`, `SellFactors` (`PriceFactors`); `ThresholdRule CreateThresholdRule()`; `TargetStockDays`, `BuyMargin`, `SellMargin` are gone.
  - `GoodDefinition`: `MinPrice` and `MaxPrice` are gone.
  - `TestMarkets` (tests): `ThresholdRule Thresholds`, `PriceCurve BuyCurve`, `PriceCurve SellCurve`, `MarketPricing Pricing`.

- [ ] **Step 1: Add the shared test helpers**

Create `Assets/Tests/EditMode/TestMarkets.cs`:

```csharp
using DarkFantasyMerchant.Core;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>The thresholds and prices of the markets the tests build.</summary>
    public static class TestMarkets
    {
        /// <summary>
        /// For a good the city does not produce: T0 at 10 days of consumption, T1 at 30, T2 at
        /// T1 and T3 at 40. A market that starts with 30 days of a good is at its threshold T1.
        /// </summary>
        public static ThresholdRule Thresholds => new ThresholdRule(10, 20, 10, 5, 10, 15, 20);

        /// <summary>The factors at which the player buys in Patrician III.</summary>
        public static PriceCurve BuyCurve => new PriceCurve(4, 1.5, 1, 0.8, 0.6);

        /// <summary>The factors at which the player sells in Patrician III, at the middle setting.</summary>
        public static PriceCurve SellCurve => new PriceCurve(2, 1.4, 1, 0.7, 0.5);

        public static MarketPricing Pricing => new MarketPricing(BuyCurve, SellCurve);
    }
}
```

In `Assets/Tests/EditMode/TestEconomy.cs`, replace the summary and remove the two lines that set `minPrice` and `maxPrice`:

```csharp
        /// <summary>A good priced from its base price by the economy's curves.</summary>
        public static GoodDefinition CreateGood(
```

and the body keeps:

```csharp
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("category").enumValueIndex = (int)category;
            serialized.FindProperty("basePrice").longValue = basePrice;
            serialized.FindProperty("weeklyConsumptionPerThousand").doubleValue = weeklyConsumptionPerThousand;
```

- [ ] **Step 2: Rewrite `MarketPricingTests`**

Replace the whole of `Assets/Tests/EditMode/MarketPricingTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class MarketPricingTests
    {
        private static readonly StockThresholds Thresholds = new StockThresholds(20, 40, 60, 80);

        // A good the city does not produce: T2 is T1.
        private static readonly StockThresholds Step = new StockThresholds(20, 60, 60, 80);

        private static MarketPricing Pricing => TestMarkets.Pricing;

        [Test]
        public void AroundT1_TheTwoPricesAreCloseToTheBasePrice()
        {
            // Bought: 100 x 1.0125, the average over [39, 40], rounded up.
            Assert.AreEqual(102, Pricing.BuyPrice(100, 40, Thresholds));

            // Sold: 100 x 0.9925, the average over [40, 41], rounded down.
            Assert.AreEqual(99, Pricing.SellPrice(100, 40, Thresholds));
        }

        [Test]
        public void BuyPrice_IsTheAverageOverTheBarrelThatLeaves_RoundedUp()
        {
            // The last barrel: 10 x 3.9375, the average from 4 at 0 to 3.875 at 1.
            Assert.AreEqual(40, Pricing.BuyPrice(10, 1, Thresholds));
        }

        [Test]
        public void SellPrice_IsTheAverageOverTheBarrelThatComesIn_RoundedDown()
        {
            // Into an empty stock: 10 x 1.985, the average from 2 at 0 to 1.97 at 1.
            Assert.AreEqual(19, Pricing.SellPrice(10, 0, Thresholds));
        }

        [Test]
        public void ABarrelPastAnEmptyStock_CostsTheEmptyFactor()
        {
            Assert.AreEqual(40, Pricing.BuyPrice(10, 0, Thresholds));
            Assert.AreEqual(40, Pricing.BuyPrice(10, -3, Thresholds));
        }

        [Test]
        public void Prices_ThatAreWholeNumbers_AreNotRoundedAway()
        {
            // 20 x 1.1 is 22.000000000000004 and 100 x 0.29 is 28.999999999999996 in floating
            // point: still 22 and 29, however far above T3.
            var pricing = new MarketPricing(new PriceCurve(4, 1.5, 1.2, 1.1, 1.1), new PriceCurve(2, 1.2, 1, 0.5, 0.29));

            for (int stock = 81; stock <= 1000; stock++)
            {
                Assert.AreEqual(22, pricing.BuyPrice(20, stock, Thresholds), $"stock {stock}");
                Assert.AreEqual(29, pricing.SellPrice(100, stock, Thresholds), $"stock {stock}");
            }

            Assert.AreEqual(22, pricing.BuyPrice(20, 100000, Thresholds));
            Assert.AreEqual(29, pricing.SellPrice(100, 100000, Thresholds));
        }

        [Test]
        public void BuyPrice_IsAtLeastOneCoin_AndSellPriceCanBeNothing()
        {
            // 1 x 0.6 and 1 x 0.5.
            Assert.AreEqual(1, Pricing.BuyPrice(1, 500, Thresholds));
            Assert.AreEqual(0, Pricing.SellPrice(1, 500, Thresholds));
        }

        [Test]
        public void ATradeOfSeveralBarrels_CostsTheIntegralOfTheCurve_UpToTheRounding()
        {
            MarketPricing pricing = Pricing;
            long bought = 0;
            long sold = 0;

            for (int i = 0; i < 50; i++)
            {
                bought += pricing.BuyPrice(10, 70 - i, Thresholds);
                sold += pricing.SellPrice(10, 20 + i, Thresholds);
            }

            // From 70 barrels down to 20, and from 20 up to 70.
            double boughtIntegral = 10 * 50 * pricing.BuyCurve.AverageFactor(20, 70, Thresholds);
            double soldIntegral = 10 * 50 * pricing.SellCurve.AverageFactor(20, 70, Thresholds);

            Assert.GreaterOrEqual(bought, boughtIntegral - 1e-6);
            Assert.Less(bought, boughtIntegral + 50);
            Assert.LessOrEqual(sold, soldIntegral + 1e-6);
            Assert.Greater(sold, soldIntegral - 50);
        }

        [TestCase(1)]
        [TestCase(10)]
        [TestCase(600)]
        public void BuyingABarrelAndSellingItBack_NeverGainsGold(long basePrice)
        {
            MarketPricing pricing = Pricing;

            foreach (StockThresholds thresholds in new[] { Thresholds, Step })
            {
                for (int stock = 1; stock <= 300; stock++)
                {
                    long paid = pricing.BuyPrice(basePrice, stock, thresholds);
                    long received = pricing.SellPrice(basePrice, stock - 1, thresholds);

                    Assert.LessOrEqual(received, paid, $"stock {stock}");
                }
            }
        }

        [Test]
        public void WithTheSameCurveForBoth_BuyingAndSellingBackIsStillNeverAGain()
        {
            var pricing = new MarketPricing(TestMarkets.BuyCurve, TestMarkets.BuyCurve);

            for (int stock = 1; stock <= 300; stock++)
            {
                Assert.LessOrEqual(pricing.SellPrice(10, stock - 1, Step), pricing.BuyPrice(10, stock, Step));
            }
        }

        [Test]
        public void RejectsASellCurveAboveTheBuyCurve_AtAnyPoint()
        {
            // 0.9 at T2, above the 0.8 at which the player buys.
            var above = new PriceCurve(2, 1.4, 1, 0.9, 0.5);

            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketPricing(TestMarkets.BuyCurve, above));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketPricing(TestMarkets.SellCurve, TestMarkets.BuyCurve));
        }

        [Test]
        public void RejectsMissingCurves()
        {
            Assert.Throws<ArgumentNullException>(() => new MarketPricing(null, TestMarkets.SellCurve));
            Assert.Throws<ArgumentNullException>(() => new MarketPricing(TestMarkets.BuyCurve, null));
        }
    }
}
```

- [ ] **Step 3: Refresh.** Expected: compile error, `MarketPricing` has no constructor that takes two curves. Go on: the code follows.

- [ ] **Step 4: Rewrite `MarketPricing`**

Replace the whole of `Assets/Scripts/Core/MarketPricing.cs`:

```csharp
using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What a barrel costs in a city, as in Patrician III: a good's base price times a factor
    /// that follows the city's stock against its thresholds. The player buys on one curve and
    /// sells on another, never above it. Every barrel is priced at the average factor over the
    /// stock it takes away or adds, so a trade of several barrels costs the integral of the curve.
    /// </summary>
    public sealed class MarketPricing
    {
        // Prices are worked out in floating point: 20 x 1.1 must round up to 22, not 23.
        private const double RoundingMargin = 1e-9;

        /// <param name="buyCurve">The factors of what the player pays.</param>
        /// <param name="sellCurve">The factors of what the player is paid, never above the buy curve.</param>
        public MarketPricing(PriceCurve buyCurve, PriceCurve sellCurve)
        {
            BuyCurve = buyCurve ?? throw new ArgumentNullException(nameof(buyCurve));
            SellCurve = sellCurve ?? throw new ArgumentNullException(nameof(sellCurve));

            // Both curves are straight lines between the same thresholds: checking the five
            // points checks every stock.
            if (sellCurve.Empty > buyCurve.Empty
                || sellCurve.AtT0 > buyCurve.AtT0
                || sellCurve.AtT1 > buyCurve.AtT1
                || sellCurve.AtT2 > buyCurve.AtT2
                || sellCurve.AtT3 > buyCurve.AtT3)
            {
                throw new ArgumentOutOfRangeException(nameof(sellCurve), "A barrel never sells for more than it costs.");
            }
        }

        public PriceCurve BuyCurve { get; }

        public PriceCurve SellCurve { get; }

        /// <summary>
        /// What the player pays for one barrel from a city whose stock is
        /// <paramref name="stock"/>: the base price times the average buy factor from the
        /// stock once the barrel is gone to the stock before, rounded up, at least one coin.
        /// </summary>
        public long BuyPrice(long basePrice, double stock, StockThresholds thresholds)
        {
            double price = basePrice * BuyCurve.AverageFactor(stock - 1d, stock, thresholds);

            return Math.Max(1L, (long)Math.Ceiling(price - RoundingMargin));
        }

        /// <summary>
        /// What the player is paid for one barrel by a city whose stock is
        /// <paramref name="stock"/>: the base price times the average sell factor from the
        /// stock before to the stock once the barrel is in, rounded down. Buying a barrel and
        /// selling it back never gains gold.
        /// </summary>
        public long SellPrice(long basePrice, double stock, StockThresholds thresholds)
        {
            double price = basePrice * SellCurve.AverageFactor(stock, stock + 1d, thresholds);

            return Math.Max(0L, (long)Math.Floor(price + RoundingMargin));
        }
    }
}
```

- [ ] **Step 5: Drop the bounds from `MarketGood`**

In `Assets/Scripts/Core/MarketGood.cs`, replace the summary, the constructor and the price properties so that the file reads:

```csharp
using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The figures of one good in one city: its base price, and what the city consumes and
    /// produces of it for the inhabitants it has.
    /// </summary>
    public readonly struct MarketGood
    {
        /// <param name="basePrice">Gold per barrel that the factors of the price curves multiply, at least 1.</param>
        /// <param name="dailyConsumptionPerThousand">Barrels 1 000 inhabitants consume in a day; it can be a fraction.</param>
        /// <param name="productionRate">Production as a share of the consumption; 0 for a good the city does not produce.</param>
        /// <param name="ceilingDays">Days of consumption above which the city's production stops raising the stock.</param>
        /// <param name="isFood">Whether the stock of the good feeds the city: only food makes its population change.</param>
        public MarketGood(
            long basePrice,
            double dailyConsumptionPerThousand,
            double productionRate,
            double ceilingDays,
            bool isFood = false)
        {
            if (basePrice < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(basePrice));
            }

            RequireNotNegative(dailyConsumptionPerThousand, nameof(dailyConsumptionPerThousand));
            RequireNotNegative(productionRate, nameof(productionRate));
            RequireNotNegative(ceilingDays, nameof(ceilingDays));

            BasePrice = basePrice;
            DailyConsumptionPerThousand = dailyConsumptionPerThousand;
            ProductionRate = productionRate;
            CeilingDays = ceilingDays;
            IsFood = isFood;
        }

        public long BasePrice { get; }

        public double DailyConsumptionPerThousand { get; }

        public double ProductionRate { get; }

        public double CeilingDays { get; }

        public bool IsFood { get; }

        public bool IsProduced => ProductionRate > 0d;

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

- [ ] **Step 6: Give `CityMarket` a threshold rule**

In `Assets/Scripts/Core/CityMarket.cs`:

1. In the class summary, replace `the price of a good follows its stock against a target.` by `the price of a good follows its stock against thresholds that follow its consumption and production.`
2. Replace the field `private readonly double targetDays;` by `private readonly ThresholdRule thresholdRule;`.
3. Replace the `targetDays` parameter, its doc and its check. The constructor's head becomes:

```csharp
        /// <param name="thresholds">How the stock thresholds of a good, at which its prices are given, follow what the city consumes and produces of it.</param>
        /// <param name="startingDays">Days of consumption the city holds of every good when the game starts.</param>
        /// <param name="daysPerWeek">Days of the calendar's week, over which the weekly figures are given.</param>
        /// <param name="population">The inhabitants of the city, who set what it consumes.</param>
        /// <param name="foodGrowth">How food changes the population. Without it, the population stays what it is.</param>
        public CityMarket(
            IReadOnlyList<MarketGood> goods,
            ThresholdRule thresholds,
            double startingDays,
            MarketPricing pricing,
            int daysPerWeek,
            CityPopulation population,
            FoodGrowth foodGrowth = null)
        {
            if (goods == null)
            {
                throw new ArgumentNullException(nameof(goods));
            }

            thresholdRule = thresholds ?? throw new ArgumentNullException(nameof(thresholds));

            // The negated comparisons also reject NaN.
            if (!(startingDays >= 0d) || double.IsInfinity(startingDays))
```

   and delete the line `this.targetDays = targetDays;` further down.
4. Replace `TargetOf` by:

```csharp
        /// <summary>The stocks at which the prices of a good are given, for the population of the moment.</summary>
        public StockThresholds ThresholdsOf(int good)
        {
            RequireGood(good);

            double consumption = DailyConsumptionOf(good);

            return thresholdRule.ThresholdsFor(consumption, consumption * goods[good].ProductionRate);
        }
```

5. Replace the bodies of `BuyPriceOf` and `SellPriceOf` after their two `Require` calls:

```csharp
            return pricing.BuyPrice(goods[good].BasePrice, StockOf(good) - barrelsAlreadyBought, ThresholdsOf(good));
```

```csharp
            return pricing.SellPrice(goods[good].BasePrice, StockOf(good) + barrelsAlreadySold, ThresholdsOf(good));
```

   (the local `MarketGood figures` goes).

- [ ] **Step 7: Add `PriceFactors`**

Create `Assets/Scripts/Game/PriceFactors.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The five factors of a price curve, as a setting of the economy: what a good's base price
    /// is multiplied by at an empty stock and at each of the four thresholds.
    /// </summary>
    [Serializable]
    public struct PriceFactors
    {
        [Tooltip("At an empty stock.")]
        [SerializeField, Min(0f)] private double empty;

        [Tooltip("At T0. Not above the factor at an empty stock.")]
        [SerializeField, Min(0f)] private double atT0;

        [Tooltip("At T1. Not above the factor at T0.")]
        [SerializeField, Min(0f)] private double atT1;

        [Tooltip("At T2. Not above the factor at T1.")]
        [SerializeField, Min(0f)] private double atT2;

        [Tooltip("At T3 and above. Not above the factor at T2.")]
        [SerializeField, Min(0f)] private double atT3;

        public PriceFactors(double empty, double atT0, double atT1, double atT2, double atT3)
        {
            this.empty = empty;
            this.atT0 = atT0;
            this.atT1 = atT1;
            this.atT2 = atT2;
            this.atT3 = atT3;
        }

        public double Empty => empty;

        public double AtT0 => atT0;

        public double AtT1 => atT1;

        public double AtT2 => atT2;

        public double AtT3 => atT3;

        /// <summary>The curve of these factors. Throws when they are not above 0 or rise with the stock.</summary>
        public PriceCurve ToCurve()
        {
            return new PriceCurve(empty, atT0, atT1, atT2, atT3);
        }
    }
}
```

- [ ] **Step 8: Change the settings of `EconomyDefinition`**

In `Assets/Scripts/Game/EconomyDefinition.cs`:

1. Delete the `targetStockDays` field with its tooltip, the `TargetStockDays` property, the `Margins` header with the `buyMargin` and `sellMargin` fields, and the `BuyMargin` and `SellMargin` properties.
2. After the `inefficientCeilingDays` field, add:

```csharp
        [Header("Stock thresholds T0 to T3 of a good in a city, at which its prices are given")]
        [Tooltip("T0: days of the city's consumption.")]
        [SerializeField, Min(0f)] private double t0Days = 7d;

        [Tooltip("T1: T0 plus these days of consumption.")]
        [SerializeField, Min(0f)] private double t1AddedDays = 14d;

        [Tooltip("T2: T1 plus these days of the city's production; T2 is T1 for a good the city does not produce. T3 is T2 plus T0.")]
        [SerializeField, Min(0f)] private double t2ProductionDays = 10d;

        [Tooltip("The lowest T0, in barrels. Above 0.")]
        [SerializeField, Min(0f)] private double t0Floor = 5d;

        [Tooltip("The lowest T1, in barrels. At least the T0 floor.")]
        [SerializeField, Min(0f)] private double t1Floor = 10d;

        [Tooltip("The lowest T2, in barrels. At least the T1 floor.")]
        [SerializeField, Min(0f)] private double t2Floor = 15d;

        [Tooltip("The lowest T3, in barrels. At least the T2 floor.")]
        [SerializeField, Min(0f)] private double t3Floor = 20d;

        [Header("Prices, as factors of a good's base price")]
        [Tooltip("What the player pays when buying from a city.")]
        [SerializeField] private PriceFactors buyFactors = new PriceFactors(4d, 1.5d, 1d, 0.8d, 0.6d);

        [Tooltip("What the player is paid when selling to a city. At no point above the buy factors.")]
        [SerializeField] private PriceFactors sellFactors = new PriceFactors(2d, 1.4d, 1d, 0.7d, 0.5d);
```

3. After the `InefficientCeilingDays` property, add:

```csharp
        public double T0Days => t0Days;

        public double T1AddedDays => t1AddedDays;

        public double T2ProductionDays => t2ProductionDays;

        public double T0Floor => t0Floor;

        public double T1Floor => t1Floor;

        public double T2Floor => t2Floor;

        public double T3Floor => t3Floor;

        public PriceFactors BuyFactors => buyFactors;

        public PriceFactors SellFactors => sellFactors;
```

4. Replace `CreatePricing` and add `CreateThresholdRule` after it:

```csharp
        /// <summary>
        /// The pricing of these settings. Throws when one of them is unusable: the
        /// attributes only constrain the Inspector, not the file.
        /// </summary>
        public MarketPricing CreatePricing()
        {
            return new MarketPricing(buyFactors.ToCurve(), sellFactors.ToCurve());
        }

        /// <summary>
        /// How the stock thresholds of a good follow a city's consumption and production, from
        /// these settings. Throws when one of them is unusable.
        /// </summary>
        public ThresholdRule CreateThresholdRule()
        {
            return new ThresholdRule(t0Days, t1AddedDays, t2ProductionDays, t0Floor, t1Floor, t2Floor, t3Floor);
        }
```

5. In `CreateMarket`, the good becomes:

```csharp
                marketGoods[i] = new MarketGood(
                    good.BasePrice,
                    good.WeeklyConsumptionPerThousand / daysPerWeek,
                    rate,
                    ceilingDays,
                    good.Category == GoodCategory.Food);
```

   and the market:

```csharp
            return new CityMarket(
                marketGoods,
                CreateThresholdRule(),
                startingStockDays,
                pricing,
                daysPerWeek,
                new CityPopulation(Math.Max(0, city.Population)),
                foodGrowth);
```

- [ ] **Step 9: Keep only the base price in `GoodDefinition` and `WorldMapSetup`**

In `Assets/Scripts/Game/GoodDefinition.cs`, delete the `minPrice` and `maxPrice` fields with their tooltips and the `MinPrice` and `MaxPrice` properties, and give `basePrice` this tooltip:

```csharp
        [Tooltip("Gold coins a barrel costs, before the factor of the city's stock (see the economy's price factors).")]
        [SerializeField, Min(1)] private long basePrice = 10;
```

In `Assets/Scripts/Editor/WorldMapSetup.cs`:

1. In `SampleGood`, remove the `minPrice` and `maxPrice` parameters, their assignments and the `MinPrice` and `MaxPrice` properties, so that the constructor is `SampleGood(string assetName, string displayName, GoodCategory category, long basePrice, double weeklyConsumptionPerThousand)`.
2. Replace the comment and table:

```csharp
        // In the order of the catalogue, which is their order on screen. Category, base price,
        // then what 1 000 inhabitants consume in a week: the values a good that is missing is
        // created with, which are then balanced by hand in the asset.
        private static readonly SampleGood[] SampleGoods =
        {
            new SampleGood("Fish", "Poisson", GoodCategory.Food, 12, 10.5),
            new SampleGood("Iron", "Fer", GoodCategory.RawMaterial, 50, 2.8),
            new SampleGood("Beer", "Bière", GoodCategory.Food, 50, 50),
            new SampleGood("Grain", "Grain", GoodCategory.Food, 80, 40),
            new SampleGood("Meat", "Viande", GoodCategory.Food, 600, 8),
            new SampleGood("Vegetables", "Légumes", GoodCategory.Food, 100, 30),
        };
```

3. In the method that creates a good, delete the two lines that set `minPrice` and `maxPrice`.

- [ ] **Step 10: Adapt `CityMarketTests`**

In `Assets/Tests/EditMode/CityMarketTests.cs`:

1. Replace the head of the class, up to `FedMarket`, by:

```csharp
        private const double Tolerance = 1e-9;

        // The tests start a good at its threshold T1 unless they say otherwise: 30 days.
        private const double StartingDays = 30;

        private const int DaysPerWeek = 7;

        private static ThresholdRule Thresholds => TestMarkets.Thresholds;

        private static MarketPricing Pricing => TestMarkets.Pricing;

        // Two barrels a day, base price 10.
        private static MarketGood NotProduced => new MarketGood(10, 2, 0, 0);
        private static MarketGood Efficient => new MarketGood(10, 2, 1.5, 60);
        private static MarketGood Inefficient => new MarketGood(10, 2, 1.1, 36);

        // Food, two barrels a day for a thousand inhabitants.
        private static MarketGood Food => new MarketGood(10, 2, 0, 0, isFood: true);
```

2. In the whole file, replace every `TargetDays` argument of `new CityMarket(` by `Thresholds`.
3. Replace the other `MarketGood`s, dropping the first and third numbers: `new MarketGood(20, 40, 100, 0.3, 0, 0)` → `new MarketGood(40, 0.3, 0, 0)` (twice), `new MarketGood(5, 10, 25, 2.4, 0, 0)` → `new MarketGood(10, 2.4, 0, 0)`, `new MarketGood(5, 10, 25, 4, 0, 0, isFood: true)` → `new MarketGood(10, 4, 0, 0, isFood: true)`, `new MarketGood(5, 10, 25, 0, 0, 0, isFood: true)` → `new MarketGood(10, 0, 0, 0, isFood: true)`.
4. `TargetOf` becomes the threshold T1: in `AGood_StartsWithTheStartingDaysOfItsConsumption` and `EveryGood_StartsWithTheSameDays_ProducedOrNot`, `Assert.AreEqual(60, market.TargetOf(0), Tolerance);` → `Assert.AreEqual(60, market.ThresholdsOf(0).T1, Tolerance);`; in `WhatACityConsumes_FollowsItsInhabitants`, `Assert.AreEqual(150, market.TargetOf(0), Tolerance);` → `Assert.AreEqual(150, market.ThresholdsOf(0).T1, Tolerance);`.
5. Replace the tests `Prices_AreThoseOfTheNextBarrel`, `Prices_OfLaterBarrels_FollowTheStockTheyWouldLeave` and `AGoodTheCityDoesNotConsume_IsAtItsBasePrice_AndOutOfStock` by:

```csharp
        [Test]
        public void Prices_AreThoseOfTheNextBarrel()
        {
            CityMarket market = MarketOf(NotProduced);

            // Stock 60, the threshold T1. Bought: 10 x 1.00625, the average buy factor over [59, 60].
            Assert.AreEqual(11, market.BuyPriceOf(0));

            // Sold: T2 is T1 for a good the city does not produce, so the barrel is already on
            // the segment from T2 to T3: 10 x 0.695.
            Assert.AreEqual(6, market.SellPriceOf(0));
        }

        [Test]
        public void Prices_OfLaterBarrels_FollowTheStockTheyWouldLeave()
        {
            CityMarket market = MarketOf(NotProduced);

            // The last of the 60 barrels: 10 x 3.9375, the average over [0, 1].
            Assert.AreEqual(40, market.BuyPriceOf(0, 59));

            // The 61st barrel sold: 120 in stock, above T3 at 80, so 10 x 0.5.
            Assert.AreEqual(5, market.SellPriceOf(0, 60));
        }

        [Test]
        public void AGoodTheCityDoesNotConsume_IsPricedAgainstTheFloors()
        {
            CityMarket market = MarketOf(new MarketGood(10, 0, 0, 0));
            StockThresholds thresholds = market.ThresholdsOf(0);

            Assert.AreEqual(5, thresholds.T0, Tolerance);
            Assert.AreEqual(10, thresholds.T1, Tolerance);
            Assert.AreEqual(15, thresholds.T2, Tolerance);
            Assert.AreEqual(20, thresholds.T3, Tolerance);
            Assert.AreEqual(0, market.AvailableOf(0));

            // An empty stock: 10 x 4 to buy, 10 x 1.94 (the average over [0, 1]) to sell.
            Assert.AreEqual(40, market.BuyPriceOf(0));
            Assert.AreEqual(19, market.SellPriceOf(0));
        }

        [Test]
        public void ThresholdsOf_FollowTheProduction()
        {
            CityMarket market = MarketOf(NotProduced, Efficient, Inefficient);

            // Two barrels a day consumed; 0, 3 and 2.2 produced: T2 adds 10 days of production.
            StockThresholds notProduced = market.ThresholdsOf(0);
            StockThresholds efficient = market.ThresholdsOf(1);
            StockThresholds inefficient = market.ThresholdsOf(2);

            Assert.AreEqual(20, notProduced.T0, Tolerance);
            Assert.AreEqual(60, notProduced.T1, Tolerance);
            Assert.AreEqual(60, notProduced.T2, Tolerance);
            Assert.AreEqual(80, notProduced.T3, Tolerance);
            Assert.AreEqual(90, efficient.T2, Tolerance);
            Assert.AreEqual(110, efficient.T3, Tolerance);
            Assert.AreEqual(82, inefficient.T2, Tolerance);
            Assert.AreEqual(102, inefficient.T3, Tolerance);
        }
```

6. Delete `Prices_StayBetweenTheMinimumAndTheMaximumOfTheGood` and `MarketGood_AcceptsBoundsEqualToItsBasePrice`.
7. Replace `MarketGood_RejectsFiguresThatMakeNoSense`'s body by:

```csharp
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(0, 2, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, -1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, double.NaN, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, 2, -0.5, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, 2, 1.5, -1));
```

8. Replace `RejectsANegativeTarget_AndMissingArguments` by:

```csharp
        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new CityMarket(new[] { NotProduced }, null, StartingDays, Pricing, DaysPerWeek, new CityPopulation(1000)));
            Assert.Throws<ArgumentNullException>(() => new CityMarket(null, Thresholds, StartingDays, Pricing, DaysPerWeek, new CityPopulation(1000)));
            Assert.Throws<ArgumentNullException>(() => new CityMarket(new[] { NotProduced }, Thresholds, StartingDays, null, DaysPerWeek, new CityPopulation(1000)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityMarket(new[] { NotProduced }, Thresholds, StartingDays, Pricing, 0, new CityPopulation(1000)));
            Assert.Throws<ArgumentNullException>(() => new CityMarket(new[] { NotProduced }, Thresholds, StartingDays, Pricing, DaysPerWeek, null));
        }
```

- [ ] **Step 11: Adapt `EconomyDefinitionTests`**

In `Assets/Tests/EditMode/EconomyDefinitionTests.cs`:

1. In `ANewDefinition_HasTheGamesSettings`, replace `Assert.AreEqual(10, fresh.TargetStockDays);` by nothing, and the two margin asserts by:

```csharp
            Assert.AreEqual(7, fresh.T0Days);
            Assert.AreEqual(14, fresh.T1AddedDays);
            Assert.AreEqual(10, fresh.T2ProductionDays);
            Assert.AreEqual(5, fresh.T0Floor);
            Assert.AreEqual(10, fresh.T1Floor);
            Assert.AreEqual(15, fresh.T2Floor);
            Assert.AreEqual(20, fresh.T3Floor);
            Assert.AreEqual(4, fresh.BuyFactors.Empty);
            Assert.AreEqual(1.5, fresh.BuyFactors.AtT0);
            Assert.AreEqual(1, fresh.BuyFactors.AtT1);
            Assert.AreEqual(0.8, fresh.BuyFactors.AtT2);
            Assert.AreEqual(0.6, fresh.BuyFactors.AtT3);
            Assert.AreEqual(2, fresh.SellFactors.Empty);
            Assert.AreEqual(1.4, fresh.SellFactors.AtT0);
            Assert.AreEqual(1, fresh.SellFactors.AtT1);
            Assert.AreEqual(0.7, fresh.SellFactors.AtT2);
            Assert.AreEqual(0.5, fresh.SellFactors.AtT3);
```

2. In `CreateMarket_StocksEveryGoodOfTheCatalogue_FromThePopulation`, replace the last comment and assert by:

```csharp
            // Salt is not produced: T0 is 7 days, T1 14 more.
            Assert.AreEqual(21, market.ThresholdsOf(2).T0, Tolerance);
            Assert.AreEqual(63, market.ThresholdsOf(2).T1, Tolerance);
```

3. In `CreateMarket_SpreadsTheWeeklyConsumptionOverTheDaysOfTheWeek`, replace the comment and assert by:

```csharp
            // Wheat: 84 barrels a week, over a week of 10 days; T0 is 7 days of it.
            CityMarket market = economy.CreateMarket(city, economy.CreatePricing(), 10);

            Assert.AreEqual(8.4 * 7, market.ThresholdsOf(0).T0, Tolerance);
```

4. Replace `CreatePricing_UsesTheDefinitionsMargins`, `CreateMarket_PricesAGoodBetweenItsOwnMinimumAndMaximum` and `ANewGood_HasBoundsAroundItsBasePrice` by:

```csharp
        [Test]
        public void CreatePricing_UsesTheDefinitionsFactors()
        {
            MarketPricing pricing = economy.CreatePricing();

            Assert.AreEqual(4, pricing.BuyCurve.Empty);
            Assert.AreEqual(0.6, pricing.BuyCurve.AtT3);
            Assert.AreEqual(2, pricing.SellCurve.Empty);
            Assert.AreEqual(0.5, pricing.SellCurve.AtT3);
        }

        [Test]
        public void CreatePricing_RejectsSellFactorsAboveTheBuyFactors()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("sellFactors.atT1").doubleValue = 1.2;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<System.ArgumentOutOfRangeException>(() => economy.CreatePricing());
        }

        [Test]
        public void CreateThresholdRule_UsesTheDefinitionsDaysAndFloors()
        {
            // Two barrels a day consumed and four produced.
            StockThresholds thresholds = economy.CreateThresholdRule().ThresholdsFor(2, 4);

            Assert.AreEqual(14, thresholds.T0, Tolerance);
            Assert.AreEqual(42, thresholds.T1, Tolerance);
            Assert.AreEqual(82, thresholds.T2, Tolerance);
            Assert.AreEqual(96, thresholds.T3, Tolerance);
        }

        [Test]
        public void CreateMarket_ThresholdsFollowTheProductionOfTheCity()
        {
            // Wheat efficient (12 a day, 24 produced), fish inefficient (9, 13.5), salt not produced (3).
            CityDefinition city = City(6000, new[] { wheat }, new[] { fish });
            CityMarket market = economy.CreateMarket(city, economy.CreatePricing(), DaysPerWeek);

            Assert.AreEqual(492, market.ThresholdsOf(0).T2, Tolerance);
            Assert.AreEqual(576, market.ThresholdsOf(0).T3, Tolerance);
            Assert.AreEqual(324, market.ThresholdsOf(1).T2, Tolerance);
            Assert.AreEqual(387, market.ThresholdsOf(1).T3, Tolerance);
            Assert.AreEqual(63, market.ThresholdsOf(2).T2, Tolerance);
            Assert.AreEqual(84, market.ThresholdsOf(2).T3, Tolerance);
        }

        [Test]
        public void CreateMarket_PricesAGoodByTheCurvesAndItsThresholds()
        {
            // Salt: base price 18, 3 barrels a day, 21 in stock: T0 exactly.
            CityDefinition city = City(6000, new GoodDefinition[0], new GoodDefinition[0]);
            CityMarket market = economy.CreateMarket(city, economy.CreatePricing(), DaysPerWeek);

            // Bought: 18 x 1.5595, the average over [20, 21] from 1.619 to 1.5.
            Assert.AreEqual(29, market.BuyPriceOf(2));

            // Sold: 18 x 1.3952, the average over [21, 22] from 1.4 to 1.3905.
            Assert.AreEqual(25, market.SellPriceOf(2));
        }
```

- [ ] **Step 12: Adapt the other test files**

The rule for every remaining call: `new MarketGood(min, base, max, consumption, rate, ceiling[, isFood])` becomes `new MarketGood(base, consumption, rate, ceiling[, isFood])`; `new MarketPricing(0.05, 0.05)` becomes `TestMarkets.Pricing`; the second argument of `new CityMarket(`, `30`, becomes `TestMarkets.Thresholds`.

- `Assets/Tests/EditMode/TradeTests.cs:19-22`: `new CityMarket(new[] { new MarketGood(10, 2, 0, 0) }, TestMarkets.Thresholds, 30, TestMarkets.Pricing, 7, new CityPopulation(1000))`; the comment above it becomes `// One good at base price 10, two barrels a day: 60 in stock, the threshold T1.`
- `Assets/Tests/EditMode/AiMerchantFleetTests.cs:50-53`: the market becomes `new CityMarket(new[] { new MarketGood(10, 2, 0, 0) }, TestMarkets.Thresholds, days, TestMarkets.Pricing, 7, new CityPopulation(1000))`; its summary becomes `/// <summary>One good, 2 barrels a day, its threshold T1 at 60 barrels.</summary>`.
- `Assets/Tests/EditMode/CityMarketSectionTests.cs:36-42` and `:244-247`: `new MarketGood(10, 2, 0, 0)`, `new MarketGood(60, 0, 0, 0)`, `TestMarkets.Thresholds`, `TestMarkets.Pricing`.
- `Assets/Tests/EditMode/DebugCitiesTableTests.cs:42-48`, `:51-57`, `:211-218`, `:320-321`, `:395-396`: `new MarketGood(10, 2, 0, 0)`, `new MarketGood(50, 2, 1.5, 60)`, `new MarketGood(50, 1, 0, 0)`, `new MarketGood(10, 2, 0, 0, isFood: true)`, `TestMarkets.Thresholds`, `TestMarkets.Pricing`.
- `Assets/Tests/EditMode/TradeRoutePlannerTests.cs`:
  - `Market` (`:20-26`): `new[] { new MarketGood(10, 2, 0, 0), new MarketGood(50, 2, 0, 0) }`, `TestMarkets.Thresholds`, `TestMarkets.Pricing`; the comment above the constants becomes `// Days of consumption a market starts with. T1 is 30 days.` and the one above `Market` `// Two goods, 2 barrels a day each: 60 barrels at T1, 120 in surplus, above T3.`
  - `MarketsOfDifferentCatalogues_TradeTheGoodsTheyShare` (`:260-261`): `new CityMarket(new[] { new MarketGood(10, 2, 0, 0) }, TestMarkets.Thresholds, 0, TestMarkets.Pricing, 7, new CityPopulation(1000))`.
  - Replace `LoadingStopsAtTheStock`: a city that does not consume a good now sells its last barrels at the empty-stock factor, so the destination must pay far more for it:

```csharp
        [Test]
        public void LoadingStopsAtTheStock()
        {
            // The source does not consume fish and holds 10 barrels: its last one costs
            // 10 x 3.75. The destination, empty, prices fish at 100 and pays at least 170 a
            // barrel: only the stock stops the loading.
            var source = new CityMarket(
                new[] { new MarketGood(10, 0, 0, 0), new MarketGood(50, 0, 0, 0) },
                TestMarkets.Thresholds,
                0,
                TestMarkets.Pricing,
                7,
                new CityPopulation(1000));
            source.Put(Fish, 10);
            var destination = new CityMarket(
                new[] { new MarketGood(100, 2, 0, 0), new MarketGood(50, 2, 0, 0) },
                TestMarkets.Thresholds,
                0,
                TestMarkets.Pricing,
                7,
                new CityPopulation(1000));
            var planner = new TradeRoutePlanner(new[] { source, destination }, Days(2, 1d), 1d);

            Assert.IsTrue(planner.TryPlanTrade(0, 200, null, out TradePlan plan));
            Assert.AreEqual(10, plan.BarrelsOf(Fish));
            Assert.AreEqual(0, plan.BarrelsOf(Iron));
        }
```

- [ ] **Step 13: Refresh, read the console for compile errors, run all EditMode tests.** Expected: no compile error, all pass.

  The scenarios of `TradeRoutePlannerTests`, `AiMerchantFleetTests` and `TradeTests` keep their meaning with `TestMarkets.Thresholds` (empty: under T0, 30 days: T1, 60 days: above T3) and are expected to pass unchanged. If one fails, do not change `TradeRoutePlanner` or `AiMerchantFleet`: print the prices of the barrels concerned (`BuyPriceOf` / `SellPriceOf` of source and destination), check them against the model with the curve of the task, and adjust the days the test's markets start with so that its comment holds again, saying why in that comment.

  Then search for anything left of the old model; expected: no match outside `docs/`.

```bash
grep -rn "MinPrice\|MaxPrice\|minPrice\|maxPrice\|TargetOf\|TargetStockDays\|targetStockDays\|BuyMargin\|SellMargin\|buyMargin\|sellMargin" Assets/Scripts Assets/Tests
```

- [ ] **Step 14: Commit**

```bash
git add Assets/Scripts Assets/Tests
git commit -m "Prix : courbes d'achat et de vente de Patrician III sur les seuils de stock

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Assets, documentation and a game run

**Files:**
- Modify: `Assets/Data/Economy/Economy.asset`
- Modify: `Assets/Data/Goods/Beer.asset`, `Fish.asset`, `Grain.asset`, `Iron.asset`, `Meat.asset`, `Vegetables.asset`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: the fields of `EconomyDefinition`, `PriceFactors` and `GoodDefinition` (Task 3).
- Produces: nothing new.

- [ ] **Step 1: Clean the good assets**

In each of the six files of `Assets/Data/Goods/`, delete the `minPrice:` and `maxPrice:` lines and leave `basePrice:` as it is (beer 50, fish 500, grain 80, iron 300, meat 600, vegetables 100).

- [ ] **Step 2: Set the economy's new settings**

In `Assets/Data/Economy/Economy.asset`, delete the `targetStockDays: 10`, `buyMargin: 0.05` and `sellMargin: 0.05` lines, and after `inefficientCeilingDays: 36` insert:

```yaml
  t0Days: 7
  t1AddedDays: 14
  t2ProductionDays: 10
  t0Floor: 5
  t1Floor: 10
  t2Floor: 15
  t3Floor: 20
  buyFactors:
    empty: 4
    atT0: 1.5
    atT1: 1
    atT2: 0.8
    atT3: 0.6
  sellFactors:
    empty: 2
    atT0: 1.4
    atT1: 1
    atT2: 0.7
    atT3: 0.5
```

Refresh, select `Economy.asset` in the Project window and check in the Inspector that the two headers show these values. Run all EditMode tests: all pass.

- [ ] **Step 3: Update `CLAUDE.md`**

In the *Economy* section of `CLAUDE.md`:

1. In the first bullet, replace `its minimum, base and maximum price,` by `its base price,`.
2. In the bullet that starts `A day of a city, per good:`, replace the end `At the start a city holds the same days of its consumption of every good, produced or not (`EconomyDefinition.startingStockDays`, 7): under the target, so prices open above the base price, producers fill up over the weeks and the others run out within the first one. Both ceilings are far above twice the target, so a producer left alone ends at its minimum price.` by:

```markdown
At the start a city holds the same days of its consumption of every good, produced or not (`EconomyDefinition.startingStockDays`, 7): T0, so prices open at 1.5 and 1.4 times the base price, producers fill up over the weeks and the others run out within the first one. An efficient producer left alone rises above its T3 (48 days) and ends at the lowest factors; an inefficient one stops at its ceiling, which is its T2 (36 days), at 0.8 and 0.7.
```

3. Replace the `MarketPricing` bullet by:

```markdown
- Prices follow Patrician III (`patrician3-re\resultats\Calcul des prix.md`): a good's base price times a factor of the city's stock. `ThresholdRule` (Core) gives the four thresholds of a good in a city (`CityMarket.ThresholdsOf`, a `StockThresholds`), worked out when a price is asked so that they follow the population: T0 = 7 days of consumption, T1 = T0 + 14 days of consumption, T2 = T1 + 10 days of the city's production (T1 itself for a good it does not produce), T3 = T2 + T0, each at least its floor (5, 10, 15, 20 barrels), so a good the city does not consume is priced against the floors. A `PriceCurve` (Core) is five factors, at an empty stock and at T0 to T3, in straight lines between them and constant above T3; two equal thresholds make a step. `MarketPricing` holds two: the player buys at 4.0, 1.5, 1.0, 0.8, 0.6 and sells at 2.0, 1.4, 1.0, 0.7, 0.5, never above the first at any point (checked when it is created). A barrel is bought at the base price times the average buy factor over the stock it takes away, rounded up, and sold at the average sell factor over the stock it adds, rounded down, so a trade of several barrels costs the integral of the curve and buying and selling back never gains gold. Every figure is a setting of `EconomyDefinition` (`PriceFactors` for the curves); the base prices are balanced by hand in the good assets and no test pins their values.
```

4. In the bullet that starts `What a city consumes of a good`, replace `production, ceilings and price targets` by `production, ceilings and price thresholds`.
5. In the bullet that starts `Production is a share`, replace `and sits near the maximum price until then` by `and pays high prices for it until then`.
6. In the *AI merchants* section, in the `TradeRoutePlanner` bullet, replace `(the destination's sell price minus the source's buy price, the real ones, margins included)` by `(the destination's sell price minus the source's buy price, each on its own curve)`.

- [ ] **Step 4: Run the game in the Editor**

Open `Assets/Scenes/WorldMap.unity`, enter Play mode and check, writing down what is seen:
- The console shows no error or exception.
- The panel of a city shows, on the first day, buy prices about 1.5 times the base price and sell prices about 1.4 times (e.g. grain about 120 and 112), the same in every city.
- With the player's ship in port, buying 10 barrels of one good (Shift-click) raises its next buy price, and selling them back gives less gold than they cost.
- After a few days of fast forward, producers show low prices for what they produce and the others high ones; the AI ships (F1, "Navires") start trading and their profit rises.

If the AI ships never trade after two weeks of game time, report it with the prices seen rather than changing the factors: balancing is out of scope.

- [ ] **Step 5: Commit**

```bash
git add Assets/Data/Economy/Economy.asset Assets/Data/Goods CLAUDE.md
git commit -m "Prix : réglages des seuils et des facteurs, et CLAUDE.md

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
