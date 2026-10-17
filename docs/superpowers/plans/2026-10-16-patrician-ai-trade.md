# AI Merchants Trade Like Patrician III — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** AI ships regulate supply as in Patrician III: each has a home city, makes round trips to a final destination with stopovers on the way back, and loads quantities given in one closed formula on the stock thresholds, ranked by weighted gap of price factors — at a cost that no longer depends on the size of the holds.

**Architecture:** A pure `ShipmentQuantity` gives the quantity of one good between two stocks. A new `SupplyPlanner` (Core) evaluates a good between two ports (projected stock, quantity, score), chooses a final destination, a stopover and a cargo. `AiMerchantFleet` is rewritten around the tour (home, final destination, next port, cities visited on the return, under-way records per ship) and uses the planner; `TradeRoutePlanner`, `TradePlan` and `StartPortDraw` go away. In `Game`, goods get an AI weight, the AI definition a number of ships per city, and the debug tools show each ship's home and final destination.

**Tech Stack:** Unity 6000.6.4f1, C#, UI Toolkit, Unity Test Framework (NUnit, EditMode), Python for the asset files.

**Spec:** `docs/superpowers/specs/2026-10-16-patrician-ai-trade-design.md`

## Global Constraints

- All code, comments, log messages, asset and folder names are in English. Everything the player reads is in French: "Attache", "Destination finale", "—".
- `Core` contains no `MonoBehaviour` and nothing that needs a scene, and does not reference `Game`.
- Values pinned by the spec: smallest lot 5 units for a good counted in barrels, 3 for a good counted in bushels; target point `T0 + 3/8 × (T1 − T0)`; stopover detour `< 1.2 ×` the direct trip; second loading only with **more than** a quarter of the hold free; `shipsPerCity` = 1 (at least 0); default AI weight 9; weights 16 grain, salt; 11 timber, metal, wool; 8 pitch, pig iron, tiles; 9 every other good.
- Gold stays unlimited; the stopover stays `stopoverDays` (1); ship names stay "Marchand indépendant N", numbered in spawn order.
- Never hand-write `.meta` files: Unity generates them on import. A deleted script loses its `.meta` with it.
- Never edit the generated `.sln` / `.csproj`. Exclude `Library/` from searches.
- Match the surrounding code: Allman braces, a blank line before `return` and after blocks as in the existing files, XML `<summary>` on public types and members, "The negated comparison also rejects NaN." where a negated comparison validates.
- Commit directly on `main`. Commit messages are in French, in the form "Marchands IA : …", and end with:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
- Differences from the spec's wording, intentional:
  - The new planner is a new class, `SupplyPlanner`, so that Task 4 compiles beside the old `TradeRoutePlanner`, which Task 5 deletes.
  - The weights reach `Core` as a list given to the planner (`EconomyDefinition.CreateAiWeights`), not inside `MarketGood`: a weight is the same in every city. A planner without weights gives every good a weight of 1.
  - A ship that enters a port other than its home with no final destination (only possible when sent back to its last port from the sea) takes home as its final destination.

## Review Focus

Conditions the spec implies and no step's test would otherwise exercise, most likely first:

1. A ship removed from the fleet while it carries cargo for a port: the other ships must stop counting that delivery, or the port is never supplied again. Test in Task 5 (`ARemovedShip_NoLongerCountsAsUnderWay`).
2. A hold left with a quarter or less free after the loading for home: the stopover is dropped and the ship sails home, instead of sailing to a stopover it carries nothing for. Test in Task 5 (`WithAQuarterOfTheHoldOrLessFree_ItDropsTheStopover`).
3. A hold with room left under a bushel while the best good is counted in bushels: the bushel is skipped and goods in barrels still load. Test in Task 4 (`Load_SkipsABushelThatNoLongerFits_AndLoadsBarrels`).
4. Degenerate thresholds (T0 = T1, a zero range on either side): no division by zero, no NaN. Test in Task 3 (`DegenerateThresholds_GiveAFiniteQuantity`).
5. A port whose own cargo for home already covers home's lack: reloading at a stopover buys nothing more for home. Test in Task 5 (`AtAStopover_ItDoesNotBuyAgainWhatItCarriesForHome`).

## Commands

The Unity Editor is normally **open** on this project. Then use the `unity-mcp` tools: `Unity_RunCommand` to refresh the asset database and to run the EditMode tests, `Unity_GetConsoleLogs` to read compile errors.

Refresh and compile (run after every file change, before the tests):

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
        testNames = null, // or groupNames = new[] { "DarkFantasyMerchant\\.Tests\\.EditMode\\.SupplyPlannerTests" }
    }));
