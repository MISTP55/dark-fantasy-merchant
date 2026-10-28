# Player's Workshops Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the player's workshops run: they produce into the player's trading post of their city, take their inputs from it, cost a weekly running cost and wages, and can be built only where the player has a trading post.

**Architecture:** `CityMarket` (Core) gets the merchant output rule and the player's trading post (an `IGoodStore`), and runs the player's workshops in `StartDay` right after they hire. `Construction` refuses a workshop when the market has no trading post. The weekly bill gets two new amounts, computed in Core (`PlayerWorkshopCosts`) from the markets, wired by `PlayerExpenses`. `WorldEconomy` hands each opened warehouse to its city's market.

**Tech Stack:** Unity 6000.6.4f1, C#, NUnit EditMode tests (Unity Test Framework), UI Toolkit.

**Spec:** `docs/superpowers/specs/2026-10-27-player-workshops-design.md`

## Global Constraints

- Core holds logic and state (no `MonoBehaviour`), covered by EditMode tests; Game only wires. All code, comments and identifiers in English; everything the player reads in French.
- Running cost: a field of `WorkshopDefinition`, **200 gold a week by default**. Wage share: one field of `ExpensesDefinition`, **0.8 by default**. New serialized fields get their default from their C# initializer: existing assets need no setup change.
- Wages of a city's workshops of a good = `PlayerDailyOutputOf(good)` (workers × merchant output per worker × bonus) × days per week × base price × wage share; summed over every good of every city, then rounded to the nearest gold once (`Math.Round`, `MidpointRounding.AwayFromZero`).
- The player's workshops never touch the market's stocks, demand, thresholds, `MarketWeekLog` or `LastDay`.
- No goods created or lost by rounding.
- No mention of the game this one draws inspiration from, anywhere (code, comments, rules, commits).
- Run tests with the `run-tests` skill. Commit on `main` at the end of each task, no push. Stage files by name, never `git commit -a`: `CLAUDE.md` shows as modified before this work starts (line endings only).

## Review Focus

1. The player moves away (transfer, sale) an input unit that was backing a fractional debt → no exception, no negative units; the workshop makes nothing more of that good until the post holds enough again. (Task 1: `InputTakenAway_WhileOwed_ProductionWaits`.)
2. A good whose recipe has no input (or only inactive ones) → its workshops produce from workers alone. (Task 1: `WithoutInputs_ProducesFromWorkersAlone`.)
3. A workshop not connected to roads, or with no workers → makes nothing, still owes its running cost. (Task 1: `UnconnectedWorkshop_MakesNothing`; Task 3: `RunningCost_IsOwedForEveryWorkshop_WithOrWithoutWorkers`.)
4. One good the input of another, workshops built input-first → the input is used the same day; built the other way → the next day. (Task 1: `AnInputMadeEarlier_ServesTheSameDay`.)
5. A trading post far above capacity → production still enters. (Task 1: `ProductionEnters_AboveCapacity`.)

---

### Task 1: The player's workshops produce into the trading post

