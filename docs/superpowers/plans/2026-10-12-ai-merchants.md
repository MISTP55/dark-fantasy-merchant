# AI Merchants Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Five AI ships, tinted blue, start in random ports, trade on their own from surplus to shortage, can be selected but not ordered, and are listed with the player's in a new "Navires" tab of the debug panel.

**Architecture:** The trade decision and the life of an AI ship are plain `Core` classes: `TradeRoutePlanner` (which cargo, which destination), `PortTravelTable` over `SeaRoutes` (days between ports), `AiMerchantFleet<TPort>` (sell on docking, stopover, buy and leave) and `StartPortDraw`. In `Game`, `ShipsView` learns who owns a ship, an `AiMerchantsDefinition` asset holds the settings and an `AiMerchants` MonoBehaviour only wires the `Core` classes to the scene. The debug panel becomes one panel with two tabs; `DebugShipsTable` builds the new one.

**Tech Stack:** Unity 6000.6.4f1, C#, UI Toolkit (UXML + USS), Unity Test Framework (NUnit, EditMode).

**Spec:** `docs/superpowers/specs/2026-10-12-ai-merchants-design.md`

## Global Constraints

- All code, comments, log messages, asset and folder names are in English. Everything the player reads is in French, written directly in the UXML, the controllers and the assets.
- Numbers shown to the player use a no-break space (`\u00A0`) as thousands separator.
- `Core` contains no `MonoBehaviour` and nothing that needs a scene, and does not reference `Game`.
- Runtime UI is UI Toolkit only. No uGUI.
- ScriptableObjects are never written while the game runs.
- AI gold is unlimited: no `Treasury` for an AI merchant, no wages.
- Defaults: 5 ships, the merchant ship, 14 sailors, a stopover of 1 day, tint about `#7FB2FF`, names "Marchand indépendant 1" to "5".
- Debug tools read the simulation and change nothing, and exist only when `Debug.isDebugBuild`.
- The existing tests must pass **unchanged**, except where a task says otherwise.
- Never hand-write `.meta` files. Unity generates them on import; commit them with their file. A file that is renamed is renamed with `git mv`, its `.meta` too, so its GUID is kept.
- Never edit the generated `.sln` / `.csproj`. Exclude `Library/` from searches.
- Match the surrounding code: Allman braces, a blank line before `return` / `if` blocks as in the existing files, XML `<summary>` on public types, comments only where the reason is not obvious.
- Commit directly on `main`. Commit messages are in French and end with:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
- Differences from the spec's wording, intentional:
  - The life of an AI ship is a `Core` class, `AiMerchantFleet<TPort>`, not code of the `AiMerchants` MonoBehaviour, so that it is tested. It listens to `ShipDocking.Docked` itself rather than to `ShipsView.ShipArrived`.
  - A ship found idle at sea is ordered back to the port it last lay in, not to the nearest port: that port is known to be reachable.
  - The table of travel times is `PortTravelTable`; `SeaRoutes` only measures one route.
  - `WorldMapInteraction` closes the city panel when the ship selected in it leaves its port by itself, which the spec does not mention: without it a ship at sea and a city would both be selected.

## Review Focus

Conditions the spec implies and that are most likely to bite:

1. A fast forward step longer than a stopover: a ship must leave once and buy once. Test in Task 4 (`ALongStep_BuysOnce`).
2. Prices that fell while the ship sailed: it still sells its whole hold. Test in Task 4 (`OnArrival_ItSellsEvenAtALoss`).
3. A map with one city, or with no city that reaches another: no plan, no ship, no exception. Tests in Task 3 (`ASinglePort_HasNoTrade`) and Task 4 (`Draw_WithoutCandidates_DrawsNothing`).
4. A hold without room, or a stopover and a trip of no time: no division by zero, no plan for nothing. Tests in Task 3 (`WithoutRoom_ThereIsNoTrade`, `ATripOfNoTime_IsStillCompared`).
5. Several ships deciding in the same frame: they must not all take the same shortage. Test in Task 4 (`TwoShips_DoNotAimAtTheSameShortage`).

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
        testNames = null, // or groupNames = new[] { "DarkFantasyMerchant\\.Tests\\.EditMode\\.TradeTests" }
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

Append `'-testFilter','DarkFantasyMerchant.Tests.EditMode.TradeTests'` to run one fixture. Exit code `0` passed, `2` test failures, `1` compile error (read `Logs\test.log`).

## File Structure

| File | Responsibility |
|---|---|
| `Assets/Scripts/Core/Trade.cs` (modify) | Overloads that trade without a treasury. |
| `Assets/Scripts/Core/SeaRoutes.cs` (new) | Length of the route between two points, and whether it gets there. |
| `Assets/Scripts/Core/PortTravelTable.cs` (new) | Days between every pair of ports, measured one pair at a time. |
| `Assets/Scripts/Core/TradePlan.cs` (new) | What a ship buys and where it takes it. |
| `Assets/Scripts/Core/TradeRoutePlanner.cs` (new) | Picks the trade, or the port to go and trade from. |
| `Assets/Scripts/Core/StartPortDraw.cs` (new) | Draws the start ports of the fleet. |
| `Assets/Scripts/Core/AiMerchantFleet.cs` (new) | The AI ships: sell on docking, stopover, buy and leave, profit. |
| `Assets/Scripts/Game/ShipsView.cs`, `ShipView.cs` (modify) | Ownership, names, tint, spawning an AI ship. |
| `Assets/Scripts/Game/WorldMapInteraction.cs`, `PlayerExpenses.cs`, `ShipArrivalNotifier.cs`, `CityInfoPanelController.cs` (modify) | Player-only behaviour. |
| `Assets/Scripts/Game/AiMerchantsDefinition.cs` (new) | Settings asset. |
| `Assets/Scripts/Game/AiMerchants.cs` (new) | Wires the fleet to the scene. |
| `Assets/Scripts/Game/DebugPanelController.cs` (renamed from `DebugCitiesPanelController.cs`) | The debug panel and its tabs. |
| `Assets/Scripts/Game/DebugShipsTable.cs` (new) | The ships tab. |
| `Assets/UI/Debug/DebugPanel.uxml`, `.uss` (renamed from `DebugCitiesPanel.*`) | The panel's tree and styles. |
| `Assets/Scripts/Editor/WorldMapSetup.cs` (modify) | Creates the asset and the scene object, wires the references. |

---

### Task 1: Trading without a treasury

**Files:**
- Modify: `Assets/Scripts/Core/Trade.cs`
- Test: `Assets/Tests/EditMode/TradeTests.cs`

**Interfaces:**
- Produces: `Trade.Buy(CityMarket market, CargoHold hold, int good, int barrels)` and `Trade.Sell(CityMarket market, CargoHold hold, int good, int barrels)`, both returning `TradeResult`.

- [ ] **Step 1: Write the failing tests.** Add to `TradeTests`, before `RejectsAQuantityThatIsNotPositive`:

```csharp
        [Test]
        public void BuyWithoutATreasury_MovesTheBarrelsAndGivesTheirPrice()
        {
            long expected = 0;

            for (int i = 0; i < 30; i++)
            {
                expected += market.BuyPriceOf(Good, i);
            }

            TradeResult result = Trade.Buy(market, hold, Good, 30);

            Assert.AreEqual(30, result.Barrels);
            Assert.AreEqual(expected, result.Gold);
            Assert.AreEqual(30, market.AvailableOf(Good));
            Assert.AreEqual(30, hold.BarrelsOf(Good));
        }

        [Test]
        public void BuyWithoutATreasury_StopsAtTheStockAndAtTheHold()
        {
            Assert.AreEqual(60, Trade.Buy(market, hold, Good, 100).Barrels);

            market.Put(Good, 60);
            hold = new CargoHold(5);

            Assert.AreEqual(5, Trade.Buy(market, hold, Good, 100).Barrels);
        }

        [Test]
        public void SellWithoutATreasury_MovesTheBarrelsAndGivesTheirPrice()
        {
            hold.TryAdd(Good, 5);
            long expected = market.SellPriceOf(Good, 0) + market.SellPriceOf(Good, 1) + market.SellPriceOf(Good, 2);

            TradeResult result = Trade.Sell(market, hold, Good, 3);

            Assert.AreEqual(3, result.Barrels);
            Assert.AreEqual(expected, result.Gold);
            Assert.AreEqual(2, hold.BarrelsOf(Good));
            Assert.AreEqual(63, market.AvailableOf(Good));
        }

        [Test]
        public void SellWithoutATreasury_StopsAtTheBarrelsAboard()
        {
            hold.TryAdd(Good, 2);

            Assert.AreEqual(2, Trade.Sell(market, hold, Good, 100).Barrels);
            Assert.AreEqual(0, Trade.Sell(market, hold, Good, 100).Barrels);
        }

        [Test]
        public void ATradeWithoutATreasury_ChangesTheMarketAndTheHoldOnce()
        {
            int marketChanges = 0;
            int holdChanges = 0;
            market.Changed += () => marketChanges++;
            hold.Changed += () => holdChanges++;

            Trade.Buy(market, hold, Good, 10);
            Trade.Sell(market, hold, Good, 10);

            Assert.AreEqual(2, marketChanges);
            Assert.AreEqual(2, holdChanges);
        }

        [Test]
        public void WithoutATreasury_RejectsMissingArgumentsAndQuantities()
        {
            Assert.Throws<ArgumentNullException>(() => Trade.Buy(null, hold, Good, 1));
            Assert.Throws<ArgumentNullException>(() => Trade.Sell(market, null, Good, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Trade.Buy(market, hold, Good, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Trade.Sell(market, hold, Good, 0));
        }
```

- [ ] **Step 2: Run `TradeTests`.** Expected: compile error, no overload of `Buy` takes 4 arguments.

- [ ] **Step 3: Implement.** In `Trade.cs`, change the class summary's first sentence to "Buying and selling between a city's market, a ship's hold and, for the player, a treasury.", replace `Sell` and `Require`, and add the two overloads:

```csharp
        /// <summary>
        /// Buys up to <paramref name="barrels"/> barrels for a merchant whose gold is not
        /// counted: as many as the city has and the hold has room for.
        /// </summary>
        /// <returns>The barrels bought and what they cost.</returns>
        public static TradeResult Buy(CityMarket market, CargoHold hold, int good, int barrels)
        {
            Require(market, hold, barrels);

            int bought = Math.Min(barrels, Math.Min(market.AvailableOf(good), hold.Free));

            if (bought == 0)
            {
                return default;
            }

            long gold = 0;

            for (int i = 0; i < bought; i++)
            {
                gold += market.BuyPriceOf(good, i);
            }

            market.Take(good, bought);
            hold.TryAdd(good, bought);
            return new TradeResult(bought, gold);
        }

        /// <summary>Sells up to <paramref name="barrels"/> barrels: as many as are aboard.</summary>
        public static TradeResult Sell(CityMarket market, CargoHold hold, Treasury treasury, int good, int barrels)
        {
            Require(market, hold, treasury, barrels);

            TradeResult result = Sell(market, hold, good, barrels);

            if (result.Barrels > 0)
            {
                treasury.Deposit(result.Gold);
            }

            return result;
        }

        /// <summary>
        /// Sells up to <paramref name="barrels"/> barrels for a merchant whose gold is not
        /// counted: as many as are aboard.
        /// </summary>
        /// <returns>The barrels sold and what they were paid.</returns>
        public static TradeResult Sell(CityMarket market, CargoHold hold, int good, int barrels)
        {
            Require(market, hold, barrels);

            int sold = Math.Min(barrels, hold.BarrelsOf(good));

            if (sold == 0)
            {
                return default;
            }

            long gold = 0;

            for (int i = 0; i < sold; i++)
            {
                gold += market.SellPriceOf(good, i);
            }

            hold.TryRemove(good, sold);
            market.Put(good, sold);
            return new TradeResult(sold, gold);
        }

        private static void Require(CityMarket market, CargoHold hold, Treasury treasury, int barrels)
        {
            Require(market, hold, barrels);

            if (treasury == null)
            {
                throw new ArgumentNullException(nameof(treasury));
            }
        }

        private static void Require(CityMarket market, CargoHold hold, int barrels)
        {
            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            if (hold == null)
            {
                throw new ArgumentNullException(nameof(hold));
            }

            if (barrels < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(barrels));
            }
        }
```

The existing `Buy` with a treasury is unchanged.

- [ ] **Step 4: Run `TradeTests`.** Expected: all pass, the old ones included.

- [ ] **Step 5: Commit.** `git add Assets/Scripts/Core/Trade.cs Assets/Tests/EditMode/TradeTests.cs` — message: `Commerce : achat et vente sans trésor, pour les marchands IA`.

---

### Task 2: Travel times between ports

**Files:**
- Create: `Assets/Scripts/Core/SeaRoutes.cs`, `Assets/Scripts/Core/PortTravelTable.cs`
- Test: `Assets/Tests/EditMode/SeaRoutesTests.cs`, `Assets/Tests/EditMode/PortTravelTableTests.cs`

**Interfaces:**
- Consumes: `NavigationPathfinder.TryFindPath(Vector2 from, Vector2 to, List<Vector2> waypoints)`, `MapProjection.NormalizedToWorld`.
- Produces:
  - `static bool SeaRoutes.TryMeasure(NavigationPathfinder navigation, MapProjection projection, Vector2 from, Vector2 to, List<Vector2> waypoints, out float length)`
  - `PortTravelTable(IReadOnlyList<Vector2> anchorages, NavigationPathfinder navigation, MapProjection projection, double worldUnitsPerDay)`, `int PortCount`, `bool IsComplete`, `void MeasureNext()`, `double[,] Days` (infinity when unreachable, 0 on the diagonal), `bool ReachesAnother(int port)`.

- [ ] **Step 1: Write the failing tests.**

`SeaRoutesTests.cs`:

```csharp
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class SeaRoutesTests
    {
        private MapProjection projection;
        private NavigationGrid grid;
        private NavigationPathfinder navigation;
        private List<Vector2> waypoints;

        [SetUp]
        public void SetUp()
        {
            // 40 x 20 world units over 8 x 4 cells of 5 units. Cell (7, 0) is a pond of its own.
            projection = new MapProjection(40f, 2f);
            grid = GridAssert.FromRows(
                "........",
                "...#....",
                "...#..##",
                "...#..#.");
            navigation = new NavigationPathfinder(grid);
            waypoints = new List<Vector2>();
        }

        private Vector2 Cell(int x, int y)
        {
            return GridAssert.CellCenter(grid, x, y);
        }

        [Test]
        public void WithoutAPathfinder_ItIsAStraightLine()
        {
            bool reached = SeaRoutes.TryMeasure(
                null, projection, new Vector2(0.25f, 0.5f), new Vector2(0.75f, 0.5f), waypoints, out float length);

            Assert.IsTrue(reached);
            Assert.AreEqual(20f, length, 1e-4f);
        }

        [Test]
        public void OverOpenWater_ItIsTheDistance()
        {
            bool reached = SeaRoutes.TryMeasure(navigation, projection, Cell(0, 3), Cell(2, 3), waypoints, out float length);

            Assert.IsTrue(reached);
            Assert.AreEqual(10f, length, 1e-3f);
        }

        [Test]
        public void AroundLand_ItIsLongerThanTheStraightLine()
        {
            bool reached = SeaRoutes.TryMeasure(navigation, projection, Cell(2, 0), Cell(4, 0), waypoints, out float length);

            Assert.IsTrue(reached);
            Assert.Greater(length, 10f + 1f);
        }

        [Test]
        public void APointInAnotherSea_IsNotReached()
        {
            Assert.IsFalse(SeaRoutes.TryMeasure(navigation, projection, Cell(0, 3), Cell(7, 0), waypoints, out _));
        }

        [Test]
        public void TheSamePoint_IsReachedAtNoDistance()
        {
            bool reached = SeaRoutes.TryMeasure(navigation, projection, Cell(0, 3), Cell(0, 3), waypoints, out float length);

            Assert.IsTrue(reached);
            Assert.AreEqual(0f, length);
        }

        [Test]
        public void ANaNPoint_IsNotReached()
        {
            var nowhere = new Vector2(float.NaN, 0.5f);

            Assert.IsFalse(SeaRoutes.TryMeasure(navigation, projection, Cell(0, 3), nowhere, waypoints, out _));
            Assert.IsFalse(SeaRoutes.TryMeasure(null, projection, nowhere, Cell(0, 3), waypoints, out _));
        }
    }
}
```

`PortTravelTableTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class PortTravelTableTests
    {
        private MapProjection projection;
        private NavigationGrid grid;
        private NavigationPathfinder navigation;

        [SetUp]
        public void SetUp()
        {
            projection = new MapProjection(40f, 2f);
            grid = GridAssert.FromRows(
                "........",
                "...#....",
                "...#..##",
                "...#..#.");
            navigation = new NavigationPathfinder(grid);
        }

        private Vector2 Cell(int x, int y)
        {
            return GridAssert.CellCenter(grid, x, y);
        }

        private PortTravelTable Complete(params Vector2[] anchorages)
        {
            // 5 world units a day: one cell a day.
            var table = new PortTravelTable(anchorages, navigation, projection, 5d);

            while (!table.IsComplete)
            {
                table.MeasureNext();
            }

            return table;
        }

        [Test]
        public void ItMeasuresOnePairAtATime()
        {
            var table = new PortTravelTable(new[] { Cell(0, 3), Cell(2, 3), Cell(4, 3) }, navigation, projection, 5d);
            int steps = 0;

            while (!table.IsComplete)
            {
                table.MeasureNext();
                steps++;
            }

            Assert.AreEqual(3, steps);
            Assert.DoesNotThrow(() => table.MeasureNext());
        }

        [Test]
        public void DaysAreTheLengthOverTheSpeed_BothWays()
        {
            PortTravelTable table = Complete(Cell(0, 3), Cell(2, 3));

            Assert.AreEqual(2d, table.Days[0, 1], 1e-3);
            Assert.AreEqual(2d, table.Days[1, 0], 1e-3);
            Assert.AreEqual(0d, table.Days[0, 0]);
        }

        [Test]
        public void APortInAnotherSea_IsInfinitelyFar()
        {
            PortTravelTable table = Complete(Cell(0, 3), Cell(2, 3), Cell(7, 0));

            Assert.IsTrue(double.IsPositiveInfinity(table.Days[0, 2]));
            Assert.IsTrue(double.IsPositiveInfinity(table.Days[2, 1]));
            Assert.IsTrue(table.ReachesAnother(0));
            Assert.IsFalse(table.ReachesAnother(2));
        }

        [Test]
        public void BeforeItIsMeasured_APairIsInfinitelyFar()
        {
            var table = new PortTravelTable(new[] { Cell(0, 3), Cell(2, 3) }, navigation, projection, 5d);

            Assert.IsFalse(table.IsComplete);
            Assert.IsTrue(double.IsPositiveInfinity(table.Days[0, 1]));
        }

        [Test]
        public void FewerThanTwoPorts_AreCompleteAtOnce()
        {
            Assert.IsTrue(new PortTravelTable(new Vector2[0], navigation, projection, 5d).IsComplete);

            var single = new PortTravelTable(new[] { Cell(0, 3) }, navigation, projection, 5d);
            Assert.IsTrue(single.IsComplete);
            Assert.IsFalse(single.ReachesAnother(0));
        }

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void RejectsASpeedThatIsNotPositive(double worldUnitsPerDay)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new PortTravelTable(new[] { Cell(0, 3) }, navigation, projection, worldUnitsPerDay));
        }
    }
}
```

- [ ] **Step 2: Run both fixtures.** Expected: compile error, `SeaRoutes` and `PortTravelTable` do not exist.

- [ ] **Step 3: Implement.**

`SeaRoutes.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>How long the route between two points of the map is, and whether it gets there.</summary>
    public static class SeaRoutes
    {
        /// <summary>
        /// Measures the route a ship sails from one point to another: over water with a
        /// pathfinder, otherwise in a straight line.
        /// </summary>
        /// <param name="navigation">Null for a map without a navigation mask.</param>
        /// <param name="waypoints">Working list, cleared and filled with the route.</param>
        /// <param name="length">The length of the route in world units.</param>
        /// <returns>
        /// False when the route does not end on <paramref name="to"/>: a point in another
        /// sea, a NaN point or a map without water.
        /// </returns>
        public static bool TryMeasure(
            NavigationPathfinder navigation,
            MapProjection projection,
            Vector2 from,
            Vector2 to,
            List<Vector2> waypoints,
            out float length)
        {
            if (projection == null)
            {
                throw new ArgumentNullException(nameof(projection));
            }

            if (waypoints == null)
            {
                throw new ArgumentNullException(nameof(waypoints));
            }

            length = 0f;
            waypoints.Clear();

            if (IsNaN(from) || IsNaN(to))
            {
                return false;
            }

            from = ClampToMap(from);
            to = ClampToMap(to);

            if (navigation == null)
            {
                waypoints.Add(to);
            }
            else if (!navigation.TryFindPath(from, to, waypoints))
            {
                return false;
            }

            // A route to another sea ends on the nearest shore of this one. Compared per
            // component: the pathfinder ends a route it can complete on the point itself.
            Vector2 end = waypoints.Count > 0 ? waypoints[waypoints.Count - 1] : from;

            if (end.x != to.x || end.y != to.y)
            {
                return false;
            }

            Vector2 previous = projection.NormalizedToWorld(from);

            foreach (Vector2 waypoint in waypoints)
            {
                Vector2 next = projection.NormalizedToWorld(waypoint);
                length += (next - previous).magnitude;
                previous = next;
            }

            return true;
        }

        private static Vector2 ClampToMap(Vector2 point)
        {
            return new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y));
        }

        private static bool IsNaN(Vector2 value)
        {
            return float.IsNaN(value.x) || float.IsNaN(value.y);
        }
    }
}
```

`PortTravelTable.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The days a ship takes between every pair of ports. Measured one pair at a time,
    /// so that the searches can be spread over several frames.
    /// </summary>
    public sealed class PortTravelTable
    {
        private readonly IReadOnlyList<Vector2> anchorages;
        private readonly NavigationPathfinder navigation;
        private readonly MapProjection projection;
        private readonly double worldUnitsPerDay;

        // Reused by every measure.
        private readonly List<Vector2> waypoints = new List<Vector2>();

        // The next pair to measure: first is below second.
        private int first;
        private int second = 1;

        /// <param name="anchorages">Where a ship lies at each port, in normalized map positions.</param>
        /// <param name="navigation">Null for a map without a navigation mask.</param>
        /// <param name="worldUnitsPerDay">What the ships sail in a day.</param>
        public PortTravelTable(
            IReadOnlyList<Vector2> anchorages,
            NavigationPathfinder navigation,
            MapProjection projection,
            double worldUnitsPerDay)
        {
            this.anchorages = anchorages ?? throw new ArgumentNullException(nameof(anchorages));
            this.projection = projection ?? throw new ArgumentNullException(nameof(projection));

            // The negated comparison also rejects NaN.
            if (!(worldUnitsPerDay > 0d) || double.IsInfinity(worldUnitsPerDay))
            {
                throw new ArgumentOutOfRangeException(nameof(worldUnitsPerDay));
            }

            this.navigation = navigation;
            this.worldUnitsPerDay = worldUnitsPerDay;

            int count = anchorages.Count;
            Days = new double[count, count];

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    Days[i, j] = i == j ? 0d : double.PositiveInfinity;
                }
            }
        }

        public int PortCount => anchorages.Count;

        /// <summary>
        /// Days from a port to another, the same both ways. Infinity for a port that
        /// cannot be reached, and for a pair that is not measured yet.
        /// </summary>
        public double[,] Days { get; }

        public bool IsComplete => first >= PortCount - 1;

        /// <summary>Measures one more pair of ports. Does nothing once complete.</summary>
        public void MeasureNext()
        {
            if (IsComplete)
            {
                return;
            }

            if (SeaRoutes.TryMeasure(navigation, projection, anchorages[first], anchorages[second], waypoints, out float length))
            {
                double days = length / worldUnitsPerDay;
                Days[first, second] = days;
                Days[second, first] = days;
            }

            second++;

            if (second >= PortCount)
            {
                first++;
                second = first + 1;
            }
        }

        /// <returns>Whether a ship can sail from this port to at least one other, among the pairs measured.</returns>
        public bool ReachesAnother(int port)
        {
            if (port < 0 || port >= PortCount)
            {
                throw new ArgumentOutOfRangeException(nameof(port));
            }

            for (int other = 0; other < PortCount; other++)
            {
                if (other != port && !double.IsInfinity(Days[port, other]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
```

- [ ] **Step 4: Run both fixtures.** Expected: all pass.

- [ ] **Step 5: Commit** the four files and their `.meta` — message: `Navigation : longueur des routes et temps de trajet entre les ports`.

---

### Task 3: The trade decision

**Files:**
- Create: `Assets/Scripts/Core/TradePlan.cs`, `Assets/Scripts/Core/TradeRoutePlanner.cs`
- Test: `Assets/Tests/EditMode/TradeRoutePlannerTests.cs`

**Interfaces:**
- Consumes: `CityMarket.AvailableOf`, `BuyPriceOf(good, barrelsAlreadyBought)`, `SellPriceOf(good, barrelsAlreadySold)`, `GoodCount`.
- Produces:
  - `readonly struct TradePlan`: `int Destination`, `long Profit`, `int BarrelsOf(int good)`, `int TotalBarrels`.
  - `TradeRoutePlanner(IReadOnlyList<CityMarket> markets, double[,] travelDays, double stopoverDays)`, `int PortCount`, `int GoodCount`.
  - `bool TryPlanTrade(int from, int capacity, int[,] incoming, out TradePlan plan)` — `incoming[port, good]`, null for none.
  - `bool TryPlanRelocation(int from, int capacity, int[,] incoming, out int destination)`.

