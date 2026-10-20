# Debug Observatory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the debug panel (F1) into an observatory of the simulation: a daily record of every city kept over a chosen horizon, shown as curves (three ways), present-state tables, a journal of the cities' events and a journal of the trades.

**Architecture:** Everything that records is `Core`, plain C# under EditMode tests: `CityMarket` fills a `CityDayReport` during `StartDay`; `AiMerchantFleet` and `Construction` report their trades; a `SimulationRecord` (history of daily snapshots, event journal, trade journal, all trimmed to a horizon) is fed once per day. In `Game`, a debug-only `SimulationRecorder` MonoBehaviour wires the record to the scene (`WorldEconomy.DayEnded`, the AI merchants, the city panel's trades), and the debug panel is rebuilt as five tabs, each a plain C# view class, with a hand-drawn `TimeSeriesChart` element (UI Toolkit `generateVisualContent` + `Painter2D`).

**Tech Stack:** Unity 6000.6.4f1, C#, UI Toolkit (UXML/USS, `ListView`, `DropdownField`, `Painter2D`), Unity Test Framework (NUnit, EditMode).

**Spec:** `docs/superpowers/specs/2026-10-20-debug-observatory-design.md` (mockups in `docs/superpowers/specs/2026-10-20-debug-observatory-mockups/`).

## Global Constraints

- The record reads the simulation and changes no figure of it. Every existing EditMode test passes unchanged at the end of every task.
- Debug only: the recorder and the panel exist only when `Debug.isDebugBuild` (Editor and development builds), as the debug tools today.
- Code, comments, asset and folder names in English; everything the user reads in French. Numbers as the debug tables write them (`DebugCitiesTable.FormatQuantity`, `FormatNet`, `FormatRate`, `TreasuryHudController.FormatNumber`); dates as `CalendarDefinition.Format(GameDate.FromElapsedDays(day, calendar.StartYear, calendar.MonthsPerYear, calendar.DaysPerMonth))`.
- Every list the user reads is sorted with `NameOrder` (cities, goods, indicators in drop-downs); fixed sequences (tabs, columns) and chronological lists (journals, newest first) keep their order.
- Every list of rows is striped: `ListStripes.Apply` for built rows; a virtualised `ListView` uses `showAlternatingRowBackground = AlternatingRowBackground.ContentOnly`, styled in USS with the colour of `.list-row--alternate`.
- Buttons, toggles, drop-downs and sliders of the panel are not focusable (`focusable = false`): the keyboard pans the map.
- Horizon choices, in this order: 3, 6, 12, 24, 48 months, "Toute la partie"; default 12 months; a month is `CalendarDefinition.DaysPerMonth` days (30). Not saved: back to 12 months at every Play.
- Tabs, in this order: "Villes" (sub-tabs "Population", "Marchés"), "Courbes" ("Par ville", "Par indicateur", "Composer"), "Événements", "Échanges" ("Journal", "Flux par marchandise"), "Navires".
- New scene objects and references go through `WorldMapSetup` (`Tools > Dark Fantasy Merchant > Build World Map Scene`), which must also complete an existing scene.
- Run the tests with the project's `run-tests` skill. Commit at the end of every task, message in French as the repository's history, ending with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- **The very first days**: a chart with 0 or 1 recorded day, a series whose values are all equal, or NaN must draw nothing wrong and never divide by zero; covered in Task 9 (`ChartMath`).
- **Shortening the horizon** (from "Toute la partie" to 3 months, or while the panel is open): history, events and trades are trimmed together, the last day is always kept, and the open views redraw without an index out of range; covered in Task 4 and Task 6.
- **The events list frozen ("Figer") while days keep passing**: the rows shown stay the same rows even when older events are trimmed and new ones arrive; covered in Task 12.
- **An AI ship removed with cargo aboard, or a sale of more units than the ship's tracked cost covers**: the profit stays defined (cost of units never tracked is 0) and the fleet's existing behaviour and profit are unchanged; covered in Task 2.
- **A city without a market, or a good beyond a market's `GoodCount`**: the recorder skips the city, the tables write `DebugCitiesTable.NoFigure`; covered in Task 5 and Task 8.

---

### Task 1: The day's report of a city

