# Patrician III Goods Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The twenty goods of Patrician III replace the six of the game, with their decimal base prices, and goods are counted either in barrels or in bushels ("boisseaux", 10 barrels of room in a hold).

**Architecture:** A Core `GoodUnits` gives the barrels of room a unit of each good takes; `CargoHold` counts units of each good and room in barrels; `Trade` and `TradeRoutePlanner` never split a unit and the planner weighs margins per barrel of room. Markets already count in the good's unit and only get a decimal base price. In `Game`, `GoodDefinition` has a `GoodUnit`, `EconomyDefinition` the barrels per bushel, ships get their units from the economy, and the panels write a quantity with its unit. The content (twenty goods, the catalogue, the cities' productions) is written last.

**Tech Stack:** Unity 6000.6.4f1, C#, UI Toolkit (UXML + USS), Unity Test Framework (NUnit, EditMode), Python for the asset files.

**Spec:** `docs/superpowers/specs/2026-10-15-patrician-goods-design.md`

## Global Constraints

- All code, comments, log messages, asset and folder names are in English. Everything the player reads is in French: "tonneau(x)", "boisseau(x)".
- `Core` contains no `MonoBehaviour` and nothing that needs a scene, and does not reference `Game`.
- Defaults: a bushel takes 10 barrels of room; the merchant ship keeps 200 barrels of room.
- The good table of the spec (order, names, units, base prices, consumption, food, maximum workers, output per worker) and its city productions are the content, copied as they are.
- Never hand-write `.meta` files: Unity generates them on import. A renamed asset keeps its `.meta` (renamed with it); a deleted asset loses it.
- Never edit the generated `.sln` / `.csproj`. Exclude `Library/` from searches.
- Match the surrounding code: Allman braces, a blank line before `return` and after blocks as in the existing files, XML `<summary>` on public types and members, "The negated comparison also rejects NaN." where a negated comparison validates.
- Commit directly on `main`. Commit messages are in French and end with:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
- `PlayerStartContentTests.ThePlayer_StartsWithTenThousandGold` fails since the user's commit d8b51f3 (starting gold 1 000 000); it is not this plan's to fix, and every other test must pass.
- Differences from the spec's wording, intentional:
  - `TradePlan` gets `UnitsOf` and `TotalUnits` but no `Room`: nothing reads it; tests work it out from the units.
  - `TradeResult.Barrels` becomes `TradeResult.Units`, like the other members counted in units.
  - `GoodUnits.RoomOf` gives 1 for a good beyond its list, so that `GoodUnits.AllBarrels` is an empty list and holds of tests keep one barrel per unit.
  - `ShipsView` without an economy gives its holds one barrel per unit, with a warning, as it already copes with missing references.

## Review Focus

Conditions the spec implies and that are most likely to bite:

1. A hold with free room under a bushel (9 barrels) buying a bushel good: nothing is bought, the buy price is disabled, no exception. Tests in Task 2 (`Buy_StopsWhenNotOneBushelFits`) and Task 5 (`ABuyPrice_IsDisabled_WithoutRoomForOneUnit`).
2. Mixed cargo in the planner: a bushel that pays more per unit but less per barrel of room loses to a barrel good, and a bushel that no longer fits is skipped while barrels still load. Tests in Task 3.
3. Selling bushels frees ten barrels each, and a hold's room never goes negative or above its capacity. Test in Task 1 (`TryRemove_FreesTheRoomOfItsUnits`).
4. A decimal base price at a stock where the factor is whole: the rounding margin still keeps whole prices whole. Test in Task 4 (`ADecimalBasePrice_GivesWholeCoins`).
5. A barrels per bushel under 1 typed in the asset: refused and named by `TryValidateSettings`, and ships fall back to one barrel per unit instead of failing. Test in Task 5 (`TryValidateSettings_SaysWhenABushelHasNoRoom`).

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
        testNames = null, // or groupNames = new[] { "DarkFantasyMerchant\\.Tests\\.EditMode\\.CargoHoldTests" }
    }));