- [ ] **Step 1: Write the failing tests.** `TradeRoutePlannerTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class TradeRoutePlannerTests
    {
        private const int Fish = 0;
        private const int Iron = 1;

        // Days of consumption a market starts with. The target is 30 days.
        private const double Empty = 0d;
        private const double AtTarget = 30d;
        private const double Surplus = 60d;

        // Two goods, 2 barrels a day each: 60 barrels at the target, 120 in surplus.
        private static CityMarket Market(double fishDays, double ironDays)
        {
            var market = new CityMarket(
                new[] { new MarketGood(5, 10, 25, 2, 0, 0), new MarketGood(25, 50, 125, 2, 0, 0) },
                30,
                0,
                new MarketPricing(0.05, 0.05),
                7);

            Put(market, Fish, (int)(fishDays * 2));
            Put(market, Iron, (int)(ironDays * 2));
            return market;
        }

        private static void Put(CityMarket market, int good, int barrels)
        {
            if (barrels > 0)
            {
                market.Put(good, barrels);
            }
        }

        /// <summary>Every port a number of days from every other.</summary>
        private static double[,] Days(int ports, double days)
        {
            var table = new double[ports, ports];

            for (int i = 0; i < ports; i++)
            {
                for (int j = 0; j < ports; j++)
                {
                    table[i, j] = i == j ? 0d : days;
                }
            }

            return table;
        }

        private static void SetDays(double[,] table, int a, int b, double days)
        {
            table[a, b] = days;
            table[b, a] = days;
        }

        /// <summary>What loading one good alone gives: barrels while the next one pays.</summary>
        private static long ProfitOfOneGood(CityMarket source, CityMarket destination, int good, int capacity, out int barrels)
        {
            long profit = 0;
            barrels = 0;

            while (barrels < capacity && barrels < source.AvailableOf(good))
            {
                long margin = destination.SellPriceOf(good, barrels) - source.BuyPriceOf(good, barrels);

                if (margin <= 0)
                {
                    break;
                }

                profit += margin;
                barrels++;
            }

            return profit;
        }

        [Test]
        public void ItCarriesTheSurplusToTheShortage()
        {
            CityMarket source = Market(Surplus, AtTarget);
            CityMarket shortage = Market(Empty, AtTarget);
            CityMarket neutral = Market(AtTarget, AtTarget);
            var planner = new TradeRoutePlanner(new[] { source, neutral, shortage }, Days(3, 1d), 1d);

            Assert.IsTrue(planner.TryPlanTrade(0, 200, null, out TradePlan plan));

            long expected = ProfitOfOneGood(source, shortage, Fish, 200, out int barrels);
            Assert.AreEqual(2, plan.Destination);
            Assert.AreEqual(barrels, plan.BarrelsOf(Fish));
            Assert.AreEqual(0, plan.BarrelsOf(Iron));
            Assert.AreEqual(expected, plan.Profit);
            Assert.AreEqual(barrels, plan.TotalBarrels);
        }

        [Test]
        public void LoadingStopsWhenTheNextBarrelNoLongerPays()
        {
            CityMarket source = Market(Surplus, AtTarget);
            CityMarket shortage = Market(Empty, AtTarget);
            var planner = new TradeRoutePlanner(new[] { source, shortage }, Days(2, 1d), 1d);

            planner.TryPlanTrade(0, 200, null, out TradePlan plan);

            int loaded = plan.BarrelsOf(Fish);
            Assert.Greater(loaded, 0);
            Assert.Less(loaded, 120);
            Assert.Greater(shortage.SellPriceOf(Fish, loaded - 1), source.BuyPriceOf(Fish, loaded - 1));
            Assert.LessOrEqual(shortage.SellPriceOf(Fish, loaded), source.BuyPriceOf(Fish, loaded));
        }

        [Test]
        public void LoadingStopsAtTheCapacity()
        {
            var planner = new TradeRoutePlanner(
                new[] { Market(Surplus, AtTarget), Market(Empty, AtTarget) }, Days(2, 1d), 1d);

            planner.TryPlanTrade(0, 7, null, out TradePlan plan);

            Assert.AreEqual(7, plan.TotalBarrels);
        }

        [Test]
        public void LoadingStopsAtTheStock()
        {
            // A city that does not consume a good sells it at its base price down to the
            // last barrel: only its stock of 10 stops the loading.
            var source = new CityMarket(
                new[] { new MarketGood(5, 10, 25, 0, 0, 0), new MarketGood(25, 50, 125, 0, 0, 0) },
                30,
                0,
                new MarketPricing(0.05, 0.05),
                7);
            source.Put(Fish, 10);
            var planner = new TradeRoutePlanner(new[] { source, Market(Empty, AtTarget) }, Days(2, 1d), 1d);

            Assert.IsTrue(planner.TryPlanTrade(0, 200, null, out TradePlan plan));
            Assert.AreEqual(10, plan.BarrelsOf(Fish));
            Assert.AreEqual(0, plan.BarrelsOf(Iron));
        }

        [Test]
        public void ACargoMixesTheGoodsThatPayBest()
        {
            CityMarket source = Market(Surplus, Surplus);
            CityMarket shortage = Market(Empty, Empty);
            var planner = new TradeRoutePlanner(new[] { source, shortage }, Days(2, 1d), 1d);

            planner.TryPlanTrade(0, 60, null, out TradePlan plan);

            long fishAlone = ProfitOfOneGood(source, shortage, Fish, 60, out _);
            long ironAlone = ProfitOfOneGood(source, shortage, Iron, 60, out _);
            Assert.AreEqual(60, plan.TotalBarrels);
            Assert.Greater(plan.BarrelsOf(Fish), 0);
            Assert.Greater(plan.BarrelsOf(Iron), 0);
            Assert.GreaterOrEqual(plan.Profit, fishAlone);
            Assert.GreaterOrEqual(plan.Profit, ironAlone);
        }

        [Test]
        public void AtEqualProfit_TheNearerDestinationWins()
        {
            double[,] days = Days(3, 1d);
            SetDays(days, 0, 1, 3d);
            var planner = new TradeRoutePlanner(
                new[] { Market(Surplus, AtTarget), Market(Empty, AtTarget), Market(Empty, AtTarget) }, days, 1d);

            planner.TryPlanTrade(0, 200, null, out TradePlan plan);

            Assert.AreEqual(2, plan.Destination);
        }

        [Test]
        public void APortThatCannotBeReached_IsIgnored()
        {
            double[,] days = Days(3, 1d);
            SetDays(days, 0, 2, double.PositiveInfinity);
            var planner = new TradeRoutePlanner(
                new[] { Market(Surplus, AtTarget), Market(AtTarget, AtTarget), Market(Empty, AtTarget) }, days, 1d);

            Assert.IsTrue(planner.TryPlanTrade(0, 200, null, out TradePlan plan));
            Assert.AreEqual(1, plan.Destination);
        }

        [Test]
        public void CargoUnderWayToAPort_CountsAsDeliveredThere()
        {
            double[,] days = Days(3, 1d);
            SetDays(days, 0, 2, 3d);
            var planner = new TradeRoutePlanner(
                new[] { Market(Surplus, AtTarget), Market(Empty, AtTarget), Market(Empty, AtTarget) }, days, 1d);
            var incoming = new int[3, 2];
            incoming[1, Fish] = 200;

            planner.TryPlanTrade(0, 200, incoming, out TradePlan plan);

            Assert.AreEqual(2, plan.Destination);
        }

        [Test]
        public void WhenNothingPays_ThereIsNoTrade()
        {
            var planner = new TradeRoutePlanner(
                new[] { Market(AtTarget, AtTarget), Market(AtTarget, AtTarget) }, Days(2, 1d), 1d);

            Assert.IsFalse(planner.TryPlanTrade(0, 200, null, out TradePlan plan));
            Assert.AreEqual(0, plan.TotalBarrels);
            Assert.IsFalse(planner.TryPlanRelocation(0, 200, null, out _));
        }

        [Test]
        public void ASinglePort_HasNoTrade()
        {
            var planner = new TradeRoutePlanner(new[] { Market(Surplus, Surplus) }, Days(1, 0d), 1d);

            Assert.IsFalse(planner.TryPlanTrade(0, 200, null, out _));
            Assert.IsFalse(planner.TryPlanRelocation(0, 200, null, out _));
        }

        [Test]
        public void NoPort_IsAPlannerWithoutGoods()
        {
            var planner = new TradeRoutePlanner(new CityMarket[0], new double[0, 0], 1d);

            Assert.AreEqual(0, planner.PortCount);
            Assert.AreEqual(0, planner.GoodCount);
        }

        [Test]
        public void WithoutRoom_ThereIsNoTrade()
        {
            var planner = new TradeRoutePlanner(
                new[] { Market(Surplus, AtTarget), Market(Empty, AtTarget) }, Days(2, 1d), 1d);

            Assert.IsFalse(planner.TryPlanTrade(0, 0, null, out _));
        }

        [Test]
        public void ATripOfNoTime_IsStillCompared()
        {
            var planner = new TradeRoutePlanner(
                new[] { Market(Surplus, AtTarget), Market(Empty, AtTarget) }, Days(2, 0d), 0d);

            Assert.IsTrue(planner.TryPlanTrade(0, 200, null, out TradePlan plan));
            Assert.AreEqual(1, plan.Destination);
            Assert.Greater(plan.Profit, 0);
        }

        [Test]
        public void MarketsOfDifferentCatalogues_TradeTheGoodsTheyShare()
        {
            var small = new CityMarket(
                new[] { new MarketGood(5, 10, 25, 2, 0, 0) }, 30, 0, new MarketPricing(0.05, 0.05), 7);
            var planner = new TradeRoutePlanner(new[] { Market(Surplus, Surplus), small }, Days(2, 1d), 1d);

            Assert.AreEqual(1, planner.GoodCount);
            Assert.IsTrue(planner.TryPlanTrade(0, 200, null, out TradePlan plan));
            Assert.AreEqual(0, plan.BarrelsOf(Iron));
        }

        [Test]
        public void Relocation_GoesWhereTheBestTradeStarts()
        {
            // Nothing to buy here; port 1 has a surplus that port 2 lacks.
            var planner = new TradeRoutePlanner(
                new[] { Market(Empty, AtTarget), Market(Surplus, AtTarget), Market(Empty, AtTarget) }, Days(3, 1d), 1d);

            Assert.IsFalse(planner.TryPlanTrade(0, 200, null, out _));
            Assert.IsTrue(planner.TryPlanRelocation(0, 200, null, out int destination));
            Assert.AreEqual(1, destination);
        }

        [Test]
        public void Relocation_IgnoresASourceThatCannotBeReached()
        {
            double[,] days = Days(3, 1d);
            SetDays(days, 0, 1, double.PositiveInfinity);
            var planner = new TradeRoutePlanner(
                new[] { Market(Empty, AtTarget), Market(Surplus, AtTarget), Market(Empty, AtTarget) }, days, 1d);

            Assert.IsFalse(planner.TryPlanRelocation(0, 200, null, out _));
        }

        [Test]
        public void RejectsBadArguments()
        {
            CityMarket[] markets = { Market(Surplus, AtTarget), Market(Empty, AtTarget) };
            var planner = new TradeRoutePlanner(markets, Days(2, 1d), 1d);

            Assert.Throws<ArgumentNullException>(() => new TradeRoutePlanner(null, Days(2, 1d), 1d));
            Assert.Throws<ArgumentNullException>(() => new TradeRoutePlanner(markets, null, 1d));
            Assert.Throws<ArgumentException>(() => new TradeRoutePlanner(markets, Days(3, 1d), 1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TradeRoutePlanner(markets, Days(2, 1d), -1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => planner.TryPlanTrade(2, 200, null, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => planner.TryPlanTrade(0, -1, null, out _));
            Assert.Throws<ArgumentException>(() => planner.TryPlanTrade(0, 200, new int[1, 2], out _));
        }
    }
}
```

- [ ] **Step 2: Run `TradeRoutePlannerTests`.** Expected: compile error, the types do not exist.

- [ ] **Step 3: Implement.**

`TradePlan.cs`:

```csharp
namespace DarkFantasyMerchant.Core
{
    /// <summary>A trade a ship can make from the port it is in: what to buy and where to sell it.</summary>
    public readonly struct TradePlan
    {
        private readonly int[] barrels;

        /// <param name="barrels">Barrels to buy of each good, by index. Kept, not copied.</param>
        public TradePlan(int destination, int[] barrels, long profit)
        {
            Destination = destination;
            this.barrels = barrels;
            Profit = profit;
        }

        /// <summary>Index of the port where the cargo is sold.</summary>
        public int Destination { get; }

        /// <summary>What the cargo is sold for minus what it costs, if no price changes meanwhile.</summary>
        public long Profit { get; }

        public int TotalBarrels
        {
            get
            {
                int total = 0;

                if (barrels != null)
                {
                    foreach (int count in barrels)
                    {
                        total += count;
                    }
                }

                return total;
            }
        }

        /// <returns>Barrels to buy of a good; none for a good the plan does not know.</returns>
        public int BarrelsOf(int good)
        {
            return barrels != null && good >= 0 && good < barrels.Length ? barrels[good] : 0;
        }
    }
}
```

`TradeRoutePlanner.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Chooses what a merchant ship buys in the port it is in and where it sells it: the
    /// cargo and the destination that earn the most per day. Prices follow stocks, so
    /// that is a good in surplus here carried to a port that is short of it. Ports are
    /// indices into the markets it was given.
    /// </summary>
    public sealed class TradeRoutePlanner
    {
        // A trip and a stopover of no time would give a profit per day of infinity.
        private const double ShortestTripDays = 1e-6;

        private readonly IReadOnlyList<CityMarket> markets;
        private readonly double[,] travelDays;
        private readonly double stopoverDays;

        // Barrels of each good in the cargo being worked out.
        private readonly int[] loaded;

        /// <param name="travelDays">
        /// Days from a port to another, by index; infinity when the ship cannot sail there.
        /// Read on every plan, so it can still be filled in after the planner is made.
        /// </param>
        /// <param name="stopoverDays">Days a ship stays in a port, counted in the length of a trade.</param>
        public TradeRoutePlanner(IReadOnlyList<CityMarket> markets, double[,] travelDays, double stopoverDays)
        {
            this.markets = markets ?? throw new ArgumentNullException(nameof(markets));
            this.travelDays = travelDays ?? throw new ArgumentNullException(nameof(travelDays));

            if (travelDays.GetLength(0) != markets.Count || travelDays.GetLength(1) != markets.Count)
            {
                throw new ArgumentException("The travel days must have a row and a column per market.", nameof(travelDays));
            }

            // The negated comparison also rejects NaN.
            if (!(stopoverDays >= 0d) || double.IsInfinity(stopoverDays))
            {
                throw new ArgumentOutOfRangeException(nameof(stopoverDays));
            }

            this.stopoverDays = stopoverDays;

            // Markets made from different catalogues only trade the goods they all have.
            int goodCount = markets.Count > 0 ? int.MaxValue : 0;

            foreach (CityMarket market in markets)
            {
                if (market == null)
                {
                    throw new ArgumentException("A market is missing.", nameof(markets));
                }

                goodCount = Math.Min(goodCount, market.GoodCount);
            }

            GoodCount = goodCount;
            loaded = new int[goodCount];
        }

        public int PortCount => markets.Count;

        public int GoodCount { get; }

        /// <summary>The trade from a port that earns the most per day of trip and stopover.</summary>
        /// <param name="capacity">Barrels the ship has room for.</param>
        /// <param name="incoming">
        /// Barrels other ships are carrying to each port, by port then good: they count as
        /// already delivered there. Null for none.
        /// </param>
        /// <returns>False when no destination gives a profit.</returns>
        public bool TryPlanTrade(int from, int capacity, int[,] incoming, out TradePlan plan)
        {
            Require(from, capacity, incoming);

            plan = default;

            int bestDestination = -1;
            long bestProfit = 0;
            double bestScore = 0d;
            int[] bestBarrels = null;

            for (int to = 0; to < PortCount; to++)
            {
                if (to == from || !IsReachable(from, to))
                {
                    continue;
                }

                long profit = Load(from, to, capacity, incoming);

                if (profit <= 0)
                {
                    continue;
                }

                double score = profit / TripDays(from, to);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDestination = to;
                    bestProfit = profit;

                    if (bestBarrels == null)
                    {
                        bestBarrels = new int[GoodCount];
                    }

                    Array.Copy(loaded, bestBarrels, GoodCount);
                }
            }

            if (bestDestination < 0)
            {
                return false;
            }

            plan = new TradePlan(bestDestination, bestBarrels, bestProfit);
            return true;
        }

        /// <summary>
        /// Where to sail empty when there is nothing to trade from a port: the port from
        /// which the best trade starts, counting the days to get there.
        /// </summary>
        /// <returns>False when no port the ship can reach has a trade that pays.</returns>
        public bool TryPlanRelocation(int from, int capacity, int[,] incoming, out int destination)
        {
            Require(from, capacity, incoming);

            destination = -1;
            double bestScore = 0d;

            for (int source = 0; source < PortCount; source++)
            {
                if (source == from || !IsReachable(from, source))
                {
                    continue;
                }

                if (!TryPlanTrade(source, capacity, incoming, out TradePlan trade))
                {
                    continue;
                }

                double score = trade.Profit / (TripDays(from, source) + TripDays(source, trade.Destination));

                if (score > bestScore)
                {
                    bestScore = score;
                    destination = source;
                }
            }

            return destination >= 0;
        }

        /// <summary>
        /// Fills <see cref="loaded"/> with the cargo that earns the most between two ports:
        /// barrel by barrel, the good whose next barrel pays best, while one pays.
        /// The margin of a good only falls as it is loaded, so this is the best cargo.
        /// </summary>
        /// <returns>What the cargo earns.</returns>
        private long Load(int from, int to, int capacity, int[,] incoming)
        {
            CityMarket source = markets[from];
            CityMarket destination = markets[to];
            long profit = 0;

            Array.Clear(loaded, 0, loaded.Length);

            for (int total = 0; total < capacity; total++)
            {
                int bestGood = -1;
                long bestMargin = 0;

                for (int good = 0; good < GoodCount; good++)
                {
                    if (loaded[good] >= source.AvailableOf(good))
                    {
                        continue;
                    }

                    int delivered = (incoming != null ? incoming[to, good] : 0) + loaded[good];
                    long margin = destination.SellPriceOf(good, delivered) - source.BuyPriceOf(good, loaded[good]);

                    if (margin > bestMargin)
                    {
                        bestMargin = margin;
                        bestGood = good;
                    }
                }

                if (bestGood < 0)
                {
                    break;
                }

                loaded[bestGood]++;
                profit += bestMargin;
            }

            return profit;
        }

        private double TripDays(int from, int to)
        {
            return Math.Max(ShortestTripDays, travelDays[from, to] + stopoverDays);
        }

        // False for NaN too.
        private bool IsReachable(int from, int to)
        {
            double days = travelDays[from, to];

            return days >= 0d && !double.IsInfinity(days);
        }

        private void Require(int from, int capacity, int[,] incoming)
        {
            if (from < 0 || from >= PortCount)
            {
                throw new ArgumentOutOfRangeException(nameof(from));
            }

            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (incoming != null && (incoming.GetLength(0) < PortCount || incoming.GetLength(1) < GoodCount))
            {
                throw new ArgumentException("The incoming barrels must have a row per port and a column per good.", nameof(incoming));
            }
        }
    }
}
```

- [ ] **Step 4: Run `TradeRoutePlannerTests`.** Expected: all pass. If `ACargoMixesTheGoodsThatPayBest` fails on one of its `BarrelsOf` assertions, read the margins of the two goods barrel by barrel and fix the test's capacity, not the planner: iron's first barrels pay about five times fish's, and fish comes in once iron's margin has fallen under it.

- [ ] **Step 5: Commit** — message: `Marchands IA : choix de la cargaison et de la destination les plus rentables`.

---

### Task 4: The fleet

**Files:**
- Create: `Assets/Scripts/Core/StartPortDraw.cs`, `Assets/Scripts/Core/AiMerchantFleet.cs`
- Test: `Assets/Tests/EditMode/StartPortDrawTests.cs`, `Assets/Tests/EditMode/AiMerchantFleetTests.cs`

**Interfaces:**
- Consumes: `TradeRoutePlanner` (Task 3), `Trade.Buy` / `Trade.Sell` without a treasury (Task 1), `ShipDocking<TPort>` (`Docked`, `IsDocked`, `PortOf`, `DestinationPortOf`, `OrderToPort`).
- Produces:
  - `static void StartPortDraw.Draw(int count, IReadOnlyList<int> candidates, Func<int, int> nextBelow, List<int> results)` — `nextBelow(n)` returns a number from 0 to n − 1.
  - `AiMerchantFleet<TPort>(ShipDocking<TPort> docking, IReadOnlyList<TPort> ports, IReadOnlyList<Vector2> portPositions, IReadOnlyList<CityMarket> markets, TradeRoutePlanner planner, double stopoverSeconds)`, `void Add(Ship ship, int startPort)`, `bool Contains(Ship ship)`, `long ProfitOf(Ship ship)`, `void Advance(double seconds)`, `int Count`.

- [ ] **Step 1: Write the failing tests.**

`StartPortDrawTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class StartPortDrawTests
    {
        private List<int> results;

        [SetUp]
        public void SetUp()
        {
            results = new List<int> { 99 };
        }

        [Test]
        public void Draw_GivesDifferentPorts_WhileThereAreEnough()
        {
            var random = new Random(7);

            StartPortDraw.Draw(4, new[] { 3, 5, 8, 9, 12 }, random.Next, results);

            Assert.AreEqual(4, results.Count);
            CollectionAssert.AllItemsAreUnique(results);
            CollectionAssert.IsSubsetOf(results, new[] { 3, 5, 8, 9, 12 });
        }

        [Test]
        public void Draw_RepeatsPorts_OnlyOnceAllWereDrawn()
        {
            var random = new Random(7);

            StartPortDraw.Draw(5, new[] { 3, 5 }, random.Next, results);

            Assert.AreEqual(5, results.Count);
            Assert.AreEqual(3, results.FindAll(port => port == results[0]).Count);
            Assert.AreNotEqual(results[0], results[1]);
        }

        [Test]
        public void Draw_FollowsTheNumbersItIsGiven()
        {
            // Always the first of what is left to shuffle: the order is reversed.
            StartPortDraw.Draw(3, new[] { 1, 2, 3 }, n => 0, results);

            CollectionAssert.AreEqual(new[] { 2, 3, 1 }, results);
        }

        [Test]
        public void Draw_WithoutCandidates_DrawsNothing()
        {
            StartPortDraw.Draw(5, new int[0], n => 0, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void Draw_OfNoShip_DrawsNothing()
        {
            StartPortDraw.Draw(0, new[] { 1, 2 }, n => 0, results);

            Assert.AreEqual(0, results.Count);
        }
    }
}
```

`Draw_FollowsTheNumbersItIsGiven` pins the shuffle: a Fisher–Yates from the end, `for i = last down to 1: swap(i, nextBelow(i + 1))`. With `nextBelow` always 0 on `[1, 2, 3]`: swap(2, 0) gives `[3, 2, 1]`, swap(1, 0) gives `[2, 3, 1]`.

`AiMerchantFleetTests.cs`:

```csharp
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class AiMerchantFleetTests
    {
        private sealed class Port
        {
        }

        private const int Fish = 0;
        private const float Speed = 4f;
        private const double Stopover = 30d;

        // Ports: A where the surplus is, B near it, C far from both.
        private const int A = 0;
        private const int B = 1;
        private const int C = 2;

        private MapProjection projection;
        private NavigationGrid grid;
        private NavigationPathfinder navigation;
        private ShipDocking<Port> docking;
        private Port[] ports;
        private Vector2[] positions;
        private CityMarket[] markets;
        private AiMerchantFleet<Port> fleet;

        [SetUp]
        public void SetUp()
        {
            projection = new MapProjection(40f, 2f);
            grid = GridAssert.FromRows(
                "........",
                "...#....",
                "...#..##",
                "...#..#.");
            navigation = new NavigationPathfinder(grid);
            docking = new ShipDocking<Port>();
            ports = new[] { new Port(), new Port(), new Port() };
            positions = new[]
            {
                GridAssert.CellCenter(grid, 0, 3), GridAssert.CellCenter(grid, 5, 0), GridAssert.CellCenter(grid, 0, 0),
            };
        }

        /// <summary>One good, 2 barrels a day, a target of 60 barrels.</summary>
        private static CityMarket Market(double days)
        {
            return new CityMarket(
                new[] { new MarketGood(5, 10, 25, 2, 0, 0) }, 30, days, new MarketPricing(0.05, 0.05), 7);
        }

        private void CreateFleet(double daysInA, double daysInB, double daysInC)
        {
            markets = new[] { Market(daysInA), Market(daysInB), Market(daysInC) };

            var days = new double[3, 3];
            days[A, B] = days[B, A] = 1d;
            days[A, C] = days[C, A] = 2d;
            days[B, C] = days[C, B] = 2d;

            fleet = new AiMerchantFleet<Port>(
                docking, ports, positions, markets, new TradeRoutePlanner(markets, days, 1d), Stopover);
        }

        private Ship AddShip(int port)
        {
            var ship = new Ship(projection, positions[port], Speed, TestCrew.Full, navigation, new CargoHold(200));
            fleet.Add(ship, port);
            docking.Update();
            return ship;
        }

        private void Arrive(Ship ship)
        {
            ship.Advance(1000f);
            docking.Update();
        }

        [Test]
        public void AShipAdded_EntersItsStartPort()
        {
            CreateFleet(60, 0, 30);

            Ship ship = AddShip(A);

            Assert.AreSame(ports[A], docking.PortOf(ship));
            Assert.IsTrue(fleet.Contains(ship));
            Assert.AreEqual(1, fleet.Count);
            Assert.AreEqual(0, fleet.ProfitOf(ship));
        }

        [Test]
        public void DuringTheStopover_ItStaysInPort()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(A);

            fleet.Advance(Stopover - 1d);

            Assert.IsTrue(docking.IsDocked(ship));
            Assert.AreEqual(0, ship.Cargo.Used);
        }

        [Test]
        public void AfterTheStopover_ItBuysAndSailsToTheShortage()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(A);

            fleet.Advance(Stopover - 1d);
            fleet.Advance(1d);

            Assert.IsFalse(docking.IsDocked(ship));
            Assert.AreSame(ports[B], docking.DestinationPortOf(ship));
            Assert.Greater(ship.Cargo.Used, 0);
            Assert.AreEqual(120 - ship.Cargo.Used, markets[A].AvailableOf(Fish));
            Assert.Less(fleet.ProfitOf(ship), 0);
        }

        [Test]
        public void OnArrival_ItSellsItsWholeHold()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(A);
            fleet.Advance(Stopover);
            int carried = ship.Cargo.Used;

            Arrive(ship);

            Assert.AreSame(ports[B], docking.PortOf(ship));
            Assert.AreEqual(0, ship.Cargo.Used);
            Assert.AreEqual(carried, markets[B].AvailableOf(Fish));
            Assert.Greater(fleet.ProfitOf(ship), 0);
        }

        [Test]
        public void OnArrival_ItSellsEvenAtALoss()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(A);
            fleet.Advance(Stopover);

            // Someone else filled the port meanwhile.
            markets[B].Put(Fish, 1000);
            Arrive(ship);

            Assert.AreEqual(0, ship.Cargo.Used);
            Assert.Less(fleet.ProfitOf(ship), 0);
        }

        [Test]
        public void ALongStep_BuysOnce()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(A);

            fleet.Advance(Stopover * 10d);
            fleet.Advance(Stopover * 10d);

            Assert.AreSame(ports[B], docking.DestinationPortOf(ship));
            Assert.AreEqual(120 - ship.Cargo.Used, markets[A].AvailableOf(Fish));
        }

        [Test]
        public void WithoutARoute_ItWaitsAndAsksAgain()
        {
            CreateFleet(30, 30, 30);
            Ship ship = AddShip(A);

            fleet.Advance(Stopover);
            Assert.IsTrue(docking.IsDocked(ship));

            // A surplus appears: not acted on before the next stopover has passed.
            markets[A].Put(Fish, 200);
            fleet.Advance(Stopover - 1d);
            Assert.IsTrue(docking.IsDocked(ship));

            fleet.Advance(1d);
            Assert.IsFalse(docking.IsDocked(ship));
            Assert.Greater(ship.Cargo.Used, 0);
        }

        [Test]
        public void WithNothingToBuyHere_ItSailsEmptyToTheBestSource()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(B);

            fleet.Advance(Stopover);

            Assert.AreSame(ports[A], docking.DestinationPortOf(ship));
            Assert.AreEqual(0, ship.Cargo.Used);
            Assert.AreEqual(0, fleet.ProfitOf(ship));
        }

        [Test]
        public void TwoShips_DoNotAimAtTheSameShortage()
        {
            // Enough in A for both; B and C both lack it.
            CreateFleet(300, 0, 0);
            Ship first = AddShip(A);
            Ship second = AddShip(A);

            fleet.Advance(Stopover);

            Assert.AreSame(ports[B], docking.DestinationPortOf(first));
            Assert.AreSame(ports[C], docking.DestinationPortOf(second));
        }

        [Test]
        public void AShipThatIsNotOfTheFleet_IsLeftAlone()
        {
            CreateFleet(60, 0, 30);
            var stranger = new Ship(projection, positions[A], Speed, TestCrew.Full, navigation, new CargoHold(200));
            stranger.Cargo.TryAdd(Fish, 10);

            docking.OrderToPort(stranger, ports[B], positions[B]);
            Arrive(stranger);
            fleet.Advance(Stopover * 3d);

            Assert.IsFalse(fleet.Contains(stranger));
            Assert.AreEqual(0, fleet.ProfitOf(stranger));
            Assert.AreEqual(10, stranger.Cargo.Used);
            Assert.IsTrue(docking.IsDocked(stranger));
        }

        [Test]
        public void AShipIdleAtSea_IsSentBackToThePortItLeft()
        {
            CreateFleet(60, 0, 30);
            Ship ship = AddShip(A);
            fleet.Advance(Stopover);

            // An order that is not the fleet's leaves it at sea, with no port to enter.
            docking.OrderToPoint(ship, GridAssert.CellCenter(grid, 2, 3));
            ship.Advance(1000f);
            docking.Update();
            Assert.IsFalse(docking.IsDocked(ship));

            fleet.Advance(1d);

            Assert.AreSame(ports[A], docking.DestinationPortOf(ship));
        }
    }
}
```

- [ ] **Step 2: Run both fixtures.** Expected: compile error, the types do not exist.

- [ ] **Step 3: Implement.**

`StartPortDraw.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Draws the ports a fleet starts in.</summary>
    public static class StartPortDraw
    {
        /// <summary>
        /// Draws a port for each ship among the candidates, in a random order, without
        /// repeating one before all were drawn.
        /// </summary>
        /// <param name="nextBelow">Gives a random number from 0 to its argument, excluded.</param>
        /// <param name="results">Cleared, then filled with a candidate per ship; empty without candidates.</param>
        public static void Draw(int count, IReadOnlyList<int> candidates, Func<int, int> nextBelow, List<int> results)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (nextBelow == null)
            {
                throw new ArgumentNullException(nameof(nextBelow));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            if (count < 1 || candidates.Count == 0)
            {
                return;
            }

            var shuffled = new int[candidates.Count];

            for (int i = 0; i < shuffled.Length; i++)
            {
                shuffled[i] = candidates[i];
            }

            for (int i = shuffled.Length - 1; i > 0; i--)
            {
                // Clamped: a generator that breaks its promise must not break the draw.
                int other = Math.Min(i, Math.Max(0, nextBelow(i + 1)));
                int swapped = shuffled[i];
                shuffled[i] = shuffled[other];
                shuffled[other] = swapped;
            }

            for (int i = 0; i < count; i++)
            {
                results.Add(shuffled[i % shuffled.Length]);
            }
        }
    }
}
```

`AiMerchantFleet.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The merchant ships that trade on their own. A ship sells its whole hold when it
    /// enters a port, stays there for a stopover, then buys the cargo that earns the most
    /// and sails to where it sells; with nothing to trade it sails empty to a better
    /// port, or waits. Their gold is not limited: only their profit is counted.
    /// </summary>
    public sealed class AiMerchantFleet<TPort> where TPort : class
    {
        private sealed class Merchant
        {
            public Ship Ship;
            public long Profit;

            // Seconds left of its stopover, counted while it lies in port.
            public double Wait;

            // The port it last lay in, or the one it starts in: one it can sail to.
            public int LastPort;

            // It could not be sent back to that port: nothing more is tried for it at sea.
            public bool IsStranded;
        }

        private readonly ShipDocking<TPort> docking;
        private readonly IReadOnlyList<TPort> ports;
        private readonly IReadOnlyList<Vector2> portPositions;
        private readonly IReadOnlyList<CityMarket> markets;
        private readonly TradeRoutePlanner planner;
        private readonly double stopoverSeconds;

        private readonly Dictionary<TPort, int> portIndices = new Dictionary<TPort, int>();
        private readonly Dictionary<Ship, Merchant> merchantsByShip = new Dictionary<Ship, Merchant>();
        private readonly List<Merchant> merchants = new List<Merchant>();

        // Barrels under way to each port, by port then good. Rebuilt for every decision.
        private readonly int[,] incoming;

        /// <param name="docking">The docking of the map: the ships are ordered through it.</param>
        /// <param name="ports">The ports the ships trade between, in the planner's order.</param>
        /// <param name="portPositions">Normalized map position of each port, as an order to it takes.</param>
        /// <param name="markets">The market of each port.</param>
        /// <param name="stopoverSeconds">Seconds of the world a ship stays in a port.</param>
        public AiMerchantFleet(
            ShipDocking<TPort> docking,
            IReadOnlyList<TPort> ports,
            IReadOnlyList<Vector2> portPositions,
            IReadOnlyList<CityMarket> markets,
            TradeRoutePlanner planner,
            double stopoverSeconds)
        {
            this.docking = docking ?? throw new ArgumentNullException(nameof(docking));
            this.ports = ports ?? throw new ArgumentNullException(nameof(ports));
            this.portPositions = portPositions ?? throw new ArgumentNullException(nameof(portPositions));
            this.markets = markets ?? throw new ArgumentNullException(nameof(markets));
            this.planner = planner ?? throw new ArgumentNullException(nameof(planner));

            if (ports.Count != planner.PortCount || portPositions.Count != planner.PortCount
                || markets.Count != planner.PortCount)
            {
                throw new ArgumentException("There must be a port, a position and a market per port of the planner.");
            }

            // The negated comparison also rejects NaN.
            if (!(stopoverSeconds >= 0d) || double.IsInfinity(stopoverSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(stopoverSeconds));
            }

            this.stopoverSeconds = stopoverSeconds;
            incoming = new int[planner.PortCount, planner.GoodCount];

            for (int i = 0; i < ports.Count; i++)
            {
                portIndices[ports[i]] = i;
            }

            docking.Docked += OnDocked;
        }

        public int Count => merchants.Count;

        public bool Contains(Ship ship)
        {
            return ship != null && merchantsByShip.ContainsKey(ship);
        }

        /// <returns>Gold the ship's sales brought minus what its purchases cost; 0 for a ship that is not of the fleet.</returns>
        public long ProfitOf(Ship ship)
        {
            return ship != null && merchantsByShip.TryGetValue(ship, out Merchant merchant) ? merchant.Profit : 0;
        }

        /// <summary>Takes a ship into the fleet and orders it into the port it starts beside.</summary>
        public void Add(Ship ship, int startPort)
        {
            if (ship == null)
            {
                throw new ArgumentNullException(nameof(ship));
            }

            if (startPort < 0 || startPort >= ports.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(startPort));
            }

            if (merchantsByShip.ContainsKey(ship))
            {
                throw new ArgumentException("The ship is already in the fleet.", nameof(ship));
            }

            var merchant = new Merchant { Ship = ship, LastPort = startPort, Wait = stopoverSeconds };
            merchants.Add(merchant);
            merchantsByShip.Add(ship, merchant);
            SendTo(merchant, startPort);
        }

        /// <summary>Counts the stopovers down and sends off the ships whose stopover is over.</summary>
        /// <param name="seconds">Seconds of the world since the last call.</param>
        public void Advance(double seconds)
        {
            // The negated comparison also rejects NaN.
            if (!(seconds > 0d))
            {
                return;
            }

            foreach (Merchant merchant in merchants)
            {
                Ship ship = merchant.Ship;

                if (docking.IsDocked(ship))
                {
                    merchant.Wait -= seconds;

                    if (merchant.Wait <= 0d)
                    {
                        Depart(merchant);
                    }
                }
                else if (!ship.IsMoving && docking.DestinationPortOf(ship) == null && !merchant.IsStranded)
                {
                    // Idle at sea, which its own orders never leave it: back to a port it knows.
                    merchant.IsStranded = !SendTo(merchant, merchant.LastPort);
                }
            }
        }

        private void OnDocked(Ship ship, TPort port)
        {
            if (!merchantsByShip.TryGetValue(ship, out Merchant merchant) || !portIndices.TryGetValue(port, out int index))
            {
                return;
            }

            merchant.LastPort = index;
            merchant.IsStranded = false;
            merchant.Wait = stopoverSeconds;

            // The whole hold, whatever the prices have become since it was bought.
            CityMarket market = markets[index];

            for (int good = 0; good < market.GoodCount; good++)
            {
                int barrels = ship.Cargo.BarrelsOf(good);

                if (barrels > 0)
                {
                    merchant.Profit += Trade.Sell(market, ship.Cargo, good, barrels).Gold;
                }
            }
        }

        private void Depart(Merchant merchant)
        {
            Ship ship = merchant.Ship;

            if (!portIndices.TryGetValue(docking.PortOf(ship), out int from))
            {
                // In a port the fleet does not trade in: it waits there.
                merchant.Wait = stopoverSeconds;
                return;
            }

            CountIncoming(merchant);

            int capacity = ship.Cargo.Free;

            if (planner.TryPlanTrade(from, capacity, incoming, out TradePlan plan))
            {
                for (int good = 0; good < planner.GoodCount; good++)
                {
                    int barrels = plan.BarrelsOf(good);

                    if (barrels > 0)
                    {
                        merchant.Profit -= Trade.Buy(markets[from], ship.Cargo, good, barrels).Gold;
                    }
                }

                SendTo(merchant, plan.Destination);
            }
            else if (planner.TryPlanRelocation(from, capacity, incoming, out int destination))
            {
                SendTo(merchant, destination);
            }
            else
            {
                merchant.Wait = stopoverSeconds;
            }
        }

        /// <summary>What the other ships of the fleet are carrying to each port.</summary>
        private void CountIncoming(Merchant deciding)
        {
            Array.Clear(incoming, 0, incoming.Length);

            foreach (Merchant other in merchants)
            {
                if (ReferenceEquals(other, deciding))
                {
                    continue;
                }

                TPort destination = docking.DestinationPortOf(other.Ship);

                if (destination == null || !portIndices.TryGetValue(destination, out int port))
                {
                    continue;
                }

                for (int good = 0; good < planner.GoodCount; good++)
                {
                    incoming[port, good] += other.Ship.Cargo.BarrelsOf(good);
                }
            }
        }

        private bool SendTo(Merchant merchant, int port)
        {
            return docking.OrderToPort(merchant.Ship, ports[port], portPositions[port]);
        }
    }
}
```