**Files:**
- Create: `Assets/Scripts/Core/Debug/CityDayReport.cs`
- Modify: `Assets/Scripts/Core/Economy/CityMarket.cs` (`StartDay`, `Hire`, `LayOff`, `FillVacancies`, `LetThePoorLeave`, `BuildCityHouse`)
- Test: `Assets/Tests/EditMode/Debug/CityDayReportTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class CityDayReport` with, per good index: `double NeededOf(int good)` (the day's `DailyConsumptionOf` before consuming), `double ConsumedOf(int good)`, `double MissingOf(int good)` (= needed − consumed, never below 0), `double ProducedOf(int good)`, `int HiredOf(int good)` (from reserves and beggars together), `int LaidOffOf(int good)`, `bool IsHaltedOf(int good)` (a produced good whose workshop did not produce because its stock is above T3); per day: `int ClassChangeOf(SocialClass c)` (count after `RiseAndFall` minus before, for Rich and Wealthy; 0 for Poor), `int PoorLeft`, `int WorkersLeft`, `int BeggarChange` (after `SetBeggars` minus before), `int HousesBuiltOf(SocialClass c)`. An `internal void Clear()` and internal setters/adders for `CityMarket` (same assembly).
  - `CityMarket.LastDay` (`CityDayReport`): the report of the last `StartDay`, all zeros before the first one. The same instance is reused every day.

- [ ] **Step 1: Write the failing tests** in `CityDayReportTests`, with markets built as in `DebugCitiesTableTests.SetUp` (`TestMarkets.Thresholds`, `Workforce`, `Pricing`, `daysPerWeek` 7):

```csharp
[Test] public void Shortage_IsWhatTheCityNeededAndDidNotGet()
{
    // A thousand inhabitants, fish 2 a day, not produced, nothing in stock.
    var market = new CityMarket(new[] { new MarketGood(10, 2, 0, 0) }, TestMarkets.Thresholds, TestMarkets.Workforce, 0, TestMarkets.Pricing, 7, new CityPopulation(1000));
    market.StartDay();
    Assert.AreEqual(2d, market.LastDay.NeededOf(0), 1e-9);
    Assert.AreEqual(0d, market.LastDay.ConsumedOf(0), 1e-9);
    Assert.AreEqual(2d, market.LastDay.MissingOf(0), 1e-9);
}

[Test] public void Shortage_OfAProducedGood_IsSeenEvenWhenTheEveningStockIsNotEmpty()
// Iron 2 a day, workshop of 30 making 0.1 each (3 a day), 0 starting days: Missing 2, Produced 3, StockOf(1) == 3.

[Test] public void AFullStock_HaltsTheWorkshop()
// Iron as above with 100 starting days (above T3): IsHaltedOf(1) true, ProducedOf(1) 0.

[Test] public void Report_IsClearedEveryDay()
// Fish shortage on day 1, then Put(0, 100) and StartDay: MissingOf(0) == 0.

[Test] public void Hires_LayOffs_Beggars_Classes_AndHouses_AreCounted()
// With TestMarkets.Labour and TestMarkets.Social, a city where a workshop hires from beggars:
// HiredOf(good) equals WorkersOf(good) after minus before; BeggarChange equals Beggars after minus before;
// ClassChangeOf(Rich) equals CountOf(Rich) after RiseAndFall minus before (read the counts around StartDay on a city
// where nothing else moves the rich); HousesBuiltOf(Poor) == 1 on the day CityHouses goes up by 1.

[Test] public void Report_ChangesNoFigureOfTheSimulation()
// Two identical markets with labour and social rules, 60 days of StartDay each, one with LastDay read every day:
// stocks, workers, reserves, the class counts, satisfactions and beggars are equal at the end.
```

- [ ] **Step 2: Run the tests** (`run-tests` skill, filter `DarkFantasyMerchant.Tests.EditMode.CityDayReportTests`). Expected: compile failure, `LastDay` does not exist.

- [ ] **Step 3: Implement `CityDayReport` and fill it in `CityMarket.StartDay`.** `StartDay` calls `LastDay.Clear()` next to `weekLog.StartDay()`; each existing step adds to it where the figure is already known (`consumed[i]`, `produced`, the `fromBeggars` and reserve takes of `Hire`, `laidOff`, `removed` of `LetThePoorLeave`, `CityHouses++`). Class changes and the beggar change are differences of counts read around `RiseAndFall` and `SetBeggars`. No line of the simulation's arithmetic changes.

- [ ] **Step 4: Run all EditMode tests.** Expected: all pass, the existing `CityMarket*Tests` unchanged.

- [ ] **Step 5: Commit** — `Débogage : rapport de la journée d'une ville`.

---

### Task 2: The AI merchants report their trades

**Files:**
- Create: `Assets/Scripts/Core/AiMerchants/FleetTrade.cs`
- Modify: `Assets/Scripts/Core/AiMerchants/AiMerchantFleet.cs` (`Merchant`, `LoadFor`, `SellEverything`, `SellUpToT1`)
- Modify: `Assets/Scripts/Game/AiMerchants/AiMerchants.cs`
- Test: `Assets/Tests/EditMode/AiMerchants/AiMerchantFleetTests.cs`

**Interfaces:**
- Produces:
  - `public readonly struct FleetTrade { Ship Ship; CityMarket Market; int Good; int Units; long Gold; bool IsSale; long? Profit; }` with a constructor taking them in that order. `Profit` is null for a buy.
  - `AiMerchantFleet<TPort>.Traded` (`event Action<FleetTrade>`), raised after every `Trade.Buy`/`Trade.Sell` that moved at least one unit.
  - The fleet keeps per merchant `long[] costAboard` (gold paid for the units of each good aboard). A buy adds its gold. A sale of `units` out of `aboard` (units aboard before the sale) takes `cost = costAboard[good] * units / aboard` (integer division, 0 when `aboard` is 0), subtracts it, and its profit is `gold − cost`.
  - `AiMerchants.Traded` (`event Action<FleetTrade>`), re-raised from the fleet once it exists; `int AiMerchants.UnderWayTo(CityDefinition city, int good)` (0 for a city that is not a port of the fleet or before the fleet exists).

- [ ] **Step 1: Write the failing tests** in `AiMerchantFleetTests`, with its `CreateFleet` helpers:

```csharp
[Test] public void Traded_ReportsTheBuyAndTheSale_WithTheProfitOfTheSale()
// A tour home → destination as in the existing tour test; collect fleet.Traded into a list.
// The buy: IsSale false, Profit null, Market == home market, Units and Gold those of the hold and of ProfitOf's drop.
// The sale at the destination: IsSale true, Profit == sale Gold − buy Gold (everything sold),
// and fleet.ProfitOf(ship) equals the sum of sale gold minus buy gold, as before.

[Test] public void APartialSale_CostsItsShareOfThePurchase()
// A stopover selling up to T1 sells part of the cargo: Profit == Gold − buyGold * units / unitsBought.

[Test] public void Traded_ChangesNothingOfTheTour()
// Same scenario with and without a Traded subscriber: identical ProfitOf, cargo and FinalDestinationOf after the tour.
```

- [ ] **Step 2: Run the tests.** Expected: compile failure, `Traded` does not exist.

- [ ] **Step 3: Implement** `FleetTrade`, the cost tracking and the event in `AiMerchantFleet`; then `AiMerchants.Traded` (subscribe when the fleet is created, unsubscribe in `OnDestroy`) and `AiMerchants.UnderWayTo` (port index of the city in `ports`, then `fleet.UnderWayTo`).

- [ ] **Step 4: Run all EditMode tests.** Expected: all pass.

- [ ] **Step 5: Commit** — `Marchands IA : chaque achat et chaque vente signalés, avec le bénéfice de la vente`.

---

### Task 3: The player's trades are reported

**Files:**
- Modify: `Assets/Scripts/Core/Population/Construction.cs` (`TryBuildHouse`, `TryBuildWorkshop`, `Pay`)
- Modify: `Assets/Scripts/Game/Economy/CityMarketSection.cs` (`Buy`, `Sell`)
- Modify: `Assets/Scripts/Game/Population/CityBuildingsSection.cs` (`BuildHouse`, `BuildWorkshop`)
- Modify: `Assets/Scripts/Game/WorldMap/CityInfoPanelController.cs`
- Test: `Assets/Tests/EditMode/Population/ConstructionTests.cs`, `Assets/Tests/EditMode/Economy/CityMarketSectionTests.cs`

**Interfaces:**
- Produces:
  - `Construction.TryBuildHouse(..., int room, Action<int, int, long> materialBought = null)` and `TryBuildWorkshop(..., int jobs, Action<int, int, long> materialBought = null)`: called once per material bought from the market with `(good, units, gold)`, the gold of those units at the prices `Quote` used (sum of `BuyPriceOf(good, i)` before `Take`). Not called for what comes from the hold.
  - `public enum PlayerTradeKind { Buy, Sell, Construction }` in `Assets/Scripts/Core/Economy/Trade.cs`.
  - `CityMarketSection.Traded` (`event Action<int good, TradeResult result, PlayerTradeKind kind>`), raised by `Buy`/`Sell` when units moved.
  - `CityBuildingsSection.MaterialBought` (`event Action<int good, int units, long gold>`).
  - `CityInfoPanelController.PlayerTraded` (`event Action<CityDefinition city, int good, int units, long gold, PlayerTradeKind kind>`), re-raising both for the city shown.

- [ ] **Step 1: Write the failing tests:**

```csharp
// ConstructionTests
[Test] public void TryBuildHouse_ReportsTheMaterialsBoughtFromTheMarket()
// A cost of 10 timber, 4 aboard: materialBought called once with (timber, 6, sum of BuyPriceOf(timber, 0..5) read before building).
[Test] public void TryBuildHouse_ReportsNothing_WhenTheHoldHoldsEverything()

// CityMarketSectionTests
[Test] public void Buy_RaisesTraded_WithWhatWasBought()  // kind Buy, result.Units and result.Gold of the trade
[Test] public void Sell_RaisesNothing_WhenNothingIsAboard()
```

- [ ] **Step 2: Run the tests.** Expected: compile failure.
- [ ] **Step 3: Implement** the callback in `Construction.Pay`, the two section events and `CityInfoPanelController.PlayerTraded` (subscribe where the sections are created, in `TryGetMarketSection` and the buildings' equivalent).
- [ ] **Step 4: Run all EditMode tests.** Expected: all pass.
- [ ] **Step 5: Commit** — `Joueur : achats, ventes et matériaux de construction signalés`.

---

### Task 4: The history of the cities

**Files:**
- Create: `Assets/Scripts/Core/Debug/CityIndicator.cs` (both enums)
- Create: `Assets/Scripts/Core/Debug/CitySnapshot.cs`
- Create: `Assets/Scripts/Core/Debug/SimulationHistory.cs`
- Test: `Assets/Tests/EditMode/Debug/SimulationHistoryTests.cs`

**Interfaces:**
- Consumes: `CityMarket.LastDay` (Task 1).
- Produces:
  - `public enum CityIndicator { Inhabitants, Rich, Wealthy, Poor, RichRoom, WealthyRoom, PoorRoom, RichSatisfaction, WealthySatisfaction, PoorSatisfaction, Beggars, BeggarTarget, CityHouses }`.
  - `public enum GoodIndicator { Stock, T0, T1, T2, T3, BuyPrice, SellPrice, NominalWeeklyProduction, RecentProduction, NominalWeeklyConsumption, RecentConsumption, Missing, Workers, Reserve, UnderWay }` (`RecentProduction`/`RecentConsumption` are the week log's, as the tables'; `Missing` is the day's `MissingOf`).
  - `public static class CitySnapshot`: `int SizeFor(int goodCount)`; `void Capture(CityMarket market, Func<int, int> underWay, float[] into)` (`underWay(good)`; prices are `BuyPriceOf(good)`/`SellPriceOf(good)`; a good beyond `market.GoodCount` gets `float.NaN`); `int IndexOf(CityIndicator)`; `int IndexOf(GoodIndicator, int good)`.
  - `public sealed class SimulationHistory`: `SimulationHistory(int cityCount, int goodCount, int? horizonDays)`; `int? HorizonDays`; `void SetHorizon(int? days)` (null = the whole game; trims at once); `float[][] BeginDay(int day)` returning one row per city to fill (reusing the arrays of a trimmed day); `int DayCount`; `int DayAt(int index)` (oldest first); `float ValueOf(int index, int city, CityIndicator indicator)`; `float ValueOf(int index, int city, GoodIndicator indicator, int good)`; `int FirstDay` (`DayAt(0)`, or −1 when empty). A day at or before the last recorded one is refused (`ArgumentOutOfRangeException`). Trimming keeps the days with `day > last − horizonDays` and never the last day itself.

- [ ] **Step 1: Write the failing tests:**

```csharp
[Test] public void Capture_WritesEveryIndicator()
// A market of the DebugCitiesTableTests kind after one StartDay: ValueOf(Stock, fish) == StockOf(fish), T0..T3 == ThresholdsOf,
// BuyPrice == BuyPriceOf, Missing == LastDay.MissingOf, UnderWay == what underWay returned (7), Inhabitants, Rich, satisfaction.
[Test] public void History_KeepsOnlyTheHorizon()           // horizon 3, days 1..5 → DayCount 3, DayAt(0) == 3
[Test] public void SetHorizon_Shorter_TrimsAtOnce()         // whole game, 10 days, SetHorizon(4) → DayAt(0) == 7
[Test] public void SetHorizon_Longer_BringsNothingBack()    // horizon 2, 5 days, SetHorizon(null) → DayCount 2
[Test] public void History_NeverDropsTheLastDay()           // SetHorizon(0) is refused (ArgumentOutOfRangeException); horizon 1 keeps 1 day
[Test] public void BeginDay_RefusesADayNotAfterTheLast()
[Test] public void AGoodTheMarketLacks_IsNaN()
```

- [ ] **Step 2: Run the tests.** Expected: compile failure.
- [ ] **Step 3: Implement.** One `float[]` of `SizeFor(goodCount)` per city and day (city indicators first, then each good's block of `GoodIndicator` values); days in a `List`, trimmed from the front, trimmed arrays pooled for `BeginDay`.
- [ ] **Step 4: Run all EditMode tests.** Expected: all pass.
- [ ] **Step 5: Commit** — `Débogage : historique quotidien des villes`.

---

### Task 5: Events, trades and the record that holds them

**Files:**
- Create: `Assets/Scripts/Core/Debug/StockBand.cs` (enum and `StockBands.Of`)
- Create: `Assets/Scripts/Core/Debug/SimulationEvent.cs` (struct and `SimulationEventType`)
- Create: `Assets/Scripts/Core/Debug/CityEventDetector.cs`
- Create: `Assets/Scripts/Core/Debug/TradeRecord.cs`
- Create: `Assets/Scripts/Core/Debug/SimulationRecord.cs`
- Test: `Assets/Tests/EditMode/Debug/CityEventDetectorTests.cs`, `Assets/Tests/EditMode/Debug/SimulationRecordTests.cs`

**Interfaces:**
- Consumes: `CityDayReport` (Task 1), `SimulationHistory`, `CitySnapshot` (Task 4), `FleetTrade` (Task 2), `PlayerTradeKind` (Task 3).
- Produces:
  - `public enum StockBand { BelowT0, T0ToT1, T1ToT2, T2ToT3, AboveT3 }`; `StockBands.Of(double stock, StockThresholds t)` (a stock equal to a threshold is in the band above it).
  - `public enum SimulationEventType { Shortage, ThresholdCrossing, Hired, LaidOff, ProductionHalted, ProductionResumed, PoorLeft, ClassChange, BeggarsArrived, BeggarsLeft, CityHouseBuilt }`.
  - `public readonly struct SimulationEvent { int Day; int City; int Good; SimulationEventType Type; double Amount; int From; int To; }`; `Good` is −1 for a city event; `From`/`To` are the `StockBand`s of a crossing, the `SocialClass` of a class change or house in `To`; `Amount` is the units missing, the workers, the inhabitants (signed for a class change) or the beggars; for `PoorLeft`, `Amount` is the poor and `To` the workers they took (`WorkersLeft`).
  - `public static class CityEventDetector`: `void Detect(int day, int city, CityMarket market, CityDayReport report, float[] previous, float[] current, bool[] wasHalted, List<SimulationEvent> into)`; `previous` is null on the first recorded day (no crossing then); `wasHalted` is the detector's memory per good, updated by the call.
  - `public enum Trader { AiMerchant, Player }`; `public readonly struct TradeRecord { int Day; int City; int Good; Trader Trader; Ship Ship; PlayerTradeKind Kind; int Units; long Gold; long? Profit; }` (`Kind` is `Buy` or `Sell` for an AI trade).
  - `public sealed class SimulationRecord`: `SimulationRecord(IReadOnlyList<CityMarket> markets, int goodCount, int? horizonDays)`; `SimulationHistory History`; `IReadOnlyList<SimulationEvent> Events` and `IReadOnlyList<TradeRecord> Trades` (oldest first); `long EventsTrimmed` (events dropped from the front since the start, so that a frozen view can map its rows); `void RecordDay(int day, Func<int, int, int> underWay)` (`underWay(city, good)`); `void AddTrade(int day, CityMarket market, FleetTrade trade)`; `void AddPlayerTrade(int day, CityMarket market, int good, int units, long gold, PlayerTradeKind kind)`; `void SetHorizon(int? days)`; `void FlowOf(int city, int good, out long received, out long shipped)` over the trades kept (received: units sold into the city; shipped: units bought from it, construction included); `event Action Recorded` (after `RecordDay` and after `SetHorizon`).

- [ ] **Step 1: Write the failing tests:**

```csharp
// CityEventDetectorTests
[Test] public void AShortage_IsAnEvent_WithTheUnitsMissing()          // Missing 2 → Shortage, Good fish, Amount 2
[Test] public void ACrossing_IsAnEvent_WhateverMoved()                // stock band T1ToT2 yesterday, T0ToT1 today → From T1ToT2, To T0ToT1
[Test] public void TheFirstDay_HasNoCrossing()                         // previous null
[Test] public void HaltedThenResumed_AreTwoEvents()                    // halted on day 1 → ProductionHalted; produced on day 2 → ProductionResumed
[Test] public void HiresAndLayOffs_AreOneEventEachPerGood()            // HiredOf 4 → Hired Amount 4; nothing for 0
[Test] public void ClassMoves_Leaving_Beggars_AndHouses_AreEvents()    // ClassChangeOf(Rich) -3 → ClassChange To Rich Amount -3; BeggarChange +5 → BeggarsArrived 5; -2 → BeggarsLeft 2; HousesBuiltOf(Poor) 1 → CityHouseBuilt To Poor

// SimulationRecordTests
[Test] public void RecordDay_RecordsEveryCity_AndItsEvents()
[Test] public void SetHorizon_TrimsHistoryEventsAndTradesTogether()   // 10 days with events and trades each day, SetHorizon(3): no event nor trade before day 8; EventsTrimmed counts the dropped
[Test] public void Flows_CountTheAiAndThePlayer()                     // AI sells 40 fish into city 1, player buys 6 timber for a construction in city 1: FlowOf(1, fish) received 40; FlowOf(1, timber) shipped 6
[Test] public void ATradeInACityNotRecorded_IsIgnored()
```

- [ ] **Step 2: Run the tests.** Expected: compile failure.
- [ ] **Step 3: Implement.** `RecordDay` captures every city's snapshot into `History.BeginDay(day)`, then runs the detector against the previous day's row (kept aside before trimming), then trims events and trades to `History.FirstDay`. A trade's `Day` is the last recorded day, which is the day under way (a day is recorded right after its `StartDay`).
- [ ] **Step 4: Run all EditMode tests.** Expected: all pass.
- [ ] **Step 5: Commit** — `Débogage : journal des événements et des échanges`.

---

### Task 6: The recorder in the scene

**Files:**
- Create: `Assets/Scripts/Game/Debug/SimulationRecorder.cs`
- Modify: `Assets/Scripts/Game/Economy/WorldEconomy.cs` (`DayEnded`)
- Modify: `Assets/Scripts/Editor/WorldMap/WorldMapSetup.cs` (`AddDebugToolsToScene`)
- Test: `Assets/Tests/EditMode/Debug/HorizonChoicesTests.cs`

**Interfaces:**
- Consumes: Tasks 2, 3, 5.
- Produces:
  - `WorldEconomy.DayEnded` (`event Action<GameDate>`), raised by `OnDayStarted` after every market's `StartDay`.
  - `public static class HorizonChoices` (Game/Debug): `IReadOnlyList<string> Labels` = `"3 mois", "6 mois", "12 mois", "24 mois", "48 mois", "Toute la partie"`; `int DefaultIndex` = 2; `int? DaysOf(int index, int daysPerMonth)` (null for the last).
  - `SimulationRecorder : MonoBehaviour` with serialized `worldEconomy`, `mapView`, `worldClock`, optional `aiMerchants`, optional `cityPanel` (`CityInfoPanelController`). Disables itself outside debug builds. Once the economy is ready (first `Update` where `IsReady`), it builds a `SimulationRecord` over the cities of `mapView.Cities` that have a market, in that order, records day `clock.ElapsedDays` at once, then every `DayEnded`. Exposes `bool IsReady`, `SimulationRecord Record`, `IReadOnlyList<CityDefinition> Cities` (record order), `IReadOnlyList<string> GoodNames` (catalogue order), `CalendarDefinition Calendar`, `int HorizonIndex`, `void SetHorizon(int index)`, `string FormatDay(int day)`.
  - `WorldMapSetup` adds a "Simulation Recorder" object with its references and fills the debug panel's new `recorder` reference.

- [ ] **Step 1: Write the failing test** `HorizonChoicesTests`: `DaysOf(2, 30) == 360`, `DaysOf(0, 30) == 90`, `DaysOf(5, 30) == null`, `Labels[DefaultIndex] == "12 mois"`.
- [ ] **Step 2: Run it.** Expected: compile failure.
- [ ] **Step 3: Implement** `HorizonChoices`, `DayEnded`, `SimulationRecorder` and the setup. The recorder forwards `AiMerchants.Traded` to `Record.AddTrade` and `CityInfoPanelController.PlayerTraded` to `Record.AddPlayerTrade` (market from `worldEconomy.MarketOf(city)`), and passes `underWay` as `(city, good) => aiMerchants != null ? aiMerchants.UnderWayTo(Cities[city], good) : 0`.
- [ ] **Step 4: Run all EditMode tests**, then run `Build World Map Scene` through `Unity_RunCommand` and check the console has no error and the scene has a `SimulationRecorder` with its references.
- [ ] **Step 5: Measure**: in Play mode at ×600 for 30 real seconds, log the mean time of `RecordDay` with a `Stopwatch` (temporary log, removed before committing). Expected: under 1 ms a day.
- [ ] **Step 6: Commit** — `Débogage : enregistreur de la simulation dans la scène`.

---

### Task 7: The panel's tabs, header and size

**Files:**
- Create: `Assets/Scripts/Game/Debug/DebugTabs.cs`
- Modify: `Assets/UI/Debug/DebugPanel.uxml`, `Assets/UI/Debug/DebugPanel.uss`
- Modify: `Assets/Scripts/Game/Debug/DebugPanelController.cs`
- Test: `Assets/Tests/EditMode/Debug/DebugTabsTests.cs`

**Interfaces:**
- Consumes: `SimulationRecorder` (Task 6).
- Produces:
  - `public sealed class DebugTabs`: `DebugTabs(IReadOnlyList<Button> buttons, IReadOnlyList<VisualElement> pages)`; `int Selected`; `void Select(int index)` (shows its page, hides the others, toggles `debug-panel__tab--selected`; makes the buttons not focusable); `event Action<int> Changed`, raised by `Select` when the selected index changes (the buttons' clicks call `Select`).
  - UXML: the header has the five tabs, a `DropdownField` named `horizon` (choices `HorizonChoices.Labels`), the speed slider and the close button. One page per tab (`cities-page`, `curves-page`, `events-page`, `trades-page`, `ships-page`); pages with sub-tabs hold a row of sub-tab buttons (class `debug-panel__subtab`) and one sub-page per sub-tab. Names of the sub-pages: `population-page`, `markets-page`, `city-curves-page`, `indicator-curves-page`, `composed-curves-page`, `trade-journal-page`, `trade-flows-page`.
  - `.debug-panel` has `width: 90%` and `height: 100%` of the root's area under the time HUD (the root keeps its `padding-top: 110px`); only the content of a page scrolls.
  - `DebugPanelController`: new serialized `recorder` (`SimulationRecorder`). It owns the `DebugTabs`, the horizon drop-down (`recorder.SetHorizon`), and a dirty flag set by `recorder.Record.Recorded`; its `Update` refreshes the shown view at most once per frame when dirty (and the ships table every frame, as today). Each tab's view (Tasks 8, 10–13) implements `IDebugView { void Show(); void Hide(); void Refresh(); }`. Without a ready recorder, the tabs other than "Villes" and "Navires" show the label "Enregistrement indisponible."

- [ ] **Step 1: Write the failing tests** `DebugTabsTests`: `Select_ShowsOnlyItsPage`, `Select_MarksOnlyItsButton`, `Changed_IsRaisedOnlyOnAChange` (`Select(1)` twice raises it once), `Buttons_AreNotFocusable`.
- [ ] **Step 2: Run them.** Expected: compile failure.
- [ ] **Step 3: Implement** `DebugTabs`, the new UXML/USS and the controller's tabs, horizon and refresh loop; move the existing cities table into `markets-page` (its "Toutes les marchandises" view, see Task 8) and the ships table into `ships-page`, unchanged.
- [ ] **Step 4: Run all EditMode tests; in Play mode** (`Unity_RunCommand`) open the panel, select every tab and sub-tab and change the horizon: no console error.
- [ ] **Step 5: Commit** — `Débogage : onglets et sous-onglets, choix de l'historique`.

---

### Task 8: "Villes": Population and Marchés

**Files:**
- Create: `Assets/Scripts/Game/Debug/DebugPopulationTable.cs`
- Create: `Assets/Scripts/Game/Debug/DebugGoodMarketTable.cs`
- Create: `Assets/Scripts/Game/Debug/DebugCitiesView.cs` (`IDebugView` of the tab: sub-tabs and the good's drop-down)
- Modify: `Assets/UI/Debug/DebugPanel.uxml`, `DebugPanel.uss`, `DebugPanelController.cs`
- Test: `Assets/Tests/EditMode/Debug/DebugPopulationTableTests.cs`, `Assets/Tests/EditMode/Debug/DebugGoodMarketTableTests.cs`

**Interfaces:**
- Consumes: `DebugCitiesTable` formatters, `CityMarket`, `AiMerchants.UnderWayTo` (Task 2).
- Produces:
  - `DebugPopulationTable(VisualElement header, VisualElement rows, VisualElement totals)`; `AddCity(string name, CityMarket market)`; `Show()`/`Hide()` (follows `market.Changed` while shown). Columns, in order: "Ville", "Population", "Mendiants", "Riches", "Aisés", "Pauvres", "Croiss. totale", "Maisons de la ville" (`CityHouses`), with the formats and the total row of today's city columns (houses summed).
  - `DebugGoodMarketTable(VisualElement header, VisualElement rows, VisualElement totals)`; `AddCity(string name, CityMarket market, Func<int, int> underWay)` (`underWay(good)`, from `AiMerchants.UnderWayTo` for that city, or 0); `SetGood(int good)`; `Show()`/`Hide()`. Columns: "Ville", "Stock", "T0", "T1", "T2", "T3", "Achat", "Vente", "Prod.", "Conso.", "Net", "Ouv.", "En route (IA)". Thresholds as `FormatQuantity`, prices as `FormatNumber`, production/consumption/net/workers as `DebugCitiesTable` writes them. Total row: stock, production, consumption, net, workers, under way summed; thresholds and prices "—".
  - The "Marchés" drop-down lists "Toutes les marchandises" first, then the goods in `NameOrder`; "Toutes" shows today's `DebugCitiesTable`, a good shows `DebugGoodMarketTable`.

- [ ] **Step 1: Write the failing tests**, on the markets of `DebugCitiesTableTests.SetUp`:

```csharp
[Test] public void Population_HasTheCityHousesColumn()          // row of "Cologne": last cell "0"; after a house is built (StartDay on a crowded city), "1"
[Test] public void Population_RowsAreAlphabetical()
[Test] public void GoodMarket_WritesThresholdsAndPrices()       // fish of the 1 000 city: T0 cell == FormatQuantity(ThresholdsOf(fish).T0), Achat == FormatNumber(BuyPriceOf(fish))
[Test] public void GoodMarket_WritesUnderWay()                   // underWay returns 40 → "40"; total sums both cities
[Test] public void GoodMarket_AGoodTheMarketLacks_IsADash()      // SetGood beyond village.GoodCount → every cell NoFigure
[Test] public void GoodMarket_TotalsHaveNoThresholdNorPrice()
```

- [ ] **Step 2: Run them.** Expected: compile failure.
- [ ] **Step 3: Implement** the two tables (rows striped with `ListStripes.Apply`, `NameOrder` for the rows) and `DebugCitiesView`.
- [ ] **Step 4: Run all EditMode tests; in Play mode** check both sub-tabs fill and follow the days.
- [ ] **Step 5: Commit** — `Débogage : onglet Villes en deux sous-onglets, seuils et prix`.

---

### Task 9: The time series chart

**Files:**
- Create: `Assets/Scripts/Game/Debug/ChartMath.cs`
- Create: `Assets/Scripts/Game/Debug/TimeSeriesChart.cs`
- Modify: `Assets/UI/Debug/DebugPanel.uss` (`.debug-chart`, `.debug-chart__title`, `.debug-chart__tooltip`)
- Test: `Assets/Tests/EditMode/Debug/ChartMathTests.cs`

**Interfaces:**
- Produces:
  - `public static class ChartMath`: `bool TryRange(IReadOnlyList<float[]> series, int count, out float min, out float max)` (ignores NaN; false when nothing is finite; a flat range widens to `[v − 1, v + 1]`, or `[0, 1]` for 0); `float YOf(float value, float min, float max, float height)` (top is 0); `int DayIndexAt(float x, float width, int count)` (nearest point, clamped to `[0, count − 1]`, −1 when `count` is 0); `float XOf(int index, int count, float width)` (a single point at the middle).
  - `public enum ChartSeriesStyle { Line, Dashed, StackedArea }`.
  - `public sealed class ChartSeries { string Name; Color Color; ChartSeriesStyle Style; float[] Values; Func<float, string> Format; bool OwnScale; }`.
  - `public sealed class ChartBand { float[] Lower; float[] Upper; Color Color; }` (per-day bounds, so that bands follow the thresholds).
  - `public sealed class TimeSeriesChart : VisualElement`: `TimeSeriesChart(string title)`; `void SetData(int count, Func<int, string> dayLabel, IReadOnlyList<ChartSeries> series, IReadOnlyList<ChartBand> bands, float? commonMin = null, float? commonMax = null)`; draws with `generateVisualContent` and `Painter2D` (lines, dashes as short segments, stacked areas cumulated in list order, bands as filled polygons clipped to the range); every series uses one shared range (or the common one) except those with `OwnScale`, which get their own; `MarkDirtyRepaint` on `SetData`. On `PointerMoveEvent` it shows a tooltip label: `dayLabel(index)` then one line per series, `Name : Format(value)`; hidden on `PointerLeaveEvent`. NaN values break the line.

- [ ] **Step 1: Write the failing tests** `ChartMathTests`: `TryRange_IgnoresNaN`, `TryRange_OfNothing_IsFalse`, `TryRange_Flat_Widens` (all 5 → 4 and 6; all 0 → 0 and 1), `DayIndexAt_IsTheNearestDay` (width 100, count 11, x 14 → 1, x −5 → 0, x 500 → 10), `DayIndexAt_WithNoDay_IsMinusOne`, `XOf_SinglePoint_IsCentred`, `YOf_MaxIsTop_MinIsBottom`.
- [ ] **Step 2: Run them.** Expected: compile failure.
- [ ] **Step 3: Implement** `ChartMath` and `TimeSeriesChart`.
- [ ] **Step 4: Run all EditMode tests.**
- [ ] **Step 5: Commit** — `Débogage : graphique de séries temporelles`.

---

### Task 10: "Courbes" › "Par ville"

**Files:**
- Create: `Assets/Scripts/Game/Debug/DebugIndicators.cs`
- Create: `Assets/Scripts/Game/Debug/DebugCityCurvesView.cs`
- Create: `Assets/Scripts/Game/Debug/DebugCurvesView.cs` (`IDebugView` of the tab: its three sub-tabs)
- Test: `Assets/Tests/EditMode/Debug/DebugIndicatorsTests.cs`

**Interfaces:**
- Consumes: `SimulationRecord` (Task 5), `TimeSeriesChart` (Task 9), `SimulationRecorder` (Task 6).
- Produces:
  - `public readonly struct DebugIndicator { bool IsGood; CityIndicator City; GoodIndicator Good; string Name; bool AddsUp; }`; `public static class DebugIndicators`: `IReadOnlyList<DebugIndicator> All` (every `CityIndicator` then every `GoodIndicator`, sorted by `Name` with `NameOrder`); `float ValueOf(SimulationHistory history, int index, int city, DebugIndicator indicator, int good)`; `float WorldValueOf(SimulationHistory history, int index, int cityCount, DebugIndicator indicator, int good)` (sum over the cities, NaN skipped; NaN for an indicator that does not add up); `string Format(DebugIndicator indicator, float value)`.
  - Names: "Habitants", "Riches", "Aisés", "Pauvres", "Place des riches", "Place des aisés", "Place des pauvres", "Satisfaction des riches", "Satisfaction des aisés", "Satisfaction des pauvres", "Mendiants", "Cible des mendiants", "Maisons bâties par la ville"; "Stock", "Seuil T0", "Seuil T1", "Seuil T2", "Seuil T3", "Prix d'achat", "Prix de vente", "Production théorique (semaine)", "Production réelle (7 jours)", "Consommation théorique (semaine)", "Consommation réelle (7 jours)", "Manque (jour)", "Ouvriers", "Réserve", "En route (IA)". `AddsUp` is false for the three satisfactions and the two prices.
  - `DebugCityCurvesView`: the cities as a column of buttons (`NameOrder`), the selected one marked; on the right, a "Population" row of three charts — "Habitants par classe et mendiants" (stacked areas Rich, Wealthy, Poor, Beggars), "Satisfaction" (three lines), "Ouvriers et réserve" (total workers and total reserve over the goods) — then a "Marchandises" grid of one chart per good (`NameOrder`), titled with `GoodUnitText.NameWithUnit`: stock as a line over four bands (under T0 red, T0–T1 amber, T1–T2 green, T2–T3 blue, the colours of the mockup), buy price as a dashed line with its own scale. The selected city is kept while the panel is closed.

- [ ] **Step 1: Write the failing tests** `DebugIndicatorsTests`: `All_IsAlphabetical`, `All_HasEveryIndicatorOnce` (13 + 15), `WorldValue_SumsTheCities` (two cities' stocks 10 and 5 → 15), `WorldValue_OfASatisfaction_IsNaN`, `WorldValue_SkipsNaN`.
- [ ] **Step 2: Run them.** Expected: compile failure.
- [ ] **Step 3: Implement** `DebugIndicators`, `DebugCityCurvesView` and `DebugCurvesView` (sub-tabs through `DebugTabs`; the other two sub-pages filled in Task 11). `Refresh` rebuilds the series arrays of the shown city from the history, reusing the arrays when the day count has not changed.
- [ ] **Step 4: Run all EditMode tests; in Play mode** at ×600, check the charts move day by day, the hover shows the date and values, and the frame rate holds with the sub-tab shown.
- [ ] **Step 5: Commit** — `Débogage : courbes par ville`.

---

### Task 11: "Courbes" › "Par indicateur" and "Composer"

**Files:**
- Create: `Assets/Scripts/Game/Debug/DebugIndicatorCurvesView.cs`
- Create: `Assets/Scripts/Game/Debug/DebugComposedCurvesView.cs`
- Create: `Assets/Scripts/Game/Debug/ComposedSeries.cs`
- Modify: `Assets/Scripts/Game/Debug/DebugCurvesView.cs`, `Assets/UI/Debug/DebugPanel.uss`
- Test: `Assets/Tests/EditMode/Debug/ComposedSeriesTests.cs`

**Interfaces:**
- Consumes: `DebugIndicators` (Task 10), `TimeSeriesChart` (Task 9).
- Produces:
  - `DebugIndicatorCurvesView`: drop-downs "Indicateur" (`DebugIndicators.All`) and "Marchandise" (goods in `NameOrder`, disabled when the indicator is a city's), a toggle "Échelle commune"; a grid of one small chart per city (`NameOrder`), then a "Monde" chart when the indicator adds up. With "Échelle commune", every chart gets the min and max over all cities (`commonMin`/`commonMax`).
  - `public readonly struct ComposedSeries { int City; DebugIndicator Indicator; int Good; }` and `public sealed class ComposedSeriesList`: `bool TryAdd(ComposedSeries s)` (false for a duplicate); `void Remove(int index)`; `IReadOnlyList<ComposedSeries> Items`; `Color ColorOf(int index)` (a fixed palette of 8 colours, cycled); `string LabelOf(ComposedSeries s, IReadOnlyList<string> cityNames, IReadOnlyList<string> goodNames)` = `"Corvaux · Blé · Prix d'achat"` (no good for a city indicator).
  - `DebugComposedCurvesView`: drop-downs "Ville", "Marchandise", "Indicateur" and a button "Ajouter"; the series as chips (label in its colour, a "✕" button removing it); one large chart where every series has `OwnScale`.

- [ ] **Step 1: Write the failing tests** `ComposedSeriesTests`: `TryAdd_RefusesADuplicate`, `Remove_KeepsTheOthersInOrder`, `Colours_Cycle` (`ColorOf(8) == ColorOf(0)`), `Label_OfACityIndicator_HasNoGood`, `Label_OfAGoodIndicator` (`"Corvaux · Blé · Prix d'achat"`).
- [ ] **Step 2: Run them.** Expected: compile failure.
- [ ] **Step 3: Implement** both views.
- [ ] **Step 4: Run all EditMode tests; in Play mode** check both sub-tabs, the common scale, the "Monde" chart and adding/removing chips.
- [ ] **Step 5: Commit** — `Débogage : courbes par indicateur et graphique composé`.

---

### Task 12: "Événements"

**Files:**
- Create: `Assets/Scripts/Game/Debug/DebugEventText.cs`
- Create: `Assets/Scripts/Game/Debug/DebugEventFilter.cs`
- Create: `Assets/Scripts/Game/Debug/DebugEventsView.cs`
- Modify: `Assets/UI/Debug/DebugPanel.uxml`, `DebugPanel.uss`
- Test: `Assets/Tests/EditMode/Debug/DebugEventTextTests.cs`, `Assets/Tests/EditMode/Debug/DebugEventFilterTests.cs`

**Interfaces:**
- Consumes: `SimulationRecord.Events`, `EventsTrimmed` (Task 5).
- Produces:
  - `DebugEventText`: `string TypeName(SimulationEventType t)` — "Pénurie", "Seuil", "Embauche", "Licenciement", "Arrêt", "Reprise", "Départ", "Classes", "Mendiants" (arrived and left), "Maison"; `string Detail(SimulationEvent e, IReadOnlyList<string> goodNames)`, for example "Blé : 2,0 manquants", "Blé : le stock passe de T1–T2 à T0–T1", "Blé : 4 ouvriers embauchés", "Blé : 8 ouvriers en réserve", "Blé : production arrêtée (stock au-dessus de T3)", "Blé : production reprise", "48 pauvres quittent la ville, 12 ouvriers perdus" (with `WorkersLeft` in `To`), "12 pauvres deviennent aisés" / "3 riches redeviennent pauvres", "5 mendiants arrivent" / "2 mendiants partent", "La ville construit une maison de pauvres". Band names: "sous T0", "T0–T1", "T1–T2", "T2–T3", "au-dessus de T3".
  - `DebugEventFilter`: `int City` (−1 all), `int Good` (−1 all), `bool IsShown(SimulationEventType t)`, `void SetShown(SimulationEventType t, bool shown)`; by default every type is shown but `ThresholdCrossing`; `bool Matches(SimulationEvent e)`; a good filter hides the city events (`Good` −1).
  - `DebugEventsView`: drop-downs "Ville" and "Marchandise" ("Toutes" first), one toggle per type (in the order of the enum, labelled with `TypeName`), a toggle "Figer", and a `ListView` (columns "Date", "Ville", "Type", "Détail"), newest first. It keeps the matching events as absolute positions (`EventsTrimmed + index`): new events are filtered as they come, trimmed ones dropped from the end of the list; a filter change rebuilds. While frozen, the list does not change; it catches up when unfrozen.

- [ ] **Step 1: Write the failing tests**: `DebugEventTextTests` — one test per type checking the exact French detail above with the good "Blé"; `DebugEventFilterTests` — `Default_HidesThresholdCrossings`, `CityFilter`, `GoodFilter_HidesCityEvents`, `TypeFilter`.
- [ ] **Step 2: Run them.** Expected: compile failure.
- [ ] **Step 3: Implement** the three classes.
- [ ] **Step 4: Run all EditMode tests; in Play mode** at ×600 with every type shown: the list scrolls without stutter; "Figer" stops it; shortening the horizon while frozen raises no error.
- [ ] **Step 5: Commit** — `Débogage : journal des événements des villes`.

---

### Task 13: "Échanges": Journal and Flux

**Files:**
- Create: `Assets/Scripts/Game/Debug/DebugTradeText.cs`
- Create: `Assets/Scripts/Game/Debug/DebugTradesView.cs`
- Create: `Assets/Scripts/Game/Debug/DebugFlowsTable.cs`
- Modify: `Assets/UI/Debug/DebugPanel.uxml`, `DebugPanel.uss`
- Test: `Assets/Tests/EditMode/Debug/DebugTradeTextTests.cs`, `Assets/Tests/EditMode/Debug/DebugFlowsTableTests.cs`

**Interfaces:**
- Consumes: `SimulationRecord.Trades`, `FlowOf` (Task 5); `ShipsView.DisplayNameOf` for the AI ships' names.
- Produces:
  - `DebugTradeText`: `string Operation(TradeRecord t)` — "Achat", "Vente", "Construction"; `string Trader(TradeRecord t, Func<Ship, string> shipName)` — the ship's name, or "Joueur"; `string UnitPrice(TradeRecord t)` = `FormatNumber(Gold / Units)` (rounded to the nearest); `string Profit(TradeRecord t)` = `NoFigure` when null, else signed and coloured as `FormatNet` does but for whole gold ("+1 480", "-90").
  - `DebugTradesView`: the "Journal" sub-tab is a `ListView`, newest first, columns "Date", "Navire", "Ville", "Opération", "Marchandise", "Qté", "Prix moyen", "Total", "Bénéfice". The "Flux par marchandise" sub-tab has a good drop-down (`NameOrder`) and a `DebugFlowsTable`.
  - `DebugFlowsTable(VisualElement header, VisualElement rows, VisualElement totals)`; `void Write(IReadOnlyList<string> cityNames, Func<int, (long received, long shipped)> flowOf)`; columns "Ville", "Reçu", "Expédié", "Net" (net = received − shipped, coloured as `FormatNet`); rows in `NameOrder`, a total row.

- [ ] **Step 1: Write the failing tests**: `DebugTradeTextTests` — `AnAiSale` (operation "Vente", profit "+1 480" coloured), `APlayerConstruction` (trader "Joueur", operation "Construction", profit "—"), `UnitPrice_IsRounded` (Gold 1 160 for 40 → "29"); `DebugFlowsTableTests` — `Net_IsReceivedMinusShipped`, `Rows_AreAlphabetical`, `Totals`.
- [ ] **Step 2: Run them.** Expected: compile failure.
- [ ] **Step 3: Implement** the three classes.
- [ ] **Step 4: Run all EditMode tests; in Play mode** buy and sell with the player's ship and build a house: the three trades appear marked "Joueur"; the AI trades appear with their profit.
- [ ] **Step 5: Commit** — `Débogage : journal et flux des échanges`.

---

### Task 14: Rules and final check

**Files:**
- Modify: `.claude/rules/debug-tools.md`
- Modify: `CLAUDE.md` (the "Runtime UI" line on labels rewritten every frame names the debug views refreshed once per frame)

- [ ] **Step 1: Update `debug-tools.md`**: the recorder (debug builds only, records from the start, `WorldEconomy.DayEnded`), `CityDayReport` and its rule (filling it changes no figure), the trade reports (`AiMerchantFleet.Traded`, `Construction`'s callback, `CityInfoPanelController.PlayerTraded`), the horizon (session only), the refresh once per frame, the `ListView` stripes exception, and what counts as a shortage and a crossing.
- [ ] **Step 2: Run all EditMode tests** (`run-tests`). Expected: all pass.
- [ ] **Step 3: Final check in Play mode** with the user, at ×600 for a game year: every tab and sub-tab fills; the record changes nothing (with the panel closed and open, the cities' figures follow the same course as before this work, the existing tests being the proof for the logic).
- [ ] **Step 4: Commit** — `Débogage : règles de l'observatoire`.