```

The run writes `C:\Users\Spellman\AppData\LocalLow\DefaultCompany\Dark Fantasy Merchant\TestResults.xml`: read its `<test-run>` line and the `<test-case ... result="Failed">` entries. `Unity_RunCommand` refuses code that writes files with `System.IO` or needs interaction; log to the console instead. In Play mode, the unfocused Editor runs at a few frames: set `Application.runInBackground = true` for the run and back to false after.

When the Editor is closed, use the batch-mode commands of `CLAUDE.md` ("Commands").

## File Structure

| File | Responsibility |
|---|---|
| `Assets/Scripts/Game/GoodDefinition.cs`, `EconomyDefinition.cs` (modify) | The AI weight of a good; the list of weights; its validation. |
| `Assets/Scripts/Editor/WorldMapSetup.cs` (modify) | Weights of the sample goods. |
| `Assets/Data/Goods/*.asset` (modify) | The twenty weights. |
| `Assets/Scripts/Core/CityMarket.cs` (modify) | Price factors at a stock; daily production. |
| `Assets/Scripts/Core/ShipmentQuantity.cs` (new) | The closed-form quantity of one good between two stocks; the smallest lot. |
| `Assets/Scripts/Core/SupplyPlanner.cs` (new) | Projection, evaluation, destination, stopover, cargo. |
| `Assets/Scripts/Core/AiMerchantFleet.cs` (rewrite) | The tour and the under-way records. |
| `Assets/Scripts/Core/TradeRoutePlanner.cs`, `TradePlan.cs`, `StartPortDraw.cs` and their tests (delete) | The old model. |
| `Assets/Scripts/Core/HomePortChoice.cs` (new) | The home of a ship added by the debug tools. |
| `Assets/Scripts/Game/AiMerchantsDefinition.cs`, `AiMerchants.cs` (modify), `Assets/Data/AI/AiMerchants.asset` (modify) | Ships per city, homes, the tour for the debug tools. |
| `Assets/Scripts/Game/DebugShipsTable.cs`, `DebugPanelController.cs` (modify) | The "Attache" and "Destination finale" columns. |
| `CLAUDE.md` (modify) | Project state, Economy, AI merchants, Debug tools. |

---

### Task 0: Baseline

- [ ] **Step 1: Run every EditMode test before any change**

Run all EditMode tests (Commands). Note every test that already fails: those are not this plan's to fix, and no other test may fail at the end of any task.

---

### Task 1: AI weights of the goods

**Files:**
- Modify: `Assets/Scripts/Game/GoodDefinition.cs`, `Assets/Scripts/Game/EconomyDefinition.cs`, `Assets/Scripts/Editor/WorldMapSetup.cs`, the twenty `Assets/Data/Goods/*.asset`, `CLAUDE.md`
- Test: `Assets/Tests/EditMode/EconomyDefinitionTests.cs`, `Assets/Tests/EditMode/EconomyContentTests.cs`, `Assets/Tests/EditMode/TestEconomy.cs`

**Interfaces:**
- Produces: `GoodDefinition.AiWeight` (int, serialized field `aiWeight`, default 9, `[Min(0)]`); `int[] EconomyDefinition.CreateAiWeights()` (by catalogue index; throws `ArgumentOutOfRangeException` whose message names the good's asset for a negative weight); `TestEconomy.CreateGood(..., int aiWeight = 9)`.

- [ ] **Step 1: Write the failing tests**

In `EconomyDefinitionTests.cs`:

```csharp
[Test]
public void ANewGood_HasAnAiWeightOfNine()
{
    var good = ScriptableObject.CreateInstance<GoodDefinition>();
    Assert.AreEqual(9, good.AiWeight);
    Object.DestroyImmediate(good);
}

[Test]
public void CreateAiWeights_GivesTheWeightOfEveryGood_InTheCataloguesOrder()
{
    GoodDefinition grain = Track(TestEconomy.CreateGood("Céréales", 110, 4.725, aiWeight: 16));
    GoodDefinition tiles = Track(TestEconomy.CreateGood("Tuiles", 79.8, 0.016, aiWeight: 8));
    EconomyDefinition mixed = Track(TestEconomy.CreateEconomy(grain, tiles));

    CollectionAssert.AreEqual(new[] { 16, 8 }, mixed.CreateAiWeights());
}

[Test]
public void TryValidateSettings_SaysWhichGoodHasANegativeAiWeight()
{
    GoodDefinition salt = Track(TestEconomy.CreateGood("Sel", 28.5, 0.35, aiWeight: -1));
    EconomyDefinition broken = Track(TestEconomy.CreateEconomy(salt));

    Assert.IsFalse(broken.TryValidateSettings(out string problem));
    StringAssert.Contains("AI weight", problem);
    StringAssert.Contains("Sel", problem);
}
```

(`Track` is the fixture's helper that destroys in `TearDown`; reuse it as `CreateGoodUnits_GivesABushelTheBarrelsPerBushel` does. `TestEconomy.CreateGood` names the asset after `displayName`.)

In `EconomyContentTests.cs`, add an `int aiWeight` parameter to the twenty `TestCase`s of `TheCatalogue_HasPatricianIIIsGoods_InOrder`, with the weights of Global Constraints (grain 16, meat 9, fish 9, beer 9, salt 16, honey 9, spices 9, wine 9, cloth 9, skins 9, oil 9, timber 11, metal 11, leather 9, wool 11, pitch 8, pig iron 8, hemp 9, pottery 9, tiles 8), and assert `Assert.AreEqual(aiWeight, good.AiWeight);`.

- [ ] **Step 2: Run them to verify they fail**

Expected: compile error (`AiWeight`, `CreateAiWeights`, `aiWeight` unknown).

- [ ] **Step 3: Implement**

- `GoodDefinition`: `[Tooltip("How much the AI merchants care to carry the good, as in Patrician III: the gap of price factors between two cities is multiplied by it. 0: they never carry it.")] [SerializeField, Min(0)] private int aiWeight = 9;` and `public int AiWeight => aiWeight;`.
- `EconomyDefinition.CreateAiWeights()`: one entry per good of the catalogue; a negative weight throws `new ArgumentOutOfRangeException(nameof(goods), $"'{good.name}' has a negative AI weight.")`.
- `TryValidateSettings`: add `?? ProblemOf(() => CreateAiWeights(), "the AI weights")` after the barrels per bushel.
- `TestEconomy.CreateGood`: an optional `int aiWeight = 9` written to `aiWeight`.
- `WorldMapSetup`: `SampleGood` gets an `AiWeight` (last constructor parameter), the twenty samples get the weights above, and `CreateGood` writes `aiWeight`.
- Assets: append `  aiWeight: N` after the `dailyOutputPerWorker` line of each of the twenty `Assets/Data/Goods/*.asset` (asset name → weight: Grain 16, Meat 9, Fish 9, Beer 9, Salt 16, Honey 9, Spices 9, Wine 9, Cloth 9, Skins 9, Oil 9, Timber 11, Metal 11, Leather 9, Wool 11, Pitch 8, PigIron 8, Hemp 9, Pottery 9, Tiles 8), with a short Python script; keep the files' line endings.
- `CLAUDE.md`, Economy, first bullet: `GoodDefinition` also holds the weight the AI merchants give the good (see AI merchants).

- [ ] **Step 4: Refresh, run the tests, verify they pass**

Run `EconomyDefinitionTests` and `EconomyContentTests`. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Game/GoodDefinition.cs Assets/Scripts/Game/EconomyDefinition.cs Assets/Scripts/Editor/WorldMapSetup.cs Assets/Data/Goods Assets/Tests/EditMode CLAUDE.md
git commit -m "Marchands IA : poids des marchandises de Patrician III"
```

---

### Task 2: Price factors and daily production of a market

**Files:**
- Modify: `Assets/Scripts/Core/CityMarket.cs`
- Test: `Assets/Tests/EditMode/CityMarketTests.cs`

**Interfaces:**
- Produces: `double CityMarket.BuyFactorAt(int good, double stock)` (`pricing.BuyCurve.FactorAt(stock, ThresholdsOf(good))`), `double CityMarket.SellFactorAt(int good, double stock)` (same on `SellCurve`), `double CityMarket.DailyProductionOf(int good)` (workers of the moment × daily output per worker; 0 for a good the city does not produce). All throw `ArgumentOutOfRangeException` for a good out of range.

- [ ] **Step 1: Write the failing tests**

```csharp
[Test]
public void TheFactors_AreReadOnTheCurves_AtTheGivenStock()
{
    // Two barrels a day: T0 20, T1 60, T2 60, T3 80.
    CityMarket market = MarketOf(NotProduced);

    Assert.AreEqual(4.0, market.BuyFactorAt(0, 0), Tolerance);
    Assert.AreEqual(1.25, market.BuyFactorAt(0, 40), Tolerance);
    Assert.AreEqual(0.6, market.BuyFactorAt(0, 120), Tolerance);
    Assert.AreEqual(2.0, market.SellFactorAt(0, 0), Tolerance);
    Assert.AreEqual(1.2, market.SellFactorAt(0, 40), Tolerance);
    Assert.AreEqual(1.0, market.SellFactorAt(0, 60), Tolerance);
}

[Test]
public void DailyProduction_IsTheWorkersOfTheMomentTimesTheirOutput()
{
    CityMarket market = MarketOf(NotProduced, Efficient);

    Assert.AreEqual(0d, market.DailyProductionOf(0), Tolerance);
    Assert.AreEqual(3d, market.DailyProductionOf(1), Tolerance);
    Assert.AreEqual(market.NominalWeeklyProductionOf(1) / DaysPerWeek, market.DailyProductionOf(1), Tolerance);
}

[Test]
public void TheFactorsAndTheProduction_RejectAGoodOutOfRange()
{
    CityMarket market = MarketOf(NotProduced);

    Assert.Throws<ArgumentOutOfRangeException>(() => market.BuyFactorAt(1, 0));
    Assert.Throws<ArgumentOutOfRangeException>(() => market.SellFactorAt(-1, 0));
    Assert.Throws<ArgumentOutOfRangeException>(() => market.DailyProductionOf(1));
}
```

- [ ] **Step 2: Run them to verify they fail**

Expected: compile error (methods unknown).

- [ ] **Step 3: Implement the three methods in `CityMarket`**, each starting with `RequireGood(good)`, with a `<summary>` ("The factor at which the city sells the good to a buyer when its stock is `stock`, as the AI merchants weigh it: one point of the curve, no integral." and its sell counterpart).

- [ ] **Step 4: Run `CityMarketTests`, verify they pass**

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/CityMarket.cs Assets/Tests/EditMode/CityMarketTests.cs
git commit -m "Marchands IA : facteurs de prix et production du jour d'un marché"
```

---

### Task 3: Shipment quantity

**Files:**
- Create: `Assets/Scripts/Core/ShipmentQuantity.cs`
- Test: `Assets/Tests/EditMode/ShipmentQuantityTests.cs` (new)

**Interfaces:**
- Produces: `public static class ShipmentQuantity` with `const int SmallestBarrelLot = 5`, `const int SmallestBushelLot = 3`, `static int SmallestLotFor(int unitRoom)` (3 when a unit takes more than one barrel, else 5), and `static double Of(double sourceStock, StockThresholds source, double projectedStock, StockThresholds destination)`: the units to carry, not rounded, never negative, never NaN.

The algorithm, as the spec gives it (steps 1–4; rounding and the smallest lot are the planner's):

```
midA = (A.T0 + A.T1) / 2
if a <= midA or b >= B.T1: 0
need = B.T1 − b
if a − A.T1 >= need: need
q1 = max(0, a − A.T1)
targetB = B.T0 + 0.375 × (B.T1 − B.T0)
q2 = max(0, min(targetB − (b + q1), (a − q1) − midA))
a' = a − q1 − q2 ; b' = b + q1 + q2
ra = A.T1 − midA ; rb = B.T1 − targetB
q3 = (b' >= targetB and a' > midA and ra + rb > 0)
     ? max(0, ((a' − midA) × rb − (b' − targetB) × ra) / (ra + rb)) : 0
q1 + q2 + q3
```

- [ ] **Step 1: Write the failing tests**

Thresholds of both cities in every test unless said: `new StockThresholds(10, 30, 30, 40)` (mid 20, target 17.5).

```csharp
private static readonly StockThresholds Usual = new StockThresholds(10, 30, 30, 40);

[TestCase(20)]
[TestCase(15)]
public void ASourceAtOrUnderTheMiddleOfT0AndT1_GivesNothing(double sourceStock)
{
    Assert.AreEqual(0d, ShipmentQuantity.Of(sourceStock, Usual, 0, Usual));
}

[TestCase(30)]
[TestCase(45)]
public void ADestinationAtOrAboveT1_GetsNothing(double projected)
{
    Assert.AreEqual(0d, ShipmentQuantity.Of(100, Usual, projected, Usual));
}

[Test]
public void ASurplusAboveT1ThatCoversTheNeed_GivesTheNeed()
{
    Assert.AreEqual(10d, ShipmentQuantity.Of(60, Usual, 20, Usual), 1e-9);
}

[Test]
public void UpToTheTargetPoint_TheSourceGivesDownToItsMiddle()
{
    // No surplus; 8 from 28 down to 20, short of the target 17.5.
    Assert.AreEqual(8d, ShipmentQuantity.Of(28, Usual, 0, Usual), 1e-9);
}

[Test]
public void TheThreeSteps_SurplusTargetAndEqualRelativePosition()
{
    // 10 above T1, then 7.5 up to the target, then 1.3889 so that both end at the same share of their range.
    Assert.AreEqual(18.888888889, ShipmentQuantity.Of(40, Usual, 0, Usual), 1e-6);
}

[Test]
public void AboveTheTargetPoint_TheRestIsShared()
{
    // 20 of surplus, then 4.444: the source ends at 25.556 and the destination at 24.444, both at 55.6 %.
    Assert.AreEqual(24.444444444, ShipmentQuantity.Of(50, Usual, 0, Usual), 1e-6);
}

[Test]
public void DegenerateThresholds_GiveAFiniteQuantity()
{
    var flat = new StockThresholds(10, 10, 10, 10);

    Assert.AreEqual(0d, ShipmentQuantity.Of(10, flat, 0, Usual));
    Assert.AreEqual(10d, ShipmentQuantity.Of(20, flat, 0, flat), 1e-9);
    // 5 above T1, then nothing: both ranges are empty, and nothing is divided by them.
    double quantity = ShipmentQuantity.Of(15, flat, 0, flat);
    Assert.IsFalse(double.IsNaN(quantity));
    Assert.AreEqual(5d, quantity, 1e-9);
}

[TestCase(1, 5)]
[TestCase(10, 3)]
public void TheSmallestLot_IsFiveBarrelsOrThreeBushels(int unitRoom, int lot)
{
    Assert.AreEqual(lot, ShipmentQuantity.SmallestLotFor(unitRoom));
}
```

- [ ] **Step 2: Run them to verify they fail** — compile error.
- [ ] **Step 3: Implement `ShipmentQuantity`** with the algorithm above; the summary names Patrician III's rules (the source never below the middle of T0 and T1, the destination never above T1).
- [ ] **Step 4: Run `ShipmentQuantityTests`, verify they pass**
- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/ShipmentQuantity.cs Assets/Tests/EditMode/ShipmentQuantityTests.cs
git commit -m "Marchands IA : quantité à transporter calculée sur les seuils"
```

---

### Task 4: Supply planner

**Files:**
- Create: `Assets/Scripts/Core/SupplyPlanner.cs`
- Test: `Assets/Tests/EditMode/SupplyPlannerTests.cs` (new)

**Interfaces:**
- Consumes: `ShipmentQuantity.Of`, `ShipmentQuantity.SmallestLotFor` (Task 3); `CityMarket.BuyFactorAt`, `SellFactorAt`, `DailyProductionOf` (Task 2).
- Produces: `public sealed class SupplyPlanner`:
  - `SupplyPlanner(IReadOnlyList<CityMarket> markets, double[,] travelDays, GoodUnits units = null, IReadOnlyList<int> weights = null)` — same argument checks as `TradeRoutePlanner`'s (null markets or days, a missing market, days not ports × ports); `GoodCount` is the smallest `GoodCount` of the markets; a negative weight throws `ArgumentOutOfRangeException`; a good beyond `weights`, or every good without them, weighs 1. `travelDays` is read on every call (it is filled after the planner is made).
  - `int PortCount`, `int GoodCount`, `const double DetourLimit = 1.2`.
  - `bool IsReachable(int from, int to)` — finite, not negative, not NaN.
  - `double ProjectedStockOf(int port, int good, double travelDays, int underWay)` = `max(0, stock + underWay + (DailyProductionOf − DailyConsumptionOf) × travelDays)`.
  - `bool TryEvaluate(int from, int to, int good, int[,] underWay, out int units, out double score)` — false (units 0, score 0) when `from == to`, `to` unreachable, the weight is 0, the floored quantity is under the smallest lot, or the score is 0 or below. `underWay` is `[port, good]` or null for none. Floors with the `+ 1e-9` margin `CityMarket` uses.
  - `int ChooseDestination(int home, int[,] underWay)` — -1 for none.
  - `int ChooseStopover(int port, int home, int[,] underWay, IReadOnlyList<bool> excluded)` — -1 for none; `excluded` by port, null for none.
  - `int Load(int from, int to, int room, int[,] underWay, int[] units)` — fills `units` (cleared first, length at least `GoodCount`), returns the barrels of room the cargo takes.
  - Arguments: a port out of range throws `ArgumentOutOfRangeException`, a negative room too; an `underWay` smaller than ports × goods, an `excluded` shorter than the ports, or a `units` shorter than the goods throws `ArgumentException`.

Rules the methods follow (from the spec):

- Score: `(markets[to].SellFactorAt(good, b) − markets[from].BuyFactorAt(good, a)) × weight`, with `a = StockOf` of the source and `b` the projected stock over `travelDays[from, to]` with `underWay[to, good]`.
- Destination: over every reachable `C ≠ home`, the sum over goods of the score home → C when it is a transport, else the score C → home when that is one (projected over `travelDays[C, home]` with `underWay[home, good]`), else 0. The highest sum above 0; on a tie the lowest index. The trip's length divides nothing.
- Stopover: candidates `C` reachable from `port`, from which home is reachable, `C ≠ port`, `C ≠ home`, not excluded, and `travelDays[port, C] + travelDays[C, home] < DetourLimit × travelDays[port, home]`. Value = sum over goods of the score port → C when it is a transport. Highest value above 0; tie → shortest detour (`travelDays[port, C] + travelDays[C, home]`); then lowest index.
- Load: every good that is a transport from → to, in decreasing score (tie → lowest index), each `min(units, AvailableOf, room / RoomOf(good))`; any whole unit that fits is taken once the good is chosen, even fewer than the smallest lot. The cut to `AvailableOf` is a guard: the formula never gives more than the source holds, so it has no test.

- [ ] **Step 1: Write the failing tests**

Helpers in the fixture: `Market(double stock, double dailyConsumption = 2, int maxWorkers = 0, double output = 0)` built with `TestMarkets` (population 1000, so consumption is per day; thresholds T0 = 10 days, T1 = 30 days, T3 = 40 days of it; with no consumption, the floors 5, 10, 15, 20), its stock set by `startingDays = stock / dailyConsumption` (or by `Put` when consumption is 0); `MarketPricedBy(MarketPricing pricing)`, an empty market of the same good on other curves; `Days(int n, double value)` filling a symmetric matrix with `value`, 0 on the diagonal; `Planner(params CityMarket[] markets)` on `Days(n, 1)`, and `Planner(CityMarket[] markets, double[,] days, GoodUnits units = null, int[] weights = null)`. Factors are the curves of `TestMarkets`.

```csharp
[Test]
public void TheProjectedStock_AddsWhatIsUnderWay_AndTheDaysOfTheTrip()
{
    // 2 consumed a day; 40 in stock; 4 under way; 5 days: 40 + 4 − 10.
    var planner = Planner(Market(40), Market(0));
    Assert.AreEqual(34d, planner.ProjectedStockOf(0, 0, 5, 4), 1e-9);
}

[Test]
public void TheProjectedStock_CountsTheProductionOfTheDays()
{
    // A workshop of 30 workers making 0.1 a day: 3 a day, 2 consumed.
    var planner = Planner(Market(40, 2, 30, 0.1), Market(0));
    Assert.AreEqual(45d, planner.ProjectedStockOf(0, 0, 5, 0), 1e-9);
}

[Test]
public void TheProjectedStock_IsNeverBelowNothing()
{
    var planner = Planner(Market(6), Market(0));
    Assert.AreEqual(0d, planner.ProjectedStockOf(0, 0, 5, 0));
}

[Test]
public void TheScore_IsTheGapOfFactors_TimesTheWeight()
{
    // A at 120 (above T3: buys at 0.6), B empty (sells at 2.0); weight 16.
    var planner = Planner(new[] { Market(120), Market(0) }, Days(2, 1), weights: new[] { 16 });

    Assert.IsTrue(planner.TryEvaluate(0, 1, 0, null, out int units, out double score));
    Assert.AreEqual(60, units);
    Assert.AreEqual((2.0 - 0.6) * 16, score, 1e-9);
}

[Test]
public void AWeightOfZero_IsNeverCarried()
{
    var planner = Planner(new[] { Market(120), Market(0) }, Days(2, 1), weights: new[] { 0 });
    Assert.IsFalse(planner.TryEvaluate(0, 1, 0, null, out _, out _));
}

[Test]
public void AScoreOfZeroOrBelow_IsNoTransport()
{
    // The destination pays 0.5 at any stock: under the 0.6 the source sells at.
    CityMarket flat = MarketPricedBy(new MarketPricing(TestMarkets.BuyCurve, new PriceCurve(0.5, 0.5, 0.5, 0.5, 0.5)));
    var planner = Planner(Market(120), flat);
    Assert.IsFalse(planner.TryEvaluate(0, 1, 0, null, out _, out _));
}

[Test]
public void UnderTheSmallestLot_IsNoTransport()
{
    // B projected at 56: 4 short of T1, under 5 barrels.
    var planner = Planner(new[] { Market(120), Market(58) }, Days(2, 1));
    Assert.IsFalse(planner.TryEvaluate(0, 1, 0, null, out _, out _));
}

[Test]
public void WhatIsUnderWay_LowersTheQuantity()
{
    // B projected at 0 + 30 − 2 = 28: 32 short of T1.
    var underWay = new int[2, 1];
    underWay[1, 0] = 30;
    var planner = Planner(new[] { Market(120), Market(0) }, Days(2, 1));

    Assert.IsTrue(planner.TryEvaluate(0, 1, 0, underWay, out int units, out _));
    Assert.AreEqual(32, units);
}

[Test]
public void TheDestination_IsTheHighestTotal()                  // two goods; C2's two scores beat C1's one
[Test]
public void TheDestination_CountsWhatItWouldBringBack()         // home has no surplus; C has what home lacks → C
[Test]
public void TheDestination_LeavesOutHome_AndUnreachableCities() // the only needy city unreachable → -1
[Test]
public void WithNothingWorthItAnywhere_ThereIsNoDestination()   // every market at 15 days (30, under the middle 40) → -1
[Test]
public void TheTripsLength_DividesNothing()
// Source at 120 (2 a day); near city at 1 day with 1 in stock, far city at 10 days with 0, both consuming
// nothing (thresholds at the floors 5, 10, 15, 20): the far city scores 1.4, the near one 1.28 → far.

[Test]
public void AStopover_MustBeAShortDetour()
// port 0, home 1, candidate 2: days 0–1 = 2; 0–2 = 1 and 2–1 = 1.3 (2.3 < 2.4) → 2; with 2–1 = 1.4 (2.4) → -1.
[Test]
public void TheStopover_IsWhereTheShipIsMostUseful()           // two candidates passing the detour; the higher value wins
[Test]
public void AStopoverTie_GoesToTheShortestDetour()             // two equal candidates, detours 2.1 and 2.2 → the 2.1 one
[Test]
public void AnExcludedCity_IsNoStopover()                      // the only candidate excluded → -1
[Test]
public void WithoutACandidate_ThereIsNoStopover()              // → -1

[Test]
public void Load_TakesTheGoods_ByDecreasingScore_WithinTheRoom()
// Two goods: weight 16 and 9, same stocks and needs (60 each); room 80 → 60 of the first, 20 of the second; returns 80.
[Test]
public void Load_CountsABushelForTenBarrels()
// GoodUnits { 10 }: source with a surplus of 60 bushels, room 25 → 2 bushels; returns 20.
[Test]
public void Load_SkipsABushelThatNoLongerFits_AndLoadsBarrels()
// GoodUnits { 10, 1 }: the bushel good scores higher; room 9 → 0 bushels, 9 barrels.
[Test]
public void Load_TakesTheUnitsThatFit_EvenUnderTheSmallestLot()
// Quantity 60, room 3 → 3.
[Test]
public void TheArguments_AreChecked()
// TryEvaluate with a port out of range; Load with room -1; an underWay of [1, 1]; excluded of length 1 for 3 ports; units of length 0.
```

Write each test in the style of the first ones, with the setup its comment gives and the result as its assertion.

- [ ] **Step 2: Run them to verify they fail** — compile error.
- [ ] **Step 3: Implement `SupplyPlanner`** following the rules above. Keep a reusable score array for `Load`'s ordering; no allocation per call beyond it.
- [ ] **Step 4: Run `SupplyPlannerTests`, verify they pass**
- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/SupplyPlanner.cs Assets/Tests/EditMode/SupplyPlannerTests.cs
git commit -m "Marchands IA : planificateur d'approvisionnement à la façon de Patrician III"
```

---

### Task 5: The tour

**Files:**
- Modify (rewrite): `Assets/Scripts/Core/AiMerchantFleet.cs`
- Modify: `Assets/Scripts/Game/AiMerchants.cs` (`StartFleet` only), `CLAUDE.md`
- Delete: `Assets/Scripts/Core/TradeRoutePlanner.cs`, `Assets/Scripts/Core/TradePlan.cs`, `Assets/Tests/EditMode/TradeRoutePlannerTests.cs`, with their `.meta`
- Test: `Assets/Tests/EditMode/AiMerchantFleetTests.cs` (rewrite)

**Interfaces:**
- Consumes: `SupplyPlanner` (Task 4), `EconomyDefinition.CreateAiWeights` (Task 1).
- Produces:
  - `AiMerchantFleet(ShipDocking<TPort> docking, IReadOnlyList<TPort> ports, IReadOnlyList<Vector2> portPositions, IReadOnlyList<CityMarket> markets, SupplyPlanner planner, double stopoverSeconds)` — same checks as today.
  - `void Add(Ship ship, int home)` — the ship's home, where it is ordered to.
  - `int HomeOf(Ship ship)`, `int FinalDestinationOf(Ship ship)` — -1 for a ship not of the fleet, and for no final destination.
  - `int ShipsHomedAt(int port)` — ships of the fleet whose home it is.
  - `int UnderWayTo(int port, int good)` — the sum of every ship's records.
  - `Count`, `Contains`, `ProfitOf`, `Remove`, `Advance` unchanged in signature.

State of a merchant: `Ship`, `Profit`, `Wait`, `LastPort`, `IsStranded` (as today), and `Home`, `FinalDestination` (-1 none), `NextPort`, `bool[] Visited` (cities of this return trip), `int[,] Records` (units loaded for a port, by port then good). The fleet keeps `int[,] underWay`, the sum of every ship's records, changed only through private helpers that update both (record, cut to aboard, clear a port, clear all).

On docking at port `p` (a port of the fleet):
1. `LastPort = p`, not stranded, `Wait = stopoverSeconds`; clear the ship's records for `p`.
2. If `p == Home`: sell everything; clear `Visited`; `FinalDestination = planner.ChooseDestination(Home, underWay)`; `NextPort = FinalDestination`.
3. Else: if `FinalDestination < 0`, it becomes `Home`. If `p == FinalDestination`: sell everything, `FinalDestination = Home`. Otherwise sell partially: per good aboard, `keep = ceil(max(0, home.T1 − home.StockOf) − 1e-9)`, sell `floor(min(aboard − keep, max(0, T1 − StockOf)) + 1e-9)` when above 0. Then `Visited[p] = true`; cut every record to what is aboard of its good; `NextPort = planner.ChooseStopover(p, Home, underWay, Visited)`, or `Home` when -1.

At the end of the stopover (`Depart`), in port `p`:
1. Not a port of the fleet: wait another stopover (as today).
2. At home with `FinalDestination < 0`: choose it again; still none → wait another stopover.
3. Load for `FinalDestination` (`planner.Load(p, FinalDestination, Cargo.Free, underWay, units)`), buy each good (`Trade.Buy`, profit −= gold), record the units bought for `FinalDestination`.
4. If `NextPort != FinalDestination`: if `Cargo.Free * 4 > Cargo.Capacity`, load, buy and record the same way for `NextPort`; otherwise `NextPort = FinalDestination`.
5. Order the ship to `NextPort`.

`Remove` clears the ship's records. A ship idle at sea is sent back to `LastPort`, as today.

- [ ] **Step 1: Rewrite the fleet tests**

Fixture: keep `Port`, the grid, `Fish`, `Speed`, `Stopover`; ports `H = 0` (home), `D = 1`, `S = 2`, plus `S2 = 3` at `GridAssert.CellCenter(grid, 7, 0)` for the last test; `Market(double days, double dailyConsumption = 2)` (one good, base price 10, no workshop; T0 10 days, T1 30 days, middle 20 days, target 17.5 days, T3 40 days of its consumption). Days: H–D 2, D–S 1, S–H 1.1, symmetric (for S2, see its test). Planner: `new SupplyPlanner(markets, days)`. Helpers `AddShip(int home)`, `Arrive(Ship)` as today, `Depart()` = `fleet.Advance(Stopover)`.

Every number below follows from the spec's formulas on these markets. If one disagrees with the code, check the code against the spec before touching the test.

Kept from today, adapted: `AShipAdded_EntersItsHome` (also `HomeOf == H`), `DuringTheStopover_ItStaysInPort`, `ALongStep_BuysOnce`, `AShipThatIsNotOfTheFleet_IsLeftAlone`, `AShipIdleAtSea_IsSentBackToThePortItLeft`, `ARemovedShip_NoLongerTrades`, `ARemovedShip_NoLongerSellsWhenItDocks`, `Remove_OfAShipThatIsNotOfTheFleet_IsRefused`. Removed: the tests of sailing empty to a source and of claimed ports.

New, scenario **out and back** — H at 60 days (120), D at 0, S at 20 days (40), all 2 a day:

```csharp
[Test]
public void FromHome_ItSailsStraightToItsFinalDestination()
{
    // D needs 60 (score 1.4), S 22 (0.62): D. S would be a short detour, but none is made on the way out.
    CreateFleet(Market(60), Market(0), Market(20));
    Ship ship = AddShip(H);

    Assert.AreEqual(D, fleet.FinalDestinationOf(ship));
    Depart();

    Assert.AreSame(ports[D], docking.DestinationPortOf(ship));
    Assert.AreEqual(60, ship.Cargo.UnitsOf(Fish));
    Assert.AreEqual(60, fleet.UnderWayTo(D, Fish));
    Assert.Less(fleet.ProfitOf(ship), 0);
}

[Test]
public void AtItsFinalDestination_ItUnloadsEverything_AndHeadsHomeByAStopover()
{
    // … then Arrive: D holds 60, the hold is empty, nothing under way to D, home is the final destination.
    // Departing from D: nothing for home (H at T1: a quantity of 1.78, under a lot), 9 for S; sails to S.
}

[Test]
public void BackHome_ItUnloadsEverything_AndChoosesANewDestination()
// … Arrive at S (sells its 9: S at 49), Depart (nothing to load, sails to H), Arrive at H:
// the hold is empty and FinalDestinationOf == S (S, at 49, needs 5 from home's 60).
```

Scenario **home short** — H at 0, D at 100 days (200), S at 20 days (40):

```csharp
[Test]
public void UnderWay_IsRecordedOnLoading_AndClearedOnEnteringThePort()
// Final destination D (what D would bring back: 1.4). Depart (empty), Arrive at D, Depart:
// UnderWayTo(H) == 60, UnderWayTo(S) == 22, 82 aboard, sailing to S. Arrive at S: UnderWayTo(S) == 0, UnderWayTo(H) == 60.

[Test]
public void AtAStopover_ItSellsOnlyUpToT1_AndKeepsWhatHomeLacks()
// At S: keeps 60 for home, sells 20 (S at its T1, 60), 62 aboard.

[Test]
public void AtAStopover_ItDoesNotBuyAgainWhatItCarriesForHome()
// Depart from S: H projected at 57.8 with its own 60 under way → nothing bought; still 62 aboard, S at 60, sailing to H.

[Test]
public void APartialSale_CutsWhatIsUnderWay_ToWhatIsLeftAboard()
// After departing from D (82 aboard): markets[H].Put(Fish, 60) (nothing lacks at home), markets[S].Take(Fish, 40) (S empty).
// Arrive at S: sells 60, 22 aboard, UnderWayTo(H) == 22.

[Test]
public void ARemovedShip_NoLongerCountsAsUnderWay()
// After departing from D: Remove → UnderWayTo(H) == 0 and UnderWayTo(S) == 0.
```

Other cases:

```csharp
[Test]
public void TwoShips_DoNotSupplyTheSameLack()
// Out-and-back markets. Ship 1 departs for D with 60. Ship 2 added at H: D projected at 56 → S is its final destination.

[Test]
public void AtHomeWithNoDestination_ItWaitsAndTriesAgain()
// Every market at 15 days: FinalDestinationOf == -1 and docked after a stopover. markets[H].Put(Fish, 200):
// still docked after Stopover − 1, then sailing with cargo after 1 more second.

[Test]
public void WithAQuarterOfTheHoldOrLessFree_ItDropsTheStopover()
// H consuming 6 a day at 0 (T1 180), D at 120 days (240), S at 0. At D: S is the stopover (needs 60),
// but home takes 180 of the 200 barrels: sailing to H, 180 aboard, UnderWayTo(S) == 0, S still at 0.

[Test]
public void ACity_IsAStopoverOnlyOncePerReturnTrip()
// Four ports: H, D, S, S2; days H–D 4, D–S 1, D–S2 1, S–S2 0.5, S–H 3.5, S2–H 3.5.
// H at 20 days (40), D at 100 days (200), S and S2 at 0. Final destination D; from D, S then S2.
// Before arriving at S2: markets[S2].Put(Fish, 200) and markets[S].Take(Fish, 60) (S empty again), so that S is worth
// a stopover from S2 (1.4) and passes the detour (0.5 + 3.5 < 4.2): the ship still sails to H.

[Test]
public void ShipsHomedAt_CountsTheShipsOfEachHome()
// Two ships homed at H, one at D: 2, 1, 0 for H, D, S; after removing one of H's: 1 for H.
```

Clearing the cities of the return trip when the ship enters home has no test of its own: a test would need a whole second tour. It is one line of the docking at home, for the reviewer to check.

Write the tests whose bodies are given as comments in the style of the first, with the setup and assertions their comments give.

- [ ] **Step 2: Run them to verify they fail** — compile error (`SupplyPlanner` in the fleet's constructor, `FinalDestinationOf`, `UnderWayTo`).

- [ ] **Step 3: Rewrite `AiMerchantFleet`** as described above; its summary says what a tour is. Then in `AiMerchants.StartFleet`, build `new SupplyPlanner(markets, travelTable.Days, worldEconomy.Economy.CreateGoodUnits(), worldEconomy.Economy.CreateAiWeights())` (it throws `ArgumentException`s, caught as today). Delete `TradeRoutePlanner.cs`, `TradePlan.cs`, `TradeRoutePlannerTests.cs` and their `.meta`.

- [ ] **Step 4: Update `CLAUDE.md`**: AI merchants, the `AiMerchantFleet` and `TradeRoutePlanner` bullets become the tour (home, final destination, stopovers on the way back, full unload at the final destination and at home, partial sale to T1 keeping home's lack, the quarter of the hold, waiting at home) and `SupplyPlanner` with `ShipmentQuantity` (projection, quantity on the thresholds, score = gap of factors × weight, destination with the return score, stopover detour under 1.2, deliveries under way per ship, its own counted); remove the empty relocation. Economy, `CargoHold` bullet: the planner no longer loads by margin per barrel.

- [ ] **Step 5: Refresh, run every EditMode test, verify they pass** (except the baseline failures of Task 0).

- [ ] **Step 6: Commit**

```bash
git add -A Assets/Scripts/Core Assets/Scripts/Game/AiMerchants.cs Assets/Tests/EditMode CLAUDE.md
git commit -m "Marchands IA : tournée depuis la ville d'attache, escales au retour et livraisons en route"
```

---

### Task 6: One ship per city, each with its home

**Files:**
- Create: `Assets/Scripts/Core/HomePortChoice.cs`
- Modify: `Assets/Scripts/Game/AiMerchantsDefinition.cs`, `Assets/Scripts/Game/AiMerchants.cs`, `Assets/Data/AI/AiMerchants.asset`, `CLAUDE.md`
- Delete: `Assets/Scripts/Core/StartPortDraw.cs`, `Assets/Tests/EditMode/StartPortDrawTests.cs`, with their `.meta`
- Test: `Assets/Tests/EditMode/HomePortChoiceTests.cs` (new), `AiMerchantsDefinitionTests.cs`, `AiMerchantsContentTests.cs`

**Interfaces:**
- Consumes: `AiMerchantFleet.ShipsHomedAt`, `HomeOf`, `FinalDestinationOf` (Task 5).
- Produces:
  - `public static class HomePortChoice` with `static int Pick(IReadOnlyList<int> candidates, Func<int, int> shipsHomedAt, Func<int, int> nextBelow)`: the candidate with the fewest ships homed at it; among ties, the one at `nextBelow(tieCount)` in candidate order; -1 for no candidate.
  - `AiMerchantsDefinition.ShipsPerCity` (serialized `shipsPerCity`, default 1, `[Min(0)]`), replacing `ShipCount`.
  - `bool AiMerchants.TryGetTour(Ship ship, out CityDefinition home, out CityDefinition finalDestination)` — false for a ship not of the fleet; `finalDestination` null when it has none.

- [ ] **Step 1: Write the failing tests**

```csharp
[Test]
public void Pick_TakesTheCityWithTheFewestHomedShips()
{
    var homed = new Dictionary<int, int> { { 3, 2 }, { 5, 1 }, { 8, 2 } };
    Assert.AreEqual(5, HomePortChoice.Pick(new[] { 3, 5, 8 }, p => homed[p], n => 0));
}

[Test]
public void Pick_DrawsAmongTies()
{
    int asked = -1;
    Assert.AreEqual(8, HomePortChoice.Pick(new[] { 3, 5, 8 }, p => p == 5 ? 1 : 0, n => { asked = n; return 1; }));
    Assert.AreEqual(2, asked);
}

[Test]
public void Pick_WithoutACandidate_IsNone()
{
    Assert.AreEqual(-1, HomePortChoice.Pick(new int[0], p => 0, n => 0));
}
```

`AiMerchantsDefinitionTests.TheDefaults_AreTheGames`: `Assert.AreEqual(1, definition.ShipsPerCity);`. `AiMerchantsContentTests`: rename to `OneMerchantShipPerCity_SailsAtFullSpeed`, `Assert.AreEqual(1, merchants.ShipsPerCity);`.

- [ ] **Step 2: Run them to verify they fail** — compile error.

- [ ] **Step 3: Implement**
  - `HomePortChoice.Pick` as described.
  - `AiMerchantsDefinition`: `shipCount` → `[Tooltip("AI ships whose home is each city that reaches another by water. Each starts in the port of its home.")] [SerializeField, Min(0)] private int shipsPerCity = 1;`, `ShipsPerCity`. The class summary says "how many ships each city has".
  - Asset: `shipCount: 5` → `shipsPerCity: 1` in `Assets/Data/AI/AiMerchants.asset`.
  - `AiMerchants.StartFleet`: after the candidates, for every candidate in order, `definition.ShipsPerCity` times `SpawnShip(candidate)` (it becomes the home through `fleet.Add`); remove the start draw. `AddShip`: `SpawnShip(HomePortChoice.Pick(candidates, fleet.ShipsHomedAt, bound => UnityEngine.Random.Range(0, bound)))`. Add `TryGetTour`. The class summary: ships homed in every city.
  - Delete `StartPortDraw.cs`, `StartPortDrawTests.cs` and their `.meta`.
  - `CLAUDE.md`: Project state ("an AI merchant ship per city" instead of "five ships"); AI merchants, first two bullets (`shipsPerCity` 1, 7 ships on the present map, numbered in the order of the cities; every candidate city spawns its ships in its port as their home; no start draw); Debug tools: "Ajouter un navire IA" homes the ship in the candidate city with the fewest AI ships homed there, at random among ties (`HomePortChoice`).

- [ ] **Step 4: Refresh, run every EditMode test, verify they pass**
- [ ] **Step 5: Commit**

```bash
git add -A Assets/Scripts Assets/Data/AI Assets/Tests/EditMode CLAUDE.md
git commit -m "Marchands IA : un navire par ville, chacun avec sa ville d'attache"
```

---

### Task 7: Home and final destination in the ships tab

**Files:**
- Modify: `Assets/Scripts/Game/DebugShipsTable.cs`, `Assets/Scripts/Game/DebugPanelController.cs`, `CLAUDE.md`
- Test: `Assets/Tests/EditMode/DebugShipsTableTests.cs`

**Interfaces:**
- Consumes: `AiMerchants.TryGetTour` (Task 6).
- Produces: `DebugShipInfo(string name, bool isPlayer, Ship ship, string portName, string destinationName, long? profit, string homeName = null, string finalDestinationName = null)`, with `HomeName` and `FinalDestinationName` (null: none). Two columns right after "Destination": "Attache" then "Destination finale", text cells, `NoValue` when null.

- [ ] **Step 1: Write the failing tests**

In `DebugShipsTableTests`, the cell constants become `HomeCell = 5`, `FinalDestinationCell = 6`, `CrewCell = 7`, `SpeedCell = 8`, `HoldCell = 9`, `FirstGoodCell = 10`, `ProfitCell = 12`. `TheHeader_NamesTheColumns_TheGoodsAmongThem` also asserts `"Attache"` and `"Destination finale"`. New:

```csharp
[Test]
public void AnAiShip_ShowsItsHome_AndItsFinalDestination()
{
    ships.Add(new DebugShipInfo("A", false, CreateShip(), "Elforth", null, 0, "Sparia", "Elforth"));
    ships.Add(new DebugShipInfo("B", false, CreateShip(), "Sparia", null, 0, "Sparia", null));
    ships.Add(new DebugShipInfo("Navire marchand", true, CreateShip(), null, null, null));

    table.Refresh(ships);

    Assert.AreEqual("Sparia", Text(0, HomeCell));
    Assert.AreEqual("Elforth", Text(0, FinalDestinationCell));
    Assert.AreEqual(DebugShipsTable.NoValue, Text(1, FinalDestinationCell));
    Assert.AreEqual(DebugShipsTable.NoValue, Text(2, HomeCell));
    Assert.AreEqual(DebugShipsTable.NoValue, Text(2, FinalDestinationCell));
}
```

- [ ] **Step 2: Run them to verify they fail**
- [ ] **Step 3: Implement**: the two labels in `Row`, `BuildRow` (after `Destination`, `TextCellClass`), the header titles, `Write` (`info.HomeName ?? NoValue`, `info.FinalDestinationName ?? NoValue`). `DebugPanelController.RefreshShips` passes the names from `aiMerchants.TryGetTour` (`DisplayName`, null when none); the comment of its `AddShip` says the ship is homed where the fewest are. `CLAUDE.md`, Debug tools, ships tab bullet: the two columns ("—" for the player's ships and while there is no final destination; "Destination" is still the next port).
- [ ] **Step 4: Run `DebugShipsTableTests`, then every EditMode test, verify they pass**
- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Game/DebugShipsTable.cs Assets/Scripts/Game/DebugPanelController.cs Assets/Tests/EditMode/DebugShipsTableTests.cs CLAUDE.md
git commit -m "Débogage : ville d'attache et destination finale des navires IA"
```

---

### Task 8: Check in the Editor

- [ ] **Step 1: Play `WorldMap.unity` in fast forward with the debug panel open (F1), "Navires" tab, over a few weeks of the world**

Expected: 7 AI ships, one homed in each city ("Attache"); ships leave their home straight for their "Destination finale", come back with stopovers on the way, and the final destination is home on the way back. No error in the console (`Unity_GetConsoleLogs`).

- [ ] **Step 2: "Ajouter un navire IA"**

Expected: the new ship, number 8, is homed in a city that had the fewest AI ships; "Retirer" on it removes it.

- [ ] **Step 3: "Villes" tab**

Expected: the stocks of goods a city does not produce move toward T1 over the weeks, instead of staying empty.

- [ ] **Step 4: Report what was seen to the user**; nothing is committed in this task.