- [ ] **Step 4: Run both fixtures, then all EditMode tests.** Expected: all pass.

- [ ] **Step 5: Commit** — message: `Marchands IA : flotte qui vend à l'arrivée, fait escale, achète et repart`.

---

### Task 5: Who owns a ship

**Files:**
- Modify: `Assets/Scripts/Game/ShipView.cs`, `ShipsView.cs`, `WorldMapInteraction.cs`, `PlayerExpenses.cs`, `ShipArrivalNotifier.cs`, `CityInfoPanelController.cs`

**Interfaces:**
- Produces on `ShipsView`:
  - `IReadOnlyList<Ship> PlayerShips`
  - `bool IsPlayerShip(Ship ship)`
  - `NavigationPathfinder Navigation` (null on a map without a mask)
  - `Ship SpawnAiShip(ShipDefinition definition, Vector2 position, ShipCrew crew, string displayName, Color tint)` — null, with an error logged, when the view is not ready or the definition is unusable.
- `ShipView.Initialize(Ship ship, ShipDefinition definition, string displayName, Color? tint = null)`.

There is no EditMode test for these MonoBehaviours; the deliverable is checked by the whole suite still passing and by Task 6's play check.

- [ ] **Step 1: `ShipView`.** Replace `Initialize`:

```csharp
        /// <param name="tint">Replaces the ship's normal color; null keeps the prefab's.</param>
        public void Initialize(Ship ship, ShipDefinition definition, string displayName, Color? tint = null)
        {
            this.ship = ship;
            this.definition = definition;

            if (tint.HasValue)
            {
                normalColor = tint.Value;
            }

            name = $"Ship ({displayName})";
            Refresh();
        }
```

- [ ] **Step 2: `ShipsView`.**

Add the fields after `definitions`:

```csharp
        // Name of each entry of ships, in the same order: several ships can share a definition.
        private readonly List<string> displayNames = new List<string>();

        private readonly List<Ship> playerShips = new List<Ship>();
```

Add after `Ships`:

```csharp
        /// <summary>The ships of <see cref="Ships"/> that the player owns and orders about.</summary>
        public IReadOnlyList<Ship> PlayerShips => playerShips;

        /// <summary>Where ships can sail. Null before Start, and on a map without a navigation mask.</summary>
        public NavigationPathfinder Navigation => navigation;
```

In `Start`, replace the two validation blocks (speed and crew capacity, from the comment `// The Min attribute only constrains the Inspector` down to the closing brace of the crew capacity check) by:

```csharp
            if (!IsUsable(playerShipDefinition))
            {
                enabled = false;
                return;
            }
```

and the `Spawn(...)` line by:

```csharp
            playerShips.Add(Spawn(
                playerShipDefinition,
                StartPosition(),
                CreatePlayerCrew(playerShipDefinition.CrewCapacity),
                playerShipDefinition.DisplayName,
                null));
```

Replace `DisplayNameOf`:

```csharp
        /// <returns>The name of the ship, or null for a ship that is not on this map.</returns>
        public string DisplayNameOf(Ship ship)
        {
            int index = ship != null ? ships.IndexOf(ship) : -1;

            return index >= 0 ? displayNames[index] : null;
        }

        public bool IsPlayerShip(Ship ship)
        {
            return ship != null && playerShips.Contains(ship);
        }

        /// <summary>
        /// Adds a ship that the player does not own: it is shown, hovered and selected
        /// like the player's, and takes its orders from whoever spawned it.
        /// </summary>
        /// <param name="position">Normalized map position; the ship starts on the water nearest to it.</param>
        /// <param name="tint">The color of its sprite, which tells it from the player's.</param>
        /// <returns>The ship, or null when this view is not ready or the definition is unusable.</returns>
        public Ship SpawnAiShip(ShipDefinition definition, Vector2 position, ShipCrew crew, string displayName, Color tint)
        {
            if (!IsReady)
            {
                Debug.LogError("ShipsView cannot spawn a ship before it has started.", this);
                return null;
            }

            if (definition == null || crew == null)
            {
                Debug.LogError("ShipsView needs a ship definition and a crew to spawn a ship.", this);
                return null;
            }

            return IsUsable(definition) ? Spawn(definition, position, crew, displayName, tint) : null;
        }
```

Add the validation and change `Spawn`:

```csharp
        // The Min attributes only constrain the Inspector, not the asset file.
        private static bool IsUsable(ShipDefinition definition)
        {
            float speed = definition.Speed;

            if (!(speed > 0f) || float.IsInfinity(speed))
            {
                Debug.LogError($"ShipDefinition '{definition.name}' has an invalid speed ({speed}).", definition);
                return false;
            }

            if (definition.CrewCapacity < 1)
            {
                Debug.LogError(
                    $"ShipDefinition '{definition.name}' has an invalid crew capacity ({definition.CrewCapacity}).",
                    definition);
                return false;
            }

            return true;
        }

        private Ship Spawn(ShipDefinition definition, Vector2 position, ShipCrew crew, string displayName, Color? tint)
        {
            // A NaN city position would be rejected by the ship.
            if (float.IsNaN(position.x) || float.IsNaN(position.y))
            {
                position = MapCenter;
            }

            // With a pathfinder, a ship that starts on a city starts on the water beside it.
            // The Min attribute only constrains the Inspector, not the asset file.
            var cargo = new CargoHold(Mathf.Max(0, definition.CargoCapacity));
            var ship = new Ship(mapView.Projection, position, definition.Speed, crew, navigation, cargo);
            ShipView view = Instantiate(shipPrefab, transform);
            view.Initialize(ship, definition, displayName, tint);
            arrivals.Track(ship);

            ships.Add(ship);
            shipWorldPositions.Add(ship.WorldPosition);
            views.Add(view);
            definitions.Add(definition);
            displayNames.Add(displayName);
            return ship;
        }
```

`definitions` is no longer read: delete the field and its `Add`. Update the class summary to: "Owns the ships of the map, the player's and the others, their views, ...".

`IsReady` is `ships.Count > 0`, true once the player ship exists; a ship spawned later gets its scale from `SetShipScale` on the next `LateUpdate`.

- [ ] **Step 3: `WorldMapInteraction`.**

In `OnCommanded`, replace `if (ship == null)` by:

```csharp
            // Only the player's ships take the player's orders.
            if (ship == null || !shipsView.IsPlayerShip(ship))
```

In `OnFastForwardChanged`, replace the `awaitedShip = ...` statement by:

```csharp
            // Only a ship of the player is awaited: the others arrive all the time.
            awaitedShip = shipBeforeFastForward != null && shipBeforeFastForward.IsMoving
                && shipsView.IsPlayerShip(shipBeforeFastForward)
                ? shipBeforeFastForward
                : null;
```

In `Start`, after `mapView.Selection.SelectedChanged += OnCitySelected;`:

```csharp
            if (shipsView != null)
            {
                shipsView.Docking.Undocked += OnShipUndocked;
            }
```

In `OnDestroy`, after the `if (mapView != null)` block:

```csharp
            if (shipsView != null)
            {
                shipsView.Docking.Undocked -= OnShipUndocked;
            }
```

Add after `OnCitySelected`:

```csharp
        // A ship that leaves its port by itself is on the map again, like one that is
        // ordered out: its city's panel closes, which leaves the ship alone selected.
        private void OnShipUndocked(Ship ship, CityDefinition city)
        {
            if (ReferenceEquals(shipsView.Selection.Selected, ship) && ReferenceEquals(mapView.Selection.Selected, city))
            {
                mapView.Selection.ClearSelection();
            }
        }
```

Update the class summary: after "right clicks into move orders" add "for the player's ships".

- [ ] **Step 4: `PlayerExpenses`.** In `CountSailors`, iterate `shipsView.PlayerShips` instead of `shipsView.Ships`.

- [ ] **Step 5: `ShipArrivalNotifier`.** At the top of `OnShipArrived`:

```csharp
            // The other merchants' ships come and go without a word.
            if (!shipsView.IsPlayerShip(ship))
            {
                return;
            }
```

Update the class summary to "Tells the player when one of their ships reaches the end of its route, in a port or at sea."

- [ ] **Step 6: `CityInfoPanelController`.** Replace the body of `HoldOfSelectedShipInPort`:

```csharp
            Ship selected = shipsView != null ? shipsView.Selection.Selected : null;

            // Only the player's ships trade here: another merchant's is only looked at.
            return selected != null && shipsView.IsPlayerShip(selected) && listedShips.Contains(selected)
                ? selected.Cargo
                : null;
```

and its summary to "The hold of the selected ship when it is the player's and lies in the shown city's port, otherwise null."

- [ ] **Step 7: Refresh, read the console for compile errors, run all EditMode tests.** Expected: no error, all pass.

- [ ] **Step 8: Commit** — message: `Navires : propriété, nom et teinte par navire, et ordres réservés aux navires du joueur`.

---

### Task 6: The AI merchants in the scene

**Files:**
- Create: `Assets/Scripts/Game/AiMerchantsDefinition.cs`, `Assets/Scripts/Game/AiMerchants.cs`
- Modify: `Assets/Scripts/Editor/WorldMapSetup.cs`
- Generated by the setup tool: `Assets/Data/AI/AiMerchants.asset`, the `AI Merchants` object of `Assets/Scenes/WorldMap.unity`
- Test: `Assets/Tests/EditMode/AiMerchantsDefinitionTests.cs`, `Assets/Tests/EditMode/AiMerchantsContentTests.cs`

**Interfaces:**
- Consumes: `ShipsView.SpawnAiShip`, `Navigation`, `Docking` (Task 5); `PortTravelTable` (Task 2); `TradeRoutePlanner` (Task 3); `AiMerchantFleet<CityDefinition>`, `StartPortDraw` (Task 4).
- Produces:
  - `AiMerchantsDefinition`: `int ShipCount`, `ShipDefinition ShipDefinition`, `Color Tint`, `int Crew`, `float StopoverDays`, `string ShipNameFor(int number)`, `ShipCrew CreateCrew()`.
  - `AiMerchants`: `bool TryGetProfit(Ship ship, out long profit)` — false for a ship that is not an AI merchant's.

- [ ] **Step 1: Write the failing tests.**

`AiMerchantsDefinitionTests.cs`:

```csharp
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class AiMerchantsDefinitionTests
    {
        private AiMerchantsDefinition definition;
        private ShipDefinition shipDefinition;

        [SetUp]
        public void SetUp()
        {
            definition = ScriptableObject.CreateInstance<AiMerchantsDefinition>();
            shipDefinition = ScriptableObject.CreateInstance<ShipDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(shipDefinition);
        }

        private void Set(string property, System.Action<SerializedProperty> write)
        {
            var serialized = new SerializedObject(definition);
            write(serialized.FindProperty(property));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void TheDefaults_AreTheGames()
        {
            Assert.AreEqual(5, definition.ShipCount);
            Assert.AreEqual(14, definition.Crew);
            Assert.AreEqual(1f, definition.StopoverDays);
            Assert.Greater(definition.Tint.b, definition.Tint.r);
        }

        [Test]
        public void ShipNameFor_NumbersTheShips()
        {
            Assert.AreEqual("Marchand indépendant 3", definition.ShipNameFor(3));
        }

        [TestCase("Rival", "Rival 2")]
        [TestCase("", "Marchand indépendant 2")]
        [TestCase("Rival {1}", "Rival {1} 2")]
        [TestCase("Rival {0", "Rival {0 2")]
        public void ShipNameFor_SurvivesAFormatWithoutAUsableNumber(string format, string expected)
        {
            Set("shipNameFormat", property => property.stringValue = format);

            Assert.AreEqual(expected, definition.ShipNameFor(2));
        }

        [Test]
        public void CreateCrew_MansTheShipOfTheDefinition()
        {
            Set("shipDefinition", property => property.objectReferenceValue = shipDefinition);
            var serialized = new SerializedObject(shipDefinition);
            serialized.FindProperty("crewCapacity").intValue = 28;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ShipCrew crew = definition.CreateCrew();

            Assert.AreEqual(28, crew.Capacity);
            Assert.AreEqual(14, crew.Count);
            Assert.AreEqual(1f, crew.SpeedFactor);
        }

        [Test]
        public void CreateCrew_IsCutDownToTheCapacity()
        {
            // A new ship definition has room for 10.
            Set("shipDefinition", property => property.objectReferenceValue = shipDefinition);

            Assert.AreEqual(10, definition.CreateCrew().Count);
        }

        [Test]
        public void CreateCrew_WithoutAShip_IsNull()
        {
            Assert.IsNull(definition.CreateCrew());
        }
    }
}
```

`AiMerchantsContentTests.cs`:

```csharp
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>Checks the AI merchants generated by the world map setup tool.</summary>
    public class AiMerchantsContentTests
    {
        private const string AiMerchantsPath = "Assets/Data/AI/AiMerchants.asset";
        private const string ShipDefinitionPath = "Assets/Data/Ships/MerchantShip.asset";

        [Test]
        public void FiveMerchantShips_SailAtFullSpeed()
        {
            var merchants = AssetDatabase.LoadAssetAtPath<AiMerchantsDefinition>(AiMerchantsPath);
            Assert.IsNotNull(merchants, AiMerchantsPath);

            Assert.AreEqual(5, merchants.ShipCount);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<ShipDefinition>(ShipDefinitionPath), merchants.ShipDefinition);
            Assert.AreEqual(1f, merchants.CreateCrew().SpeedFactor);
        }
    }
}
```