```

The run writes `C:\Users\Spellman\AppData\LocalLow\DefaultCompany\Dark Fantasy Merchant\TestResults.xml`: read its `<test-run>` line and the `<test-case ... result="Failed">` entries. `Unity_RunCommand` refuses code that writes files with `System.IO` or needs interaction; log to the console instead. In Play mode, the unfocused Editor runs at a few frames: set `Application.runInBackground = true` for the run and back to false after.

## File Structure

| File | Responsibility |
|---|---|
| `Assets/Scripts/Core/GoodUnits.cs` (new) | Barrels of room a unit of each good takes. |
| `Assets/Scripts/Core/CargoHold.cs` (modify) | Units of each good, room in barrels. |
| `Assets/Scripts/Core/Trade.cs`, `TradeResult.cs` (modify) | Trades in units that fit. |
| `Assets/Scripts/Core/TradeRoutePlanner.cs`, `TradePlan.cs`, `AiMerchantFleet.cs` (modify) | Cargo by margin per barrel of room. |
| `Assets/Scripts/Core/MarketGood.cs`, `MarketPricing.cs` (modify) | Decimal base price. |
| `Assets/Scripts/Game/GoodUnit.cs` (new), `GoodUnitText.cs` (new) | The unit of a good; its French text. |
| `Assets/Scripts/Game/GoodDefinition.cs`, `EconomyDefinition.cs` (modify) | Unit, decimal base price, barrels per bushel. |
| `Assets/Scripts/Game/ShipsView.cs`, `AiMerchants.cs` (modify) | Units for holds and the planner. |
| `Assets/Scripts/Game/CityMarketSection.cs`, `CityInfoPanelController.cs`, `ShipInfoPanelController.cs`, `DebugPanelController.cs`, `DebugShipsTable.cs` (modify) | Units on screen. |
| `Assets/UI/WorldMap/CityInfoPanel.uxml`, `CityInfoPanel.uss` (modify) | Scrolling market rows. |
| `Assets/Scripts/Editor/WorldMapSetup.cs` (modify) | Sample content; wires the ships view's economy. |
| `Assets/Data/Goods/*.asset`, `Assets/Data/Economy/Economy.asset`, `Assets/Data/Cities/*.asset`, `Assets/Scenes/WorldMap.unity` (modify) | Content; the scene's reference. |
| `CLAUDE.md` (modify) | The goods and the bushel. |

---

### Task 1: Units in the hold

**Files:**
- Create: `Assets/Scripts/Core/GoodUnits.cs`
- Modify: `Assets/Scripts/Core/CargoHold.cs`, and every reader of `CargoHold.BarrelsOf`: `Assets/Scripts/Core/Trade.cs`, `Assets/Scripts/Core/AiMerchantFleet.cs`, `Assets/Scripts/Game/CityMarketSection.cs`, `Assets/Scripts/Game/ShipInfoPanelController.cs`, `Assets/Scripts/Game/DebugShipsTable.cs`
- Test: `Assets/Tests/EditMode/GoodUnitsTests.cs` (new), `Assets/Tests/EditMode/CargoHoldTests.cs` and every test calling `CargoHold.BarrelsOf`

**Interfaces:**
- Produces: `public sealed class GoodUnits` with `GoodUnits(IReadOnlyList<int> rooms)` (throws `ArgumentNullException`, and `ArgumentOutOfRangeException` for a room under 1), `static GoodUnits AllBarrels`, `int Count`, `int RoomOf(int good)` (1 beyond the list; throws for a negative good). `CargoHold(int capacity, GoodUnits units = null)`, `int UnitsOf(int good)` (replaces `BarrelsOf`), `int RoomFor(int good)` (whole units that still fit), `TryAdd(int good, int units)`, `TryRemove(int good, int units)`; `Capacity`, `Used`, `Free` stay barrels of room.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/GoodUnitsTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class GoodUnitsTests
    {
        [Test]
        public void RoomOf_IsTheBarrelsAUnitTakes()
        {
            var units = new GoodUnits(new[] { 10, 1, 10 });

            Assert.AreEqual(3, units.Count);
            Assert.AreEqual(10, units.RoomOf(0));
            Assert.AreEqual(1, units.RoomOf(1));
            Assert.AreEqual(10, units.RoomOf(2));
        }

        [Test]
        public void AGoodBeyondTheList_TakesOneBarrel()
        {
            Assert.AreEqual(1, new GoodUnits(new[] { 10 }).RoomOf(5));
            Assert.AreEqual(0, GoodUnits.AllBarrels.Count);
            Assert.AreEqual(1, GoodUnits.AllBarrels.RoomOf(3));
        }

        [Test]
        public void TheRooms_AreCopied()
        {
            var rooms = new[] { 10 };
            var units = new GoodUnits(rooms);

            rooms[0] = 3;

            Assert.AreEqual(10, units.RoomOf(0));
        }

        [Test]
        public void RejectsARoomUnderOneBarrel_AMissingList_AndANegativeGood()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GoodUnits(new[] { 10, 0 }));
            Assert.Throws<ArgumentNullException>(() => new GoodUnits(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => GoodUnits.AllBarrels.RoomOf(-1));
        }
    }
}
```

In `Assets/Tests/EditMode/CargoHoldTests.cs`, rename every `BarrelsOf` to `UnitsOf` (the tests keep their figures: without units every unit takes one barrel), then add:

```csharp
        // Wheat in bushels of ten barrels, wine in barrels.
        private static GoodUnits Units => new GoodUnits(new[] { 10, 1, 1, 1, 1, 1, 1, 1, 1 });

        [Test]
        public void ABushel_TakesTenBarrelsOfRoom()
        {
            var hold = new CargoHold(200, Units);

            Assert.IsTrue(hold.TryAdd(Wheat, 3));
            Assert.IsTrue(hold.TryAdd(Wine, 20));

            Assert.AreEqual(3, hold.UnitsOf(Wheat));
            Assert.AreEqual(20, hold.UnitsOf(Wine));
            Assert.AreEqual(50, hold.Used);
            Assert.AreEqual(150, hold.Free);
        }

        [Test]
        public void RoomFor_IsTheWholeUnitsThatStillFit()
        {
            var hold = new CargoHold(200, Units);
            hold.TryAdd(Wine, 191);

            Assert.AreEqual(9, hold.Free);
            Assert.AreEqual(0, hold.RoomFor(Wheat));
            Assert.AreEqual(9, hold.RoomFor(Wine));
        }

        [Test]
        public void TryAdd_ABushelThatDoesNotFit_IsRefused()
        {
            var hold = new CargoHold(25, Units);

            Assert.IsFalse(hold.TryAdd(Wheat, 3));
            Assert.AreEqual(0, hold.Used);
            Assert.IsTrue(hold.TryAdd(Wheat, 2));
            Assert.AreEqual(5, hold.Free);
        }

        [Test]
        public void TryRemove_FreesTheRoomOfItsUnits()
        {
            var hold = new CargoHold(200, Units);
            hold.TryAdd(Wheat, 5);

            Assert.IsTrue(hold.TryRemove(Wheat, 2));

            Assert.AreEqual(3, hold.UnitsOf(Wheat));
            Assert.AreEqual(30, hold.Used);
            Assert.AreEqual(170, hold.Free);
            Assert.IsFalse(hold.TryRemove(Wheat, 4));
            Assert.AreEqual(30, hold.Used);
        }
```

In every other test file that calls `CargoHold.BarrelsOf` (`TradeTests.cs`, `CityMarketSectionTests.cs`, `TradeRoutePlannerTests.cs` where it reads a hold), rename it to `UnitsOf`; `TradePlan.BarrelsOf` is renamed in Task 3, not here.

- [ ] **Step 2: Refresh.** Expected: compile errors, `GoodUnits` does not exist and `CargoHold` has no `UnitsOf`.

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/Core/GoodUnits.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The room a unit of each good takes in a hold, in barrels: one for a good counted in
    /// barrels, more for one counted in bushels. Goods are indices into the catalogue; a
    /// good beyond the list takes one barrel.
    /// </summary>
    public sealed class GoodUnits
    {
        private readonly int[] rooms;

        /// <param name="rooms">Barrels of room a unit of each good takes, by index, at least 1. Copied.</param>
        public GoodUnits(IReadOnlyList<int> rooms)
        {
            if (rooms == null)
            {
                throw new ArgumentNullException(nameof(rooms));
            }

            this.rooms = new int[rooms.Count];

            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i] < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(rooms), "A unit takes at least one barrel of room.");
                }

                this.rooms[i] = rooms[i];
            }
        }

        /// <summary>Every good counted in barrels.</summary>
        public static GoodUnits AllBarrels { get; } = new GoodUnits(new int[0]);

        public int Count => rooms.Length;

        /// <summary>Barrels of room a unit of a good takes.</summary>
        public int RoomOf(int good)
        {
            if (good < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(good));
            }

            return good < rooms.Length ? rooms[good] : 1;
        }
    }
}
```

Replace `Assets/Scripts/Core/CargoHold.cs` by:

```csharp
using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The goods aboard one ship, out of the room it has. Goods are indices into the
    /// catalogue and are counted in their own unit, a barrel or a bushel; the room they
    /// take is counted in barrels, a bushel taking several.
    /// </summary>
    public sealed class CargoHold
    {
        // Units of each good, by index; grown when a good is first loaded.
        private readonly List<int> unitsByGood = new List<int>();
        private readonly GoodUnits units;

        /// <param name="capacity">Barrels of room; zero for a ship that carries nothing.</param>
        /// <param name="units">The room a unit of each good takes. Without it, every unit takes one barrel.</param>
        public CargoHold(int capacity, GoodUnits units = null)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Capacity = capacity;
            this.units = units ?? GoodUnits.AllBarrels;
        }

        /// <summary>Barrels of room.</summary>
        public int Capacity { get; }

        /// <summary>Barrels of room taken, all goods together.</summary>
        public int Used { get; private set; }

        public int Free => Capacity - Used;

        /// <summary>Raised when goods were loaded or unloaded, not by a refusal.</summary>
        public event Action Changed;

        /// <summary>Units of a good aboard.</summary>
        public int UnitsOf(int good)
        {
            RequireGood(good);

            return good < unitsByGood.Count ? unitsByGood[good] : 0;
        }

        /// <summary>The whole units of a good the free room still takes.</summary>
        public int RoomFor(int good)
        {
            RequireGood(good);

            return Free / units.RoomOf(good);
        }

        /// <returns>False when the hold has no room for all of them; nothing was loaded then.</returns>
        public bool TryAdd(int good, int count)
        {
            RequireGood(good);
            RequirePositive(count);

            long room = (long)count * units.RoomOf(good);

            if (room > Free)
            {
                return false;
            }

            while (unitsByGood.Count <= good)
            {
                unitsByGood.Add(0);
            }

            unitsByGood[good] += count;
            Used += (int)room;
            Changed?.Invoke();
            return true;
        }

        /// <returns>False when fewer are aboard; nothing was unloaded then.</returns>
        public bool TryRemove(int good, int count)
        {
            RequireGood(good);
            RequirePositive(count);

            if (count > UnitsOf(good))
            {
                return false;
            }

            unitsByGood[good] -= count;
            Used -= count * units.RoomOf(good);
            Changed?.Invoke();
            return true;
        }

        private static void RequireGood(int good)
        {
            if (good < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(good));
            }
        }

        private static void RequirePositive(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
        }
    }
}
```

In `Trade.cs`, `AiMerchantFleet.cs`, `CityMarketSection.cs`, `ShipInfoPanelController.cs` and `DebugShipsTable.cs`, rename the calls `.BarrelsOf(` on a hold (`hold.`, `ship.Cargo.`, `other.Ship.Cargo.`, `shownCargo.`) to `.UnitsOf(`; `plan.BarrelsOf` stays until Task 3.

- [ ] **Step 4: Refresh and run all EditMode tests.** Expected: all pass but the known gold test.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts Assets/Tests
git commit -m "Marchandises : place en cale par unité, un boisseau prenant plusieurs tonneaux

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Trade in units that fit

**Files:**
- Modify: `Assets/Scripts/Core/Trade.cs`, `Assets/Scripts/Core/TradeResult.cs`, and every reader of `TradeResult.Barrels`
- Test: `Assets/Tests/EditMode/TradeTests.cs`

**Interfaces:**
- Consumes: `CargoHold.RoomFor`, `UnitsOf` (Task 1).
- Produces: `TradeResult(int units, long gold)` with `Units` (replaces `Barrels`); the four `Trade.Buy` / `Trade.Sell` take `int units` and buy at most `hold.RoomFor(good)`.

- [ ] **Step 1: Write the failing tests**

In `Assets/Tests/EditMode/TradeTests.cs`, rename every `.Barrels` of a result to `.Units`, then add:

```csharp
        [Test]
        public void Buy_StopsWhenNotOneBushelFits()
        {
            // The good counted in bushels of ten barrels; nine barrels of room left.
            hold = new CargoHold(29, new GoodUnits(new[] { 10 }));

            TradeResult result = Trade.Buy(market, hold, treasury, Good, 10);

            Assert.AreEqual(2, result.Units);
            Assert.AreEqual(9, hold.Free);
            Assert.AreEqual(0, Trade.Buy(market, hold, treasury, Good, 1).Units);
            Assert.AreEqual(0, Trade.Buy(market, hold, Good, 1).Units);
        }

        [Test]
        public void SellingBushels_FreesTheirRoom()
        {
            hold = new CargoHold(200, new GoodUnits(new[] { 10 }));
            Trade.Buy(market, hold, treasury, Good, 5);

            TradeResult result = Trade.Sell(market, hold, treasury, Good, 3);

            Assert.AreEqual(3, result.Units);
            Assert.AreEqual(2, hold.UnitsOf(Good));
            Assert.AreEqual(180, hold.Free);
        }
```

- [ ] **Step 2: Refresh and run `TradeTests`.** Expected: compile error, `TradeResult` has no `Units`.

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/TradeResult.cs`:

```csharp
namespace DarkFantasyMerchant.Core
{
    /// <summary>What a purchase or a sale came to: it can be less than what was asked for.</summary>
    public readonly struct TradeResult
    {
        public TradeResult(int units, long gold)
        {
            Units = units;
            Gold = gold;
        }

        /// <summary>Units of the good that changed hands, barrels or bushels; zero when nothing could be traded.</summary>
        public int Units { get; }

        /// <summary>Gold paid for a purchase, or received for a sale.</summary>
        public long Gold { get; }
    }
}
```

In `Assets/Scripts/Core/Trade.cs`:

1. Every parameter `int barrels` becomes `int units`, and `nameof(barrels)` becomes `nameof(units)`; in the summaries "barrels" becomes "units" ("Units are priced one by one ... a trade stops at the first unit that cannot be traded"; "Buys up to <paramref name="units"/> units: as many as the city has, the hold has room for and the gold covers").
2. The two limits of a purchase use the room for whole units:

```csharp
            int limit = Math.Min(units, Math.Min(market.AvailableOf(good), hold.RoomFor(good)));
```

```csharp
            int bought = Math.Min(units, Math.Min(market.AvailableOf(good), hold.RoomFor(good)));
```

3. In `Sell`, `int sold = Math.Min(barrels, hold.UnitsOf(good));` becomes `int sold = Math.Min(units, hold.UnitsOf(good));`, and `result.Barrels > 0` becomes `result.Units > 0`.

Rename `.Barrels` of a `TradeResult` wherever it is read in `Assets/Scripts` (none is expected outside `Trade.cs`; search for it).

- [ ] **Step 4: Refresh and run all EditMode tests.** Expected: all pass but the known gold test.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts Assets/Tests
git commit -m "Commerce : achats et ventes en unités entières, dans la place de la cale

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: The planner by room

**Files:**
- Modify: `Assets/Scripts/Core/TradeRoutePlanner.cs`, `Assets/Scripts/Core/TradePlan.cs`, `Assets/Scripts/Core/AiMerchantFleet.cs`
- Test: `Assets/Tests/EditMode/TradeRoutePlannerTests.cs`, `Assets/Tests/EditMode/AiMerchantFleetTests.cs`

**Interfaces:**
- Consumes: `GoodUnits` (Task 1).
- Produces: `TradeRoutePlanner(IReadOnlyList<CityMarket> markets, double[,] travelDays, double stopoverDays, GoodUnits units = null)`; `capacity` arguments are barrels of room; `TradePlan(int destination, int[] units, long profit, double profitPerDay)`, `TradePlan.UnitsOf(int good)` and `TotalUnits` (replace `BarrelsOf` and `TotalBarrels`).

- [ ] **Step 1: Write the failing tests**

In `Assets/Tests/EditMode/TradeRoutePlannerTests.cs` and `AiMerchantFleetTests.cs`, rename `plan.BarrelsOf` to `plan.UnitsOf` and `TotalBarrels` to `TotalUnits`. Then add to `TradeRoutePlannerTests`:

```csharp
        // Fish in bushels of ten barrels, iron in barrels.
        private static GoodUnits FishInBushels => new GoodUnits(new[] { 10, 1 });

        [Test]
        public void TheBestMarginPerBarrelOfRoom_Wins()
        {
            // Both are in surplus here and lacking there. A fish bushel pays more than an iron
            // barrel, but less than the ten iron barrels that take its room.
            CityMarket source = Market(Surplus, Surplus);
            CityMarket shortage = Market(Empty, Empty);
            var planner = new TradeRoutePlanner(new[] { source, shortage }, Days(2, 1d), 1d, FishInBushels);

            Assert.IsTrue(planner.TryPlanTrade(0, 10, null, out TradePlan plan));

            long fishMargin = shortage.SellPriceOf(Fish, 0) - source.BuyPriceOf(Fish, 0);
            long ironMargins = 0;

            for (int i = 0; i < 10; i++)
            {
                ironMargins += shortage.SellPriceOf(Iron, i) - source.BuyPriceOf(Iron, i);
            }

            Assert.Greater(ironMargins, fishMargin);
            Assert.AreEqual(0, plan.UnitsOf(Fish));
            Assert.AreEqual(10, plan.UnitsOf(Iron));
        }

        [Test]
        public void ABushelThatNoLongerFits_IsSkipped_WhileBarrelsStillLoad()
        {
            // Fish at 1 000 a bushel pays about 130 a barrel of room, iron at 10 a barrel about
            // 13: three bushels first, then the nine barrels left take iron.
            var goods = new[] { new MarketGood(1000, 2, 0, 0), new MarketGood(10, 2, 0, 0) };
            var source = new CityMarket(
                goods, TestMarkets.Thresholds, TestMarkets.Workforce, 0, TestMarkets.Pricing, 7, new CityPopulation(1000));
            var shortage = new CityMarket(
                goods, TestMarkets.Thresholds, TestMarkets.Workforce, 0, TestMarkets.Pricing, 7, new CityPopulation(1000));
            Put(source, Fish, 120);
            Put(source, Iron, 120);
            var planner = new TradeRoutePlanner(new[] { source, shortage }, Days(2, 1d), 1d, FishInBushels);

            Assert.IsTrue(planner.TryPlanTrade(0, 39, null, out TradePlan plan));

            Assert.AreEqual(3, plan.UnitsOf(Fish));
            Assert.AreEqual(9, plan.UnitsOf(Iron));
        }

        [Test]
        public void WithoutUnits_EveryUnitTakesOneBarrel()
        {
            CityMarket source = Market(Surplus, AtT1);
            CityMarket shortage = Market(Empty, AtT1);
            var planner = new TradeRoutePlanner(new[] { source, shortage }, Days(2, 1d), 1d);

            planner.TryPlanTrade(0, 7, null, out TradePlan plan);

            Assert.AreEqual(7, plan.TotalUnits);
        }
```

  The test markets price fish at 10 and iron at 50 a unit, so ten iron barrels earn far more than a fish bushel: the first test holds with these markets. If it does not, print both margins and fix the test's markets, not the planner.

- [ ] **Step 2: Refresh and run `TradeRoutePlannerTests`.** Expected: compile error, no constructor takes a `GoodUnits` and `TradePlan` has no `UnitsOf`.

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/TradePlan.cs`: the field and parameter `barrels` become `units` (doc: `Units to buy of each good, by index. Kept, not copied.`), `TotalBarrels` becomes `TotalUnits` (same body over `units`), and `BarrelsOf` becomes:

```csharp
        /// <returns>Units to buy of a good, barrels or bushels; none for a good the plan does not know.</returns>
        public int UnitsOf(int good)
        {
            return units != null && good >= 0 && good < units.Length ? units[good] : 0;
        }
```

`Assets/Scripts/Core/TradeRoutePlanner.cs`:

1. A field `private readonly GoodUnits units;`; the comment of `loaded` becomes `// Units of each good in the cargo being worked out.`
2. The constructor gets `GoodUnits units = null` as its last parameter, documented `<param name="units">The room a unit of each good takes in a hold. Without it, every unit takes one barrel.</param>`, and sets `this.units = units ?? GoodUnits.AllBarrels;`.
3. The `capacity` docs become `Barrels of room the ship has.`; the `incoming` docs `Units other ships are carrying to each port, by port then good`.
4. `Load` and its summary:

```csharp
        /// <summary>
        /// Fills <see cref="loaded"/> with the cargo that earns the most between two ports:
        /// unit by unit, the good whose next unit pays the most per barrel of room, among
        /// those that still fit, while one pays. The margin of a good only falls as it is
        /// loaded: with units of one size this is the best cargo, with bushels a close one.
        /// </summary>
        /// <returns>What the cargo earns.</returns>
        private long Load(int from, int to, int capacity, int[,] incoming)
        {
            CityMarket source = markets[from];
            CityMarket destination = markets[to];
            long profit = 0;
            int room = capacity;

            Array.Clear(loaded, 0, loaded.Length);

            while (true)
            {
                int bestGood = -1;
                long bestMargin = 0;
                double bestMarginPerBarrel = 0d;

                for (int good = 0; good < GoodCount; good++)
                {
                    int unitRoom = units.RoomOf(good);

                    if (unitRoom > room || loaded[good] >= source.AvailableOf(good))
                    {
                        continue;
                    }

                    int delivered = (incoming != null ? incoming[to, good] : 0) + loaded[good];
                    long margin = destination.SellPriceOf(good, delivered) - source.BuyPriceOf(good, loaded[good]);
                    double marginPerBarrel = (double)margin / unitRoom;

                    if (margin > 0 && marginPerBarrel > bestMarginPerBarrel)
                    {
                        bestMarginPerBarrel = marginPerBarrel;
                        bestMargin = margin;
                        bestGood = good;
                    }
                }

                if (bestGood < 0)
                {
                    break;
                }

                loaded[bestGood]++;
                room -= units.RoomOf(bestGood);
                profit += bestMargin;
            }

            return profit;
        }
```

5. `Array.Copy(loaded, bestBarrels, GoodCount)` and its variable: `bestBarrels` becomes `bestUnits`.

`Assets/Scripts/Core/AiMerchantFleet.cs`: the locals `barrels` become `units`, `plan.BarrelsOf(good)` becomes `plan.UnitsOf(good)`; the comment over `capacity` (if any) says barrels of room.

- [ ] **Step 4: Refresh and run all EditMode tests.** Expected: all pass but the known gold test.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts Assets/Tests
git commit -m "Marchands IA : cargaison choisie par marge par tonneau de place

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Decimal base prices

**Files:**
- Modify: `Assets/Scripts/Core/MarketGood.cs`, `Assets/Scripts/Core/MarketPricing.cs`, `Assets/Scripts/Game/GoodDefinition.cs`, `Assets/Scripts/Editor/WorldMapSetup.cs` (`SampleGood.BasePrice` and `CreateGood`), `Assets/Tests/EditMode/TestEconomy.cs`
- Test: `Assets/Tests/EditMode/MarketPricingTests.cs`, `Assets/Tests/EditMode/CityMarketTests.cs`

**Interfaces:**
- Produces: `MarketGood(double basePrice, ...)` (above 0 and finite), `MarketGood.BasePrice` a `double`; `MarketPricing.BuyPrice(double basePrice, ...)`, `SellPrice(double basePrice, ...)`; `GoodDefinition.BasePrice` a `double`; `TestEconomy.CreateGood(string, double basePrice, ...)`.

- [ ] **Step 1: Write the failing tests**

In `Assets/Tests/EditMode/MarketPricingTests.cs` add:

```csharp
        [Test]
        public void ADecimalBasePrice_GivesWholeCoins()
        {
            // Beer at 34.8: 34.8 x 1.0125 = 35.235, rounded up; 34.8 x 0.9925 = 34.539, down.
            Assert.AreEqual(36, Pricing.BuyPrice(34.8, 40, Thresholds));
            Assert.AreEqual(34, Pricing.SellPrice(34.8, 40, Thresholds));

            // Above T3: 34.8 x 0.6 = 20.88 and 34.8 x 0.5 = 17.4.
            Assert.AreEqual(21, Pricing.BuyPrice(34.8, 500, Thresholds));
            Assert.AreEqual(17, Pricing.SellPrice(34.8, 500, Thresholds));
        }
```

In `Assets/Tests/EditMode/CityMarketTests.cs`, in `MarketGood_RejectsFiguresThatMakeNoSense`, add:

```csharp
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(-1.5, 2, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(double.NaN, 2, 0, 0));
            Assert.DoesNotThrow(() => new MarketGood(0.5, 2, 0, 0));
```

- [ ] **Step 2: Refresh and run the two fixtures.** Expected: compile error, no `BuyPrice` takes a `double` base price (34.8 does not convert to `long`).

- [ ] **Step 3: Write the implementation**

- `MarketGood`: the parameter and property become `double basePrice` / `double BasePrice`; the check becomes

```csharp
            // The negated comparison also rejects NaN.
            if (!(basePrice > 0d) || double.IsInfinity(basePrice))
            {
                throw new ArgumentOutOfRangeException(nameof(basePrice));
            }
```

  and the param doc `Gold per unit that the factors of the price curves multiply, above 0; it can be a fraction.`
- `MarketPricing.BuyPrice` and `SellPrice`: `long basePrice` becomes `double basePrice`.
- `GoodDefinition`: `[SerializeField, Min(0.01f)] private double basePrice = 10d;` with the tooltip `Gold coins a unit costs, before the factor of the city's stock (see the economy's price factors). It can be a fraction: prices are rounded to whole coins.`, and `public double BasePrice => basePrice;`.
- `WorldMapSetup`: `SampleGood`'s `long basePrice` / `long BasePrice` become `double`; in `CreateGood`, `FindProperty("basePrice").longValue` becomes `.doubleValue`.
- `TestEconomy.CreateGood`: `long basePrice` becomes `double basePrice`, and `.longValue` becomes `.doubleValue`.

- [ ] **Step 4: Refresh and run all EditMode tests.** Expected: all pass but the known gold test.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts Assets/Tests
git commit -m "Prix : prix de base décimaux

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Units in the game and on screen

**Files:**
- Create: `Assets/Scripts/Game/GoodUnit.cs`, `Assets/Scripts/Game/GoodUnitText.cs`
- Modify: `Assets/Scripts/Game/GoodDefinition.cs`, `EconomyDefinition.cs`, `ShipsView.cs`, `AiMerchants.cs`, `CityMarketSection.cs`, `CityInfoPanelController.cs`, `ShipInfoPanelController.cs`, `DebugPanelController.cs`
- Modify: `Assets/UI/WorldMap/CityInfoPanel.uxml`, `Assets/UI/WorldMap/CityInfoPanel.uss`, `Assets/Scripts/Editor/WorldMapSetup.cs`
- Test: `Assets/Tests/EditMode/GoodUnitTextTests.cs` (new), `ShipInfoPanelControllerTests.cs`, `CityMarketSectionTests.cs`, `EconomyDefinitionTests.cs`, `TestEconomy.cs`

**Interfaces:**
- Consumes: `GoodUnits`, `CargoHold.RoomFor` (Task 1), `TradeRoutePlanner(..., GoodUnits)` (Task 3).
- Produces: `public enum GoodUnit { Barrel, Bushel }`; `GoodDefinition.Unit`; `EconomyDefinition.BarrelsPerBushel`, `RoomOf(GoodDefinition)`, `CreateGoodUnits()`; `GoodUnitText.SingularOf`, `PluralOf`, `FormatQuantity(long quantity, GoodUnit unit)`, `NameWithUnit(string name, GoodUnit unit)`; `ShipInfoPanelController.FormatCargoLine(string goodName, int quantity, GoodUnit unit)`; tests: `TestEconomy.CreateGood(..., GoodUnit unit = GoodUnit.Barrel)` as last parameter.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/GoodUnitTextTests.cs`:

```csharp
using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class GoodUnitTextTests
    {
        [Test]
        public void AQuantity_IsWrittenWithItsUnit_SingularUpToOne()
        {
            Assert.AreEqual("1 boisseau", GoodUnitText.FormatQuantity(1, GoodUnit.Bushel));
            Assert.AreEqual("3 boisseaux", GoodUnitText.FormatQuantity(3, GoodUnit.Bushel));
            Assert.AreEqual("0 tonneau", GoodUnitText.FormatQuantity(0, GoodUnit.Barrel));
            Assert.AreEqual("1\u00A0200 tonneaux", GoodUnitText.FormatQuantity(1200, GoodUnit.Barrel));
        }

        [Test]
        public void AName_IsFollowedByItsUnit_InThePlural()
        {
            Assert.AreEqual("Céréales (boisseaux)", GoodUnitText.NameWithUnit("Céréales", GoodUnit.Bushel));
            Assert.AreEqual("Bière (tonneaux)", GoodUnitText.NameWithUnit("Bière", GoodUnit.Barrel));
        }
    }
}
```

In `Assets/Tests/EditMode/ShipInfoPanelControllerTests.cs`, replace the test of `FormatCargoLine` by:

```csharp
        [Test]
        public void FormatCargoLine_WritesAGoodAndItsQuantity_InItsUnit()
        {
            Assert.AreEqual("Vin : 20 tonneaux", ShipInfoPanelController.FormatCargoLine("Vin", 20, GoodUnit.Barrel));
            Assert.AreEqual("Sel : 1 tonneau", ShipInfoPanelController.FormatCargoLine("Sel", 1, GoodUnit.Barrel));
            Assert.AreEqual("Céréales : 1\u00A0200 boisseaux", ShipInfoPanelController.FormatCargoLine("Céréales", 1200, GoodUnit.Bushel));
        }
```

In `Assets/Tests/EditMode/CityMarketSectionTests.cs`, add:

```csharp
        [Test]
        public void ABuyPrice_IsDisabled_WithoutRoomForOneUnit()
        {
            // Wheat in bushels of ten barrels: nine barrels of room are not enough for one.
            var bushelHold = new CargoHold(9, new GoodUnits(new[] { 10, 1 }));

            section.Show(market, bushelHold);

            Assert.IsFalse(IsEnabled(Wheat, BuyCell));
        }
```

In `Assets/Tests/EditMode/TestEconomy.cs`, `CreateGood` gets a last parameter `GoodUnit unit = GoodUnit.Barrel` and sets `serialized.FindProperty("unit").enumValueIndex = (int)unit;`.

In `Assets/Tests/EditMode/EconomyDefinitionTests.cs`:

1. In `ANewDefinition_HasTheGamesSettings`, add `Assert.AreEqual(10, fresh.BarrelsPerBushel);`; in `ANewGood_AndANewCity_HaveUsableDefaults`, add `Assert.AreEqual(GoodUnit.Barrel, good.Unit);`.
2. Add:

```csharp
        [Test]
        public void CreateGoodUnits_GivesABushelTheBarrelsPerBushel()
        {
            GoodDefinition grain = Track(TestEconomy.CreateGood("Céréales", 110, 4.725, unit: GoodUnit.Bushel));
            EconomyDefinition mixed = Track(TestEconomy.CreateEconomy(grain, salt));

            GoodUnits units = mixed.CreateGoodUnits();

            Assert.AreEqual(2, units.Count);
            Assert.AreEqual(10, units.RoomOf(0));
            Assert.AreEqual(1, units.RoomOf(1));
            Assert.AreEqual(10, mixed.RoomOf(grain));
            Assert.AreEqual(1, mixed.RoomOf(salt));
        }

        [Test]
        public void TryValidateSettings_SaysWhenABushelHasNoRoom()
        {
            var serialized = new SerializedObject(economy);
            serialized.FindProperty("barrelsPerBushel").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(economy.TryValidateSettings(out string problem));
            StringAssert.Contains("barrels per bushel", problem);
        }
```

- [ ] **Step 2: Refresh.** Expected: compile errors, `GoodUnit`, `GoodUnitText`, `CreateGoodUnits` do not exist and `FormatCargoLine` takes two arguments.

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/Game/GoodUnit.cs`:

```csharp
namespace DarkFantasyMerchant.Game
{
    /// <summary>What a good is counted, priced and stocked in.</summary>
    public enum GoodUnit
    {
        // The first, so that a new good is counted in barrels until it is said otherwise.
        Barrel,

        /// <summary>Patrician III's load: it takes the economy's barrels per bushel of room in a hold.</summary>
        Bushel,
    }
}
```

Create `Assets/Scripts/Game/GoodUnitText.cs`:

```csharp
namespace DarkFantasyMerchant.Game
{
    /// <summary>The units of goods as the panels write them, in French.</summary>
    public static class GoodUnitText
    {
        public static string SingularOf(GoodUnit unit)
        {
            return unit == GoodUnit.Bushel ? "boisseau" : "tonneau";
        }

        public static string PluralOf(GoodUnit unit)
        {
            return unit == GoodUnit.Bushel ? "boisseaux" : "tonneaux";
        }

        /// <summary>"1 boisseau", "3 boisseaux", "1 200 tonneaux": singular for 0 and 1, as French counts.</summary>
        public static string FormatQuantity(long quantity, GoodUnit unit)
        {
            string name = quantity <= 1 ? SingularOf(unit) : PluralOf(unit);

            return $"{TreasuryHudController.FormatNumber(quantity)} {name}";
        }

        /// <summary>A good's name and its unit: "Céréales (boisseaux)".</summary>
        public static string NameWithUnit(string name, GoodUnit unit)
        {
            return $"{name} ({PluralOf(unit)})";
        }
    }
}
```

`Assets/Scripts/Game/GoodDefinition.cs`: after `category` add

```csharp
        [Tooltip("What the good is counted, priced and stocked in. A bushel takes the economy's barrels per bushel of room in a hold.")]
        [SerializeField] private GoodUnit unit;
```

and `public GoodUnit Unit => unit;` after `Category`.

`Assets/Scripts/Game/EconomyDefinition.cs`:

1. After `startingStockDays`:

```csharp
        [Tooltip("Barrels of room a bushel takes in a hold: 10 is Patrician III's load.")]
        [SerializeField, Min(1)] private int barrelsPerBushel = 10;
```

2. After `StartingStockDays`: `public int BarrelsPerBushel => barrelsPerBushel;`
3. In `TryValidateSettings`, a first link of the chain: `problem = ProblemOf(() => CreateGoodUnits(), "the barrels per bushel")` followed by `?? ProblemOf(() => buyFactors.ToCurve(), "the buy factors")` and the rest as it is.
4. After `CreateWorkforceRule`:

```csharp
        /// <summary>Barrels of room a unit of a good takes in a hold: one, or the barrels per bushel.</summary>
        public int RoomOf(GoodDefinition good)
        {
            return good != null && good.Unit == GoodUnit.Bushel ? barrelsPerBushel : 1;
        }

        /// <summary>The room a unit of each good of the catalogue takes, by index. Throws when the barrels per bushel are under 1.</summary>
        public GoodUnits CreateGoodUnits()
        {
            var rooms = new int[goods.Count];

            for (int i = 0; i < goods.Count; i++)
            {
                rooms[i] = RoomOf(goods[i]);
            }

            return new GoodUnits(rooms);
        }
```

`Assets/Scripts/Game/ShipsView.cs`:

1. After the `worldClock` field:

```csharp
        [Tooltip("The economy whose goods ships carry: it sets the room a unit of each good takes in a hold. Without it, every unit takes one barrel.")]
        [SerializeField] private EconomyDefinition economy;

        private GoodUnits goodUnits;
```

2. In `Spawn`, `new CargoHold(Mathf.Max(0, definition.CargoCapacity))` becomes `new CargoHold(Mathf.Max(0, definition.CargoCapacity), GoodUnitsOfTheEconomy())`, and after `Spawn`:

```csharp
        // Made once, on the first ship: the economy is an asset and does not change in a game.
        private GoodUnits GoodUnitsOfTheEconomy()
        {
            if (goodUnits != null)
            {
                return goodUnits;
            }

            goodUnits = GoodUnits.AllBarrels;

            if (economy == null)
            {
                Debug.LogWarning("ShipsView has no economy: every good takes one barrel of room in a hold.", this);
            }
            else if (!economy.TryValidateSettings(out string problem))
            {
                Debug.LogWarning($"ShipsView: the economy cannot give the units of its goods ({problem}); every good takes one barrel of room.", this);
            }
            else
            {
                goodUnits = economy.CreateGoodUnits();
            }

            return goodUnits;
        }
```

`Assets/Scripts/Game/AiMerchants.cs`: `new TradeRoutePlanner(markets, travelTable.Days, stopoverDays)` becomes `new TradeRoutePlanner(markets, travelTable.Days, stopoverDays, worldEconomy.Economy.CreateGoodUnits())`.

`Assets/Scripts/Game/CityInfoPanelController.cs` and `Assets/Scripts/Game/DebugPanelController.cs`: where the good names are collected, `goodNames.Add(good.DisplayName);` becomes `goodNames.Add(GoodUnitText.NameWithUnit(good.DisplayName, good.Unit));`.

`Assets/Scripts/Game/CityMarketSection.cs`: in `Refresh`, `hold.Free > 0` becomes `hold.RoomFor(i) > 0`; the class summary's "the barrels aboard" becomes "the units aboard".

`Assets/Scripts/Game/ShipInfoPanelController.cs`:

```csharp
        /// <summary>One good of the hold as the panel writes it: "Vin : 20 tonneaux", "Céréales : 3 boisseaux".</summary>
        public static string FormatCargoLine(string goodName, int quantity, GoodUnit unit)
        {
            return $"{goodName} : {GoodUnitText.FormatQuantity(quantity, unit)}";
        }
```

  and in `ShowCargo`, the local `barrels` becomes `quantity`, and the call `FormatCargoLine(economy.Goods[i].DisplayName, quantity, economy.Goods[i].Unit)`. `FormatCargo`'s summary: "the barrels of room taken out of the capacity".

`Assets/UI/WorldMap/CityInfoPanel.uxml`: `<ui:VisualElement name="market-rows" />` becomes

```xml
                <ui:ScrollView class="market__scroll">
                    <ui:VisualElement name="market-rows" />
                </ui:ScrollView>
```

`Assets/UI/WorldMap/CityInfoPanel.uss`, after `.market__row--header`:

```css
/* Twenty goods do not fit on a screen: the rows scroll under their header. */
.market__scroll {
    max-height: 340px;
}
```

`Assets/Scripts/Editor/WorldMapSetup.cs`: in the method that wires `worldEconomy` (after `changed |= FillReference(worldEconomy, "worldClock", worldClock);`), add

```csharp
            // Ships take the room of a unit of each good from the economy.
            var shipsView = FindInScene<ShipsView>(scene);

            if (shipsView != null)
            {
                changed |= FillReference(shipsView, "economy", economy);
            }