**Files:**
- Modify: `Assets/Scripts/Core/Economy/CityMarket.cs` (constructor, `PlayerDailyOutputOf`, `StartDay`, new members)
- Modify: `Assets/Scripts/Game/Economy/EconomyDefinition.cs` (`CreateMarket` passes `CreateMerchantOutputRule()`)
- Modify: `Assets/Scripts/Game/Economy/WorldEconomy.cs` (`TryCreateMarkets`: on `tradingPosts.Opened`, hand the warehouse to the city's market before raising `TradingPostOpened`)
- Create: `Assets/Tests/EditMode/Population/CityMarketPlayerWorkshopsTests.cs`
- Modify: `.claude/rules/population.md` (day order step 4, "produce nothing yet"), `.claude/rules/economy.md` (Inputs: "The player's workshops produce nothing yet, so they take nothing")

**Interfaces:**
- Produces:
  - `CityMarket(..., IPlacementStrategy placement = null, MerchantOutputRule merchantOutput = null)`: new last optional parameter. Without it, the player's workshops still hire but produce nothing.
  - `MerchantOutputRule CityMarket.MerchantOutput { get; }`
  - `IGoodStore CityMarket.PlayerTradingPost { get; }`: null until set.
  - `void CityMarket.SetPlayerTradingPost(IGoodStore post)`: throws `ArgumentNullException` for null and `InvalidOperationException` when already set.
  - `double CityMarket.PlayerDailyOutputOf(int good)`: uses `MerchantOutput`, 0 without it. The existing `(int good, MerchantOutputRule rule)` overload stays; fix its summary ("They produce nothing yet" no longer holds).

- [ ] **Step 1: Write the failing tests** in `CityMarketPlayerWorkshopsTests` (namespace and helpers as in `CityMarketLabourTests`).
  - Use a market with labour, enough beggars and room, a `MerchantOutputRule(3, 1.03, 6, 1.06, 9, 1.1)`, and a `Warehouse(2000)` set as `PlayerTradingPost`.
  - Choose the merchant output per worker so the numbers stay whole where noted.

```csharp
[Test] public void AWorkshop_DeliversWholeUnits_AndCarriesTheFraction()
// 10 workers × 0.1/day, no inputs: after day 1 post holds 1; after 10 days, 10. Market stock of the good unchanged by the player.
[Test] public void WithoutInputs_ProducesFromWorkersAlone()
[Test] public void Inputs_AreTakenFromThePost_NeverFromTheMarket()
// recipe 2 units of input per unit; post holds 100 input; 10 days at 1/day → post input 80, output 10; market input stock unchanged.
[Test] public void ShortOfAnInput_ProducesInProportionToTheScarcest()
// two inputs, post holds enough of one and half of the other for the day → made = half; neither input goes negative.
[Test] public void AnInputUnit_IsTakenOnlyOnceAWholeUnitIsOwed()
// 0.1 made/day, 1 input per unit: post input stays 5 for days 1–9, is 4 after day 10.
[Test] public void InputTakenAway_WhileOwed_ProductionWaits()
// post holds 1 input; after day 1 (0.1 owed) remove it with TryRemove; days 2–5: no exception, output count unchanged, post input 0.
[Test] public void AnInputMadeEarlier_ServesTheSameDay()
// workshop of A built first, then of B (B needs A); post empty: B produces on day 1. Reverse build order: B produces nothing on day 1.
[Test] public void UnconnectedWorkshop_MakesNothing()
// on a grid, workshop placed away from roads: post unchanged after 10 days.
[Test] public void WithoutTradingPost_MakesNothing_ButHires()
[Test] public void WithoutMerchantOutputRule_MakesNothing()
[Test] public void TheBonus_AppliesToTheWorkshopsOfAGood()
// 3 workshops of a good: output = workers × perWorker × 1.03 over enough days (sum of units delivered within 1 of the exact value).
[Test] public void ProductionEnters_AboveCapacity()
// Warehouse(0): units still arrive.
[Test] public void PlayerProduction_IsNotInTheWeekLogNorTheDayReport()
[Test] public void SetPlayerTradingPost_Twice_Throws()
```

- [ ] **Step 2: Run them to see them fail.** Run the `run-tests` skill, filtered on `CityMarketPlayerWorkshopsTests`. Expected: compile errors (`SetPlayerTradingPost`, the constructor parameter).

- [ ] **Step 3: Implement in `CityMarket`.**
  - Add the constructor parameter, the members above, and a private `RunPlayerWorkshops()` called in `StartDay` right after `FillVacancies()`, inside the `Labour != null` block.
  - It does nothing without `MerchantOutput` or `PlayerTradingPost`.
  - It handles the goods of the player's workshops in the order of the first workshop built of each good.
  - The carried state is per good. `pending[good]` is the output made but not delivered, between 0 and 1. `debt[good][input]` is the input owed but not taken, between 0 and 1.

```text
potential = PlayerDailyOutputOf(good)
if potential == 0: next good
factor = 1
for each input k (good's Inputs): need = potential × k.UnitsPerUnit
    available = max(0, post.UnitsOf(k.Good) − debt[good][k])
    factor = min(factor, available / need)
made = potential × factor
for each input k: debt[good][k] += made × k.UnitsPerUnit
    take = min(floor(debt[good][k] + 1e-9), post.UnitsOf(k.Good))
    if take > 0: post.TryRemove(k.Good, take); debt[good][k] = max(0, debt[good][k] − take)
pending[good] += made
whole = floor(pending[good] + 1e-9)
if whole > 0: post.TryAdd(good, whole); pending[good] = max(0, pending[good] − whole)
```

- [ ] **Step 4: Wire Game.**
  - `EconomyDefinition.CreateMarket` passes `CreateMerchantOutputRule()`.
  - `WorldEconomy.TryCreateMarkets` changes the `Opened` handler to `MarketOf(city)?.SetPlayerTradingPost(tradingPosts.Of(city))` before `TradingPostOpened?.Invoke(city)`.
  - The home city's post is given through the same `Opened` event, so it is covered.

- [ ] **Step 5: Run all EditMode tests** with the `run-tests` skill. Expected: all pass.

- [ ] **Step 6: Update the rules.**
  - `population.md`: step 4 now reads that the player's workshops produce right after they hire, into the trading post, inputs from it. Remove "produce nothing yet" and the "will need a trading office" remark.
  - `economy.md`: replace "The player's workshops produce nothing yet, so they take nothing" with a line saying they take their inputs from the player's trading post, never the market.
  - Add one line that the fractions and the input debt are carried per good, backed by units still in the post.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Core/Economy/CityMarket.cs Assets/Scripts/Game/Economy/EconomyDefinition.cs Assets/Scripts/Game/Economy/WorldEconomy.cs Assets/Tests/EditMode/Population/CityMarketPlayerWorkshopsTests.cs* .claude/rules/population.md .claude/rules/economy.md
git commit -m "Ateliers du joueur : production dans le comptoir"
```

### Task 2: A workshop needs the player's trading post

**Files:**
- Modify: `Assets/Scripts/Core/Population/Construction.cs` (`QuoteWorkshop`, so both `TryBuildWorkshop` overloads refuse)
- Modify: `Assets/Scripts/Game/CityView/BuildBarController.cs` (`CostTipOf`, new constant)
- Modify: `Assets/Tests/EditMode/Population/ConstructionTests.cs`, `Assets/Tests/EditMode/CityView/PlayerBuilderTests.cs`, `Assets/Tests/EditMode/CityView/BuildBarTests.cs`
- Modify: `.claude/rules/population.md` (`Construction` line), `.claude/rules/city-view.md` (build bar line), `.claude/rules/debug-tools.md` (free building: "a workshop still needs a city that produces its good" + "and the player's trading post")

**Interfaces:**
- Consumes: `CityMarket.PlayerTradingPost`, `CityMarket.SetPlayerTradingPost` (Task 1).
- Produces: `public const string BuildBarController.WorkshopNeedsTradingPostText = "Il faut un comptoir dans cette ville pour y construire un atelier."`

- [ ] **Step 1: Write the failing tests.**
  - `ConstructionTests.QuoteWorkshop_WithoutTradingPost_IsImpossible`: impossible even with `isFree: true`.
  - `ConstructionTests.TryBuildWorkshop_WithoutTradingPost_TakesNothing`: both overloads; gold, market stocks and materials unchanged; `PlayerWorkshopsOf` 0.
  - `BuildBarTests.OfferState_WorkshopWithoutTradingPost_IsDisabledWithItsTip`: built like `OfferState_TradingPostBuilt_IsDisabledWithItsTip`; the workshop button is disabled and the tip reads `WorkshopNeedsTradingPostText`.
  - Existing workshop tests in `ConstructionTests` and `PlayerBuilderTests` set a trading post on the market first (`market.SetPlayerTradingPost(new Warehouse(2000))`, or `WorldEconomy.OpenTradingPost` / `TryGiveTradingPost` where the test goes through the economy).

- [ ] **Step 2: Run them to see them fail.** Run the `run-tests` skill. Expected: the new tests fail on the quote being possible.

- [ ] **Step 3: Implement.**
  - `QuoteWorkshop`'s buildable condition becomes `market.MaxWorkersOf(good) > 0 && market.PlayerTradingPost != null`.
  - `CostTipOf` returns `WorkshopNeedsTradingPostText` for a workshop offer when the bar's bound trading post is null, before the free-building tip.
  - The button is already disabled by the impossible quote.

- [ ] **Step 4: Run all EditMode tests.** Expected: all pass.

- [ ] **Step 5: Update the three rules lines, then commit.**

```bash
git add Assets/Scripts/Core/Population/Construction.cs Assets/Scripts/Game/CityView/BuildBarController.cs Assets/Tests/EditMode .claude/rules
git commit -m "Ateliers du joueur : comptoir requis pour construire"
```

### Task 3: Running cost and wages in the weekly bill

**Files:**
- Modify: `Assets/Scripts/Game/Population/WorkshopDefinition.cs` (field `weeklyRunningCost`)
- Modify: `Assets/Scripts/Game/Population/BuildingOffers.cs` (`WorkshopOffer.WeeklyRunningCost`), and where the catalogue makes the offers (`BuildingsDefinition`)
- Modify: `Assets/Scripts/Game/Player/ExpensesDefinition.cs` (field `workshopWageShare`, `CreateBilling`)
- Modify: `Assets/Scripts/Core/Economy/CityMarket.cs` (`PlayerWeeklyWagesOf`)
- Create: `Assets/Scripts/Core/Player/PlayerWorkshopCosts.cs`
- Modify: `Assets/Scripts/Core/Player/WeeklyBill.cs`, `Assets/Scripts/Core/Player/WeeklyBilling.cs`
- Modify: `Assets/Scripts/Game/Player/PlayerExpenses.cs`
- Create: `Assets/Tests/EditMode/Player/PlayerWorkshopCostsTests.cs`
- Modify: `Assets/Tests/EditMode/Player/WeeklyBillTests.cs`, `WeeklyBillingTests.cs`, `ExpensesDefinitionTests.cs`, `Assets/Tests/EditMode/Population/BuildingDefinitionTests.cs` (or `BuildingsDefinitionTests.cs`)
- Modify: `.claude/rules/player.md` (the bill's amounts)

**Interfaces:**
- Consumes: `CityMarket.PlayerDailyOutputOf(int good)`, `CityMarket.PlayerWorkshopsOf(int good)`, `CityMarket.GoodCount` (Task 1).
- Produces:
  - `WorkshopDefinition.WeeklyRunningCost` (`long`): `[SerializeField, Min(0)] private long weeklyRunningCost = 200`, and `TryValidate` rejects a negative value.
  - `WorkshopOffer.WeeklyRunningCost` (`long`): a new constructor parameter after `costText`.
  - `ExpensesDefinition.WorkshopWageShare` (`double`): `[SerializeField, Min(0f)] private double workshopWageShare = 0.8`.
  - `double CityMarket.PlayerWeeklyWagesOf(double wageShare)`: Σ over goods of `PlayerDailyOutputOf(good) × daysPerWeek × BasePrice × wageShare`, unrounded. Throws on a negative or non-finite share.
  - `static class PlayerWorkshopCosts` (Core, namespace `DarkFantasyMerchant.Core`):
    - `static long WeeklyRunningCostOf(IEnumerable<CityMarket> markets, Func<int, long> runningCostOfGood)`
    - `static long WeeklyWagesOf(IEnumerable<CityMarket> markets, double wageShare)`, rounded once.
  - `WeeklyBill(int sailorCount, long sailorWeeklyWage, long warehouseRent = 0, long workshopRunningCosts = 0, long workshopWages = 0)`, with properties `WorkshopRunningCosts` and `WorkshopWages`, both added to `Total`. A negative value throws.
  - `WeeklyBilling(..., Func<long> warehouseRent = null, Func<long> workshopRunningCosts = null, Func<long> workshopWages = null)`: each is asked on the billing day.
  - `ExpensesDefinition.CreateBilling(Treasury, int daysPerWeek, Func<int> sailorCount, Func<long> warehouseRent = null, Func<long> workshopRunningCosts = null, Func<long> workshopWages = null)`.

- [ ] **Step 1: Write the failing tests.**
  - `PlayerWorkshopCostsTests.Wages_AreEightyPercentOfTheWeeksBaseValue`: one workshop, full, making 1 unit a week of a good of base price 100, share 0.8 → 80.
  - `PlayerWorkshopCostsTests.Wages_FollowTheWorkersPresent`: half the jobs filled → half the wages.
  - `PlayerWorkshopCostsTests.Wages_PaidInFull_WhenShortOfInputs`: post empty of inputs → same wages as with inputs.
  - `PlayerWorkshopCostsTests.Wages_CountTheBonus`: 3 workshops of a good → × 1.03.
  - `PlayerWorkshopCostsTests.Wages_AreRoundedOnce_AfterSumming`: two cities at 0.4 each → 1, not 0.
  - `PlayerWorkshopCostsTests.RunningCost_IsOwedForEveryWorkshop_WithOrWithoutWorkers`: 2 workshops, 0 workers, 200 each → 400.
  - `WeeklyBillTests.Total_AddsTheWorkshopsCosts`, and a negative value throws.
  - `WeeklyBillingTests.TheWorkshopsCosts_AreAskedOnTheBillingDay_AndWithdrawn`.
  - `ExpensesDefinitionTests.WorkshopWageShare_DefaultsToEightyPercent`.
  - A `BuildingDefinitionTests` test: `WeeklyRunningCost` defaults to 200 and a negative one fails `TryValidate`.

- [ ] **Step 2: Run them to see them fail.** Expected: compile errors on the new members.

- [ ] **Step 3: Implement** the members listed in **Interfaces**.
  - In `PlayerExpenses.TryCreateBilling`, pass two new private readers, which return 0 when the economy is not ready (like `RentOfTradingPosts`):
    - `RunningCostOfWorkshops()`: the markets of `worldEconomy`'s cities, and a good's running cost from the `WorkshopOffers` whose `Good` matches (0 when none).
    - `WagesOfWorkshops()`: `PlayerWorkshopCosts.WeeklyWagesOf(markets, expenses.WorkshopWageShare)`.
  - If `WorldEconomy` has no enumeration of its markets, add `IEnumerable<CityMarket> Markets`.

- [ ] **Step 4: Run all EditMode tests.** Expected: all pass.

- [ ] **Step 5: Update `player.md`** (the bill holds the sailors, the trading posts' rent, the workshops' running cost and wages; the formula lives in `PlayerWorkshopCostsTests`). Then commit.

```bash
git add -A Assets/Scripts Assets/Tests .claude/rules/player.md
git commit -m "Ateliers du joueur : coût d'exploitation et salaires"
```

### Task 4: Play-mode check and project state

**Files:**
- Modify: `CLAUDE.md` ("Project state": the player's workshops produce into the trading post)

- [ ] **Step 1: Play-mode check** through `unity-mcp`.
  - Build a workshop in the home city with the debug free building on. Choose a good the city produces, with its inputs put in the trading post.
  - Connect it to a road.
  - Advance 7+ days with the debug speed slider.
  - Expected:
    - The trading post's units of the good rise.
    - Its inputs fall.
    - The market's stocks of both goods move only by the city's own activity.
    - The weekly notification's total grows by about the running cost plus the wages.
    - In a city without a trading post, the workshop offers are disabled with `WorkshopNeedsTradingPostText`.
  - No errors in the console.
- [ ] **Step 2: Update `CLAUDE.md`'s project state, then commit.**

```bash
git diff CLAUDE.md   # check that only this task's lines differ (an earlier change of the user's is only line endings)
git add CLAUDE.md
git commit -m "Ateliers du joueur : état du projet"
```