- [ ] **Step 2: Run both fixtures.** Expected: compile error, `AiMerchantsDefinition` does not exist.

- [ ] **Step 3: `AiMerchantsDefinition.cs`.**

```csharp
using System;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Static definition of the merchants that trade on their own: how many ships they
    /// have, what they sail and how they behave. Holds no runtime state.
    /// </summary>
    [CreateAssetMenu(fileName = "AiMerchants", menuName = "Dark Fantasy Merchant/AI Merchants")]
    public sealed class AiMerchantsDefinition : ScriptableObject
    {
        private const string DefaultShipNameFormat = "Marchand indépendant {0}";

        [Tooltip("Ships that trade on their own. Each starts in the port of a city drawn at random.")]
        [SerializeField, Min(0)] private int shipCount = 5;

        [SerializeField] private ShipDefinition shipDefinition;

        [Tooltip("Color of their sprites, which tells them from the player's ships.")]
        [SerializeField] private Color tint = new Color(0.5f, 0.7f, 1f);

        [Tooltip("Sailors aboard each ship, cut down to its capacity. Half the capacity is full speed.")]
        [SerializeField, Min(1)] private int crew = 14;

        [Tooltip("Days a ship stays in a port before it buys and leaves.")]
        [SerializeField, Min(0f)] private float stopoverDays = 1f;

        [Tooltip("Name of a ship; {0} is its number, from 1.")]
        [SerializeField] private string shipNameFormat = DefaultShipNameFormat;

        public int ShipCount => shipCount;

        public ShipDefinition ShipDefinition => shipDefinition;

        public Color Tint => tint;

        public int Crew => crew;

        public float StopoverDays => stopoverDays;

        /// <param name="number">Number of the ship, from 1.</param>
        /// <returns>The name of the ship; a format without a usable number gets it at its end.</returns>
        public string ShipNameFor(int number)
        {
            string format = string.IsNullOrWhiteSpace(shipNameFormat) ? DefaultShipNameFormat : shipNameFormat;

            if (format.Contains("{0}"))
            {
                try
                {
                    return string.Format(format, number);
                }
                catch (FormatException)
                {
                    // Another placeholder that is not filled: the format is written as it is.
                }
            }

            return $"{format} {number}";
        }

        /// <returns>The crew of one ship, or null without a ship definition that has room for a sailor.</returns>
        public ShipCrew CreateCrew()
        {
            if (shipDefinition == null || shipDefinition.CrewCapacity < 1)
            {
                return null;
            }

            int capacity = shipDefinition.CrewCapacity;

            return new ShipCrew(capacity, Mathf.Clamp(crew, 0, capacity));
        }
    }
}
```

- [ ] **Step 4: Run `AiMerchantsDefinitionTests`.** Expected: all pass. (`AiMerchantsContentTests` still fails: the asset does not exist yet.)

- [ ] **Step 5: `AiMerchants.cs`.**

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using DarkFantasyMerchant.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The merchants that trade on their own: puts their ships in the ports of cities
    /// drawn at random, and lets them buy and sell between the cities of the map as the
    /// world's time passes. The player cannot order them.
    /// </summary>
    public sealed class AiMerchants : MonoBehaviour
    {
        // Real milliseconds a frame spends measuring the routes between the cities.
        private const long MeasureBudgetMilliseconds = 4;

        [SerializeField] private AiMerchantsDefinition definition;
        [SerializeField] private ShipsView shipsView;
        [SerializeField] private WorldEconomy worldEconomy;
        [SerializeField] private WorldMapView mapView;
        [SerializeField] private WorldClock worldClock;

        // The cities that have a market, their positions and their markets, in the same order.
        private readonly List<CityDefinition> ports = new List<CityDefinition>();
        private readonly List<Vector2> portPositions = new List<Vector2>();
        private readonly List<CityMarket> markets = new List<CityMarket>();

        private readonly Stopwatch stopwatch = new Stopwatch();

        // Made once the ships view and the economy have started, then filled over a few frames.
        private PortTravelTable travelTable;

        // Made once the travel table is complete.
        private AiMerchantFleet<CityDefinition> fleet;

        /// <returns>False for a ship that is not one of these merchants'.</returns>
        public bool TryGetProfit(Ship ship, out long profit)
        {
            if (fleet == null || !fleet.Contains(ship))
            {
                profit = 0;
                return false;
            }

            profit = fleet.ProfitOf(ship);
            return true;
        }

        private void Start()
        {
            if (definition == null || shipsView == null || worldEconomy == null || mapView == null || worldClock == null)
            {
                Debug.LogError(
                    "AiMerchants needs a definition, a ships view, a world economy, a map view and a world clock.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (fleet != null)
            {
                // The world's time: stopovers are shorter in fast forward, like the trips.
                fleet.Advance(worldClock.Clock.DeltaTime);
                return;
            }

            if (travelTable == null)
            {
                // Both start in their Start, and log why when they cannot.
                if (!shipsView.IsReady || !worldEconomy.IsReady || !worldClock.IsReady)
                {
                    return;
                }

                if (!TryStartMeasuring())
                {
                    enabled = false;
                }

                return;
            }

            stopwatch.Restart();

            while (!travelTable.IsComplete && stopwatch.ElapsedMilliseconds < MeasureBudgetMilliseconds)
            {
                travelTable.MeasureNext();
            }

            if (travelTable.IsComplete)
            {
                StartFleet();
            }
        }

        private bool TryStartMeasuring()
        {
            ShipCrew crew = definition.CreateCrew();

            if (crew == null)
            {
                Debug.LogError(
                    $"AiMerchantsDefinition '{definition.name}' has no ship definition with room for a crew.", definition);
                return false;
            }

            // What a ship sails in a day of the world.
            double worldUnitsPerDay =
                (double)definition.ShipDefinition.Speed * crew.SpeedFactor * worldClock.Calendar.SecondsPerDay;

            // The negated comparison also rejects NaN.
            if (!(worldUnitsPerDay > 0d) || double.IsInfinity(worldUnitsPerDay))
            {
                Debug.LogError(
                    $"AiMerchantsDefinition '{definition.name}' gives ships that do not sail: check the ship's speed and the crew.",
                    definition);
                return false;
            }

            NavigationPathfinder navigation = shipsView.Navigation;
            var anchorages = new List<Vector2>();

            foreach (CityDefinition city in mapView.Cities)
            {
                CityMarket market = worldEconomy.MarketOf(city);

                // A city without a market is not traded with.
                if (market == null)
                {
                    continue;
                }

                ports.Add(city);
                portPositions.Add(city.MapPosition);
                markets.Add(market);
                anchorages.Add(AnchorageOf(city, navigation));
            }

            travelTable = new PortTravelTable(anchorages, navigation, mapView.Projection, worldUnitsPerDay);
            return true;
        }

        // Where a ship lies when it is at the city: what Ship.AnchorageAt gives.
        private static Vector2 AnchorageOf(CityDefinition city, NavigationPathfinder navigation)
        {
            Vector2 position = city.MapPosition;
            var clamped = new Vector2(Mathf.Clamp01(position.x), Mathf.Clamp01(position.y));

            return navigation != null && navigation.TryGetNearestNavigable(clamped, out Vector2 onWater)
                ? onWater
                : clamped;
        }

        private void StartFleet()
        {
            double stopoverDays = Math.Max(0d, definition.StopoverDays);

            try
            {
                var planner = new TradeRoutePlanner(markets, travelTable.Days, stopoverDays);
                fleet = new AiMerchantFleet<CityDefinition>(
                    shipsView.Docking,
                    ports,
                    portPositions,
                    markets,
                    planner,
                    stopoverDays * worldClock.Calendar.SecondsPerDay);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"AiMerchants could not start: {exception.Message}", this);
                enabled = false;
                return;
            }

            var candidates = new List<int>();

            for (int i = 0; i < ports.Count; i++)
            {
                if (travelTable.ReachesAnother(i))
                {
                    candidates.Add(i);
                }
                else
                {
                    // Most likely a mistake of the navigation mask.
                    Debug.LogWarning(
                        $"CityDefinition '{ports[i].name}' reaches no other city by water; AI merchants ignore it.", ports[i]);
                }
            }

            if (candidates.Count == 0)
            {
                Debug.LogWarning("AiMerchants found no city to trade between; no AI ship sails.", this);
                return;
            }

            var startPorts = new List<int>();

            // Not seeded: another draw at every launch.
            StartPortDraw.Draw(definition.ShipCount, candidates, bound => UnityEngine.Random.Range(0, bound), startPorts);

            for (int i = 0; i < startPorts.Count; i++)
            {
                int port = startPorts[i];
                Ship ship = shipsView.SpawnAiShip(
                    definition.ShipDefinition,
                    portPositions[port],
                    definition.CreateCrew(),
                    definition.ShipNameFor(i + 1),
                    definition.Tint);

                // ShipsView already logged why.
                if (ship == null)
                {
                    return;
                }

                fleet.Add(ship, port);
            }
        }
    }
}
```

- [ ] **Step 6: `WorldMapSetup.cs`.**

Constants, after `GoodsFolder`:

```csharp
        private const string AiFolder = "Assets/Data/AI";
        private const string AiMerchantsPath = AiFolder + "/AiMerchants.asset";
```

Add `AiFolder` to the list of `EnsureFolders`. In `Build`, after `FillCityEconomies();` add `CreateAiMerchants(shipDefinition);` and after `AddEconomyToScene(mapSceneHadUnsavedChanges);` add `AddAiMerchantsToScene(mapSceneHadUnsavedChanges);`.

After `CreateExpenses`:

```csharp
        // Like the expenses, new AI merchants are the game's: their defaults are the content.
        private static AiMerchantsDefinition CreateAiMerchants(ShipDefinition shipDefinition)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AiMerchantsDefinition>(AiMerchantsPath);

            if (existing != null)
            {
                return existing;
            }

            // Filled in before the asset is created, so the file is written complete.
            var merchants = ScriptableObject.CreateInstance<AiMerchantsDefinition>();
            var serialized = new SerializedObject(merchants);
            serialized.FindProperty("shipDefinition").objectReferenceValue = shipDefinition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(merchants, AiMerchantsPath);
            return merchants;
        }
```

After `AddEconomyToScene`:

```csharp
        // Like the economy they trade in, the AI merchants are added to a scene built before
        // they existed.
        private static void AddAiMerchantsToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The AI merchants", out Scene scene))
            {
                return;
            }

            var shipsView = FindInScene<ShipsView>(scene);
            var worldEconomy = FindInScene<WorldEconomy>(scene);
            var mapView = FindInScene<WorldMapView>(scene);
            var worldClock = FindInScene<WorldClock>(scene);

            // Their ships are the map's, they trade in its markets and stay in port as the days pass.
            if (shipsView == null || worldEconomy == null || mapView == null || worldClock == null)
            {
                Debug.LogWarning(
                    $"{ScenePath} has no ShipsView, WorldEconomy, WorldMapView or WorldClock; AI merchants not added.");
                return;
            }

            // Loaded only now: opening a scene unloads unreferenced assets.
            var definition = AssetDatabase.LoadAssetAtPath<AiMerchantsDefinition>(AiMerchantsPath);

            if (definition == null)
            {
                throw new FileNotFoundException("The AI merchants could not be loaded.", AiMerchantsPath);
            }

            bool changed = false;
            var aiMerchants = FindInScene<AiMerchants>(scene);

            if (aiMerchants == null)
            {
                var merchantsObject = new GameObject("AI Merchants");
                SceneManager.MoveGameObjectToScene(merchantsObject, scene);

                aiMerchants = merchantsObject.AddComponent<AiMerchants>();
                changed = true;
            }

            // Also for a scene in which one of them was deleted and made again.
            changed |= FillReference(aiMerchants, "definition", definition);
            changed |= FillReference(aiMerchants, "shipsView", shipsView);
            changed |= FillReference(aiMerchants, "worldEconomy", worldEconomy);
            changed |= FillReference(aiMerchants, "mapView", mapView);
            changed |= FillReference(aiMerchants, "worldClock", worldClock);

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The AI merchants");
            }
        }