```

  and the `economy` reference of `ShipsView` is added to the list of references the tool fills in the class summary of `WorldMapSetup` ("..., `economy` of `ShipsView`, ...") and in `CLAUDE.md` (Task 6).

- [ ] **Step 4: Refresh, read the console, run all EditMode tests.** Expected: no error; all pass but the known gold test.

- [ ] **Step 5: Wire the scene**

Through `Unity_RunCommand`, with the scene `Assets/Scenes/WorldMap.unity` open and not dirty: find the `ShipsView`, set its `economy` to `Assets/Data/Economy/Economy.asset` with a `SerializedObject`, `EditorSceneManager.MarkSceneDirty`, then `EditorSceneManager.SaveScene`. If the tool refuses to save, ask the user to save the scene (Ctrl+S) and say so in the ledger. Check with `git diff --stat Assets/Scenes/WorldMap.unity` that only that reference changed.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts Assets/Tests Assets/UI Assets/Scenes/WorldMap.unity
git commit -m "Marchandises : unités dans le jeu, sur les panneaux et dans les cales des navires

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Content, documentation and a game run

**Files:**
- Modify: `Assets/Data/Goods/*.asset` (rename `Iron` to `Metal`, delete `Vegetables`, create fifteen), `Assets/Data/Economy/Economy.asset`, `Assets/Data/Cities/*.asset`
- Modify: `Assets/Scripts/Editor/WorldMapSetup.cs` (sample goods and cities)
- Modify: `Assets/Tests/EditMode/EconomyContentTests.cs`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: the serialized names `displayName`, `category`, `unit`, `basePrice`, `weeklyConsumptionPerThousand`, `maxWorkers`, `dailyOutputPerWorker` (good), `goods`, `barrelsPerBushel` (economy), `efficientGoods`, `inefficientGoods` (city).
- Produces: nothing new.

- [ ] **Step 1: Write the failing content tests**

In `Assets/Tests/EditMode/EconomyContentTests.cs`, replace the six `TestCase`s and `TheCatalogue_HasItsGoods_InOrder_WithPricesThatMakeSense` by:

```csharp
        // Patrician III's twenty goods, in its order, with its units and base prices.
        [TestCase(0, "Céréales", GoodCategory.Food, GoodUnit.Bushel, 110)]
        [TestCase(1, "Viande", GoodCategory.Food, GoodUnit.Bushel, 957)]
        [TestCase(2, "Poisson", GoodCategory.Food, GoodUnit.Bushel, 440)]
        [TestCase(3, "Bière", GoodCategory.Food, GoodUnit.Barrel, 34.8)]
        [TestCase(4, "Sel", GoodCategory.RawMaterial, GoodUnit.Barrel, 28.5)]
        [TestCase(5, "Miel", GoodCategory.RawMaterial, GoodUnit.Barrel, 110)]
        [TestCase(6, "Épices", GoodCategory.RawMaterial, GoodUnit.Barrel, 280)]
        [TestCase(7, "Vin", GoodCategory.RawMaterial, GoodUnit.Barrel, 220)]
        [TestCase(8, "Étoffe", GoodCategory.RawMaterial, GoodUnit.Barrel, 206.8)]
        [TestCase(9, "Peaux", GoodCategory.RawMaterial, GoodUnit.Barrel, 676.5)]
        [TestCase(10, "Huile", GoodCategory.RawMaterial, GoodUnit.Barrel, 82.5)]
        [TestCase(11, "Bois", GoodCategory.RawMaterial, GoodUnit.Bushel, 55)]
        [TestCase(12, "Métal", GoodCategory.RawMaterial, GoodUnit.Barrel, 255.6)]
        [TestCase(13, "Cuir", GoodCategory.RawMaterial, GoodUnit.Barrel, 224)]
        [TestCase(14, "Laine", GoodCategory.RawMaterial, GoodUnit.Bushel, 880)]
        [TestCase(15, "Brai", GoodCategory.RawMaterial, GoodUnit.Barrel, 55.6)]
        [TestCase(16, "Fonte brute", GoodCategory.RawMaterial, GoodUnit.Bushel, 880)]
        [TestCase(17, "Chanvre", GoodCategory.RawMaterial, GoodUnit.Bushel, 440)]
        [TestCase(18, "Poterie", GoodCategory.RawMaterial, GoodUnit.Barrel, 171)]
        [TestCase(19, "Tuiles", GoodCategory.RawMaterial, GoodUnit.Bushel, 79.8)]
        public void TheCatalogue_HasPatricianIIIsGoods_InOrder(
            int index, string name, GoodCategory category, GoodUnit unit, double basePrice)
        {
            EconomyDefinition economy = LoadEconomy();

            Assert.AreEqual(20, economy.Goods.Count);

            GoodDefinition good = economy.Goods[index];
            Assert.AreEqual(name, good.DisplayName);
            Assert.AreEqual(category, good.Category);
            Assert.AreEqual(unit, good.Unit);
            Assert.AreEqual(basePrice, good.BasePrice, 1e-9);
            Assert.GreaterOrEqual(good.WeeklyConsumptionPerThousand, 0d);
            Assert.Greater(good.MaxWorkers, 0);
            Assert.Greater(good.DailyOutputPerWorker, 0d);
        }

        [Test]
        public void ABushel_TakesTenBarrels()
        {
            Assert.AreEqual(10, LoadEconomy().BarrelsPerBushel);
        }
```

  `EveryGood_IsProducedEfficientlySomewhere` and `EveryCity_ProducesSomething_OnlyGoodsOfTheCatalogue_Once` stay as they are. Search the file for any other test naming one of the six old goods ("Légumes", "Fer", "Grain") and adapt it to the new catalogue.

- [ ] **Step 2: Refresh and run `EconomyContentTests`.** Expected: the catalogue tests fail (six goods, old names).

- [ ] **Step 3: Write the assets**

With the Editor open:

1. Rename the iron asset with its `.meta`, so that its GUID is kept, and delete the vegetables with theirs:

```bash
git mv Assets/Data/Goods/Iron.asset Assets/Data/Goods/Metal.asset
git mv Assets/Data/Goods/Iron.asset.meta Assets/Data/Goods/Metal.asset.meta
git rm -q Assets/Data/Goods/Vegetables.asset Assets/Data/Goods/Vegetables.asset.meta
```

2. Write the twenty good files with a Python script (run with `python -I script.py "<project folder>"` from the scratchpad). It rewrites the five kept goods and creates the fifteen others, from the template of `Beer.asset` (its `m_Script` GUID is that of `GoodDefinition`), the French names escaped as Unity writes them (`"\xE9"` for é):

```python
import io
import sys

root = sys.argv[1] + '/Assets/Data/Goods/'
SCRIPT = '{fileID: 11500000, guid: 1b981e6ce2179f346a4079f5367a73a2, type: 3}'

# asset, name, food, bushel, base price, weekly per 1 000, max workers, output per worker
GOODS = [
    ('Grain', 'Céréales', True, True, 110, 4.725, 68, 0.03396),
    ('Meat', 'Viande', True, True, 957, 1.552, 60, 0.01643),
    ('Fish', 'Poisson', True, True, 440, 3.08, 65, 0.02316),
    ('Beer', 'Bière', True, False, 34.8, 29.575, 68, 0.21254),
    ('Salt', 'Sel', False, False, 28.5, 0.35, 54, 0.01029),
    ('Honey', 'Miel', False, False, 110, 5.425, 60, 0.08206),
    ('Spices', 'Épices', False, False, 280, 0.77, 60, 0.02038),
    ('Wine', 'Vin', False, False, 220, 9.24, 93, 0.15780),
    ('Cloth', 'Étoffe', False, False, 206.8, 8.575, 60, 0.22699),
    ('Skins', 'Peaux', False, False, 676.5, 5.25, 72, 0.11581),
    ('Oil', 'Huile', False, False, 82.5, 7.525, 60, 0.19919),
    ('Timber', 'Bois', False, True, 55, 1.96, 48, 0.02594),
    ('Metal', 'Métal', False, False, 255.6, 16.625, 78, 0.19344),
    ('Leather', 'Cuir', False, False, 224, 6.265, 60, 0.09476),
    ('Wool', 'Laine', False, True, 880, 0.875, 50, 0.02779),
    ('Pitch', 'Brai', False, False, 55.6, 0, 26, 0.03846),
    ('PigIron', 'Fonte brute', False, True, 880, 0, 70, 0.01429),
    ('Hemp', 'Chanvre', False, True, 440, 0.089, 62, 0.00228),
    ('Pottery', 'Poterie', False, False, 171, 5.46, 58, 0.14951),
    ('Tiles', 'Tuiles', False, True, 79.8, 0.016, 27, 0.00094),
]


def unity_string(text):
    if all(ord(c) < 128 for c in text):
        return text
    return '"' + ''.join(c if ord(c) < 128 else '\\x%02X' % ord(c) for c in text) + '"'


def number(value):
    return ('%.10g' % value)


for asset, name, food, bushel, price, weekly, workers, output in GOODS:
    text = '\n'.join([
        '%YAML 1.1',
        '%TAG !u! tag:unity3d.com,2011:',
        '--- !u!114 &11400000',
        'MonoBehaviour:',
        '  m_ObjectHideFlags: 0',
        '  m_CorrespondingSourceObject: {fileID: 0}',
        '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}',
        '  m_GameObject: {fileID: 0}',
        '  m_Enabled: 1',
        '  m_EditorHideFlags: 0',
        '  m_Script: ' + SCRIPT,
        '  m_Name: ' + asset,
        '  m_EditorClassIdentifier: DarkFantasyMerchant.Game::DarkFantasyMerchant.Game.GoodDefinition',
        '  displayName: ' + unity_string(name),
        '  category: ' + ('1' if food else '0'),
        '  unit: ' + ('1' if bushel else '0'),
        '  basePrice: ' + number(price),
        '  weeklyConsumptionPerThousand: ' + number(weekly),
        '  maxWorkers: ' + str(workers),
        '  dailyOutputPerWorker: ' + number(output),
        '',
    ])
    io.open(root + asset + '.asset', 'w', encoding='utf-8', newline='\n').write(text)

print('ok')
```

3. Refresh: Unity imports the fifteen new files and writes their `.meta`. Check that every one has its `.meta` (`ls Assets/Data/Goods/*.meta | wc -l` gives 20).
4. Write the catalogue and the productions with a second script that reads the GUIDs from the `.meta` files:

```python
import io
import re
import sys

project = sys.argv[1] + '/'
ORDER = ['Grain', 'Meat', 'Fish', 'Beer', 'Salt', 'Honey', 'Spices', 'Wine', 'Cloth', 'Skins', 'Oil',
         'Timber', 'Metal', 'Leather', 'Wool', 'Pitch', 'PigIron', 'Hemp', 'Pottery', 'Tiles']
CITIES = {
    'Sparia': (['Fish', 'Salt', 'Pitch'], ['Grain', 'Beer']),
    'Elforth': (['Honey', 'Timber', 'Spices'], ['Fish', 'Meat']),
    'Bactfied': (['Beer', 'Metal', 'Pottery'], ['Grain', 'Timber']),
    'Hitrun': (['Wool', 'Meat', 'Tiles'], ['Grain', 'Leather']),
    'Cerbias': (['Wine', 'Oil', 'Hemp'], ['Fish', 'Honey']),
    'HazerEmpire': (['Cloth', 'PigIron', 'Leather'], ['Beer', 'Meat', 'Metal']),
    'Liveria': (['Grain', 'Skins'], ['Timber', 'Beer', 'Fish']),
}


def guid(asset):
    meta = io.open(project + 'Assets/Data/Goods/' + asset + '.asset.meta', encoding='utf-8').read()
    return re.search(r'guid: ([0-9a-f]{32})', meta).group(1)


def ref(asset):
    return '  - {fileID: 11400000, guid: %s, type: 2}' % guid(asset)


def replace_list(text, field, assets):
    lines = [('  %s:' % field) + ('' if assets else ' []')] + [ref(a) for a in assets]
    pattern = re.compile(r'^  %s:.*\n(?:  - .*\n)*' % field, re.M)
    assert len(pattern.findall(text)) == 1, field
    return pattern.sub('\n'.join(lines) + '\n', text)


def rewrite(path, edit):
    raw = io.open(path, 'rb').read().decode('utf-8')
    crlf = '\r\n' in raw
    text = edit(raw.replace('\r\n', '\n'))
    io.open(path, 'w', encoding='utf-8', newline='').write(text.replace('\n', '\r\n') if crlf else text)


def economy(text):
    text = replace_list(text, 'goods', ORDER)
    if 'barrelsPerBushel:' not in text:
        text = text.replace('  startingStockDays: 7\n', '  startingStockDays: 7\n  barrelsPerBushel: 10\n')
    assert 'barrelsPerBushel: 10' in text
    return text


rewrite(project + 'Assets/Data/Economy/Economy.asset', economy)

for city, (efficient, inefficient) in CITIES.items():
    rewrite(project + 'Assets/Data/Cities/%s.asset' % city,
            lambda text: replace_list(replace_list(text, 'efficientGoods', efficient), 'inefficientGoods', inefficient))

print('ok')
```

5. Refresh, then log through `Unity_RunCommand` the catalogue of `Economy.asset` (index, display name, unit, base price, maximum, output) and the productions of each city of `WorldMap.asset`; they must match the spec's tables.

- [ ] **Step 4: `WorldMapSetup` sample content**

In `Assets/Scripts/Editor/WorldMapSetup.cs`:

1. `SampleGood` gets a `GoodUnit unit` parameter after `category` (field `Unit`), set in `CreateGood` with `serialized.FindProperty("unit").enumValueIndex = (int)sample.Unit;`.
2. `SampleGoods` becomes the twenty goods of the spec, in its order, as `new SampleGood("<asset>", "<name>", GoodCategory.<Food|RawMaterial>, GoodUnit.<Barrel|Bushel>, <base price>, <weekly per 1 000>, <max workers>, <output per worker>)` — the same values as the script of Step 3, the first being `new SampleGood("Grain", "Céréales", GoodCategory.Food, GoodUnit.Bushel, 110, 4.725, 68, 0.03396),`.
3. The productions of `SampleCities` become those of the spec, by asset name (`new[] { "Fish", "Salt", "Pitch" }, new[] { "Grain", "Beer" }` for Sparia, and so on).
4. The comment over `SampleCities`: `Every good is produced efficiently by at least one city.` stays true.

- [ ] **Step 5: Refresh and run all EditMode tests.** Expected: all pass but the known gold test. A test that still names an old good fails here: adapt it to the new catalogue (the economy tests use their own goods and are not concerned).

- [ ] **Step 6: `CLAUDE.md`**

1. *Project state*: `cities hold stocks of goods (six for now: fish, iron, beer, grain, meat and vegetables)` becomes `cities hold stocks of goods (the twenty of Patrician III, counted in barrels or in bushels)`.
2. *World map*, the `WorldMapSetup` bullet: add `` `economy` of `ShipsView`, `` to the list of references the tool fills, after `` `worldClock` of `ShipsView`, ``.
3. *Economy*, first bullet: replace `Goods are counted in barrels ("tonneaux"), all of the same size.` by `Goods are Patrician III's twenty, in its order (grain, meat, fish, beer, salt, honey, spices, wine, cloth, skins, oil, timber, metal, leather, wool, pitch, pig iron, hemp, pottery, tiles), with its base prices, which can be fractions (34.8 for beer). A good is counted in its unit (`GoodUnit`): barrels ("tonneaux"), or bushels ("boisseaux", Patrician III's loads) for grain, meat, fish, timber, wool, pig iron, hemp and tiles. Stocks, consumption, production, thresholds and prices are in the good's unit: the price of grain is that of a bushel. Consumption per 1 000 inhabitants is Patrician III's per class, averaged over its starting mix (10 % rich, 30 % wealthy, 60 % poor). Pitch and pig iron are consumed by no one until workshops have inputs.`, and in the same bullet `(`GoodCategory`: `Food` for fish, beer, grain, meat and vegetables, `RawMaterial` for iron, which is what a new good is; see Population)` by `(`GoodCategory`: `Food` for grain, meat, fish and beer, `RawMaterial` for the others, which is what a new good is; see Population), its unit`.
4. The `CityDefinition` bullet: `(with six goods in the catalogue for now, a city produces three to five of them, up to two efficiently, and no city produces every food)` becomes `(a city has five or six workshops, two or three of them efficient, and every good is produced efficiently somewhere)`.
5. The `CargoHold` bullet becomes: `- `CargoHold` (Core) is the goods aboard a ship, counted in their units, out of its room in barrels (`ShipDefinition.cargoCapacity`, 200 for the merchant ship); a bushel takes `EconomyDefinition.barrelsPerBushel` (10) barrels of room (`GoodUnits`, which `ShipsView` takes from its `economy`). `RoomFor` is the whole units of a good that still fit: a trade never splits a unit, and the AI merchants' planner loads the good whose next unit pays the most per barrel of room. `Ship.Cargo` holds it.`
6. In the city panel bullet: `stock, buy and sell price of the next barrel for every good, read-only` becomes `stock, buy and sell price of the next unit for every good, each named with its unit ("Céréales (boisseaux)"), in rows that scroll, read-only`; `1 barrel, Shift 10, Ctrl 100` becomes `1 unit, Shift 10, Ctrl 100`; `a full hold` becomes `no room for one unit`.
7. The ship panel bullet: `and a line per good aboard` becomes `and a line per good aboard with its quantity in its unit ("Céréales : 3 boisseaux")`.
8. *Population*: `With the five foods of the catalogue, a city holds its population with 35 days of food in all, each food counted for 14 at most (two and a half foods at their fill), and grows the fastest only with two weeks of all five.` becomes `With the four foods of the catalogue, a city holds its population with 28 days of food in all, each food counted for 14 at most (two foods at their fill), and grows the fastest only with two weeks of all four.`
9. *Debug tools*, ships tab: `its hold, the barrels aboard of every good` becomes `its hold (barrels of room), the units aboard of every good`.

- [ ] **Step 7: Run the game in the Editor**

Enter Play mode (`Application.runInBackground = true` for the run) and check, through `Unity_RunCommand` and the console, writing down what is seen:
- The console shows no error and no warning from the economy or the ships.
- Day 0: every city's market has twenty goods; grain is priced about 1.5 × 110 to buy; the player's hold is "Cale : 0 / 200 tonneaux".
- A purchase of grain by code (`Trade.Buy` on a city's market with the player ship's `Cargo`) of 25 bushels buys 20 and leaves `Free` at 0; selling 5 frees 50.
- After about 60 days of fast forward: AI profits rise; producers' stocks sit near their T2–T3.

Leave Play mode and set `Application.runInBackground` back to false.

- [ ] **Step 8: Commit**

```bash
git add -A Assets/Data Assets/Scripts/Editor/WorldMapSetup.cs Assets/Tests CLAUDE.md
git commit -m "Marchandises : les vingt de Patrician III, leurs unités et les productions des villes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
