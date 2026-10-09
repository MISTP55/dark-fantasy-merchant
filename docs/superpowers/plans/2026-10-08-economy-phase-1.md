# Economy, Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cities hold stocks of ten goods that they consume and partly produce every day; the player buys them into the limited hold of a ship in port and sells them in another city, at prices that follow the stocks.

**Architecture:** `Core` gains four plain classes: `CargoHold` (a ship's barrels), `MarketPricing` (the price curve and the spread), `CityMarket` (the stocks of one city and their daily step) and `Trade` (buying and selling between a market, a hold and the treasury). Goods are indices in `Core`. In `Game`, `GoodDefinition` and `EconomyDefinition` assets hold the catalogue and the settings, `CityDefinition` gains a population and its productions, and a `WorldEconomy` MonoBehaviour owns one market per city and steps them on `GameClock.DayStarted`. The city panel shows the market through a `CityMarketSection` class; the ship panel shows the hold.

**Tech Stack:** Unity 6000.6.4f1, C#, UI Toolkit (UXML + USS), Unity Test Framework (NUnit, EditMode).

**Spec:** `docs/superpowers/specs/2026-10-08-economy-phase-1-design.md`

## Global Constraints

- All code, comments, log messages, asset and folder names are in English. Everything the player reads is in French, written directly in the UXML, the controllers and the assets.
- Numbers shown to the player use a no-break space (`\u00A0`) as thousands separator.
- `Core` contains no `MonoBehaviour` and nothing that needs a scene, and does not reference `Game`: goods are `int` indices into the catalogue there.
- Runtime UI is UI Toolkit only. No uGUI.
- ScriptableObjects are never written while the game runs.
- Unit of goods: the barrel ("tonneau"). Merchant ship hold: 200 barrels.
- Goods, in catalogue order, with base price and consumption per 1 000 inhabitants per day: Blé 10 / 2.0, Poisson 12 / 1.5, Bois 14 / 1.0, Bière 15 / 1.5, Sel 18 / 0.5, Laine 30 / 0.5, Cuir 40 / 0.3, Fer 50 / 0.4, Vin 60 / 0.6, Épices 150 / 0.1.
- Settings: target stock 30 days; efficient production rate 1.5 with a ceiling of 60 days; inefficient 1.1 with a ceiling of 36 days; price multipliers 2.5 (empty), 1 (target), 0.5 (twice the target and above); buy margin 5 %, sell margin 5 %.
- The existing tests must pass **unchanged**, except where a task says to add a method to an existing fixture.
- Never hand-write `.meta` files. Unity generates them on import; commit them together with their file.
- Never edit the generated `.sln` / `.csproj`. Exclude `Library/` from searches.
- Match the surrounding code: Allman braces, a blank line before `return` / `if` blocks as in the existing files, XML `<summary>` on public types, comments only where the reason is not obvious.
- Differences from the spec's wording, intentional:
  - `CargoHold` has `TryAdd` / `TryRemove` (returning false when refused) instead of `Add` / `Remove`, and is not sized by the number of goods: a ship needs no reference to the catalogue.
  - `CityMarket.Take` / `Put` move several barrels in one call; `Trade` works out how many can be traded first, then moves them at once, which is what raises `Changed` once per call.
  - `Ship` takes its hold as a last optional constructor parameter; without it the ship has a hold of capacity 0. This keeps the forty existing `new Ship(...)` calls of the tests unchanged.
  - `CityMarketSection` is given the names of the goods, not the `EconomyDefinition`.
- Work on a new branch `economy-phase-1`, created from `main` after the spec and this plan are committed there.
- Commit messages are in French (matching the repository history) and end with:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`

## Commands

Batch mode requires the project to be **closed** in the Unity Editor. Check first:

```powershell
Get-Process Unity -ErrorAction SilentlyContinue
```

If Unity is running with this project, use the `unity-mcp` tools instead: `Unity_RunCommand` to trigger a refresh/compile or run tests, then `Unity_GetConsoleLogs` to read errors.

**Run EditMode tests** (all, or add `-testFilter`). `Unity.exe` is a GUI executable, so it must be started with `-Wait` or PowerShell returns immediately. Do **not** add `-nographics`: existing tests need a graphics device.

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe"
Remove-Item Logs\TestResults.xml -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList '-batchmode','-projectPath','.','-runTests','-testPlatform','EditMode','-testResults','Logs\TestResults.xml','-logFile','Logs\test.log'
"exit code: $($p.ExitCode)"
Select-String -Path Logs\TestResults.xml -Pattern '<test-run ' | ForEach-Object { $_.Line }
```

To run one fixture, append `'-testFilter','DarkFantasyMerchant.Tests.EditMode.CargoHoldTests'` to the argument list.

Reading the result:

- Exit code `0` and `result="Passed"` in the `<test-run` line: all tests passed.
- Exit code `2`: at least one test failed. Details: `Select-String -Path Logs\TestResults.xml -Pattern 'result="Failed"' -Context 0,6`.
- Exit code `1` and no `TestResults.xml`: compilation failed. Details: `Select-String -Path Logs\test.log -Pattern 'error CS'`.

A test file that references a type or member that does not exist yet fails the **whole compilation** (exit code `1`), not just that test. That is the expected "red" for the first step of each task.

A run takes one to three minutes. The first run after adding files also generates their `.meta` files; add them to the commit.

**Run the world map setup tool** (Task 8):

```powershell
$p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList '-batchmode','-projectPath','.','-executeMethod','DarkFantasyMerchant.Editor.WorldMapSetup.Build','-quit','-logFile','Logs\setup.log'
"exit code: $($p.ExitCode)"
Select-String -Path Logs\setup.log -Pattern 'World map setup finished|Exception|error CS'
```

## Review Focus

Conditions the spec implies but does not spell out, most likely first.

1. **The player is in debt when they trade** (the weekly wages took the gold below zero). Nothing can be bought, the [+] buttons are disabled, and selling still works and brings the gold back up. Tests in Task 4 (`Buy_InDebt_BuysNothing`, `Sell_InDebt_StillDeposits`) and Task 6 (`InDebt_DisablesBuying_NotSelling`).
2. **A Ctrl+click asks for 100 barrels where fewer can be traded** (stock, hold or gold runs out first). It trades what is possible, not nothing. Tests in Task 4 (`Buy_StopsAtTheStock`, `Buy_StopsAtTheHold`, `Buy_StopsAtTheGold`, `Sell_StopsAtTheBarrelsAboard`).
3. **A city holds less than one barrel of a good** (a village consumes a fraction of a barrel a day). The panel shows a stock of 0 and no buy price, and nothing can be bought. Tests in Task 3 (`AvailableOf_IsTheWholeBarrels`) and Task 6 (`AGoodOutOfStock_HasNoBuyPrice`).
4. **The ship whose hold is shown stops being the one traded with** (another ship of the port is selected, the ship is deselected, the city panel closes). The hold column and the buttons disappear and the section no longer listens to that hold. Test in Task 6 (`Show_WithoutAHold_StopsTrading`).
5. **An asset edited into something unusable** (an economy with no good, a good listed twice or an empty entry; a city that lists a good outside the catalogue, an empty entry, or the same good as efficient and inefficient). The game says so in the console and keeps running: an unusable economy starts no market, a doubtful city gets a market anyway. Tests in Task 5 (`TryValidate_…`, `AGoodInBothLists_IsEfficient`, `AnEmptyEntry_IsIgnored`); `WorldEconomy` logs (Task 5).

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/Core/CargoHold.cs` | Create | The barrels aboard one ship, out of its capacity. |
| `Assets/Scripts/Core/Ship.cs` | Modify | Holds a `CargoHold`. |
| `Assets/Scripts/Core/MarketPricing.cs` | Create | Price multiplier from a stock, buy and sell prices. |
| `Assets/Scripts/Core/MarketGood.cs` | Create | The figures of one good in one city. |
| `Assets/Scripts/Core/CityMarket.cs` | Create | The stocks of one city, their daily step, their prices. |
| `Assets/Scripts/Core/TradeResult.cs` | Create | Barrels traded and gold exchanged. |
| `Assets/Scripts/Core/Trade.cs` | Create | Buying and selling. |
| `Assets/Scripts/Game/GoodDefinition.cs` | Create | A good: name, base price, consumption. |
| `Assets/Scripts/Game/GoodProduction.cs` | Create | None, inefficient, efficient. |
| `Assets/Scripts/Game/EconomyDefinition.cs` | Create | The catalogue and the settings; creates markets. |
| `Assets/Scripts/Game/CityDefinition.cs` | Modify | Population and productions. |
| `Assets/Scripts/Game/ShipDefinition.cs` | Modify | Cargo capacity. |
| `Assets/Scripts/Game/ShipsView.cs` | Modify | Gives the player ship its hold. |
| `Assets/Scripts/Game/WorldEconomy.cs` | Create | Owns the markets, steps them every day. |
| `Assets/Scripts/Game/TreasuryHudController.cs` | Modify | `FormatNumber`. |
| `Assets/Scripts/Game/CityMarketSection.cs` | Create | The market rows of the city panel. |
| `Assets/Scripts/Game/CityInfoPanelController.cs` | Modify | Population; feeds the market section. |
| `Assets/Scripts/Game/ShipInfoPanelController.cs` | Modify | The hold of the selected ship. |
| `Assets/UI/WorldMap/CityInfoPanel.uxml`, `.uss` | Modify | Population, market section. |
| `Assets/UI/WorldMap/ShipInfoPanel.uxml`, `.uss` | Modify | Hold. |
| `Assets/Scripts/Editor/WorldMapSetup.cs` | Modify | Goods, economy, city economies, scene object, references. |
| `Assets/Data/Goods/*.asset`, `Assets/Data/Economy/Economy.asset` | Generated | By the setup tool. |
| `Assets/Data/Cities/*.asset`, `Assets/Scenes/WorldMap.unity` | Generated | Updated by the setup tool. |
| `Assets/Tests/EditMode/CargoHoldTests.cs` | Create | |
| `Assets/Tests/EditMode/MarketPricingTests.cs` | Create | |
| `Assets/Tests/EditMode/CityMarketTests.cs` | Create | |
| `Assets/Tests/EditMode/TradeTests.cs` | Create | |
| `Assets/Tests/EditMode/TestEconomy.cs` | Create | In-memory goods, cities and economies for tests. |
| `Assets/Tests/EditMode/EconomyDefinitionTests.cs` | Create | |
| `Assets/Tests/EditMode/CityMarketSectionTests.cs` | Create | |
| `Assets/Tests/EditMode/EconomyContentTests.cs` | Create | The generated assets. |
| `Assets/Tests/EditMode/ShipTests.cs`, `ShipDefinitionTests.cs`, `TreasuryHudControllerTests.cs`, `ShipInfoPanelControllerTests.cs` | Modify | One or two added methods each. |
| `CLAUDE.md` | Modify | Economy section, project state, setup tool. |

---

### Task 1: `CargoHold` and `Ship.Cargo`

**Files:**
- Create: `Assets/Scripts/Core/CargoHold.cs`
- Modify: `Assets/Scripts/Core/Ship.cs`
- Test: `Assets/Tests/EditMode/CargoHoldTests.cs`, `Assets/Tests/EditMode/ShipTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `CargoHold(int capacity)`; `int Capacity`, `int Used`, `int Free`; `int BarrelsOf(int good)`; `bool TryAdd(int good, int barrels)`; `bool TryRemove(int good, int barrels)`; `event Action Changed`.
  - `Ship(MapProjection projection, Vector2 position, float speed, ShipCrew crew, NavigationPathfinder navigation = null, CargoHold cargo = null)`; `CargoHold Ship.Cargo` (never null).

- [ ] **Step 0: Branch**

The spec and this plan must be committed on `main` first (ask the user if they are not). Then:

```powershell
git switch -c economy-phase-1
```

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/CargoHoldTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CargoHoldTests
    {
        private const int Wine = 8;
        private const int Wheat = 0;

        [Test]
        public void StartsEmpty()
        {
            var hold = new CargoHold(200);

            Assert.AreEqual(200, hold.Capacity);
            Assert.AreEqual(0, hold.Used);
            Assert.AreEqual(200, hold.Free);
            Assert.AreEqual(0, hold.BarrelsOf(Wine));
        }

        [Test]
        public void RejectsANegativeCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CargoHold(-1));
        }

        [Test]
        public void TryAdd_StoresBarrelsOfEachGood()
        {
            var hold = new CargoHold(200);

            Assert.IsTrue(hold.TryAdd(Wine, 20));
            Assert.IsTrue(hold.TryAdd(Wheat, 40));

            Assert.AreEqual(20, hold.BarrelsOf(Wine));
            Assert.AreEqual(40, hold.BarrelsOf(Wheat));
            Assert.AreEqual(60, hold.Used);
            Assert.AreEqual(140, hold.Free);
        }

        [Test]
        public void TryAdd_BeyondTheFreeRoom_IsRefusedWhole()
        {
            var hold = new CargoHold(50);
            hold.TryAdd(Wine, 30);

            Assert.IsFalse(hold.TryAdd(Wheat, 21));

            Assert.AreEqual(0, hold.BarrelsOf(Wheat));
            Assert.AreEqual(30, hold.Used);
        }

        [Test]
        public void TryAdd_FillsTheHoldExactly()
        {
            var hold = new CargoHold(50);

            Assert.IsTrue(hold.TryAdd(Wine, 50));
            Assert.AreEqual(0, hold.Free);
        }

        [Test]
        public void TryRemove_TakesBarrelsOut()
        {
            var hold = new CargoHold(200);
            hold.TryAdd(Wine, 20);

            Assert.IsTrue(hold.TryRemove(Wine, 5));

            Assert.AreEqual(15, hold.BarrelsOf(Wine));
            Assert.AreEqual(15, hold.Used);
        }

        [Test]
        public void TryRemove_MoreThanAboard_IsRefusedWhole()
        {
            var hold = new CargoHold(200);
            hold.TryAdd(Wine, 20);

            Assert.IsFalse(hold.TryRemove(Wine, 21));
            Assert.IsFalse(hold.TryRemove(Wheat, 1));

            Assert.AreEqual(20, hold.BarrelsOf(Wine));
        }

        [Test]
        public void Changed_IsRaisedOncePerMove_AndNotByARefusal()
        {
            var hold = new CargoHold(50);
            int changes = 0;
            hold.Changed += () => changes++;

            hold.TryAdd(Wine, 30);
            hold.TryRemove(Wine, 10);
            Assert.AreEqual(2, changes);

            hold.TryAdd(Wine, 100);
            hold.TryRemove(Wine, 100);
            Assert.AreEqual(2, changes);
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void RejectsAQuantityThatIsNotPositive(int barrels)
        {
            var hold = new CargoHold(50);

            Assert.Throws<ArgumentOutOfRangeException>(() => hold.TryAdd(Wine, barrels));
            Assert.Throws<ArgumentOutOfRangeException>(() => hold.TryRemove(Wine, barrels));
        }

        [Test]
        public void RejectsANegativeGood()
        {
            var hold = new CargoHold(50);

            Assert.Throws<ArgumentOutOfRangeException>(() => hold.BarrelsOf(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => hold.TryAdd(-1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => hold.TryRemove(-1, 1));
        }

        [Test]
        public void AHoldWithoutRoom_TakesNothing()
        {
            var hold = new CargoHold(0);

            Assert.IsFalse(hold.TryAdd(Wine, 1));
        }
    }
}
```

Add these two methods to the existing `ShipTests` class in `Assets/Tests/EditMode/ShipTests.cs` (it already has a `projection` field and `Center`, `Speed` constants, used by its other tests):

```csharp
        [Test]
        public void HoldsTheCargoHoldItIsGiven()
        {
            var cargo = new CargoHold(200);

            Assert.AreSame(cargo, new Ship(projection, Center, Speed, TestCrew.Full, null, cargo).Cargo);
        }

        [Test]
        public void WithoutACargoHold_HasOneWithoutRoom()
        {
            var ship = new Ship(projection, Center, Speed, TestCrew.Full);

            Assert.IsNotNull(ship.Cargo);
            Assert.AreEqual(0, ship.Cargo.Capacity);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests (see Commands). Expected: exit code `1`, `error CS0246` for `CargoHold` in `Logs\test.log`.

- [ ] **Step 3: Write `CargoHold`**

Create `Assets/Scripts/Core/CargoHold.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The barrels of goods aboard one ship, out of those it has room for. Goods are
    /// indices into the catalogue; every barrel takes the same room whatever it holds.
    /// </summary>
    public sealed class CargoHold
    {
        // Barrels of each good, by index; grown when a good is first loaded.
        private readonly List<int> barrelsByGood = new List<int>();

        /// <param name="capacity">Barrels the hold has room for; zero for a ship that carries nothing.</param>
        public CargoHold(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Capacity = capacity;
        }

        public int Capacity { get; }

        /// <summary>Barrels aboard, all goods together.</summary>
        public int Used { get; private set; }

        public int Free => Capacity - Used;

        /// <summary>Raised when barrels were loaded or unloaded, not by a refusal.</summary>
        public event Action Changed;

        public int BarrelsOf(int good)
        {
            RequireGood(good);

            return good < barrelsByGood.Count ? barrelsByGood[good] : 0;
        }

        /// <returns>False when the hold has no room for all of them; nothing was loaded then.</returns>
        public bool TryAdd(int good, int barrels)
        {
            RequireGood(good);
            RequirePositive(barrels);

            if (barrels > Free)
            {
                return false;
            }

            while (barrelsByGood.Count <= good)
            {
                barrelsByGood.Add(0);
            }

            barrelsByGood[good] += barrels;
            Used += barrels;
            Changed?.Invoke();
            return true;
        }

        /// <returns>False when fewer are aboard; nothing was unloaded then.</returns>
        public bool TryRemove(int good, int barrels)
        {
            RequireGood(good);
            RequirePositive(barrels);

            if (barrels > BarrelsOf(good))
            {
                return false;
            }

            barrelsByGood[good] -= barrels;
            Used -= barrels;
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

        private static void RequirePositive(int barrels)
        {
            if (barrels < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(barrels));
            }
        }
    }
}
```

- [ ] **Step 4: Give `Ship` its hold**

In `Assets/Scripts/Core/Ship.cs`, add the parameter documentation after the `navigation` one and the parameter itself:

```csharp
        /// <param name="cargo">The ship's hold. Null for a ship that carries nothing.</param>
        public Ship(
            MapProjection projection,
            Vector2 position,
            float speed,
            ShipCrew crew,
            NavigationPathfinder navigation = null,
            CargoHold cargo = null)
```

After the line `Crew = crew ?? throw new ArgumentNullException(nameof(crew));`, add:

```csharp
            Cargo = cargo ?? new CargoHold(0);
```

After the `Crew` property, add:

```csharp
        /// <summary>The goods aboard. A ship that was given no hold has one without room.</summary>
        public CargoHold Cargo { get; }
```

Update the class summary's first sentence to: `Runtime state of one ship: where it is, the route it follows, which way it faces, its crew and its cargo.`

- [ ] **Step 5: Run the tests to verify they pass**

Run all EditMode tests. Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/Core/CargoHold.cs Assets/Scripts/Core/CargoHold.cs.meta Assets/Scripts/Core/Ship.cs Assets/Tests/EditMode/CargoHoldTests.cs Assets/Tests/EditMode/CargoHoldTests.cs.meta Assets/Tests/EditMode/ShipTests.cs
git commit -m "Cale des navires : des tonneaux par marchandise, dans une capacité limitée

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `MarketPricing`

**Files:**
- Create: `Assets/Scripts/Core/MarketPricing.cs`
- Test: `Assets/Tests/EditMode/MarketPricingTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `MarketPricing(double emptyStockMultiplier, double surplusMultiplier, double buyMargin, double sellMargin)`; `double Multiplier(double stock, double target)`; `long BuyPrice(long basePrice, double stock, double target)` (price of one barrel bought from a city whose stock is `stock`); `long SellPrice(long basePrice, double stock, double target)` (price of one barrel sold to a city whose stock is `stock`).

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/MarketPricingTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class MarketPricingTests
    {
        private static MarketPricing Pricing => new MarketPricing(2.5, 0.5, 0.05, 0.05);

        [TestCase(0, 2.5)]
        [TestCase(50, 1.75)]
        [TestCase(100, 1.0)]
        [TestCase(150, 0.75)]
        [TestCase(200, 0.5)]
        [TestCase(5000, 0.5)]
        public void Multiplier_FollowsTheStockAgainstTheTarget(double stock, double expected)
        {
            Assert.AreEqual(expected, Pricing.Multiplier(stock, 100), 1e-9);
        }

        [Test]
        public void Multiplier_OfANegativeStock_IsThatOfAnEmptyOne()
        {
            Assert.AreEqual(2.5, Pricing.Multiplier(-1, 100), 1e-9);
        }

        [Test]
        public void Multiplier_WithoutATarget_IsOne()
        {
            Assert.AreEqual(1.0, Pricing.Multiplier(0, 0), 1e-9);
            Assert.AreEqual(1.0, Pricing.Multiplier(40, 0), 1e-9);
        }

        [Test]
        public void BuyPrice_IsThePriceOnceTheBarrelIsGone_PlusTheMargin_RoundedUp()
        {
            // One barrel left of a target of 100: the price of an empty stock, 25, plus 5 %.
            Assert.AreEqual(27, Pricing.BuyPrice(10, 1, 100));
        }

        [Test]
        public void SellPrice_IsThePriceBeforeTheBarrelIsIn_MinusTheMargin_RoundedDown()
        {
            // Empty stock: 25, minus 5 %.
            Assert.AreEqual(23, Pricing.SellPrice(10, 0, 100));
        }

        [Test]
        public void Prices_ThatAreWholeNumbers_AreNotRoundedAway()
        {
            // 20 x 1.05 is 21.000000000000004 in floating point: still 21, not 22.
            Assert.AreEqual(21, Pricing.BuyPrice(20, 101, 100));
            Assert.AreEqual(19, Pricing.SellPrice(20, 100, 100));
        }

        [Test]
        public void BuyPrice_IsAtLeastOneCoin()
        {
            Assert.AreEqual(1, Pricing.BuyPrice(1, 500, 100));
        }

        [Test]
        public void SellPrice_CanBeNothing()
        {
            Assert.AreEqual(0, Pricing.SellPrice(1, 500, 100));
        }

        [TestCase(1)]
        [TestCase(10)]
        [TestCase(150)]
        public void BuyingABarrelAndSellingItBack_AlwaysLosesGold(long basePrice)
        {
            MarketPricing pricing = Pricing;

            for (int stock = 1; stock <= 300; stock++)
            {
                long paid = pricing.BuyPrice(basePrice, stock, 100);
                long received = pricing.SellPrice(basePrice, stock - 1, 100);

                Assert.Less(received, paid, $"stock {stock}");
            }
        }

        [Test]
        public void WithoutMargins_BuyingAndSellingBackIsNeverAGain()
        {
            var pricing = new MarketPricing(2.5, 0.5, 0, 0);

            for (int stock = 1; stock <= 300; stock++)
            {
                Assert.LessOrEqual(pricing.SellPrice(10, stock - 1, 100), pricing.BuyPrice(10, stock, 100));
            }
        }

        [TestCase(0.9, 0.5, 0.05, 0.05)]
        [TestCase(2.5, 0.0, 0.05, 0.05)]
        [TestCase(2.5, 1.5, 0.05, 0.05)]
        [TestCase(2.5, 0.5, -0.1, 0.05)]
        [TestCase(2.5, 0.5, 0.05, 1.0)]
        [TestCase(double.NaN, 0.5, 0.05, 0.05)]
        public void RejectsSettingsThatMakeNoSense(double empty, double surplus, double buyMargin, double sellMargin)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketPricing(empty, surplus, buyMargin, sellMargin));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests. Expected: exit code `1`, `error CS0246` for `MarketPricing`.

- [ ] **Step 3: Write `MarketPricing`**

Create `Assets/Scripts/Core/MarketPricing.cs`:

```csharp
using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What a barrel costs in a city, from the city's stock against its target stock:
    /// dear when the stock is low, cheap when it is high. A city sells a little above
    /// that price and buys a little below it.
    /// </summary>
    public sealed class MarketPricing
    {
        // Prices are worked out in floating point: 20 x 1.05 must round up to 21, not 22.
        private const double RoundingMargin = 1e-9;

        // The stock, as a share of the target, from which the price stops falling.
        private const double SurplusRatio = 2d;

        private readonly double emptyStockMultiplier;
        private readonly double surplusMultiplier;
        private readonly double buyMargin;
        private readonly double sellMargin;

        /// <param name="emptyStockMultiplier">Share of the base price at an empty stock, 1 or more.</param>
        /// <param name="surplusMultiplier">Share of the base price at twice the target and above, above 0 and up to 1.</param>
        /// <param name="buyMargin">What the city adds when it sells to the player, from 0 to below 1.</param>
        /// <param name="sellMargin">What the city takes off when it buys from the player, from 0 to below 1.</param>
        public MarketPricing(double emptyStockMultiplier, double surplusMultiplier, double buyMargin, double sellMargin)
        {
            // The negated comparisons also reject NaN.
            if (!(emptyStockMultiplier >= 1d) || double.IsInfinity(emptyStockMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(emptyStockMultiplier));
            }

            if (!(surplusMultiplier > 0d && surplusMultiplier <= 1d))
            {
                throw new ArgumentOutOfRangeException(nameof(surplusMultiplier));
            }

            if (!(buyMargin >= 0d && buyMargin < 1d))
            {
                throw new ArgumentOutOfRangeException(nameof(buyMargin));
            }

            if (!(sellMargin >= 0d && sellMargin < 1d))
            {
                throw new ArgumentOutOfRangeException(nameof(sellMargin));
            }

            this.emptyStockMultiplier = emptyStockMultiplier;
            this.surplusMultiplier = surplusMultiplier;
            this.buyMargin = buyMargin;
            this.sellMargin = sellMargin;
        }

        /// <summary>
        /// Share of the base price at a stock: a straight line from the empty stock's
        /// to 1 at the target, then down to the surplus one at twice the target. 1 for
        /// a good without a target, which the city does not consume.
        /// </summary>
        public double Multiplier(double stock, double target)
        {
            if (!(target > 0d))
            {
                return 1d;
            }

            double ratio = Math.Max(0d, stock) / target;

            if (ratio <= 1d)
            {
                return emptyStockMultiplier + (1d - emptyStockMultiplier) * ratio;
            }

            if (ratio < SurplusRatio)
            {
                return 1d + (surplusMultiplier - 1d) * (ratio - 1d) / (SurplusRatio - 1d);
            }

            return surplusMultiplier;
        }

        /// <summary>
        /// What the player pays for one barrel from a city whose stock is
        /// <paramref name="stock"/>: the price once that barrel is gone, plus the margin,
        /// rounded up, at least one coin.
        /// </summary>
        public long BuyPrice(long basePrice, double stock, double target)
        {
            double price = basePrice * Multiplier(stock - 1d, target) * (1d + buyMargin);

            return Math.Max(1L, (long)Math.Ceiling(price - RoundingMargin));
        }

        /// <summary>
        /// What the player is paid for one barrel by a city whose stock is
        /// <paramref name="stock"/>: the price before that barrel is in, minus the margin,
        /// rounded down. Both prices are taken at the lower of the two stocks, so buying
        /// a barrel and selling it back loses gold.
        /// </summary>
        public long SellPrice(long basePrice, double stock, double target)
        {
            double price = basePrice * Multiplier(stock, target) * (1d - sellMargin);

            return Math.Max(0L, (long)Math.Floor(price + RoundingMargin));
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the fixture `DarkFantasyMerchant.Tests.EditMode.MarketPricingTests`. Expected: exit code `0`.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Core/MarketPricing.cs Assets/Scripts/Core/MarketPricing.cs.meta Assets/Tests/EditMode/MarketPricingTests.cs Assets/Tests/EditMode/MarketPricingTests.cs.meta
git commit -m "Prix des marchandises : selon le stock de la ville, avec un écart entre achat et vente

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `MarketGood` and `CityMarket`

**Files:**
- Create: `Assets/Scripts/Core/MarketGood.cs`, `Assets/Scripts/Core/CityMarket.cs`
- Test: `Assets/Tests/EditMode/CityMarketTests.cs`

**Interfaces:**
- Consumes: `MarketPricing` (Task 2): `BuyPrice(long basePrice, double stock, double target)`, `SellPrice(long basePrice, double stock, double target)`.
- Produces:
  - `MarketGood(long basePrice, double dailyConsumption, double productionRate, double ceilingDays)` (readonly struct); `BasePrice`, `DailyConsumption`, `ProductionRate`, `CeilingDays`, `bool IsProduced`, `double DailyProduction`, `double Ceiling`.
  - `CityMarket(IReadOnlyList<MarketGood> goods, double targetDays, MarketPricing pricing)`; `int GoodCount`; `double StockOf(int good)`; `int AvailableOf(int good)`; `double TargetOf(int good)`; `long BuyPriceOf(int good, int barrelsAlreadyBought = 0)`; `long SellPriceOf(int good, int barrelsAlreadySold = 0)`; `void Take(int good, int barrels)`; `void Put(int good, int barrels)`; `void StartDay()`; `event Action Changed`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/CityMarketTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityMarketTests
    {
        private const double Tolerance = 1e-9;
        private const double TargetDays = 30;

        private static MarketPricing Pricing => new MarketPricing(2.5, 0.5, 0.05, 0.05);

        // Two barrels a day, base price 10.
        private static MarketGood NotProduced => new MarketGood(10, 2, 0, 0);
        private static MarketGood Efficient => new MarketGood(10, 2, 1.5, 60);
        private static MarketGood Inefficient => new MarketGood(10, 2, 1.1, 36);

        private static CityMarket MarketOf(params MarketGood[] goods)
        {
            return new CityMarket(goods, TargetDays, Pricing);
        }

        [Test]
        public void AGoodThatIsNotProduced_StartsAtTheTargetStock()
        {
            CityMarket market = MarketOf(NotProduced);

            Assert.AreEqual(1, market.GoodCount);
            Assert.AreEqual(60, market.TargetOf(0), Tolerance);
            Assert.AreEqual(60, market.StockOf(0), Tolerance);
        }

        [Test]
        public void AGoodThatIsProduced_StartsAtItsCeiling()
        {
            CityMarket market = MarketOf(Efficient, Inefficient);

            Assert.AreEqual(120, market.StockOf(0), Tolerance);
            Assert.AreEqual(72, market.StockOf(1), Tolerance);
        }

        [Test]
        public void StartDay_ConsumesWhatIsNotProduced_DownToNothing()
        {
            CityMarket market = MarketOf(NotProduced);

            market.StartDay();
            Assert.AreEqual(58, market.StockOf(0), Tolerance);

            for (int day = 0; day < 40; day++)
            {
                market.StartDay();
            }

            Assert.AreEqual(0, market.StockOf(0), Tolerance);
        }

        [Test]
        public void StartDay_KeepsAnEfficientGoodAtItsCeiling()
        {
            CityMarket market = MarketOf(Efficient);

            for (int day = 0; day < 10; day++)
            {
                market.StartDay();
            }

            // Produced up to the ceiling of 120, then a day's consumption taken.
            Assert.AreEqual(118, market.StockOf(0), Tolerance);
        }

        [Test]
        public void StartDay_RefillsAProducedGoodByItsSurplus()
        {
            CityMarket market = MarketOf(Efficient);
            market.Take(0, 50);

            market.StartDay();

            // 70 + 3 produced - 2 consumed.
            Assert.AreEqual(71, market.StockOf(0), Tolerance);
        }

        [Test]
        public void StartDay_LeavesAloneAStockAboveItsCeiling()
        {
            CityMarket market = MarketOf(Efficient);
            market.Put(0, 100);

            market.StartDay();

            // Nothing produced above the ceiling: only consumed.
            Assert.AreEqual(218, market.StockOf(0), Tolerance);
        }

        [Test]
        public void AnInefficientGood_SettlesJustUnderItsCeiling()
        {
            CityMarket market = MarketOf(Inefficient);
            market.Take(0, 20);

            for (int day = 0; day < 200; day++)
            {
                market.StartDay();
            }

            Assert.AreEqual(70, market.StockOf(0), Tolerance);
        }

        [Test]
        public void AvailableOf_IsTheWholeBarrels()
        {
            // A village: 0.3 barrel a day, 9 in stock.
            CityMarket market = MarketOf(new MarketGood(40, 0.3, 0, 0));

            market.StartDay();

            Assert.AreEqual(8.7, market.StockOf(0), Tolerance);
            Assert.AreEqual(8, market.AvailableOf(0));
        }

        [Test]
        public void AStockUnderOneBarrel_HasNoneAvailable()
        {
            CityMarket market = MarketOf(new MarketGood(40, 0.3, 0, 0));

            for (int day = 0; day < 28; day++)
            {
                market.StartDay();
            }

            Assert.Greater(market.StockOf(0), 0d);
            Assert.AreEqual(0, market.AvailableOf(0));
        }

        [Test]
        public void Prices_AreThoseOfTheNextBarrel()
        {
            CityMarket market = MarketOf(NotProduced);

            // Stock 60 of a target of 60. Bought: 10 x multiplier(59/60) x 1.05 = 10.7625.
            Assert.AreEqual(11, market.BuyPriceOf(0));

            // Sold: 10 x 1 x 0.95.
            Assert.AreEqual(9, market.SellPriceOf(0));
        }

        [Test]
        public void Prices_OfLaterBarrels_FollowTheStockTheyWouldLeave()
        {
            CityMarket market = MarketOf(NotProduced);

            // The last of the 60 barrels: the price of an empty stock.
            Assert.AreEqual(27, market.BuyPriceOf(0, 59));

            // The 61st barrel sold: 10 x multiplier(120/60) x 0.95 = 4.75.
            Assert.AreEqual(4, market.SellPriceOf(0, 60));
        }

        [Test]
        public void AGoodTheCityDoesNotConsume_IsAtItsBasePrice_AndOutOfStock()
        {
            CityMarket market = MarketOf(new MarketGood(10, 0, 0, 0));

            Assert.AreEqual(0, market.AvailableOf(0));
            Assert.AreEqual(11, market.BuyPriceOf(0));
            Assert.AreEqual(9, market.SellPriceOf(0));
        }

        [Test]
        public void TakeAndPut_MoveWholeBarrels()
        {
            CityMarket market = MarketOf(NotProduced);

            market.Take(0, 25);
            Assert.AreEqual(35, market.StockOf(0), Tolerance);

            market.Put(0, 300);
            Assert.AreEqual(335, market.StockOf(0), Tolerance);
        }

        [Test]
        public void Take_MoreThanAvailable_Throws()
        {
            CityMarket market = MarketOf(NotProduced);

            Assert.Throws<ArgumentOutOfRangeException>(() => market.Take(0, 61));
            Assert.AreEqual(60, market.StockOf(0), Tolerance);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsAQuantityThatIsNotPositive(int barrels)
        {
            CityMarket market = MarketOf(NotProduced);

            Assert.Throws<ArgumentOutOfRangeException>(() => market.Take(0, barrels));
            Assert.Throws<ArgumentOutOfRangeException>(() => market.Put(0, barrels));
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void RejectsAGoodOutsideTheCatalogue(int good)
        {
            CityMarket market = MarketOf(NotProduced);

            Assert.Throws<ArgumentOutOfRangeException>(() => market.StockOf(good));
            Assert.Throws<ArgumentOutOfRangeException>(() => market.BuyPriceOf(good));
            Assert.Throws<ArgumentOutOfRangeException>(() => market.Put(good, 1));
        }

        [Test]
        public void Changed_IsRaisedOnceByADay_ATakeAndAPut()
        {
            CityMarket market = MarketOf(NotProduced, Efficient);
            int changes = 0;
            market.Changed += () => changes++;

            market.StartDay();
            Assert.AreEqual(1, changes);

            market.Take(0, 5);
            Assert.AreEqual(2, changes);

            market.Put(1, 5);
            Assert.AreEqual(3, changes);
        }

        [Test]
        public void AMarketWithoutGoods_IsValid()
        {
            CityMarket market = MarketOf();

            Assert.AreEqual(0, market.GoodCount);
            Assert.DoesNotThrow(() => market.StartDay());
        }

        [Test]
        public void MarketGood_RejectsFiguresThatMakeNoSense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(0, 2, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, -1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, double.NaN, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, 2, -0.5, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketGood(10, 2, 1.5, -1));
        }

        [Test]
        public void RejectsANegativeTarget_AndMissingArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CityMarket(new[] { NotProduced }, -1, Pricing));
            Assert.Throws<ArgumentNullException>(() => new CityMarket(null, TargetDays, Pricing));
            Assert.Throws<ArgumentNullException>(() => new CityMarket(new[] { NotProduced }, TargetDays, null));
        }
    }
}
```

Check of `AnInefficientGood_SettlesJustUnderItsCeiling`: every day adds 2.2 up to the ceiling of 72, then takes 2, so the stock ends each day at 70 once it has reached the ceiling. Check of `AStockUnderOneBarrel_HasNoneAvailable`: 9 − 28 × 0.3 = 0.6.

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests. Expected: exit code `1`, `error CS0246` for `MarketGood` and `CityMarket`.

- [ ] **Step 3: Write `MarketGood`**

Create `Assets/Scripts/Core/MarketGood.cs`:

```csharp
using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>The figures of one good in one city: what it costs, and what the city consumes and produces of it.</summary>
    public readonly struct MarketGood
    {
        /// <param name="basePrice">Gold per barrel at the target stock, at least 1.</param>
        /// <param name="dailyConsumption">Barrels the city consumes in a day; it can be a fraction.</param>
        /// <param name="productionRate">Production as a share of the consumption; 0 for a good the city does not produce.</param>
        /// <param name="ceilingDays">Days of consumption above which the city's production stops raising the stock.</param>
        public MarketGood(long basePrice, double dailyConsumption, double productionRate, double ceilingDays)
        {
            if (basePrice < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(basePrice));
            }

            RequireNotNegative(dailyConsumption, nameof(dailyConsumption));
            RequireNotNegative(productionRate, nameof(productionRate));
            RequireNotNegative(ceilingDays, nameof(ceilingDays));

            BasePrice = basePrice;
            DailyConsumption = dailyConsumption;
            ProductionRate = productionRate;
            CeilingDays = ceilingDays;
        }

        public long BasePrice { get; }

        public double DailyConsumption { get; }

        public double ProductionRate { get; }

        public double CeilingDays { get; }

        public bool IsProduced => ProductionRate > 0d;

        /// <summary>Barrels the city produces in a day, while its stock is under the ceiling.</summary>
        public double DailyProduction => DailyConsumption * ProductionRate;

        /// <summary>The stock, in barrels, that the city's production does not exceed.</summary>
        public double Ceiling => DailyConsumption * CeilingDays;

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

- [ ] **Step 4: Write `CityMarket`**

Create `Assets/Scripts/Core/CityMarket.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The stocks of goods of one city. Every day the city produces some goods and
    /// consumes all of them; the price of a good follows its stock against a target.
    /// There is no limit to what a city can store. Goods are indices into the catalogue.
    /// </summary>
    public sealed class CityMarket
    {
        private readonly MarketGood[] goods;
        private readonly double[] stocks;
        private readonly double targetDays;
        private readonly MarketPricing pricing;

        /// <param name="targetDays">Days of consumption a city holds when a good is at its base price.</param>
        public CityMarket(IReadOnlyList<MarketGood> goods, double targetDays, MarketPricing pricing)
        {
            if (goods == null)
            {
                throw new ArgumentNullException(nameof(goods));
            }

            // The negated comparison also rejects NaN.
            if (!(targetDays >= 0d) || double.IsInfinity(targetDays))
            {
                throw new ArgumentOutOfRangeException(nameof(targetDays));
            }

            this.pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
            this.targetDays = targetDays;
            this.goods = new MarketGood[goods.Count];
            stocks = new double[goods.Count];

            for (int i = 0; i < goods.Count; i++)
            {
                this.goods[i] = goods[i];

                // The game opens balanced: producers are full, the others at their target.
                stocks[i] = goods[i].IsProduced ? goods[i].Ceiling : TargetOf(i);
            }
        }

        public int GoodCount => goods.Length;

        /// <summary>Raised once by a day that starts, and once when barrels were taken or put.</summary>
        public event Action Changed;

        /// <summary>Barrels in stock; a city consumes fractions of a barrel, so it is not whole.</summary>
        public double StockOf(int good)
        {
            RequireGood(good);

            return stocks[good];
        }

        /// <summary>Whole barrels in stock: what is shown and what can be bought.</summary>
        public int AvailableOf(int good)
        {
            return (int)Math.Min(int.MaxValue, Math.Floor(StockOf(good)));
        }

        /// <summary>The stock at which the good is at its base price.</summary>
        public double TargetOf(int good)
        {
            RequireGood(good);

            return targetDays * goods[good].DailyConsumption;
        }

        /// <summary>What the player pays for the next barrel, or for a later one of the same purchase.</summary>
        /// <param name="barrelsAlreadyBought">Barrels of this purchase that come before it.</param>
        public long BuyPriceOf(int good, int barrelsAlreadyBought = 0)
        {
            RequireGood(good);
            RequireNotNegative(barrelsAlreadyBought, nameof(barrelsAlreadyBought));

            return pricing.BuyPrice(goods[good].BasePrice, StockOf(good) - barrelsAlreadyBought, TargetOf(good));
        }

        /// <summary>What the player is paid for the next barrel, or for a later one of the same sale.</summary>
        /// <param name="barrelsAlreadySold">Barrels of this sale that come before it.</param>
        public long SellPriceOf(int good, int barrelsAlreadySold = 0)
        {
            RequireGood(good);
            RequireNotNegative(barrelsAlreadySold, nameof(barrelsAlreadySold));

            return pricing.SellPrice(goods[good].BasePrice, StockOf(good) + barrelsAlreadySold, TargetOf(good));
        }

        /// <summary>Takes whole barrels out of the stock. Throws when fewer are available.</summary>
        public void Take(int good, int barrels)
        {
            RequirePositive(barrels);

            if (barrels > AvailableOf(good))
            {
                throw new ArgumentOutOfRangeException(nameof(barrels));
            }

            stocks[good] -= barrels;
            Changed?.Invoke();
        }

        /// <summary>Adds whole barrels to the stock, whatever it already holds.</summary>
        public void Put(int good, int barrels)
        {
            RequireGood(good);
            RequirePositive(barrels);

            stocks[good] += barrels;
            Changed?.Invoke();
        }

        /// <summary>
        /// One day of the city: it produces, up to each good's ceiling and never above,
        /// then consumes, down to nothing. A stock that deliveries raised above its
        /// ceiling is only consumed.
        /// </summary>
        public void StartDay()
        {
            for (int i = 0; i < goods.Length; i++)
            {
                MarketGood good = goods[i];
                double stock = stocks[i];

                if (stock < good.Ceiling)
                {
                    stock = Math.Min(good.Ceiling, stock + good.DailyProduction);
                }

                stocks[i] = Math.Max(0d, stock - good.DailyConsumption);
            }

            Changed?.Invoke();
        }

        private void RequireGood(int good)
        {
            if (good < 0 || good >= goods.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(good));
            }
        }

        private static void RequirePositive(int barrels)
        {
            if (barrels < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(barrels));
            }
        }

        private static void RequireNotNegative(int barrels, string name)
        {
            if (barrels < 0)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run the fixture `DarkFantasyMerchant.Tests.EditMode.CityMarketTests`. Expected: exit code `0`.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/Core/MarketGood.cs Assets/Scripts/Core/MarketGood.cs.meta Assets/Scripts/Core/CityMarket.cs Assets/Scripts/Core/CityMarket.cs.meta Assets/Tests/EditMode/CityMarketTests.cs Assets/Tests/EditMode/CityMarketTests.cs.meta
git commit -m "Marché d'une ville : des stocks consommés et produits chaque jour

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `Trade`

**Files:**
- Create: `Assets/Scripts/Core/TradeResult.cs`, `Assets/Scripts/Core/Trade.cs`
- Test: `Assets/Tests/EditMode/TradeTests.cs`

**Interfaces:**
- Consumes: `CityMarket` (Task 3): `AvailableOf`, `BuyPriceOf(good, barrelsAlreadyBought)`, `SellPriceOf(good, barrelsAlreadySold)`, `Take`, `Put`. `CargoHold` (Task 1): `Free`, `BarrelsOf`, `TryAdd`, `TryRemove`. `Treasury` (existing): `CanAfford(long)`, `TryWithdraw(long)`, `Deposit(long)`, `Gold`.
- Produces: `TradeResult` (readonly struct: `int Barrels`, `long Gold`); `static TradeResult Trade.Buy(CityMarket market, CargoHold hold, Treasury treasury, int good, int barrels)`; `static TradeResult Trade.Sell(CityMarket market, CargoHold hold, Treasury treasury, int good, int barrels)`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/TradeTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class TradeTests
    {
        private const int Good = 0;

        private CityMarket market;
        private CargoHold hold;
        private Treasury treasury;

        [SetUp]
        public void SetUp()
        {
            // One good at base price 10, two barrels a day: 60 in stock, the target.
            market = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0) }, 30, new MarketPricing(2.5, 0.5, 0.05, 0.05));
            hold = new CargoHold(200);
            treasury = new Treasury(10000);
        }

        [Test]
        public void Buy_MovesABarrelAndItsPrice()
        {
            long price = market.BuyPriceOf(Good);

            TradeResult result = Trade.Buy(market, hold, treasury, Good, 1);

            Assert.AreEqual(1, result.Barrels);
            Assert.AreEqual(price, result.Gold);
            Assert.AreEqual(59, market.AvailableOf(Good));
            Assert.AreEqual(1, hold.BarrelsOf(Good));
            Assert.AreEqual(10000 - price, treasury.Gold);
        }

        [Test]
        public void Buy_PricesEveryBarrelAtTheStockItLeaves()
        {
            long first = market.BuyPriceOf(Good);
            long expected = 0;

            for (int i = 0; i < 30; i++)
            {
                expected += market.BuyPriceOf(Good, i);
            }

            TradeResult result = Trade.Buy(market, hold, treasury, Good, 30);

            Assert.AreEqual(30, result.Barrels);
            Assert.AreEqual(expected, result.Gold);
            Assert.Greater(result.Gold, 30 * first);
            Assert.AreEqual(10000 - expected, treasury.Gold);
        }

        [Test]
        public void Buy_StopsAtTheStock()
        {
            TradeResult result = Trade.Buy(market, hold, treasury, Good, 100);

            Assert.AreEqual(60, result.Barrels);
            Assert.AreEqual(0, market.AvailableOf(Good));
            Assert.AreEqual(60, hold.BarrelsOf(Good));
        }

        [Test]
        public void Buy_StopsAtTheHold()
        {
            hold = new CargoHold(5);

            TradeResult result = Trade.Buy(market, hold, treasury, Good, 100);

            Assert.AreEqual(5, result.Barrels);
            Assert.AreEqual(0, hold.Free);
            Assert.AreEqual(55, market.AvailableOf(Good));
        }

        [Test]
        public void Buy_StopsAtTheGold()
        {
            long two = market.BuyPriceOf(Good, 0) + market.BuyPriceOf(Good, 1);
            long three = two + market.BuyPriceOf(Good, 2);
            treasury = new Treasury(three - 1);

            TradeResult result = Trade.Buy(market, hold, treasury, Good, 100);

            Assert.AreEqual(2, result.Barrels);
            Assert.AreEqual(two, result.Gold);
            Assert.AreEqual(three - 1 - two, treasury.Gold);
            Assert.IsFalse(treasury.IsInDebt);
        }

        [Test]
        public void Buy_InDebt_BuysNothing()
        {
            treasury = new Treasury(-50);

            TradeResult result = Trade.Buy(market, hold, treasury, Good, 10);

            Assert.AreEqual(0, result.Barrels);
            Assert.AreEqual(0, result.Gold);
            Assert.AreEqual(-50, treasury.Gold);
            Assert.AreEqual(60, market.AvailableOf(Good));
        }

        [Test]
        public void Sell_MovesBarrelsAndDepositsTheirPrice()
        {
            hold.TryAdd(Good, 5);
            long expected = market.SellPriceOf(Good, 0) + market.SellPriceOf(Good, 1) + market.SellPriceOf(Good, 2);

            TradeResult result = Trade.Sell(market, hold, treasury, Good, 3);

            Assert.AreEqual(3, result.Barrels);
            Assert.AreEqual(expected, result.Gold);
            Assert.AreEqual(2, hold.BarrelsOf(Good));
            Assert.AreEqual(63, market.AvailableOf(Good));
            Assert.AreEqual(10000 + expected, treasury.Gold);
        }

        [Test]
        public void Sell_StopsAtTheBarrelsAboard()
        {
            hold.TryAdd(Good, 2);

            TradeResult result = Trade.Sell(market, hold, treasury, Good, 100);

            Assert.AreEqual(2, result.Barrels);
            Assert.AreEqual(0, hold.BarrelsOf(Good));
        }

        [Test]
        public void Sell_InDebt_StillDeposits()
        {
            treasury = new Treasury(-50);
            hold.TryAdd(Good, 10);

            TradeResult result = Trade.Sell(market, hold, treasury, Good, 10);

            Assert.AreEqual(10, result.Barrels);
            Assert.AreEqual(-50 + result.Gold, treasury.Gold);
            Assert.Greater(result.Gold, 0);
        }

        [Test]
        public void Sell_PaysLessForEveryBarrel()
        {
            hold.TryAdd(Good, 100);
            long first = market.SellPriceOf(Good);

            TradeResult result = Trade.Sell(market, hold, treasury, Good, 100);

            Assert.Less(result.Gold, 100 * first);
        }

        [Test]
        public void BuyingAndSellingBack_LosesGold()
        {
            Trade.Buy(market, hold, treasury, Good, 20);
            Trade.Sell(market, hold, treasury, Good, 20);

            Assert.Less(treasury.Gold, 10000);
            Assert.AreEqual(60, market.AvailableOf(Good));
        }

        [Test]
        public void ATrade_ChangesTheMarketAndTheHoldOnce()
        {
            int marketChanges = 0;
            int holdChanges = 0;
            market.Changed += () => marketChanges++;
            hold.Changed += () => holdChanges++;

            Trade.Buy(market, hold, treasury, Good, 10);
            Assert.AreEqual(1, marketChanges);
            Assert.AreEqual(1, holdChanges);

            Trade.Sell(market, hold, treasury, Good, 10);
            Assert.AreEqual(2, marketChanges);
            Assert.AreEqual(2, holdChanges);
        }

        [Test]
        public void ATradeOfNothing_ChangesNothing()
        {
            int changes = 0;
            market.Changed += () => changes++;
            hold.Changed += () => changes++;
            treasury.Changed += gold => changes++;

            TradeResult result = Trade.Sell(market, hold, treasury, Good, 5);

            Assert.AreEqual(0, result.Barrels);
            Assert.AreEqual(0, changes);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsAQuantityThatIsNotPositive(int barrels)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Trade.Buy(market, hold, treasury, Good, barrels));
            Assert.Throws<ArgumentOutOfRangeException>(() => Trade.Sell(market, hold, treasury, Good, barrels));
        }

        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => Trade.Buy(null, hold, treasury, Good, 1));
            Assert.Throws<ArgumentNullException>(() => Trade.Buy(market, null, treasury, Good, 1));
            Assert.Throws<ArgumentNullException>(() => Trade.Sell(market, hold, null, Good, 1));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests. Expected: exit code `1`, `error CS0103` / `CS0246` for `Trade` and `TradeResult`.

- [ ] **Step 3: Write `TradeResult`**

Create `Assets/Scripts/Core/TradeResult.cs`:

```csharp
namespace DarkFantasyMerchant.Core
{
    /// <summary>What a purchase or a sale came to: it can be less than what was asked for.</summary>
    public readonly struct TradeResult
    {
        public TradeResult(int barrels, long gold)
        {
            Barrels = barrels;
            Gold = gold;
        }

        /// <summary>Barrels that changed hands; zero when nothing could be traded.</summary>
        public int Barrels { get; }

        /// <summary>Gold paid for a purchase, or received for a sale.</summary>
        public long Gold { get; }
    }
}
```

- [ ] **Step 4: Write `Trade`**

Create `Assets/Scripts/Core/Trade.cs`:

```csharp
using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Buying and selling between a city's market, a ship's hold and the player's
    /// treasury. Barrels are priced one by one, each at the stock it leaves or joins,
    /// and a trade stops at the first barrel that cannot be traded.
    /// </summary>
    public static class Trade
    {
        /// <summary>
        /// Buys up to <paramref name="barrels"/> barrels: as many as the city has, the
        /// hold has room for and the gold covers. A treasury in debt buys nothing.
        /// </summary>
        public static TradeResult Buy(CityMarket market, CargoHold hold, Treasury treasury, int good, int barrels)
        {
            Require(market, hold, treasury, barrels);

            int limit = Math.Min(barrels, Math.Min(market.AvailableOf(good), hold.Free));
            int bought = 0;
            long gold = 0;

            while (bought < limit)
            {
                long price = market.BuyPriceOf(good, bought);

                if (!treasury.CanAfford(gold + price))
                {
                    break;
                }

                gold += price;
                bought++;
            }

            if (bought == 0)
            {
                return default;
            }

            treasury.TryWithdraw(gold);
            market.Take(good, bought);
            hold.TryAdd(good, bought);
            return new TradeResult(bought, gold);
        }

        /// <summary>Sells up to <paramref name="barrels"/> barrels: as many as are aboard.</summary>
        public static TradeResult Sell(CityMarket market, CargoHold hold, Treasury treasury, int good, int barrels)
        {
            Require(market, hold, treasury, barrels);

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
            treasury.Deposit(gold);
            return new TradeResult(sold, gold);
        }

        private static void Require(CityMarket market, CargoHold hold, Treasury treasury, int barrels)
        {
            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            if (hold == null)
            {
                throw new ArgumentNullException(nameof(hold));
            }

            if (treasury == null)
            {
                throw new ArgumentNullException(nameof(treasury));
            }

            if (barrels < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(barrels));
            }
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run the fixture `DarkFantasyMerchant.Tests.EditMode.TradeTests`. Expected: exit code `0`.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/Core/TradeResult.cs Assets/Scripts/Core/TradeResult.cs.meta Assets/Scripts/Core/Trade.cs Assets/Scripts/Core/Trade.cs.meta Assets/Tests/EditMode/TradeTests.cs Assets/Tests/EditMode/TradeTests.cs.meta
git commit -m "Commerce : acheter et vendre entre le marché d'une ville, la cale et la trésorerie

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Definitions and `WorldEconomy`

**Files:**
- Create: `Assets/Scripts/Game/GoodDefinition.cs`, `Assets/Scripts/Game/GoodProduction.cs`, `Assets/Scripts/Game/EconomyDefinition.cs`, `Assets/Scripts/Game/WorldEconomy.cs`
- Modify: `Assets/Scripts/Game/CityDefinition.cs`, `Assets/Scripts/Game/ShipDefinition.cs`, `Assets/Scripts/Game/ShipsView.cs`
- Test: `Assets/Tests/EditMode/TestEconomy.cs`, `Assets/Tests/EditMode/EconomyDefinitionTests.cs`, `Assets/Tests/EditMode/ShipDefinitionTests.cs`

**Interfaces:**
- Consumes: `MarketGood`, `CityMarket`, `MarketPricing` (Tasks 2-3); `CargoHold` and the `Ship` constructor (Task 1); `WorldClock.Clock`, `WorldClock.IsReady`, `GameClock.DayStarted` (`Action<GameDate>`), `WorldMapView.Cities`, `WorldMapView.IsReady` (existing).
- Produces:
  - `GoodDefinition`: `string DisplayName`, `long BasePrice`, `double ConsumptionPerThousand`. Serialized fields: `displayName`, `basePrice`, `consumptionPerThousand`.
  - `enum GoodProduction { None, Inefficient, Efficient }`.
  - `CityDefinition`: `int Population`, `IReadOnlyList<GoodDefinition> EfficientGoods`, `IReadOnlyList<GoodDefinition> InefficientGoods`, `GoodProduction ProductionOf(GoodDefinition good)`. Serialized fields: `population`, `efficientGoods`, `inefficientGoods`.
  - `ShipDefinition.CargoCapacity` (`int`, serialized field `cargoCapacity`, default 200).
  - `EconomyDefinition`: `IReadOnlyList<GoodDefinition> Goods`, `int IndexOf(GoodDefinition good)`, `bool TryValidate(out string problem)`, `MarketPricing CreatePricing()`, `CityMarket CreateMarket(CityDefinition city, MarketPricing pricing)`, and the settings `TargetStockDays`, `EfficientProductionRate`, `EfficientCeilingDays`, `InefficientProductionRate`, `InefficientCeilingDays`, `EmptyStockPriceMultiplier`, `SurplusPriceMultiplier`, `BuyMargin`, `SellMargin` (all `double`). Serialized field of the catalogue: `goods`.
  - `WorldEconomy` (MonoBehaviour): `EconomyDefinition Economy`, `bool IsReady`, `CityMarket MarketOf(CityDefinition city)`. Serialized fields: `economy`, `mapView`, `worldClock`.

- [ ] **Step 1: Write the test helper**

Create `Assets/Tests/EditMode/TestEconomy.cs`:

```csharp
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>In-memory goods, cities and economies. The caller destroys what it creates.</summary>
    public static class TestEconomy
    {
        public static GoodDefinition CreateGood(string displayName, long basePrice, double consumptionPerThousand)
        {
            var good = ScriptableObject.CreateInstance<GoodDefinition>();
            good.name = displayName;

            var serialized = new SerializedObject(good);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("basePrice").longValue = basePrice;
            serialized.FindProperty("consumptionPerThousand").doubleValue = consumptionPerThousand;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return good;
        }

        public static CityDefinition CreateCity(int population, GoodDefinition[] efficient, GoodDefinition[] inefficient)
        {
            var city = ScriptableObject.CreateInstance<CityDefinition>();

            var serialized = new SerializedObject(city);
            serialized.FindProperty("population").intValue = population;
            SetGoods(serialized.FindProperty("efficientGoods"), efficient);
            SetGoods(serialized.FindProperty("inefficientGoods"), inefficient);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return city;
        }

        public static EconomyDefinition CreateEconomy(params GoodDefinition[] goods)
        {
            var economy = ScriptableObject.CreateInstance<EconomyDefinition>();

            var serialized = new SerializedObject(economy);
            SetGoods(serialized.FindProperty("goods"), goods);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return economy;
        }

        private static void SetGoods(SerializedProperty list, GoodDefinition[] goods)
        {
            list.arraySize = goods.Length;

            for (int i = 0; i < goods.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = goods[i];
            }
        }
    }
}
```

- [ ] **Step 2: Write the failing tests**

Create `Assets/Tests/EditMode/EconomyDefinitionTests.cs`:

```csharp
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class EconomyDefinitionTests
    {
        private const double Tolerance = 1e-9;

        private readonly List<Object> created = new List<Object>();

        private GoodDefinition wheat;
        private GoodDefinition fish;
        private GoodDefinition salt;
        private EconomyDefinition economy;

        [SetUp]
        public void SetUp()
        {
            wheat = Track(TestEconomy.CreateGood("Blé", 10, 2.0));
            fish = Track(TestEconomy.CreateGood("Poisson", 12, 1.5));
            salt = Track(TestEconomy.CreateGood("Sel", 18, 0.5));
            economy = Track(TestEconomy.CreateEconomy(wheat, fish, salt));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object instance in created)
            {
                Object.DestroyImmediate(instance);
            }

            created.Clear();
        }

        private T Track<T>(T instance) where T : Object
        {
            created.Add(instance);
            return instance;
        }

        private CityDefinition City(int population, GoodDefinition[] efficient, GoodDefinition[] inefficient)
        {
            return Track(TestEconomy.CreateCity(population, efficient, inefficient));
        }

        [Test]
        public void ANewDefinition_HasTheGamesSettings()
        {
            EconomyDefinition fresh = Track(ScriptableObject.CreateInstance<EconomyDefinition>());

            Assert.AreEqual(30, fresh.TargetStockDays);
            Assert.AreEqual(1.5, fresh.EfficientProductionRate);
            Assert.AreEqual(60, fresh.EfficientCeilingDays);
            Assert.AreEqual(1.1, fresh.InefficientProductionRate);
            Assert.AreEqual(36, fresh.InefficientCeilingDays);
            Assert.AreEqual(2.5, fresh.EmptyStockPriceMultiplier);
            Assert.AreEqual(0.5, fresh.SurplusPriceMultiplier);
            Assert.AreEqual(0.05, fresh.BuyMargin);
            Assert.AreEqual(0.05, fresh.SellMargin);
            Assert.AreEqual(0, fresh.Goods.Count);
        }

        [Test]
        public void ANewGood_AndANewCity_HaveUsableDefaults()
        {
            GoodDefinition good = Track(ScriptableObject.CreateInstance<GoodDefinition>());
            CityDefinition city = Track(ScriptableObject.CreateInstance<CityDefinition>());

            Assert.GreaterOrEqual(good.BasePrice, 1);
            Assert.AreEqual(0, city.Population);
            Assert.AreEqual(0, city.EfficientGoods.Count);
            Assert.AreEqual(0, city.InefficientGoods.Count);
        }

        [Test]
        public void IndexOf_IsThePlaceInTheCatalogue()
        {
            GoodDefinition stranger = Track(TestEconomy.CreateGood("Vin", 60, 0.6));

            Assert.AreEqual(0, economy.IndexOf(wheat));
            Assert.AreEqual(2, economy.IndexOf(salt));
            Assert.AreEqual(-1, economy.IndexOf(stranger));
            Assert.AreEqual(-1, economy.IndexOf(null));
        }

        [Test]
        public void ProductionOf_ReadsTheCitysLists()
        {
            CityDefinition city = City(6000, new[] { wheat }, new[] { fish });

            Assert.AreEqual(GoodProduction.Efficient, city.ProductionOf(wheat));
            Assert.AreEqual(GoodProduction.Inefficient, city.ProductionOf(fish));
            Assert.AreEqual(GoodProduction.None, city.ProductionOf(salt));
            Assert.AreEqual(GoodProduction.None, city.ProductionOf(null));
        }

        [Test]
        public void AGoodInBothLists_IsEfficient()
        {
            CityDefinition city = City(6000, new[] { wheat }, new[] { wheat });

            Assert.AreEqual(GoodProduction.Efficient, city.ProductionOf(wheat));
        }

        [Test]
        public void AnEmptyEntry_IsIgnored()
        {
            CityDefinition city = City(6000, new GoodDefinition[] { null, wheat }, new GoodDefinition[] { null });

            Assert.AreEqual(GoodProduction.Efficient, city.ProductionOf(wheat));
            Assert.AreEqual(GoodProduction.None, city.ProductionOf(null));
            Assert.DoesNotThrow(() => economy.CreateMarket(city, economy.CreatePricing()));
        }

        [Test]
        public void CreateMarket_StocksEveryGoodOfTheCatalogue_FromThePopulation()
        {
            CityDefinition city = City(6000, new[] { wheat }, new[] { fish });

            CityMarket market = economy.CreateMarket(city, economy.CreatePricing());

            Assert.AreEqual(3, market.GoodCount);

            // Wheat: 12 barrels a day, efficient, 60 days.
            Assert.AreEqual(720, market.StockOf(0), Tolerance);

            // Fish: 9 barrels a day, inefficient, 36 days.
            Assert.AreEqual(324, market.StockOf(1), Tolerance);

            // Salt: 3 barrels a day, not produced, the target of 30 days.
            Assert.AreEqual(90, market.StockOf(2), Tolerance);
            Assert.AreEqual(90, market.TargetOf(2), Tolerance);
        }

        [Test]
        public void CreateMarket_ProducesAtTheDefinitionsRates()
        {
            CityDefinition city = City(6000, new[] { wheat }, new[] { fish });
            CityMarket market = economy.CreateMarket(city, economy.CreatePricing());
            market.Take(0, 100);
            market.Take(1, 100);

            market.StartDay();

            // Wheat: 620 + 18 - 12. Fish: 224 + 9.9 - 9.
            Assert.AreEqual(626, market.StockOf(0), Tolerance);
            Assert.AreEqual(224.9, market.StockOf(1), Tolerance);
        }

        [Test]
        public void CreateMarket_ForACityWithoutInhabitants_HasEmptyStocks()
        {
            CityDefinition city = City(0, new[] { wheat }, new GoodDefinition[0]);

            CityMarket market = economy.CreateMarket(city, economy.CreatePricing());

            Assert.AreEqual(0, market.AvailableOf(0));
            Assert.AreEqual(0, market.AvailableOf(2));
        }

        [Test]
        public void CreatePricing_UsesTheDefinitionsMultipliersAndMargins()
        {
            MarketPricing pricing = economy.CreatePricing();

            Assert.AreEqual(2.5, pricing.Multiplier(0, 100), Tolerance);
            Assert.AreEqual(0.5, pricing.Multiplier(200, 100), Tolerance);
            Assert.AreEqual(21, pricing.BuyPrice(20, 101, 100));
            Assert.AreEqual(19, pricing.SellPrice(20, 100, 100));
        }

        [Test]
        public void TryValidate_AcceptsACatalogueOfDistinctGoods()
        {
            Assert.IsTrue(economy.TryValidate(out string problem), problem);
        }

        [Test]
        public void TryValidate_RejectsAnEmptyCatalogue()
        {
            EconomyDefinition empty = Track(TestEconomy.CreateEconomy());

            Assert.IsFalse(empty.TryValidate(out string problem));
            Assert.IsNotEmpty(problem);
        }

        [Test]
        public void TryValidate_RejectsAGoodListedTwice()
        {
            EconomyDefinition doubled = Track(TestEconomy.CreateEconomy(wheat, fish, wheat));

            Assert.IsFalse(doubled.TryValidate(out string problem));
            Assert.IsNotEmpty(problem);
        }

        [Test]
        public void TryValidate_RejectsAnEmptyEntry()
        {
            EconomyDefinition holed = Track(TestEconomy.CreateEconomy(wheat, null));

            Assert.IsFalse(holed.TryValidate(out string problem));
            Assert.IsNotEmpty(problem);
        }
    }
}
```

Add this method to the existing `ShipDefinitionTests` class in `Assets/Tests/EditMode/ShipDefinitionTests.cs` (the names are fully qualified so that it compiles whatever the file's `using` lines are):

```csharp
        [Test]
        public void ANewDefinition_HoldsTwoHundredBarrels()
        {
            var fresh = UnityEngine.ScriptableObject.CreateInstance<ShipDefinition>();

            Assert.AreEqual(200, fresh.CargoCapacity);

            UnityEngine.Object.DestroyImmediate(fresh);
        }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run the EditMode tests. Expected: exit code `1`, `error CS0246` for `GoodDefinition` and `EconomyDefinition`.

- [ ] **Step 4: Write `GoodDefinition` and `GoodProduction`**

Create `Assets/Scripts/Game/GoodDefinition.cs`:

```csharp
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>Static definition of a good that cities trade, in barrels. Holds no runtime state.</summary>
    [CreateAssetMenu(fileName = "Good", menuName = "Dark Fantasy Merchant/Good")]
    public sealed class GoodDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;

        [Tooltip("Gold coins a barrel costs in a city that holds its target stock.")]
        [SerializeField, Min(1)] private long basePrice = 10;

        [Tooltip("Barrels that 1 000 inhabitants consume in a day.")]
        [SerializeField, Min(0f)] private double consumptionPerThousand = 1d;

        public string DisplayName => displayName;

        public long BasePrice => basePrice;

        public double ConsumptionPerThousand => consumptionPerThousand;
    }
}
```

Create `Assets/Scripts/Game/GoodProduction.cs`:

```csharp
namespace DarkFantasyMerchant.Game
{
    /// <summary>How a city produces a good, against what it consumes of it.</summary>
    public enum GoodProduction
    {
        /// <summary>Not at all: the city depends on trade for it.</summary>
        None,

        /// <summary>Barely more than it consumes.</summary>
        Inefficient,

        /// <summary>More than it consumes, with a good margin.</summary>
        Efficient,
    }
}
```

- [ ] **Step 5: Extend `CityDefinition`**

In `Assets/Scripts/Game/CityDefinition.cs`, add `using System.Collections.Generic;` above `using UnityEngine;`. After the `size` field, add:

```csharp

        [Tooltip("Inhabitants. Sets how much of every good the city consumes.")]
        [SerializeField, Min(0)] private int population;

        [Tooltip("Goods the city produces well above what it consumes.")]
        [SerializeField] private List<GoodDefinition> efficientGoods = new List<GoodDefinition>();

        [Tooltip("Goods the city produces barely above what it consumes.")]
        [SerializeField] private List<GoodDefinition> inefficientGoods = new List<GoodDefinition>();
```

After the `Size` property, add:

```csharp

        public int Population => population;

        public IReadOnlyList<GoodDefinition> EfficientGoods => efficientGoods;

        public IReadOnlyList<GoodDefinition> InefficientGoods => inefficientGoods;

        /// <summary>How the city produces a good. A good in both lists is produced efficiently.</summary>
        public GoodProduction ProductionOf(GoodDefinition good)
        {
            if (good == null)
            {
                return GoodProduction.None;
            }

            if (efficientGoods.Contains(good))
            {
                return GoodProduction.Efficient;
            }

            return inefficientGoods.Contains(good) ? GoodProduction.Inefficient : GoodProduction.None;
        }
```

- [ ] **Step 6: Extend `ShipDefinition` and `ShipsView`**

In `Assets/Scripts/Game/ShipDefinition.cs`, after the `crewCapacity` field add:

```csharp

        [Tooltip("Barrels of goods the hold has room for.")]
        [SerializeField, Min(0)] private int cargoCapacity = 200;
```

and after the `CrewCapacity` property:

```csharp

        public int CargoCapacity => cargoCapacity;
```

In `Assets/Scripts/Game/ShipsView.cs`, in `Spawn`, replace:

```csharp
            var ship = new Ship(mapView.Projection, position, definition.Speed, crew, navigation);
```

with:

```csharp
            // The Min attribute only constrains the Inspector, not the asset file.
            var cargo = new CargoHold(Mathf.Max(0, definition.CargoCapacity));
            var ship = new Ship(mapView.Projection, position, definition.Speed, crew, navigation, cargo);
```

- [ ] **Step 7: Write `EconomyDefinition`**

Create `Assets/Scripts/Game/EconomyDefinition.cs`:

```csharp
using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Static definition of the economy: the catalogue of goods, in the order they are
    /// listed everywhere, and the settings of production and prices. Holds no runtime
    /// state: the stocks are in a <see cref="CityMarket"/> per city.
    /// </summary>
    [CreateAssetMenu(fileName = "Economy", menuName = "Dark Fantasy Merchant/Economy")]
    public sealed class EconomyDefinition : ScriptableObject
    {
        [Tooltip("The goods of the game. Their order is their order on screen.")]
        [SerializeField] private List<GoodDefinition> goods = new List<GoodDefinition>();

        [Tooltip("Days of its consumption a city holds when a good is at its base price.")]
        [SerializeField, Min(0f)] private double targetStockDays = 30d;

        [Header("Production, as a share of the city's own consumption")]
        [SerializeField, Min(0f)] private double efficientProductionRate = 1.5d;

        [Tooltip("Days of consumption above which an efficient production stops raising the stock.")]
        [SerializeField, Min(0f)] private double efficientCeilingDays = 60d;

        [SerializeField, Min(0f)] private double inefficientProductionRate = 1.1d;

        [Tooltip("Days of consumption above which an inefficient production stops raising the stock.")]
        [SerializeField, Min(0f)] private double inefficientCeilingDays = 36d;

        [Header("Prices, as a share of the base price")]
        [Tooltip("At an empty stock.")]
        [SerializeField, Min(1f)] private double emptyStockPriceMultiplier = 2.5d;

        [Tooltip("At twice the target stock and above.")]
        [SerializeField, Range(0.01f, 1f)] private double surplusPriceMultiplier = 0.5d;

        [Tooltip("What a city adds to the price when it sells to the player.")]
        [SerializeField, Range(0f, 0.99f)] private double buyMargin = 0.05d;

        [Tooltip("What a city takes off the price when it buys from the player.")]
        [SerializeField, Range(0f, 0.99f)] private double sellMargin = 0.05d;

        public IReadOnlyList<GoodDefinition> Goods => goods;

        public double TargetStockDays => targetStockDays;

        public double EfficientProductionRate => efficientProductionRate;

        public double EfficientCeilingDays => efficientCeilingDays;

        public double InefficientProductionRate => inefficientProductionRate;

        public double InefficientCeilingDays => inefficientCeilingDays;

        public double EmptyStockPriceMultiplier => emptyStockPriceMultiplier;

        public double SurplusPriceMultiplier => surplusPriceMultiplier;

        public double BuyMargin => buyMargin;

        public double SellMargin => sellMargin;

        /// <returns>The index of a good in the catalogue, which is what Core knows it by, or -1.</returns>
        public int IndexOf(GoodDefinition good)
        {
            return good != null ? goods.IndexOf(good) : -1;
        }

        /// <summary>Whether the catalogue can be used: at least one good, none missing, none twice.</summary>
        /// <param name="problem">What is wrong, for the console; null when nothing is.</param>
        public bool TryValidate(out string problem)
        {
            problem = null;

            if (goods.Count == 0)
            {
                problem = "it has no good";
                return false;
            }

            for (int i = 0; i < goods.Count; i++)
            {
                if (goods[i] == null)
                {
                    problem = $"its good {i} is empty";
                    return false;
                }

                if (goods.IndexOf(goods[i]) != i)
                {
                    problem = $"'{goods[i].name}' is listed twice";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The pricing of these settings. Throws when one of them is unusable: the
        /// attributes only constrain the Inspector, not the file.
        /// </summary>
        public MarketPricing CreatePricing()
        {
            return new MarketPricing(emptyStockPriceMultiplier, surplusPriceMultiplier, buyMargin, sellMargin);
        }

        /// <summary>
        /// The market of a city at the start of the game, with one stock per good of the
        /// catalogue. Call <see cref="TryValidate"/> first. Throws when a setting or a
        /// good's figure is unusable.
        /// </summary>
        public CityMarket CreateMarket(CityDefinition city, MarketPricing pricing)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            double thousands = Math.Max(0, city.Population) / 1000d;
            var marketGoods = new MarketGood[goods.Count];

            for (int i = 0; i < goods.Count; i++)
            {
                GoodDefinition good = goods[i];
                double rate = 0d;
                double ceilingDays = 0d;

                switch (city.ProductionOf(good))
                {
                    case GoodProduction.Efficient:
                        rate = efficientProductionRate;
                        ceilingDays = efficientCeilingDays;
                        break;
                    case GoodProduction.Inefficient:
                        rate = inefficientProductionRate;
                        ceilingDays = inefficientCeilingDays;
                        break;
                }

                marketGoods[i] = new MarketGood(
                    good.BasePrice, thousands * good.ConsumptionPerThousand, rate, ceilingDays);
            }

            return new CityMarket(marketGoods, targetStockDays, pricing);
        }
    }
}
```

- [ ] **Step 8: Write `WorldEconomy`**

Create `Assets/Scripts/Game/WorldEconomy.cs`:

```csharp
using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Owns the market of every city of the map and makes the cities produce and consume
    /// every day of the world clock.
    /// </summary>
    public sealed class WorldEconomy : MonoBehaviour
    {
        [SerializeField] private EconomyDefinition economy;
        [SerializeField] private WorldMapView mapView;
        [SerializeField] private WorldClock worldClock;

        private readonly Dictionary<CityDefinition, CityMarket> markets = new Dictionary<CityDefinition, CityMarket>();

        private GameClock clock;

        public EconomyDefinition Economy => economy;

        /// <summary>False until the markets exist, which is in Start, and for good when they could not be made.</summary>
        public bool IsReady { get; private set; }

        /// <returns>The market of a city of the map, or null for another city or before the markets exist.</returns>
        public CityMarket MarketOf(CityDefinition city)
        {
            return city != null && markets.TryGetValue(city, out CityMarket market) ? market : null;
        }

        // Start, not Awake: WorldMapView lists its cities in its Awake.
        private void Start()
        {
            if (economy == null || mapView == null || worldClock == null)
            {
                Debug.LogError("WorldEconomy needs an economy definition, a map view and a world clock.", this);
                return;
            }

            if (!mapView.IsReady || !worldClock.IsReady)
            {
                // WorldMapView or WorldClock already logged why it has nothing to give.
                return;
            }

            if (!economy.TryValidate(out string problem))
            {
                Debug.LogError($"EconomyDefinition '{economy.name}' is unusable: {problem}. Cities have no market.", economy);
                return;
            }

            if (!TryCreateMarkets())
            {
                markets.Clear();
                return;
            }

            clock = worldClock.Clock;
            clock.DayStarted += OnDayStarted;
            IsReady = true;
        }

        private void OnDestroy()
        {
            if (clock != null)
            {
                clock.DayStarted -= OnDayStarted;
            }
        }

        private bool TryCreateMarkets()
        {
            try
            {
                MarketPricing pricing = economy.CreatePricing();

                foreach (CityDefinition city in mapView.Cities)
                {
                    WarnAbout(city);
                    markets[city] = economy.CreateMarket(city, pricing);
                }
            }
            catch (ArgumentOutOfRangeException exception)
            {
                Debug.LogError(
                    $"EconomyDefinition '{economy.name}' or one of its goods has an invalid value "
                    + $"({exception.ParamName}); cities have no market.",
                    economy);
                return false;
            }

            return true;
        }

        // A doubtful city still gets a market: these are mistakes of content, not reasons to stop.
        private void WarnAbout(CityDefinition city)
        {
            if (city.Population < 1)
            {
                Debug.LogWarning($"CityDefinition '{city.name}' has no inhabitant; its market is empty.", city);
            }

            WarnAboutGoods(city, city.EfficientGoods);
            WarnAboutGoods(city, city.InefficientGoods);

            foreach (GoodDefinition good in city.InefficientGoods)
            {
                if (good != null && city.ProductionOf(good) == GoodProduction.Efficient)
                {
                    Debug.LogWarning(
                        $"CityDefinition '{city.name}' lists '{good.name}' as both efficient and inefficient; it is efficient.",
                        city);
                }
            }
        }

        private void WarnAboutGoods(CityDefinition city, IReadOnlyList<GoodDefinition> produced)
        {
            foreach (GoodDefinition good in produced)
            {
                if (good == null)
                {
                    Debug.LogWarning($"CityDefinition '{city.name}' has an empty entry among its productions.", city);
                }
                else if (economy.IndexOf(good) < 0)
                {
                    Debug.LogWarning(
                        $"CityDefinition '{city.name}' produces '{good.name}', which is not in the economy's goods.",
                        city);
                }
            }
        }

        private void OnDayStarted(GameDate date)
        {
            foreach (CityMarket market in markets.Values)
            {
                market.StartDay();
            }
        }
    }
}
```

- [ ] **Step 9: Run the tests to verify they pass**

Run all EditMode tests. Expected: exit code `0`. The existing content tests still pass: the city assets have no population yet and nothing checks it before Task 8.

- [ ] **Step 10: Commit**

```powershell
git add Assets/Scripts/Game Assets/Tests/EditMode
git status --short
git commit -m "Économie : marchandises, réglages, population et productions des villes, marchés du monde

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

Check in the `git status --short` output that only the files of this task and their `.meta` are staged.

---

### Task 6: The market in the city panel

**Files:**
- Create: `Assets/Scripts/Game/CityMarketSection.cs`
- Modify: `Assets/Scripts/Game/TreasuryHudController.cs`, `Assets/Scripts/Game/ShipInfoPanelController.cs`, `Assets/Scripts/Game/CityInfoPanelController.cs`, `Assets/UI/WorldMap/CityInfoPanel.uxml`, `Assets/UI/WorldMap/CityInfoPanel.uss`
- Test: `Assets/Tests/EditMode/CityMarketSectionTests.cs`, `Assets/Tests/EditMode/TreasuryHudControllerTests.cs`, `Assets/Tests/EditMode/ShipInfoPanelControllerTests.cs`

**Interfaces:**
- Consumes: `CityMarket`, `CargoHold`, `Trade`, `Treasury` (Tasks 1-4); `WorldEconomy.IsReady`, `WorldEconomy.Economy`, `WorldEconomy.MarketOf(CityDefinition)`, `EconomyDefinition.Goods`, `GoodDefinition.DisplayName`, `CityDefinition.Population` (Task 5); `PlayerTreasury.IsReady`, `PlayerTreasury.Treasury`, `ShipsView.Selection`, `Ship.Cargo` (existing / Task 1).
- Produces:
  - `static string TreasuryHudController.FormatNumber(long value)` → `"6\u00A0000"`.
  - `static string ShipInfoPanelController.FormatCargo(CargoHold hold)` → `"Cale : 60 / 200 tonneaux"`.
  - `static string CityInfoPanelController.FormatPopulation(int population)` → `"Population : 6\u00A0000"`.
  - `CityMarketSection(VisualElement section, VisualElement rows, Label holdLabel, IReadOnlyList<string> goodNames, Treasury treasury)`; `void Show(CityMarket market, CargoHold hold)`; `void Hide()`; `void Buy(int good, int barrels)`; `void Sell(int good, int barrels)`; `static int BarrelsFor(bool shift, bool ctrl)`; constants `TradingClass = "market--trading"`, `NoPrice = "—"`. Children of a row, in order: name, stock, buy price, sell price, barrels in the hold (labels), sell button, buy button.
  - New serialized fields of `CityInfoPanelController`: `worldEconomy`, `playerTreasury` (both optional).

- [ ] **Step 1: Write the failing tests**

Add to the existing `TreasuryHudControllerTests` class:

```csharp
        [Test]
        public void FormatNumber_WritesThousandsSeparators_WithoutAUnit()
        {
            Assert.AreEqual("6\u00A0000", TreasuryHudController.FormatNumber(6000));
            Assert.AreEqual("340", TreasuryHudController.FormatNumber(340));
            Assert.AreEqual("0", TreasuryHudController.FormatNumber(0));
        }
```

Add to the existing `ShipInfoPanelControllerTests` class:

```csharp
        [Test]
        public void FormatCargo_WritesTheBarrelsAboard_OutOfTheCapacity()
        {
            var hold = new CargoHold(200);
            hold.TryAdd(3, 60);

            Assert.AreEqual("Cale : 60 / 200 tonneaux", ShipInfoPanelController.FormatCargo(hold));
        }

        [Test]
        public void FormatCargo_WritesThousandsSeparators()
        {
            Assert.AreEqual("Cale : 0 / 1\u00A0200 tonneaux", ShipInfoPanelController.FormatCargo(new CargoHold(1200)));
        }
```

Create `Assets/Tests/EditMode/CityMarketSectionTests.cs`:

```csharp
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityMarketSectionTests
    {
        private const int Wheat = 0;
        private const int Wine = 1;

        // Children of a row, in order.
        private const int NameCell = 0;
        private const int StockCell = 1;
        private const int BuyCell = 2;
        private const int SellCell = 3;
        private const int HoldCell = 4;
        private const int SellButton = 5;
        private const int BuyButton = 6;

        private VisualElement sectionElement;
        private VisualElement rows;
        private Label holdLabel;
        private CityMarket market;
        private CargoHold hold;
        private Treasury treasury;
        private CityMarketSection section;

        [SetUp]
        public void SetUp()
        {
            sectionElement = new VisualElement();
            rows = new VisualElement();
            holdLabel = new Label();

            // Wheat: 2 barrels a day, 60 in stock. Wine: not consumed here, none in stock.
            market = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0), new MarketGood(60, 0, 0, 0) },
                30,
                new MarketPricing(2.5, 0.5, 0.05, 0.05));
            hold = new CargoHold(200);
            treasury = new Treasury(10000);
            section = new CityMarketSection(sectionElement, rows, holdLabel, new[] { "Blé", "Vin" }, treasury);
        }

        private string Text(int good, int cell)
        {
            return ((Label)rows[good][cell]).text;
        }

        private bool IsEnabled(int good, int button)
        {
            return rows[good][button].enabledSelf;
        }

        [Test]
        public void BuildsOneRowPerGood_InTheCatalogueOrder()
        {
            Assert.AreEqual(2, rows.childCount);
            Assert.AreEqual("Blé", Text(Wheat, NameCell));
            Assert.AreEqual("Vin", Text(Wine, NameCell));
        }

        [Test]
        public void IsHiddenUntilItShowsAMarket()
        {
            Assert.AreEqual(DisplayStyle.None, sectionElement.style.display.value);

            section.Show(market, null);
            Assert.AreEqual(DisplayStyle.Flex, sectionElement.style.display.value);

            section.Hide();
            Assert.AreEqual(DisplayStyle.None, sectionElement.style.display.value);
        }

        [Test]
        public void Show_WritesTheStockAndThePrices()
        {
            section.Show(market, null);

            Assert.AreEqual("60", Text(Wheat, StockCell));
            Assert.AreEqual(market.BuyPriceOf(Wheat).ToString(), Text(Wheat, BuyCell));
            Assert.AreEqual(market.SellPriceOf(Wheat).ToString(), Text(Wheat, SellCell));
        }

        [Test]
        public void AGoodOutOfStock_HasNoBuyPrice()
        {
            section.Show(market, hold);

            Assert.AreEqual("0", Text(Wine, StockCell));
            Assert.AreEqual(CityMarketSection.NoPrice, Text(Wine, BuyCell));
            Assert.AreEqual(market.SellPriceOf(Wine).ToString(), Text(Wine, SellCell));
            Assert.IsFalse(IsEnabled(Wine, BuyButton));
        }

        [Test]
        public void Show_WithoutAHold_IsReadOnly()
        {
            section.Show(market, null);

            Assert.IsFalse(sectionElement.ClassListContains(CityMarketSection.TradingClass));
            Assert.AreEqual(string.Empty, holdLabel.text);
        }

        [Test]
        public void Show_WithAHold_AllowsTrading()
        {
            hold.TryAdd(Wine, 20);

            section.Show(market, hold);

            Assert.IsTrue(sectionElement.ClassListContains(CityMarketSection.TradingClass));
            Assert.AreEqual("Cale : 20 / 200 tonneaux", holdLabel.text);
            Assert.AreEqual("0", Text(Wheat, HoldCell));
            Assert.AreEqual("20", Text(Wine, HoldCell));
            Assert.IsTrue(IsEnabled(Wheat, BuyButton));
            Assert.IsFalse(IsEnabled(Wheat, SellButton));
            Assert.IsTrue(IsEnabled(Wine, SellButton));
        }

        [Test]
        public void Buy_TradesAndRewritesTheRows()
        {
            section.Show(market, hold);

            section.Buy(Wheat, 10);

            Assert.AreEqual(10, hold.BarrelsOf(Wheat));
            Assert.AreEqual("50", Text(Wheat, StockCell));
            Assert.AreEqual("10", Text(Wheat, HoldCell));
            Assert.AreEqual(market.BuyPriceOf(Wheat).ToString(), Text(Wheat, BuyCell));
            Assert.AreEqual("Cale : 10 / 200 tonneaux", holdLabel.text);
            Assert.IsTrue(IsEnabled(Wheat, SellButton));
            Assert.Less(treasury.Gold, 10000);
        }

        [Test]
        public void Sell_TradesAndRewritesTheRows()
        {
            hold.TryAdd(Wine, 20);
            section.Show(market, hold);

            section.Sell(Wine, 100);

            Assert.AreEqual(0, hold.BarrelsOf(Wine));
            Assert.AreEqual("20", Text(Wine, StockCell));
            Assert.IsFalse(IsEnabled(Wine, SellButton));
            Assert.Greater(treasury.Gold, 10000);
        }

        [Test]
        public void AFullHold_DisablesBuying()
        {
            hold = new CargoHold(5);
            section.Show(market, hold);

            section.Buy(Wheat, 5);

            Assert.IsFalse(IsEnabled(Wheat, BuyButton));
            Assert.IsTrue(IsEnabled(Wheat, SellButton));
        }

        [Test]
        public void InDebt_DisablesBuying_NotSelling()
        {
            hold.TryAdd(Wheat, 5);
            section.Show(market, hold);
            Assert.IsTrue(IsEnabled(Wheat, BuyButton));

            // The weekly wages, for instance: the section follows the treasury.
            treasury.Withdraw(20000);

            Assert.IsFalse(IsEnabled(Wheat, BuyButton));
            Assert.IsTrue(IsEnabled(Wheat, SellButton));
        }

        [Test]
        public void ADayThatStarts_RewritesTheRows()
        {
            section.Show(market, hold);

            market.StartDay();

            Assert.AreEqual("58", Text(Wheat, StockCell));
        }

        [Test]
        public void Show_WithoutAHold_StopsTrading()
        {
            section.Show(market, hold);
            section.Show(market, null);

            Assert.IsFalse(sectionElement.ClassListContains(CityMarketSection.TradingClass));
            Assert.AreEqual(string.Empty, holdLabel.text);

            // Nothing is traded without a hold, and the old one is no longer listened to.
            section.Buy(Wheat, 10);
            hold.TryAdd(Wheat, 7);

            Assert.AreEqual("60", Text(Wheat, StockCell));
            Assert.AreEqual(string.Empty, holdLabel.text);
        }

        [Test]
        public void Hide_StopsListening()
        {
            section.Show(market, hold);
            section.Hide();

            market.StartDay();

            Assert.AreEqual("60", Text(Wheat, StockCell));
        }

        [Test]
        public void WithoutATreasury_TheMarketIsReadOnly()
        {
            var readOnly = new CityMarketSection(
                new VisualElement(), new VisualElement(), new Label(), new[] { "Blé", "Vin" }, null);

            Assert.DoesNotThrow(() => readOnly.Show(market, hold));
            Assert.DoesNotThrow(() => readOnly.Buy(Wheat, 1));
            Assert.AreEqual(0, hold.BarrelsOf(Wheat));
        }

        [Test]
        public void AMarketWithFewerGoodsThanRows_LeavesTheExtraRowsEmpty()
        {
            var small = new CityMarket(
                new[] { new MarketGood(10, 2, 0, 0) }, 30, new MarketPricing(2.5, 0.5, 0.05, 0.05));

            Assert.DoesNotThrow(() => section.Show(small, hold));
            Assert.AreEqual(CityMarketSection.NoPrice, Text(Wine, BuyCell));
            Assert.IsFalse(IsEnabled(Wine, BuyButton));
            Assert.IsFalse(IsEnabled(Wine, SellButton));
        }

        [TestCase(false, false, 1)]
        [TestCase(true, false, 10)]
        [TestCase(false, true, 100)]
        [TestCase(true, true, 100)]
        public void BarrelsFor_FollowsTheModifierKeys(bool shift, bool ctrl, int expected)
        {
            Assert.AreEqual(expected, CityMarketSection.BarrelsFor(shift, ctrl));
        }

        [Test]
        public void FormatPopulation_WritesThousandsSeparators()
        {
            Assert.AreEqual("Population : 6\u00A0000", CityInfoPanelController.FormatPopulation(6000));
            Assert.AreEqual("Population : 0", CityInfoPanelController.FormatPopulation(0));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests. Expected: exit code `1`, `error CS0246` for `CityMarketSection`.

- [ ] **Step 3: Add the formatting helpers**

In `Assets/Scripts/Game/TreasuryHudController.cs`, replace the body of `FormatGold` and add `FormatNumber` above it:

```csharp
        /// <summary>A whole number as the interface writes it: "6 000", thousands separated by a no-break space.</summary>
        public static string FormatNumber(long value)
        {
            return value.ToString("N0", GoldFormat);
        }
```

```csharp
        public static string FormatGold(long gold)
        {
            return FormatNumber(gold) + " or";
        }
```

In `Assets/Scripts/Game/ShipInfoPanelController.cs`, after `FormatCrew`, add:

```csharp

        /// <summary>The hold as the panels write it: "Cale : 60 / 200 tonneaux", the barrels aboard out of the capacity.</summary>
        public static string FormatCargo(CargoHold hold)
        {
            return $"Cale : {TreasuryHudController.FormatNumber(hold.Used)} / "
                + $"{TreasuryHudController.FormatNumber(hold.Capacity)} tonneaux";
        }
```

- [ ] **Step 4: Write `CityMarketSection`**

Create `Assets/Scripts/Game/CityMarketSection.cs`:

```csharp
using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The market rows of the city panel: one row per good with the city's stock and its
    /// prices, and, when it is given the hold of a ship in port, the barrels aboard and
    /// the buttons that buy and sell. Follows the market, the hold and the treasury.
    /// </summary>
    public sealed class CityMarketSection
    {
        /// <summary>On the section while a hold is shown: reveals the hold column and the buttons.</summary>
        public const string TradingClass = "market--trading";

        /// <summary>Written instead of a buy price when the city has no barrel to sell.</summary>
        public const string NoPrice = "—";

        private const string RowClass = "market__row";
        private const string CellClass = "market__cell";
        private const string NameCellClass = "market__cell--name";
        private const string NumberCellClass = "market__cell--number";
        private const string TradeCellClass = "market__cell--trade";
        private const string ButtonClass = "market__button";

        private sealed class Row
        {
            public Label Stock;
            public Label BuyPrice;
            public Label SellPrice;
            public Label Held;
            public Button Sell;
            public Button Buy;
        }

        private readonly VisualElement section;
        private readonly Label holdLabel;
        private readonly Treasury treasury;
        private readonly List<Row> rows = new List<Row>();

        private CityMarket market;
        private CargoHold hold;

        /// <param name="section">The whole section, shown and hidden with the market.</param>
        /// <param name="rowContainer">Where the rows are built, one per name.</param>
        /// <param name="goodNames">The names of the goods, in the catalogue's order.</param>
        /// <param name="treasury">What pays and is paid. Null for a market that is only read.</param>
        public CityMarketSection(
            VisualElement section,
            VisualElement rowContainer,
            Label holdLabel,
            IReadOnlyList<string> goodNames,
            Treasury treasury)
        {
            this.section = section ?? throw new ArgumentNullException(nameof(section));
            this.holdLabel = holdLabel ?? throw new ArgumentNullException(nameof(holdLabel));
            this.treasury = treasury;

            if (rowContainer == null)
            {
                throw new ArgumentNullException(nameof(rowContainer));
            }

            if (goodNames == null)
            {
                throw new ArgumentNullException(nameof(goodNames));
            }

            rowContainer.Clear();

            for (int i = 0; i < goodNames.Count; i++)
            {
                rowContainer.Add(BuildRow(i, goodNames[i]));
            }

            Hide();
        }

        /// <summary>Barrels a click trades: 1, 10 with Shift, 100 with Ctrl.</summary>
        public static int BarrelsFor(bool shift, bool ctrl)
        {
            if (ctrl)
            {
                return 100;
            }

            return shift ? 10 : 1;
        }

        /// <summary>Shows a city's market, and trades with a hold when one is given.</summary>
        /// <param name="hold">The hold of the ship in port that trades, or null to only read the market.</param>
        public void Show(CityMarket market, CargoHold hold)
        {
            Unbind();

            this.market = market ?? throw new ArgumentNullException(nameof(market));

            // Nothing pays without a treasury.
            this.hold = treasury != null ? hold : null;

            this.market.Changed += Refresh;

            if (this.hold != null)
            {
                this.hold.Changed += Refresh;
                treasury.Changed += OnGoldChanged;
            }

            section.EnableInClassList(TradingClass, this.hold != null);
            section.style.display = DisplayStyle.Flex;
            Refresh();
        }

        public void Hide()
        {
            Unbind();
            section.EnableInClassList(TradingClass, false);
            section.style.display = DisplayStyle.None;
        }

        /// <summary>Buys up to that many barrels of a good into the shown hold; nothing without one.</summary>
        public void Buy(int good, int barrels)
        {
            if (hold != null && good < market.GoodCount)
            {
                Trade.Buy(market, hold, treasury, good, barrels);
            }
        }

        /// <summary>Sells up to that many barrels of a good from the shown hold; nothing without one.</summary>
        public void Sell(int good, int barrels)
        {
            if (hold != null && good < market.GoodCount)
            {
                Trade.Sell(market, hold, treasury, good, barrels);
            }
        }

        private VisualElement BuildRow(int good, string goodName)
        {
            var element = new VisualElement();
            element.AddToClassList(RowClass);

            Label name = AddLabel(element, NameCellClass);
            name.text = goodName;

            var row = new Row
            {
                Stock = AddLabel(element, NumberCellClass),
                BuyPrice = AddLabel(element, NumberCellClass),
                SellPrice = AddLabel(element, NumberCellClass),
                Held = AddLabel(element, NumberCellClass, TradeCellClass),
            };

            row.Sell = AddButton(element, "−", evt => Sell(good, BarrelsFor(evt.shiftKey, evt.ctrlKey)));
            row.Buy = AddButton(element, "+", evt => Buy(good, BarrelsFor(evt.shiftKey, evt.ctrlKey)));
            rows.Add(row);
            return element;
        }

        private static Label AddLabel(VisualElement parent, params string[] classes)
        {
            var label = new Label();
            label.AddToClassList(CellClass);

            foreach (string className in classes)
            {
                label.AddToClassList(className);
            }

            parent.Add(label);
            return label;
        }

        // A click event, not Button.clicked: the modifier keys set the quantity.
        private static Button AddButton(VisualElement parent, string text, EventCallback<ClickEvent> onClick)
        {
            var button = new Button { text = text };

            // Buttons are clicked, not navigated: the keyboard pans the map.
            button.focusable = false;
            button.AddToClassList(ButtonClass);
            button.AddToClassList(TradeCellClass);
            button.RegisterCallback(onClick);
            parent.Add(button);
            return button;
        }

        private void Unbind()
        {
            if (market != null)
            {
                market.Changed -= Refresh;
            }

            if (hold != null)
            {
                hold.Changed -= Refresh;
                treasury.Changed -= OnGoldChanged;
            }

            market = null;
            hold = null;
        }

        private void OnGoldChanged(long gold)
        {
            Refresh();
        }

        // A trade changes the market, the hold and the gold: three rewrites of a few labels.
        private void Refresh()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];

                // A market made from another catalogue than the rows: nothing to show there.
                bool isInMarket = i < market.GoodCount;
                int available = isInMarket ? market.AvailableOf(i) : 0;
                long buyPrice = isInMarket ? market.BuyPriceOf(i) : 0;
                int held = hold != null ? hold.BarrelsOf(i) : 0;

                row.Stock.text = TreasuryHudController.FormatNumber(available);
                row.BuyPrice.text = available > 0 ? TreasuryHudController.FormatNumber(buyPrice) : NoPrice;
                row.SellPrice.text = isInMarket ? TreasuryHudController.FormatNumber(market.SellPriceOf(i)) : NoPrice;
                row.Held.text = TreasuryHudController.FormatNumber(held);

                row.Sell.SetEnabled(isInMarket && held > 0);
                row.Buy.SetEnabled(
                    hold != null && available > 0 && hold.Free > 0 && treasury.CanAfford(buyPrice));
            }

            holdLabel.text = hold != null ? ShipInfoPanelController.FormatCargo(hold) : string.Empty;
        }
    }
}
```

- [ ] **Step 5: Update the city panel's UXML and USS**

Replace `Assets/UI/WorldMap/CityInfoPanel.uxml` with:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="InfoPanel.uss" />
    <Style src="CityInfoPanel.uss" />
    <ui:VisualElement name="map-ui-root" picking-mode="Ignore" class="map-ui-root">
        <ui:Label name="hover-label" picking-mode="Ignore" class="hover-label" />
        <ui:VisualElement name="city-panel" class="info-panel city-panel">
            <ui:VisualElement class="info-panel__header">
                <ui:Label name="city-name" class="info-panel__name" />
                <ui:Button name="close-button" text="X" class="info-panel__close" />
            </ui:VisualElement>
            <ui:Label name="city-size" class="city-panel__tag" />
            <ui:Label name="city-access" class="city-panel__tag" />
            <ui:Label name="city-population" class="city-panel__tag" />
            <ui:Label name="city-description" class="city-panel__description" />
            <ui:Label text="Navires au port" class="city-panel__section" />
            <ui:Label name="no-ship-label" text="Aucun navire au port" class="city-panel__empty" />
            <ui:VisualElement name="ship-list" />
            <ui:VisualElement name="market-section" class="market">
                <ui:VisualElement class="city-panel__section market__title">
                    <ui:Label text="Marché" />
                    <ui:Label name="market-hold" class="market__hold" />
                </ui:VisualElement>
                <ui:VisualElement class="market__row market__row--header">
                    <ui:Label text="Marchandise" class="market__cell market__cell--name" />
                    <ui:Label text="Stock" class="market__cell market__cell--number" />
                    <ui:Label text="Achat" class="market__cell market__cell--number" />
                    <ui:Label text="Vente" class="market__cell market__cell--number" />
                    <ui:Label text="Cale" class="market__cell market__cell--number market__cell--trade" />
                    <ui:VisualElement class="market__buttons-space market__cell--trade" />
                </ui:VisualElement>
                <ui:VisualElement name="market-rows" />
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

Append to `Assets/UI/WorldMap/CityInfoPanel.uss`:

```css

/* Wider than the ship panel: the market is a table. */
.city-panel {
    width: 480px;
}

.market__title {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
}

.market__hold {
    color: rgb(190, 164, 120);
    font-size: 14px;
    -unity-font-style: normal;
}

.market__row {
    flex-direction: row;
    align-items: center;
    height: 26px;
}

.market__row--header {
    color: rgb(190, 164, 120);
}

.market__cell {
    margin: 0;
    padding: 0;
    color: rgb(222, 210, 188);
    font-size: 14px;
}

.market__row--header .market__cell {
    color: rgb(190, 164, 120);
    font-size: 13px;
}

.market__cell--name {
    flex-grow: 1;
    flex-shrink: 1;
}

.market__cell--number {
    width: 62px;
    -unity-text-align: middle-right;
}

/* The hold column and the buttons only exist while a ship in port trades. */
.market__cell--trade {
    display: none;
}

.market--trading .market__cell--trade {
    display: flex;
}

.market__button {
    width: 26px;
    height: 22px;
    margin: 0 0 0 6px;
    padding: 0;
    color: rgb(222, 210, 188);
    background-color: rgba(60, 44, 30, 0.9);
    border-width: 1px;
    border-radius: 0;
    border-color: rgb(110, 84, 52);
    font-size: 15px;
    -unity-font-style: bold;
}

.market__button:hover {
    background-color: rgb(84, 62, 40);
}

.market__button:active {
    background-color: rgb(110, 84, 52);
}

.market__button:disabled {
    opacity: 0.35;
}

/* As wide as the two buttons of a row and their margins. */
.market__buttons-space {
    width: 64px;
}
```

- [ ] **Step 6: Feed the section from `CityInfoPanelController`**

In `Assets/Scripts/Game/CityInfoPanelController.cs`:

Add the serialized fields after `shipsView`:

```csharp

        [Tooltip("Optional. Without it, the panel shows no market.")]
        [SerializeField] private WorldEconomy worldEconomy;

        [Tooltip("Optional. Without it, the market is only read: nothing is bought or sold.")]
        [SerializeField] private PlayerTreasury playerTreasury;
```

Add the private fields after `closeButton`:

```csharp
        private Label populationLabel;
        private VisualElement marketElement;
        private VisualElement marketRows;
        private Label marketHoldLabel;

        // Made on the first city shown: the world's economy starts after this panel is enabled.
        private CityMarketSection marketSection;
```

In `OnEnable`, after `closeButton = root.Q<Button>("close-button");`, add:

```csharp
            populationLabel = root.Q<Label>("city-population");
            marketElement = root.Q<VisualElement>("market-section");
            marketRows = root.Q<VisualElement>("market-rows");
            marketHoldLabel = root.Q<Label>("market-hold");

            // A document that is enabled again has a new tree: the section is rebuilt in it.
            marketSection = null;
```

and extend the missing-element condition to:

```csharp
            if (cityPanel == null || hoverLabel == null || nameLabel == null || sizeLabel == null
                || accessLabel == null || descriptionLabel == null || noShipLabel == null
                || shipList == null || closeButton == null || populationLabel == null
                || marketElement == null || marketRows == null || marketHoldLabel == null)
```

Still in `OnEnable`, just before `isBound = true;`, add:

```csharp
            // Hidden until a market is shown, also in a scene without an economy.
            marketElement.style.display = DisplayStyle.None;
```

In `OnDisable`, just before `isBound = false;`, add:

```csharp
            // Stops listening to the market, the hold and the treasury.
            marketSection?.Hide();
```

Add the public static method after `IsPointerOverUi`:

```csharp

        /// <summary>The population as the panel writes it: "Population : 6 000".</summary>
        public static string FormatPopulation(int population)
        {
            return $"Population : {TreasuryHudController.FormatNumber(population)}";
        }
```

Replace `ShowSelected`, `OnDockingChanged` and `OnShipSelected` with:

```csharp
        private void ShowSelected(CityDefinition city)
        {
            if (city == null)
            {
                cityPanel.style.display = DisplayStyle.None;
                marketSection?.Hide();
                return;
            }

            nameLabel.text = city.DisplayName;
            sizeLabel.text = SizeText(city.Size);
            accessLabel.text = AccessText(city.Access);
            populationLabel.text = FormatPopulation(city.Population);
            descriptionLabel.text = city.Description;
            ShowShipsIn(city);
            ShowMarketOf(city);
            cityPanel.style.display = DisplayStyle.Flex;
        }
```

```csharp
        private void OnDockingChanged(Ship ship, CityDefinition city)
        {
            if (ReferenceEquals(city, mapView.Selection.Selected))
            {
                ShowShipsIn(city);
                ShowMarketOf(city);
            }
        }

        private void OnShipSelected(Ship ship)
        {
            HighlightSelectedShip();

            // The hold that trades is the selected ship's.
            if (mapView.Selection.Selected != null)
            {
                ShowMarketOf(mapView.Selection.Selected);
            }
        }
```

Add these methods after `HighlightSelectedShip`:

```csharp

        // After ShowShipsIn: the ship that trades is one of the ships listed.
        private void ShowMarketOf(CityDefinition city)
        {
            if (!TryGetMarketSection(out CityMarketSection section))
            {
                return;
            }

            CityMarket market = worldEconomy.MarketOf(city);

            if (market == null)
            {
                section.Hide();
                return;
            }

            section.Show(market, HoldOfSelectedShipInPort());
        }

        private bool TryGetMarketSection(out CityMarketSection section)
        {
            if (marketSection == null && worldEconomy != null && worldEconomy.IsReady)
            {
                var goodNames = new List<string>();

                foreach (GoodDefinition good in worldEconomy.Economy.Goods)
                {
                    goodNames.Add(good.DisplayName);
                }

                Treasury treasury = playerTreasury != null && playerTreasury.IsReady ? playerTreasury.Treasury : null;
                marketSection = new CityMarketSection(marketElement, marketRows, marketHoldLabel, goodNames, treasury);
            }

            section = marketSection;
            return section != null;
        }

        /// <returns>The hold of the selected ship when it lies in the shown city's port, otherwise null.</returns>
        private CargoHold HoldOfSelectedShipInPort()
        {
            Ship selected = shipsView != null ? shipsView.Selection.Selected : null;

            return selected != null && listedShips.Contains(selected) ? selected.Cargo : null;
        }
```

Update the class summary to: `Presents the hovered city's name and the selected city's information panel, with the ships in its port and its market: clicking a ship selects it, so that it can trade or be ordered out. Reads the map's selection state; knows nothing about markers.`

- [ ] **Step 7: Run the tests to verify they pass**

Run all EditMode tests. Expected: exit code `0`.

- [ ] **Step 8: Commit**

```powershell
git add Assets/Scripts/Game Assets/UI/WorldMap Assets/Tests/EditMode
git status --short
git commit -m "Panneau de ville : la population et le marché, avec l'achat et la vente pour un navire au port

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: The hold in the ship panel

**Files:**
- Modify: `Assets/Scripts/Game/ShipInfoPanelController.cs`, `Assets/UI/WorldMap/ShipInfoPanel.uxml`, `Assets/UI/WorldMap/ShipInfoPanel.uss`
- Test: `Assets/Tests/EditMode/ShipInfoPanelControllerTests.cs`

**Interfaces:**
- Consumes: `CargoHold.Changed`, `CargoHold.BarrelsOf`, `Ship.Cargo` (Task 1); `EconomyDefinition.Goods`, `GoodDefinition.DisplayName` (Task 5); `ShipInfoPanelController.FormatCargo`, `TreasuryHudController.FormatNumber` (Task 6).
- Produces: `static string ShipInfoPanelController.FormatCargoLine(string goodName, int barrels)` → `"Vin : 20"`; new optional serialized field `economy` (`EconomyDefinition`) of `ShipInfoPanelController`.

- [ ] **Step 1: Write the failing test**

Add to the existing `ShipInfoPanelControllerTests` class:

```csharp
        [Test]
        public void FormatCargoLine_WritesAGoodAndItsBarrels()
        {
            Assert.AreEqual("Vin : 20", ShipInfoPanelController.FormatCargoLine("Vin", 20));
            Assert.AreEqual("Blé : 1\u00A0200", ShipInfoPanelController.FormatCargoLine("Blé", 1200));
        }
```

- [ ] **Step 2: Run the test to verify it fails**

Run the EditMode tests. Expected: exit code `1`, `error CS0117` for `FormatCargoLine`.

- [ ] **Step 3: Update the ship panel's UXML and USS**

In `Assets/UI/WorldMap/ShipInfoPanel.uxml`, after the `ship-crew` label, add:

```xml
        <ui:Label name="ship-cargo" class="ship-panel__crew" />
        <ui:VisualElement name="ship-cargo-list" class="ship-panel__cargo-list" />
```

Append to `Assets/UI/WorldMap/ShipInfoPanel.uss`:

```css

.ship-panel__cargo-list {
    margin-top: 4px;
    margin-left: 12px;
}

.ship-panel__cargo-line {
    color: rgb(190, 164, 120);
    font-size: 14px;
}
```

- [ ] **Step 4: Show the hold in `ShipInfoPanelController`**

In `Assets/Scripts/Game/ShipInfoPanelController.cs`:

Add after the `shipsView` field:

```csharp

        [Tooltip("Optional. Without it, the panel shows how full the hold is, not what it holds.")]
        [SerializeField] private EconomyDefinition economy;
```

Add the private fields after `crewLabel`:

```csharp
        private Label cargoLabel;
        private VisualElement cargoList;

        // The hold the panel listens to: the shown ship's.
        private CargoHold shownCargo;
```

and the constant at the top of the class:

```csharp
        private const string CargoLineClass = "ship-panel__cargo-line";
```

In `OnEnable`, after `crewLabel = root.Q<Label>("ship-crew");`, add:

```csharp
            cargoLabel = root.Q<Label>("ship-cargo");
            cargoList = root.Q<VisualElement>("ship-cargo-list");
```

and extend the missing-element condition to:

```csharp
            if (shipPanel == null || nameLabel == null || crewLabel == null || closeButton == null
                || cargoLabel == null || cargoList == null)
```

In `OnDisable`, just before `isBound = false;`, add:

```csharp
            ListenTo(null);
```

After `FormatCargo`, add:

```csharp

        /// <summary>One good of the hold as the panel writes it: "Vin : 20".</summary>
        public static string FormatCargoLine(string goodName, int barrels)
        {
            return $"{goodName} : {TreasuryHudController.FormatNumber(barrels)}";
        }
```

Replace `ShowSelected` with:

```csharp
        private void ShowSelected(Ship ship)
        {
            ListenTo(ship?.Cargo);

            if (ship == null)
            {
                shipPanel.style.display = DisplayStyle.None;
                return;
            }

            // Written on selection: nothing changes a crew yet.
            nameLabel.text = shipsView.DisplayNameOf(ship);
            crewLabel.text = FormatCrew(ship.Crew);
            ShowCargo();
            shipPanel.style.display = DisplayStyle.Flex;
        }

        private void ListenTo(CargoHold cargo)
        {
            if (shownCargo != null)
            {
                shownCargo.Changed -= ShowCargo;
            }

            shownCargo = cargo;

            if (shownCargo != null)
            {
                shownCargo.Changed += ShowCargo;
            }
        }

        // Rebuilt on every change: a hold has a line per good aboard, ten at most.
        private void ShowCargo()
        {
            cargoLabel.text = FormatCargo(shownCargo);
            cargoList.Clear();

            if (economy == null)
            {
                return;
            }

            for (int i = 0; i < economy.Goods.Count; i++)
            {
                int barrels = shownCargo.BarrelsOf(i);

                if (barrels < 1 || economy.Goods[i] == null)
                {
                    continue;
                }

                var line = new Label(FormatCargoLine(economy.Goods[i].DisplayName, barrels));
                line.AddToClassList(CargoLineClass);
                cargoList.Add(line);
            }
        }
```

Update the class summary to: `Presents the selected ship's panel, at the bottom left of the screen: its name, its crew and its cargo. Reads the ship selection state; knows nothing about ship views.`

- [ ] **Step 5: Run the tests to verify they pass**

Run all EditMode tests. Expected: exit code `0`.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/Game/ShipInfoPanelController.cs Assets/UI/WorldMap/ShipInfoPanel.uxml Assets/UI/WorldMap/ShipInfoPanel.uss Assets/Tests/EditMode/ShipInfoPanelControllerTests.cs
git commit -m "Panneau du navire : la cale et ce qu'elle contient

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Setup tool and content

**Files:**
- Modify: `Assets/Scripts/Editor/WorldMapSetup.cs`
- Test: `Assets/Tests/EditMode/EconomyContentTests.cs`
- Generated: `Assets/Data/Goods/*.asset`, `Assets/Data/Economy/Economy.asset`, the seven `Assets/Data/Cities/*.asset`, `Assets/Scenes/WorldMap.unity`

**Interfaces:**
- Consumes: the serialized field names of Task 5 (`displayName`, `basePrice`, `consumptionPerThousand` of `GoodDefinition`; `goods` of `EconomyDefinition`; `population`, `efficientGoods`, `inefficientGoods` of `CityDefinition`; `cargoCapacity` of `ShipDefinition`; `economy`, `mapView`, `worldClock` of `WorldEconomy`), of Task 6 (`worldEconomy`, `playerTreasury` of `CityInfoPanelController`) and of Task 7 (`economy` of `ShipInfoPanelController`).
- Produces: the assets and the `World Economy` scene object.

- [ ] **Step 1: Write the failing content tests**

Create `Assets/Tests/EditMode/EconomyContentTests.cs`:

```csharp
using System.Collections.Generic;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>Checks the economy content generated by the world map setup tool.</summary>
    public class EconomyContentTests
    {
        private const string EconomyPath = "Assets/Data/Economy/Economy.asset";
        private const string MapPath = "Assets/Data/WorldMap/WorldMap.asset";
        private const string ShipPath = "Assets/Data/Ships/MerchantShip.asset";

        private static EconomyDefinition LoadEconomy()
        {
            var economy = AssetDatabase.LoadAssetAtPath<EconomyDefinition>(EconomyPath);
            Assert.IsNotNull(economy, EconomyPath);
            return economy;
        }

        private static WorldMapDefinition LoadMap()
        {
            var map = AssetDatabase.LoadAssetAtPath<WorldMapDefinition>(MapPath);
            Assert.IsNotNull(map, MapPath);
            return map;
        }

        [TestCase(0, "Blé", 10, 2.0)]
        [TestCase(1, "Poisson", 12, 1.5)]
        [TestCase(2, "Bois", 14, 1.0)]
        [TestCase(3, "Bière", 15, 1.5)]
        [TestCase(4, "Sel", 18, 0.5)]
        [TestCase(5, "Laine", 30, 0.5)]
        [TestCase(6, "Cuir", 40, 0.3)]
        [TestCase(7, "Fer", 50, 0.4)]
        [TestCase(8, "Vin", 60, 0.6)]
        [TestCase(9, "Épices", 150, 0.1)]
        public void TheCatalogue_HasTheTenGoods_InOrder(int index, string name, long basePrice, double consumption)
        {
            EconomyDefinition economy = LoadEconomy();

            Assert.AreEqual(10, economy.Goods.Count);
            Assert.AreEqual(name, economy.Goods[index].DisplayName);
            Assert.AreEqual(basePrice, economy.Goods[index].BasePrice);
            Assert.AreEqual(consumption, economy.Goods[index].ConsumptionPerThousand, 1e-9);
        }

        [Test]
        public void TheEconomy_IsUsable()
        {
            EconomyDefinition economy = LoadEconomy();

            Assert.IsTrue(economy.TryValidate(out string problem), problem);
            Assert.DoesNotThrow(() => economy.CreatePricing());
        }

        [Test]
        public void EveryCity_HasInhabitants_AndTwoOrThreeGoodsOfEachProduction()
        {
            EconomyDefinition economy = LoadEconomy();

            foreach (CityDefinition city in LoadMap().Cities)
            {
                Assert.Greater(city.Population, 0, city.name);
                Assert.That(city.EfficientGoods.Count, Is.InRange(2, 3), city.name);
                Assert.That(city.InefficientGoods.Count, Is.InRange(2, 3), city.name);

                var seen = new HashSet<GoodDefinition>();

                foreach (GoodDefinition good in city.EfficientGoods)
                {
                    Assert.GreaterOrEqual(economy.IndexOf(good), 0, city.name);
                    Assert.IsTrue(seen.Add(good), $"{city.name}: {good.name} is listed twice");
                }

                foreach (GoodDefinition good in city.InefficientGoods)
                {
                    Assert.GreaterOrEqual(economy.IndexOf(good), 0, city.name);
                    Assert.IsTrue(seen.Add(good), $"{city.name}: {good.name} is listed twice");
                }

                Assert.DoesNotThrow(() => economy.CreateMarket(city, economy.CreatePricing()), city.name);
            }
        }

        [Test]
        public void EveryGood_IsProducedEfficientlySomewhere()
        {
            var produced = new HashSet<GoodDefinition>();

            foreach (CityDefinition city in LoadMap().Cities)
            {
                produced.UnionWith(city.EfficientGoods);
            }

            foreach (GoodDefinition good in LoadEconomy().Goods)
            {
                Assert.IsTrue(produced.Contains(good), good.name);
            }
        }

        [TestCase("Sparia", 7000)]
        [TestCase("Elforth", 1500)]
        [TestCase("Bactfied", 6000)]
        [TestCase("Hitrun", 1800)]
        [TestCase("Cerbias", 1200)]
        [TestCase("Hazer Empire", 24000)]
        [TestCase("Liveria", 18000)]
        public void ACity_HasItsPopulation(string cityName, int population)
        {
            foreach (CityDefinition city in LoadMap().Cities)
            {
                if (city.DisplayName == cityName)
                {
                    Assert.AreEqual(population, city.Population);
                    return;
                }
            }

            Assert.Fail($"{cityName} is not on the map.");
        }

        [Test]
        public void MerchantShip_HoldsTwoHundredBarrels()
        {
            var ship = AssetDatabase.LoadAssetAtPath<ShipDefinition>(ShipPath);
            Assert.IsNotNull(ship, ShipPath);

            Assert.AreEqual(200, ship.CargoCapacity);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the fixture `DarkFantasyMerchant.Tests.EditMode.EconomyContentTests`. Expected: exit code `2`; the tests fail on `Assets/Data/Economy/Economy.asset` being null and on cities without inhabitants. `MerchantShip_HoldsTwoHundredBarrels` already passes (the field's default).

- [ ] **Step 3: Add the content to `WorldMapSetup`**

In `Assets/Scripts/Editor/WorldMapSetup.cs`:

Add the constants after `ExpensesPath`:

```csharp
        private const string EconomyPath = EconomyFolder + "/Economy.asset";
        private const string GoodsFolder = "Assets/Data/Goods";
```

and after `MerchantShipCrewCapacity`:

```csharp
        private const int MerchantShipCargoCapacity = 200;
```

Add the sample goods above the `SampleCity` struct:

```csharp
        private readonly struct SampleGood
        {
            public SampleGood(string assetName, string displayName, long basePrice, double consumptionPerThousand)
            {
                AssetName = assetName;
                DisplayName = displayName;
                BasePrice = basePrice;
                ConsumptionPerThousand = consumptionPerThousand;
            }

            public string AssetName { get; }
            public string DisplayName { get; }
            public long BasePrice { get; }
            public double ConsumptionPerThousand { get; }
        }

        // In the order of the catalogue, which is their order on screen: the cheapest first.
        private static readonly SampleGood[] SampleGoods =
        {
            new SampleGood("Wheat", "Blé", 10, 2.0),
            new SampleGood("Fish", "Poisson", 12, 1.5),
            new SampleGood("Wood", "Bois", 14, 1.0),
            new SampleGood("Beer", "Bière", 15, 1.5),
            new SampleGood("Salt", "Sel", 18, 0.5),
            new SampleGood("Wool", "Laine", 30, 0.5),
            new SampleGood("Leather", "Cuir", 40, 0.3),
            new SampleGood("Iron", "Fer", 50, 0.4),
            new SampleGood("Wine", "Vin", 60, 0.6),
            new SampleGood("Spices", "Épices", 150, 0.1),
        };
```

Replace the `SampleCity` struct and the `SampleCities` array with:

```csharp
        private readonly struct SampleCity
        {
            public SampleCity(
                string name,
                float x,
                float y,
                CityAccess access,
                CitySize size,
                int population,
                string[] efficientGoods,
                string[] inefficientGoods,
                string description)
            {
                Name = name;
                Position = new Vector2(x, y);
                Access = access;
                Size = size;
                Population = population;
                EfficientGoods = efficientGoods;
                InefficientGoods = inefficientGoods;
                Description = description;
            }

            public string Name { get; }
            public Vector2 Position { get; }
            public CityAccess Access { get; }
            public CitySize Size { get; }
            public int Population { get; }

            /// <summary>Asset names of the goods produced well above the consumption.</summary>
            public string[] EfficientGoods { get; }

            /// <summary>Asset names of the goods produced barely above the consumption.</summary>
            public string[] InefficientGoods { get; }

            public string Description { get; }
        }

        // Positions are read off the placeholder image and are meant to be refined
        // with the placement tool. Every good is produced efficiently by at least one city.
        private static readonly SampleCity[] SampleCities =
        {
            new SampleCity("Sparia", 0.115f, 0.645f, CityAccess.Coastal, CitySize.Town, 7000,
                new[] { "Wool", "Wine" }, new[] { "Fish", "Beer" },
                "Une ville portuaire battue par les vents sur le cap occidental, première terre en vue des navires qui traversent la mer d'Aedean occidentale."),
            new SampleCity("Elforth", 0.196f, 0.609f, CityAccess.Coastal, CitySize.Village, 1500,
                new[] { "Fish", "Salt" }, new[] { "Wood", "Wool" },
                "Un village de pêcheurs abrité par les bois du sud, connu pour sa morue salée et ses contrebandiers discrets."),
            new SampleCity("Bactfied", 0.374f, 0.554f, CityAccess.Coastal, CitySize.Town, 6000,
                new[] { "Wood", "Iron" }, new[] { "Wheat", "Salt", "Leather" },
                "Un port marchand animé sur la mer d'Eamiq, où les barges fluviales rencontrent les navires de haute mer."),
            new SampleCity("Hitrun", 0.470f, 0.598f, CityAccess.Coastal, CitySize.Village, 1800,
                new[] { "Wood", "Leather" }, new[] { "Fish", "Iron" },
                "Un village perché au-dessus d'une crique étroite, qui vend laine et pierre aux caboteurs de passage."),
            new SampleCity("Cerbias", 0.554f, 0.500f, CityAccess.Coastal, CitySize.Village, 1200,
                new[] { "Salt", "Spices" }, new[] { "Fish", "Wine" },
                "Un mouillage isolé à la pointe de la langue de sable méridionale, dernier abri avant le large de la mer de Rakmitag."),
            new SampleCity("Hazer Empire", 0.586f, 0.627f, CityAccess.Coastal, CitySize.Capital, 24000,
                new[] { "Iron", "Wine", "Spices" }, new[] { "Wheat", "Fish", "Beer" },
                "La capitale impériale qui garde le détroit, dont les douanes taxent chaque navire en route vers la côte du désert."),
            new SampleCity("Liveria", 0.345f, 0.651f, CityAccess.River, CitySize.Capital, 18000,
                new[] { "Wheat", "Beer", "Leather" }, new[] { "Wood", "Wool", "Wine" },
                "Une capitale fluviale hérissée de clochers au cœur des Bois de la Couronne, que l'on ne rejoint qu'en barge."),
        };
```

In `Build`, after `CreateExpenses();`, add:

```csharp
            CreateEconomy();
            FillCityEconomies();
```

and after `AddExpensesToScene(mapSceneHadUnsavedChanges);`, add:

```csharp
            AddEconomyToScene(mapSceneHadUnsavedChanges);
```

In `EnsureFolders`, add `GoodsFolder` to the list, after `EconomyFolder,`:

```csharp
                EconomyFolder, GoodsFolder,
```

In `CreateShipDefinition`, after the `crewCapacity` line, add:

```csharp
            serialized.FindProperty("cargoCapacity").intValue = MerchantShipCargoCapacity;
```

Add these methods after `CreateExpenses`:

```csharp

        // The goods are created even when the economy exists, so that one deleted by mistake comes back.
        private static EconomyDefinition CreateEconomy()
        {
            var goods = new GoodDefinition[SampleGoods.Length];

            for (int i = 0; i < SampleGoods.Length; i++)
            {
                goods[i] = CreateGood(SampleGoods[i]);
            }

            var existing = AssetDatabase.LoadAssetAtPath<EconomyDefinition>(EconomyPath);

            if (existing != null)
            {
                return existing;
            }

            // Filled in before the asset is created, so the file is written complete. The
            // settings keep their defaults: like the calendar's, they are the game's.
            var economy = ScriptableObject.CreateInstance<EconomyDefinition>();
            var serialized = new SerializedObject(economy);
            SerializedProperty catalogue = serialized.FindProperty("goods");
            catalogue.arraySize = goods.Length;

            for (int i = 0; i < goods.Length; i++)
            {
                catalogue.GetArrayElementAtIndex(i).objectReferenceValue = goods[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(economy, EconomyPath);
            return economy;
        }

        private static GoodDefinition CreateGood(SampleGood sample)
        {
            string path = GoodPath(sample.AssetName);
            var existing = AssetDatabase.LoadAssetAtPath<GoodDefinition>(path);

            if (existing != null)
            {
                return existing;
            }

            var good = ScriptableObject.CreateInstance<GoodDefinition>();
            var serialized = new SerializedObject(good);
            serialized.FindProperty("displayName").stringValue = sample.DisplayName;
            serialized.FindProperty("basePrice").longValue = sample.BasePrice;
            serialized.FindProperty("consumptionPerThousand").doubleValue = sample.ConsumptionPerThousand;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(good, path);
            return good;
        }

        private static string GoodPath(string assetName)
        {
            return $"{GoodsFolder}/{assetName}.asset";
        }

        // A city without inhabitants has no economy yet: one made before the economy existed, or
        // just created. A city that has a population is the user's and is left untouched.
        private static void FillCityEconomies()
        {
            var definition = AssetDatabase.LoadAssetAtPath<WorldMapDefinition>(DefinitionPath);

            if (definition == null)
            {
                return;
            }

            foreach (CityDefinition city in definition.Cities)
            {
                if (city == null || city.Population != 0)
                {
                    continue;
                }

                foreach (SampleCity sample in SampleCities)
                {
                    if (sample.Name == city.DisplayName)
                    {
                        FillCityEconomy(city, sample);
                        break;
                    }
                }
            }
        }

        private static void FillCityEconomy(CityDefinition city, SampleCity sample)
        {
            var serialized = new SerializedObject(city);
            serialized.FindProperty("population").intValue = sample.Population;
            SetGoods(serialized.FindProperty("efficientGoods"), sample.EfficientGoods);
            SetGoods(serialized.FindProperty("inefficientGoods"), sample.InefficientGoods);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(city);
        }

        private static void SetGoods(SerializedProperty list, string[] assetNames)
        {
            list.arraySize = assetNames.Length;

            for (int i = 0; i < assetNames.Length; i++)
            {
                string path = GoodPath(assetNames[i]);
                var good = AssetDatabase.LoadAssetAtPath<GoodDefinition>(path);

                if (good == null)
                {
                    throw new FileNotFoundException("A good could not be loaded.", path);
                }

                list.GetArrayElementAtIndex(i).objectReferenceValue = good;
            }
        }
```

Add this method after `AddExpensesToScene`:

```csharp

        // Like the expenses, the world's economy is added to a scene built before it existed, and
        // the panels that show it are told where it is.
        private static void AddEconomyToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The world's economy", out Scene scene))
            {
                return;
            }

            var mapView = FindInScene<WorldMapView>(scene);
            var worldClock = FindInScene<WorldClock>(scene);

            // Cities are the map's, and they produce and consume as the days pass.
            if (mapView == null || worldClock == null)
            {
                Debug.LogWarning($"{ScenePath} has no WorldMapView or WorldClock; world economy not added.");
                return;
            }

            // Loaded only now: opening a scene unloads unreferenced assets.
            var economy = AssetDatabase.LoadAssetAtPath<EconomyDefinition>(EconomyPath);

            if (economy == null)
            {
                throw new FileNotFoundException("The economy could not be loaded.", EconomyPath);
            }

            bool changed = false;
            var worldEconomy = FindInScene<WorldEconomy>(scene);

            if (worldEconomy == null)
            {
                var economyObject = new GameObject("World Economy");
                SceneManager.MoveGameObjectToScene(economyObject, scene);

                worldEconomy = economyObject.AddComponent<WorldEconomy>();
                changed = true;
            }

            // Also for a scene in which one of them was deleted and made again.
            changed |= FillReference(worldEconomy, "economy", economy);
            changed |= FillReference(worldEconomy, "mapView", mapView);
            changed |= FillReference(worldEconomy, "worldClock", worldClock);

            // The city panel shows the market and trades with the player's gold.
            var cityPanel = FindInScene<CityInfoPanelController>(scene);

            if (cityPanel != null)
            {
                changed |= FillReference(cityPanel, "worldEconomy", worldEconomy);
                changed |= FillReference(cityPanel, "playerTreasury", FindInScene<PlayerTreasury>(scene));
            }

            // The ship panel names the goods of the hold.
            var shipPanel = FindInScene<ShipInfoPanelController>(scene);

            if (shipPanel != null)
            {
                changed |= FillReference(shipPanel, "economy", economy);
            }

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The world's economy");
            }
        }
```

Update the class summary's list of objects an existing scene gains: add `world economy` after `player expenses`, and add the sentence `Cities of the map that have no inhabitant are given the population and the productions of the sample content.`

- [ ] **Step 4: Run the setup tool**

Run the world map setup tool (see Commands). Expected: exit code `0` and `World map setup finished.` in `Logs\setup.log`, no `Exception`.

Then check what it wrote:

```powershell
git status --short
```

Expected: ten new `Assets/Data/Goods/*.asset` with their `.meta`, `Assets/Data/Goods.meta`, `Assets/Data/Economy/Economy.asset` with its `.meta`, the seven `Assets/Data/Cities/*.asset` modified, `Assets/Scenes/WorldMap.unity` modified. `Assets/Data/Ships/MerchantShip.asset` may or may not be rewritten with `cargoCapacity: 200`; both are fine.

Confirm one city by eye:

```powershell
Select-String -Path Assets\Data\Cities\Sparia.asset -Pattern 'population|efficientGoods|inefficientGoods' -Context 0,3
```

Expected: `population: 7000`, two entries under `efficientGoods`, two under `inefficientGoods`.

- [ ] **Step 5: Run the tests to verify they pass**

Run all EditMode tests. Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/Editor/WorldMapSetup.cs Assets/Data Assets/Scenes/WorldMap.unity Assets/Tests/EditMode/EconomyContentTests.cs Assets/Tests/EditMode/EconomyContentTests.cs.meta
git status --short
git commit -m "Contenu de l'économie : dix marchandises, populations et productions des villes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Check in the Editor and document

**Files:**
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: everything above.
- Produces: nothing.

- [ ] **Step 1: Play the game**

Open the project in the Unity Editor (or use the `unity-mcp` tools if it is open), open `Assets/Scenes/WorldMap.unity`, check the console is free of errors after compilation, and enter Play mode. Check each line; fix what fails before going on (a failure here is a bug: use superpowers:systematic-debugging).

1. Console: no error and no warning from `WorldEconomy`.
2. Click a city with no ship in port: the panel shows "Population : …", the "Marché" section with ten rows (Blé first, Épices last), stock, "Achat" and "Vente" columns, **no** "Cale" column and no buttons. The panel fits on the screen.
3. Select the ship and right-click the city it starts beside: it enters the port. Click the city, then the ship's row: the "Cale" column and the [−] [+] buttons appear, the header reads "Cale : 0 / 200 tonneaux", and the ship panel at the bottom left shows the same line.
4. Click [+] on a good the city produces: one barrel moves, the gold at the top right falls by the "Achat" price that was shown, the stock falls by one, the price may rise. Shift+click buys 10, Ctrl+click buys up to 100.
5. Fill the hold to 200: every [+] is disabled. The ship panel lists what is aboard ("Vin : 20").
6. Click [−] on a good aboard: it sells for less than it was bought. A good the ship does not carry has its [−] disabled.
7. Click the ship's row again (deselect): the hold column and the buttons disappear; the market stays.
8. Close the panel, sail to a city that does not produce what is aboard, enter its port and sell: the price is higher than where it was bought.
9. With a city panel open and no fast forward, wait for a day to start (30 seconds): the stocks of the goods it does not produce fall.
10. Start a fast forward for a few weeks, stop it, open a city that produces nothing of some good: that stock is at or near 0, its "Achat" reads "—" at 0, and its "Vente" price is about 2.4 times the base price.
11. The right click that orders the ship out of a port still closes the panel, and the map under the panel does not receive the clicks made on the [−] [+] buttons.

- [ ] **Step 2: Update `CLAUDE.md`**

In the **Project state** paragraph, replace `and notifications tell the player what happens (so far, a ship that arrives and the expenses of a week); nothing else of the game exists yet.` with:

```markdown
notifications tell the player what happens (so far, a ship that arrives and the expenses of a week), and cities hold stocks of ten goods that they consume and partly produce, which a ship in port buys into its hold and sells elsewhere, at prices that follow the stocks; nothing else of the game exists yet.
```

In the **World map** section, in the `Build World Map Scene` bullet: add `the goods, the economy` to the list of assets recreated when missing (after `the player start or the expenses`); add `World Economy` to the list of objects also added to an existing scene; add to the list of empty references `` `economy`, `mapView` and `worldClock` of `WorldEconomy`, `worldEconomy` and `playerTreasury` of `CityInfoPanelController`, `economy` of `ShipInfoPanelController` ``; and add at the end of the bullet: `Cities of the map with a population of 0 are given the population and the productions of the sample content, whether the tool has just created them or not.`

In the **Ships** section, in the `ShipInfoPanelController` bullet, replace `its name, its crew and a close button that deselects it` with `its name, its crew, its hold and a close button that deselects it`.

In the **Treasury** section, replace `Nothing deposits yet; the only withdrawal is the weekly expenses (see Expenses).` with `Selling goods deposits and buying them withdraws (see Economy); the other withdrawal is the weekly expenses (see Expenses).`

Add this section after **Expenses**:

```markdown
### Economy

- Goods are counted in barrels ("tonneaux"), all of the same size. `GoodDefinition` (Game, `Assets/Data/Goods`) is a good: its name, its base price and what 1 000 inhabitants consume of it in a day. `EconomyDefinition` (`Assets/Data/Economy/Economy.asset`) holds the catalogue, whose order is the order of every list of goods on screen, and the settings below. In `Core` a good is its index in the catalogue.
- `CityDefinition` holds a population, fixed for now, and the goods the city produces efficiently and inefficiently (2-3 of each); it depends on trade for the others. `WorldEconomy` owns one `CityMarket` (Core) per city of the map, created in `Start` (the map view lists its cities in `Awake`), and calls their `StartDay` on `GameClock.DayStarted`.
- A day of a city, per good: it produces (150 % of its consumption when efficient, 110 % when inefficient), which never raises the stock above a ceiling (60 and 36 days of consumption), then consumes, down to nothing. There is no limit to what a city stores: what the player delivers always adds, and a stock above its ceiling is only consumed. Stocks are fractional; what is shown and can be bought is the whole part. At the start a produced good is at its ceiling and the others at the target.
- `MarketPricing` (Core): the price is the base price times a multiplier of the stock against a target of 30 days of consumption: 2.5 at an empty stock, 1 at the target, 0.5 at twice the target and above, in straight lines. A barrel is bought at the price once it is gone plus 5 %, rounded up, and sold at the price before it is in minus 5 %, rounded down, so buying and selling back always loses gold.
- `Trade` (Core) buys and sells between a market, a `CargoHold` and the `Treasury`: barrels are priced one by one, and a trade of several stops at the first that cannot be traded (stock, room in the hold, gold; a treasury in debt buys nothing but still sells). It moves them at once, so the market and the hold raise `Changed` once per trade.
- `CargoHold` (Core) is the barrels aboard a ship out of its capacity (`ShipDefinition.cargoCapacity`, 200 for the merchant ship); `Ship.Cargo` holds it.
- The city panel shows the population and the market (`CityMarketSection`, fed by `CityInfoPanelController`): stock, buy and sell price of the next barrel for every good, read-only. When a ship in port is selected in the panel's list, the section also shows that ship's hold and the [−] / [+] buttons: a click trades 1 barrel, Shift 10, Ctrl 100, cut down to what is possible. The hold column and the buttons are shown by the `market--trading` class. The section listens to the market, the hold and the treasury while it is shown.
- The ship panel shows the hold ("Cale : 60 / 200 tonneaux") and a line per good aboard, rewritten on the hold's `Changed`.
- Production is a share of a city's own consumption, so villages export little and the world produces less of most goods than it consumes: cities that do not produce a good sit near the maximum price. Every number is a field of an asset, meant to be tuned by playing.
```

- [ ] **Step 3: Commit**

```powershell
git add CLAUDE.md
git commit -m "CLAUDE.md : l'économie, phase 1

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 4: Review before reporting done**

Run a subagent code review of the branch against `main` (the user's standing rule: a review after each feature, before reporting done), fix what it finds, run all EditMode tests once more, then use superpowers:finishing-a-development-branch.