```

In the class summary, add "AI merchants" to the list of objects an existing scene gains ("..., world economy, AI merchants and debug cities panel objects ...").

- [ ] **Step 7: Generate the content.** Refresh, check the console is free of compile errors, save the open scene if it is dirty, then run `DarkFantasyMerchant.Editor.WorldMapSetup.Build();` through `Unity_RunCommand` (Editor closed: `-batchmode -projectPath . -executeMethod DarkFantasyMerchant.Editor.WorldMapSetup.Build -quit -logFile Logs\setup.log`). Expected: "World map setup finished." in the console; `git status` shows `Assets/Data/AI.meta`, `Assets/Data/AI/AiMerchants.asset` (+ `.meta`) and a modified `Assets/Scenes/WorldMap.unity` with an `AI Merchants` object whose five references are set.

- [ ] **Step 8: Run all EditMode tests.** Expected: all pass, `AiMerchantsContentTests` included.

- [ ] **Step 9: Play check.** Enter Play mode (`UnityEditor.EditorApplication.isPlaying = true;`), wait a few seconds, read the console (no error, no warning about an isolated city unless the mask has one) and capture the game view. Run through `Unity_RunCommand`:

```csharp
var ships = UnityEngine.Object.FindFirstObjectByType<DarkFantasyMerchant.Game.ShipsView>();
var merchants = UnityEngine.Object.FindFirstObjectByType<DarkFantasyMerchant.Game.AiMerchants>();
var clock = UnityEngine.Object.FindFirstObjectByType<DarkFantasyMerchant.Game.WorldClock>();
clock.Clock.SetFastForward(true);
UnityEngine.Debug.Log($"ships: {ships.Ships.Count}, player's: {ships.PlayerShips.Count}");
```

Expected: 6 ships, 1 the player's. After about 20 real seconds of fast forward (some 40 days), log for each ship its name, whether it is docked, its `Cargo.Used` and `merchants.TryGetProfit`: at least one AI ship has a profit above 0, and blue ships are visible at sea in a game view capture. Leave Play mode.

- [ ] **Step 10: Commit** the scripts, tests, asset, folder `.meta` and scene — message: `Marchands IA : cinq navires bleus qui commercent entre les villes`.

---

### Task 7: The debug panel's ships tab

**Files:**
- Rename (with `git mv`, the `.meta` too): `Assets/Scripts/Game/DebugCitiesPanelController.cs` → `DebugPanelController.cs`; `Assets/UI/Debug/DebugCitiesPanel.uxml` → `DebugPanel.uxml`; `Assets/UI/Debug/DebugCitiesPanel.uss` → `DebugPanel.uss`
- Create: `Assets/Scripts/Game/DebugShipsTable.cs`
- Modify: `Assets/Scripts/Editor/WorldMapSetup.cs`, `Assets/Scenes/WorldMap.unity`
- Test: `Assets/Tests/EditMode/DebugShipsTableTests.cs`

**Interfaces:**
- Consumes: `ShipsView.Ships`, `DisplayNameOf`, `IsPlayerShip`, `Docking` (Task 5); `AiMerchants.TryGetProfit` (Task 6); `TreasuryHudController.FormatNumber` / `FormatGold`.
- Produces:
  - `readonly struct DebugShipInfo(string name, bool isPlayer, Ship ship, string portName, string destinationName, long? profit)`.
  - `DebugShipsTable(VisualElement header, VisualElement rowContainer, IReadOnlyList<string> goodNames)`, `void Refresh(IReadOnlyList<DebugShipInfo> ships)`, and the static formats `FormatOwner`, `FormatState`, `FormatPosition`, `FormatCrew`, `FormatSpeed`, `FormatHold`, `FormatProfit`.

- [ ] **Step 1: Write the failing tests.** `DebugShipsTableTests.cs`:

```csharp
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class DebugShipsTableTests
    {
        // Children of a row, in order.
        private const int NameCell = 0;
        private const int OwnerCell = 1;
        private const int StateCell = 2;
        private const int PlaceCell = 3;
        private const int DestinationCell = 4;
        private const int CrewCell = 5;
        private const int SpeedCell = 6;
        private const int HoldCell = 7;
        private const int FirstGoodCell = 8;
        private const int ProfitCell = 10;

        private VisualElement header;
        private VisualElement rows;
        private DebugShipsTable table;
        private List<DebugShipInfo> ships;

        [SetUp]
        public void SetUp()
        {
            header = new VisualElement();
            rows = new VisualElement();
            table = new DebugShipsTable(header, rows, new[] { "Poisson", "Fer" });
            ships = new List<DebugShipInfo>();
        }

        private static Ship CreateShip()
        {
            return new Ship(
                new MapProjection(40f, 2f), new Vector2(0.25f, 0.5f), 4f, new ShipCrew(28, 14), null, new CargoHold(200));
        }

        private string Text(int row, int cell)
        {
            return ((Label)rows[row][cell]).text;
        }

        [Test]
        public void TheHeader_NamesTheColumns_TheGoodsAmongThem()
        {
            Assert.AreEqual(1, header.childCount);
            Assert.AreEqual(ProfitCell + 1, header[0].childCount);
            Assert.AreEqual("Navire", ((Label)header[0][NameCell]).text);
            Assert.AreEqual("Propriétaire", ((Label)header[0][OwnerCell]).text);
            Assert.AreEqual("État", ((Label)header[0][StateCell]).text);
            Assert.AreEqual("Lieu", ((Label)header[0][PlaceCell]).text);
            Assert.AreEqual("Destination", ((Label)header[0][DestinationCell]).text);
            Assert.AreEqual("Équipage", ((Label)header[0][CrewCell]).text);
            Assert.AreEqual("Vitesse", ((Label)header[0][SpeedCell]).text);
            Assert.AreEqual("Cale", ((Label)header[0][HoldCell]).text);
            Assert.AreEqual("Poisson", ((Label)header[0][FirstGoodCell]).text);
            Assert.AreEqual("Fer", ((Label)header[0][FirstGoodCell + 1]).text);
            Assert.AreEqual("Profit", ((Label)header[0][ProfitCell]).text);
        }

        [Test]
        public void AShipIdleAtSea_IsWrittenWithItsPosition()
        {
            ships.Add(new DebugShipInfo("Navire marchand", true, CreateShip(), null, null, null));

            table.Refresh(ships);

            Assert.AreEqual(1, rows.childCount);
            Assert.AreEqual("Navire marchand", Text(0, NameCell));
            Assert.AreEqual("Joueur", Text(0, OwnerCell));
            Assert.AreEqual("À l'arrêt", Text(0, StateCell));
            Assert.AreEqual("0,250 ; 0,500", Text(0, PlaceCell));
            Assert.AreEqual(DebugShipsTable.NoValue, Text(0, DestinationCell));
            Assert.AreEqual("14 / 28", Text(0, CrewCell));
            Assert.AreEqual("100\u00A0%", Text(0, SpeedCell));
            Assert.AreEqual("0 / 200", Text(0, HoldCell));
            Assert.AreEqual("0", Text(0, FirstGoodCell));
            Assert.AreEqual(DebugShipsTable.NoValue, Text(0, ProfitCell));
        }

        [Test]
        public void AShipUnderWay_IsWrittenWithWhereItGoes()
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(0.75f, 0.5f));
            ship.Cargo.TryAdd(1, 60);
            ships.Add(new DebugShipInfo("Marchand indépendant 1", false, ship, null, null, -1250));

            table.Refresh(ships);

            Assert.AreEqual("IA", Text(0, OwnerCell));
            Assert.AreEqual("En mer", Text(0, StateCell));
            Assert.AreEqual("0,750 ; 0,500", Text(0, DestinationCell));
            Assert.AreEqual("60 / 200", Text(0, HoldCell));
            Assert.AreEqual("0", Text(0, FirstGoodCell));
            Assert.AreEqual("60", Text(0, FirstGoodCell + 1));
            Assert.AreEqual("-1\u00A0250 or", Text(0, ProfitCell));
        }

        [Test]
        public void TheCitiesOfAShip_AreNamed()
        {
            Ship sailing = CreateShip();
            sailing.SetDestination(new Vector2(0.75f, 0.5f));
            ships.Add(new DebugShipInfo("A", false, sailing, null, "Sparia", 0));
            ships.Add(new DebugShipInfo("B", false, CreateShip(), "Elforth", null, 0));

            table.Refresh(ships);

            Assert.AreEqual("Sparia", Text(0, DestinationCell));
            Assert.AreEqual("Au port", Text(1, StateCell));
            Assert.AreEqual("Elforth", Text(1, PlaceCell));
            Assert.AreEqual("0 or", Text(1, ProfitCell));
        }

        [Test]
        public void Refresh_FollowsTheShips_AndTheirNumber()
        {
            Ship ship = CreateShip();
            ships.Add(new DebugShipInfo("A", true, ship, null, null, null));
            ships.Add(new DebugShipInfo("B", false, CreateShip(), null, null, 0));
            table.Refresh(ships);
            Assert.AreEqual(2, rows.childCount);

            ship.Cargo.TryAdd(0, 5);
            ships.RemoveAt(1);
            table.Refresh(ships);

            Assert.AreEqual(1, rows.childCount);
            Assert.AreEqual("5", Text(0, FirstGoodCell));

            ships.Add(new DebugShipInfo("C", false, CreateShip(), null, null, 0));
            table.Refresh(ships);

            Assert.AreEqual(2, rows.childCount);
            Assert.AreEqual("C", Text(1, NameCell));
        }

        [Test]
        public void Formats_AreWrittenTheFrenchWay()
        {
            Assert.AreEqual("Joueur", DebugShipsTable.FormatOwner(true));
            Assert.AreEqual("IA", DebugShipsTable.FormatOwner(false));
            Assert.AreEqual("Au port", DebugShipsTable.FormatState(true, false));
            Assert.AreEqual("En mer", DebugShipsTable.FormatState(false, true));
            Assert.AreEqual("À l'arrêt", DebugShipsTable.FormatState(false, false));
            Assert.AreEqual("0,412 ; 0,733", DebugShipsTable.FormatPosition(new Vector2(0.4124f, 0.7331f)));
            Assert.AreEqual("12 / 28", DebugShipsTable.FormatCrew(new ShipCrew(28, 12)));
            Assert.AreEqual("50\u00A0%", DebugShipsTable.FormatSpeed(0.5f));
            Assert.AreEqual("0\u00A0%", DebugShipsTable.FormatSpeed(0f));
            Assert.AreEqual("1\u00A0200 / 2\u00A0000", DebugShipsTable.FormatHold(HoldWith(2000, 1200)));
            Assert.AreEqual("1\u00A0250 or", DebugShipsTable.FormatProfit(1250));
            Assert.AreEqual(DebugShipsTable.NoValue, DebugShipsTable.FormatProfit(null));
        }

        private static CargoHold HoldWith(int capacity, int barrels)
        {
            var hold = new CargoHold(capacity);
            hold.TryAdd(0, barrels);
            return hold;
        }
    }
}
```

- [ ] **Step 2: Run `DebugShipsTableTests`.** Expected: compile error, the types do not exist.

- [ ] **Step 3: `DebugShipsTable.cs`.**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>What the ships debug table writes of one ship.</summary>
    public readonly struct DebugShipInfo
    {
        /// <param name="portName">Name of the city whose port the ship lies in; null at sea.</param>
        /// <param name="destinationName">Name of the city whose port the ship will enter; null for none.</param>
        /// <param name="profit">Cumulated profit of a merchant that trades on its own; null for the player's ships.</param>
        public DebugShipInfo(string name, bool isPlayer, Ship ship, string portName, string destinationName, long? profit)
        {
            Name = name;
            IsPlayer = isPlayer;
            Ship = ship ?? throw new ArgumentNullException(nameof(ship));
            PortName = portName;
            DestinationName = destinationName;
            Profit = profit;
        }

        public string Name { get; }

        public bool IsPlayer { get; }

        public Ship Ship { get; }

        public string PortName { get; }

        public string DestinationName { get; }

        public long? Profit { get; }
    }

    /// <summary>
    /// The table of the ships debug tab: one row per ship, the player's and the others,
    /// with its owner, what it is doing, where it is and goes, its crew, its hold and,
    /// for a merchant that trades on its own, its profit. Rewritten by every refresh.
    /// </summary>
    public sealed class DebugShipsTable
    {
        /// <summary>Written in a cell that has nothing to show.</summary>
        public const string NoValue = "—";

        // The rows and cells of the cities table, with the widths of this one.
        private const string RowClass = "debug-cities__row";
        private const string HeaderRowClass = "debug-cities__row--header";
        private const string CellClass = "debug-cities__cell";
        private const string NameCellClass = "debug-ships__cell--name";
        private const string TextCellClass = "debug-ships__cell--text";
        private const string NumberCellClass = "debug-ships__cell--number";

        // Not the French culture: its separators depend on the platform.
        private static readonly NumberFormatInfo PositionFormat = new NumberFormatInfo { NumberDecimalSeparator = "," };

        private sealed class Row
        {
            public VisualElement Element;
            public Label Name;
            public Label Owner;
            public Label State;
            public Label Place;
            public Label Destination;
            public Label Crew;
            public Label Speed;
            public Label Hold;
            public Label[] Goods;
            public Label Profit;
        }

        private readonly VisualElement rowContainer;
        private readonly int goodCount;
        private readonly List<Row> rows = new List<Row>();

        /// <param name="header">Where the header row is built.</param>
        /// <param name="rowContainer">Where the rows are built, one per ship.</param>
        /// <param name="goodNames">The names of the goods, in the catalogue's order.</param>
        public DebugShipsTable(VisualElement header, VisualElement rowContainer, IReadOnlyList<string> goodNames)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            if (goodNames == null)
            {
                throw new ArgumentNullException(nameof(goodNames));
            }

            this.rowContainer = rowContainer ?? throw new ArgumentNullException(nameof(rowContainer));
            goodCount = goodNames.Count;

            header.Clear();
            rowContainer.Clear();

            Row titles = BuildRow();
            titles.Element.AddToClassList(HeaderRowClass);
            titles.Name.text = "Navire";
            titles.Owner.text = "Propriétaire";
            titles.State.text = "État";
            titles.Place.text = "Lieu";
            titles.Destination.text = "Destination";
            titles.Crew.text = "Équipage";
            titles.Speed.text = "Vitesse";
            titles.Hold.text = "Cale";
            titles.Profit.text = "Profit";

            for (int i = 0; i < goodCount; i++)
            {
                titles.Goods[i].text = goodNames[i];
            }

            header.Add(titles.Element);
        }

        public static string FormatOwner(bool isPlayer)
        {
            return isPlayer ? "Joueur" : "IA";
        }

        /// <summary>What a ship is doing: "Au port", "En mer" under way, "À l'arrêt" idle at sea.</summary>
        public static string FormatState(bool isDocked, bool isMoving)
        {
            if (isDocked)
            {
                return "Au port";
            }

            return isMoving ? "En mer" : "À l'arrêt";
        }

        /// <summary>A normalized map position: "0,412 ; 0,733".</summary>
        public static string FormatPosition(Vector2 position)
        {
            return $"{position.x.ToString("F3", PositionFormat)} ; {position.y.ToString("F3", PositionFormat)}";
        }

        /// <summary>The sailors aboard out of the capacity: "14 / 28".</summary>
        public static string FormatCrew(ShipCrew crew)
        {
            return $"{crew.Count} / {crew.Capacity}";
        }

        /// <summary>The share of its speed a ship sails at: "100 %", with a no-break space.</summary>
        public static string FormatSpeed(float speedFactor)
        {
            return $"{Mathf.RoundToInt(speedFactor * 100f)}\u00A0%";
        }

        /// <summary>The barrels aboard out of the capacity: "60 / 200".</summary>
        public static string FormatHold(CargoHold hold)
        {
            return $"{TreasuryHudController.FormatNumber(hold.Used)} / {TreasuryHudController.FormatNumber(hold.Capacity)}";
        }

        /// <summary>A cumulated profit: "1 250 or", or nothing for a ship that has none.</summary>
        public static string FormatProfit(long? profit)
        {
            return profit.HasValue ? TreasuryHudController.FormatGold(profit.Value) : NoValue;
        }

        /// <summary>Writes a row per ship, in order: rows are added and removed to match.</summary>
        public void Refresh(IReadOnlyList<DebugShipInfo> ships)
        {
            if (ships == null)
            {
                throw new ArgumentNullException(nameof(ships));
            }

            while (rows.Count < ships.Count)
            {
                Row row = BuildRow();
                rows.Add(row);
                rowContainer.Add(row.Element);
            }

            while (rows.Count > ships.Count)
            {
                Row last = rows[rows.Count - 1];
                rowContainer.Remove(last.Element);
                rows.RemoveAt(rows.Count - 1);
            }

            for (int i = 0; i < ships.Count; i++)
            {
                Write(rows[i], ships[i]);
            }
        }

        private void Write(Row row, DebugShipInfo info)
        {
            Ship ship = info.Ship;
            bool isDocked = info.PortName != null;

            row.Name.text = info.Name;
            row.Owner.text = FormatOwner(info.IsPlayer);
            row.State.text = FormatState(isDocked, ship.IsMoving);
            row.Place.text = isDocked ? info.PortName : FormatPosition(ship.Position);
            row.Destination.text = DestinationOf(info);
            row.Crew.text = FormatCrew(ship.Crew);
            row.Speed.text = FormatSpeed(ship.Crew.SpeedFactor);
            row.Hold.text = FormatHold(ship.Cargo);
            row.Profit.text = FormatProfit(info.Profit);

            for (int i = 0; i < goodCount; i++)
            {
                row.Goods[i].text = TreasuryHudController.FormatNumber(ship.Cargo.BarrelsOf(i));
            }
        }

        private static string DestinationOf(DebugShipInfo info)
        {
            if (info.DestinationName != null)
            {
                return info.DestinationName;
            }

            Vector2? destination = info.Ship.Destination;

            return destination.HasValue ? FormatPosition(destination.Value) : NoValue;
        }

        private Row BuildRow()
        {
            var element = new VisualElement();
            element.AddToClassList(RowClass);

            var row = new Row
            {
                Element = element,
                Name = AddLabel(element, NameCellClass),
                Owner = AddLabel(element, TextCellClass),
                State = AddLabel(element, TextCellClass),
                Place = AddLabel(element, TextCellClass),
                Destination = AddLabel(element, TextCellClass),
                Crew = AddLabel(element, NumberCellClass),
                Speed = AddLabel(element, NumberCellClass),
                Hold = AddLabel(element, NumberCellClass),
                Goods = new Label[goodCount],
            };

            for (int i = 0; i < goodCount; i++)
            {
                row.Goods[i] = AddLabel(element, NumberCellClass);
            }

            row.Profit = AddLabel(element, NumberCellClass);
            return row;
        }

        private static Label AddLabel(VisualElement parent, string cellClass)
        {
            var label = new Label();
            label.AddToClassList(CellClass);
            label.AddToClassList(cellClass);
            parent.Add(label);
            return label;
        }
    }
}
```

- [ ] **Step 4: Run `DebugShipsTableTests`.** Expected: all pass.

- [ ] **Step 5: Rename the panel.**

```bash
git mv Assets/Scripts/Game/DebugCitiesPanelController.cs Assets/Scripts/Game/DebugPanelController.cs
git mv Assets/Scripts/Game/DebugCitiesPanelController.cs.meta Assets/Scripts/Game/DebugPanelController.cs.meta
git mv Assets/UI/Debug/DebugCitiesPanel.uxml Assets/UI/Debug/DebugPanel.uxml
git mv Assets/UI/Debug/DebugCitiesPanel.uxml.meta Assets/UI/Debug/DebugPanel.uxml.meta
git mv Assets/UI/Debug/DebugCitiesPanel.uss Assets/UI/Debug/DebugPanel.uss
git mv Assets/UI/Debug/DebugCitiesPanel.uss.meta Assets/UI/Debug/DebugPanel.uss.meta
```

In `Assets/Scenes/WorldMap.unity`, replace the one line `m_EditorClassIdentifier: DarkFantasyMerchant.Game::DarkFantasyMerchant.Game.DebugCitiesPanelController` by the same with `DebugPanelController`, and `m_Name: Debug Cities Panel` by `m_Name: Debug Panel`. Do this while the scene has no unsaved change in the Editor, then let the Editor reload it.

- [ ] **Step 6: `DebugPanel.uxml`.**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="project://database/Assets/UI/WorldMap/InfoPanel.uss" />
    <Style src="DebugPanel.uss" />
    <ui:VisualElement class="debug-panel-root" picking-mode="Ignore">
        <ui:VisualElement name="debug-panel" class="info-panel debug-panel">
            <ui:VisualElement class="info-panel__header">
                <ui:Label text="Débogage" class="info-panel__name" />
                <ui:VisualElement class="debug-panel__tabs">
                    <ui:Button name="cities-tab" text="Villes" class="debug-panel__tab" />
                    <ui:Button name="ships-tab" text="Navires" class="debug-panel__tab" />
                </ui:VisualElement>
                <ui:Button name="close-button" text="X" class="info-panel__close" />
            </ui:VisualElement>
            <ui:VisualElement name="cities-page" class="debug-panel__page">
                <ui:Label text="Tonneaux par semaine : valeur théorique, puis valeur réelle de la dernière semaine entre parenthèses." class="debug-cities__legend" />
                <ui:ScrollView mode="VerticalAndHorizontal" class="debug-cities__scroll">
                    <ui:VisualElement name="debug-cities-header" />
                    <ui:VisualElement name="debug-cities-rows" />
                    <ui:VisualElement name="debug-cities-totals" />
                </ui:ScrollView>
            </ui:VisualElement>
            <ui:VisualElement name="ships-page" class="debug-panel__page">
                <ui:Label text="Tous les navires de la carte. Le profit est celui des marchands IA depuis le début de la partie." class="debug-cities__legend" />
                <ui:ScrollView mode="VerticalAndHorizontal" class="debug-cities__scroll">
                    <ui:VisualElement name="debug-ships-header" />
                    <ui:VisualElement name="debug-ships-rows" />
                </ui:ScrollView>
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 7: `DebugPanel.uss`.** Rename `.debug-cities-root` to `.debug-panel-root` and `.debug-cities` (the rule with `width: auto`) to `.debug-panel`; keep every `.debug-cities__*` rule. Change the comment above the first rule's sibling to "A panel as wide as its table, scrolled when it does not fit." and append:

```css
/* Between the title and the close button. */
.debug-panel__tabs {
    flex-direction: row;
    flex-grow: 1;
    margin-left: 24px;
}

.debug-panel__tab {
    height: 28px;
    margin: 0 4px 0 0;
    padding: 0 12px;
    color: rgb(190, 164, 120);
}

.debug-panel__tab--selected {
    color: rgb(240, 226, 196);
    border-color: rgb(190, 164, 120);
    -unity-font-style: bold;
}

/* Shrinks with the panel, so that its table scrolls. */
.debug-panel__page {
    flex-shrink: 1;
}

.debug-ships__cell--name {
    width: 210px;
    -unity-text-align: middle-left;
}

.debug-ships__cell--text {
    width: 130px;
    -unity-text-align: middle-left;
}

.debug-ships__cell--number {
    width: 100px;
}
```

- [ ] **Step 8: `DebugPanelController.cs`.** Replace the whole file:

```csharp
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The debug tools' panel, with one tab per tool: the cities, with their population
    /// and the weekly production, consumption and net gain of every good, and the ships,
    /// the player's and the others, with what they are doing and carrying. Opened and
    /// closed with the debug key of the map's input; exists only in the Editor and in
    /// development builds. Reads the simulation and changes nothing.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DebugPanelController : MonoBehaviour
    {
        private const string SelectedTabClass = "debug-panel__tab--selected";

        [SerializeField] private WorldEconomy worldEconomy;
        [SerializeField] private WorldMapView mapView;
        [SerializeField] private WorldMapInput input;

        [Tooltip("Optional. Without it, the panel has no ships tab.")]
        [SerializeField] private ShipsView shipsView;

        [Tooltip("Optional. Without it, the ships tab shows no profit.")]
        [SerializeField] private AiMerchants aiMerchants;

        private VisualElement panel;
        private VisualElement citiesPage;
        private VisualElement shipsPage;
        private VisualElement citiesHeader;
        private VisualElement citiesRows;
        private VisualElement citiesTotals;
        private VisualElement shipsHeader;
        private VisualElement shipsRows;
        private Button citiesTab;
        private Button shipsTab;
        private Button closeButton;

        // Made the first time the panel is opened: the world's economy starts after this panel is enabled.
        private DebugCitiesTable citiesTable;
        private DebugShipsTable shipsTable;

        // Reused by every refresh of the ships tab.
        private readonly List<DebugShipInfo> shipInfos = new List<DebugShipInfo>();

        private bool isBound;
        private bool isOpen;

        // Kept while the panel is closed: it opens on the tab it was last on.
        private bool showsShips;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            if (root == null)
            {
                Debug.LogError("DebugPanelController needs a UIDocument with a source asset.", this);
                return;
            }

            // Only the panel itself should block the pointer, not the full-screen root.
            root.pickingMode = PickingMode.Ignore;

            // Covers the screen whatever else shares the panel. Set before anything can
            // fail: a root left in the flow would push the city panel off the center.
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;

            // A debug tool is not a part of the game the player gets.
            if (!Debug.isDebugBuild)
            {
                root.style.display = DisplayStyle.None;
                return;
            }

            if (worldEconomy == null || mapView == null || input == null)
            {
                Debug.LogError("DebugPanelController needs a world economy, a map view and an input component.", this);
                root.style.display = DisplayStyle.None;
                return;
            }

            panel = root.Q<VisualElement>("debug-panel");
            citiesPage = root.Q<VisualElement>("cities-page");
            shipsPage = root.Q<VisualElement>("ships-page");
            citiesHeader = root.Q<VisualElement>("debug-cities-header");
            citiesRows = root.Q<VisualElement>("debug-cities-rows");
            citiesTotals = root.Q<VisualElement>("debug-cities-totals");
            shipsHeader = root.Q<VisualElement>("debug-ships-header");
            shipsRows = root.Q<VisualElement>("debug-ships-rows");
            citiesTab = root.Q<Button>("cities-tab");
            shipsTab = root.Q<Button>("ships-tab");
            closeButton = root.Q<Button>("close-button");

            if (panel == null || citiesPage == null || shipsPage == null || citiesHeader == null || citiesRows == null
                || citiesTotals == null || shipsHeader == null || shipsRows == null || citiesTab == null
                || shipsTab == null || closeButton == null)
            {
                Debug.LogError("DebugPanel.uxml is missing an expected element.", this);
                root.style.display = DisplayStyle.None;
                return;
            }

            // A document that is enabled again has a new tree: the tables are rebuilt in it.
            citiesTable = null;
            shipsTable = null;
            isOpen = false;
            panel.style.display = DisplayStyle.None;

            // Without ships there is nothing to list.
            if (shipsView == null)
            {
                showsShips = false;
                shipsTab.style.display = DisplayStyle.None;
            }

            // The buttons are clicked, not navigated: the keyboard pans the map.
            closeButton.focusable = false;
            citiesTab.focusable = false;
            shipsTab.focusable = false;

            closeButton.clicked += Close;
            citiesTab.clicked += ShowCities;
            shipsTab.clicked += ShowShips;
            input.DebugToggled += Toggle;
            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            closeButton.clicked -= Close;
            citiesTab.clicked -= ShowCities;
            shipsTab.clicked -= ShowShips;
            input.DebugToggled -= Toggle;

            // Stops listening to the markets.
            citiesTable?.Hide();

            isBound = false;
        }

        // Ships sail every frame: their tab is rewritten as often, while it is shown.
        private void Update()
        {
            if (isBound && isOpen && showsShips)
            {
                RefreshShips();
            }
        }

        private void Toggle()
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        private void Open()
        {
            if (citiesTable == null && !TryBuildTables())
            {
                return;
            }

            isOpen = true;
            panel.style.display = DisplayStyle.Flex;
            ShowTab(showsShips);
        }

        private void Close()
        {
            citiesTable?.Hide();
            panel.style.display = DisplayStyle.None;
            isOpen = false;
        }

        private void ShowCities()
        {
            ShowTab(false);
        }

        private void ShowShips()
        {
            ShowTab(true);
        }

        private void ShowTab(bool ships)
        {
            showsShips = ships;
            citiesPage.style.display = ships ? DisplayStyle.None : DisplayStyle.Flex;
            shipsPage.style.display = ships ? DisplayStyle.Flex : DisplayStyle.None;
            citiesTab.EnableInClassList(SelectedTabClass, !ships);
            shipsTab.EnableInClassList(SelectedTabClass, ships);

            // A table that is not shown costs nothing: the cities' only follows the markets on its tab.
            if (ships)
            {
                citiesTable.Hide();
                RefreshShips();
            }
            else
            {
                citiesTable.Show();
            }
        }

        private void RefreshShips()
        {
            shipInfos.Clear();

            // The player's ship is the first of the map's.
            foreach (Ship ship in shipsView.Ships)
            {
                CityDefinition port = shipsView.Docking.PortOf(ship);
                CityDefinition destination = shipsView.Docking.DestinationPortOf(ship);
                long? profit = aiMerchants != null && aiMerchants.TryGetProfit(ship, out long gold) ? gold : (long?)null;

                shipInfos.Add(new DebugShipInfo(
                    shipsView.DisplayNameOf(ship),
                    shipsView.IsPlayerShip(ship),
                    ship,
                    port != null ? port.DisplayName : null,
                    destination != null ? destination.DisplayName : null,
                    profit));
            }

            shipsTable.Refresh(shipInfos);
        }

        private bool TryBuildTables()
        {
            if (!worldEconomy.IsReady)
            {
                // WorldEconomy already logged why cities have no market.
                return false;
            }

            var goodNames = new List<string>();

            foreach (GoodDefinition good in worldEconomy.Economy.Goods)
            {
                goodNames.Add(good.DisplayName);
            }

            citiesTable = new DebugCitiesTable(citiesHeader, citiesRows, citiesTotals, goodNames);

            foreach (CityDefinition city in mapView.Cities)
            {
                CityMarket market = worldEconomy.MarketOf(city);

                if (market != null)
                {
                    citiesTable.AddCity(city.DisplayName, city.Population, market);
                }
            }

            shipsTable = new DebugShipsTable(shipsHeader, shipsRows, goodNames);
            return true;
        }
    }
}
```

- [ ] **Step 9: `WorldMapSetup.cs`.** Rename `DebugCitiesPanelTemplatePath` to `DebugPanelTemplatePath` with the value `DebugUiFolder + "/DebugPanel.uxml"`. In `AddDebugToolsToScene`: replace `DebugCitiesPanelController` by `DebugPanelController` (two places), `"Debug Cities Panel"` by `"Debug Panel"`, the words "debug cities panel" by "debug panel" in the comment, the three messages and `TryOpenMapScene`'s argument, and add after the three `FillReference` lines:

```csharp
            // The ships tab lists the ships, with the profit of the merchants that trade on their own.
            changed |= FillReference(panel, "shipsView", FindInScene<ShipsView>(scene));
            changed |= FillReference(panel, "aiMerchants", FindInScene<AiMerchants>(scene));
```

In the class summary, "debug cities panel" becomes "debug panel".

- [ ] **Step 10: Wire the scene.** Refresh, check the console for compile errors, then run `DarkFantasyMerchant.Editor.WorldMapSetup.Build();` as in Task 6 Step 7. Expected: the `Debug Panel` object's `shipsView` and `aiMerchants` references are set in `WorldMap.unity`, and its `UIDocument` still points at the renamed UXML (same GUID).

- [ ] **Step 11: Run all EditMode tests.** Expected: all pass.

- [ ] **Step 12: Play check.** In Play mode, open the panel (through `Unity_RunCommand`, invoke the input's event by simulating F1 is not possible: call the private `Toggle` by reflection on the `DebugPanelController`), capture the game view on each tab. Expected: "Villes" shows the table as before; "Navires" lists 6 rows, "Navire marchand / Joueur" first, five "Marchand indépendant n / IA", with states and holds that change while fast forward runs; no error in the console.

- [ ] **Step 13: Commit** — message: `Outils de débogage : onglets Villes et Navires, liste de tous les navires de la carte`.

---

### Task 8: Play through, document, review

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Play through the spec's manual checks** in Play mode, with game view captures and `Unity_RunCommand` to drive what needs input:
  - AI ships are tinted blue, the player's is not.
  - Selecting an AI ship (`shipsView.Selection.Select(ship)`) shows the ship panel with its name, crew and hold, and its route on the map.
  - `WorldMapInteraction` ignores an order for it: after selecting an AI ship under way, note `ship.Destination`, invoke the private `OnCommanded(new Vector2(Screen.width / 2f, Screen.height / 2f))` by reflection, and check the destination is unchanged.
  - Selecting a city where an AI ship lies lists it; selecting it there shows no hold column (`market--trading` class absent on the market section).
  - After a week of fast forward the player's treasury has paid 120 gold of wages, not more.
  - No arrival notification names a "Marchand indépendant".

- [ ] **Step 2: Update `CLAUDE.md`.**
  - Project state: after the sentence about the debug tool, say that five AI merchant ships trade between the cities on their own, and that the debug tool has a second tab that lists the ships.
  - World map, the `WorldMapSetup` bullet: add the AI merchants asset to what is created when missing, `AI Merchants` to the objects added to an existing scene, rename `Debug Cities Panel` to `Debug Panel`, and add the references (`definition`, `shipsView`, `worldEconomy`, `mapView` and `worldClock` of `AiMerchants`; `shipsView` and `aiMerchants` of `DebugPanelController`).
  - Ships: `ShipsView` owns the player's ships and the others (`PlayerShips`, `IsPlayerShip`, `SpawnAiShip`), each with its own name and an optional tint; only the player's take orders, are awaited by fast forward, are paid wages and are announced; a ship that leaves its port by itself closes its city's panel when it was selected there.
  - Economy: `Trade` has overloads without a treasury; the city panel trades only for a ship of the player.
  - A new "AI merchants" section after Economy, covering: `AiMerchantsDefinition` and its defaults; `AiMerchants` (travel table measured over a few frames, start cities drawn among those that reach another, isolated cities warned about); `AiMerchantFleet<TPort>` (sells the whole hold on docking, a stopover counted in the world's time, then a trade, a relocation or another stopover; unlimited gold, profit counted); `TradeRoutePlanner` (barrel by barrel with the real prices, profit per day of trip and stopover, cargo under way counted as delivered, relocation); `PortTravelTable` / `SeaRoutes`; the expected idle first days.
  - Debug tools: the panel is `DebugPanelController` / `DebugPanel.uxml` with the tabs "Villes" and "Navires"; `DebugShipsTable` and its columns; rewritten every frame while its tab is shown; opens on the last tab.

- [ ] **Step 3: Run all EditMode tests one last time.** Expected: all pass.

- [ ] **Step 4: Request a code review from a subagent** over `git diff e75e7f8..HEAD -- Assets CLAUDE.md` plus the uncommitted `CLAUDE.md`, against the spec and this plan; fix what it finds that is real, re-run the tests.

- [ ] **Step 5: Commit** — message: `CLAUDE.md : les marchands IA et l'onglet des navires`.
