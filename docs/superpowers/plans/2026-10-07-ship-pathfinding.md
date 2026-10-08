# Ship Pathfinding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ships sail only on the navigable cells of the map's navigation mask, following the shortest route over water to the clicked point.

**Architecture:** Three plain C# classes in `Core`. `NavigationLineOfSight` says whether a straight leg stays on water. `NavigationCellSearch` is an A\* search over the cells of a `NavigationGrid`. `NavigationPathfinder` labels the water regions, resolves where a route starts and ends, runs the search and removes the waypoints a straight leg can skip. `Ship` optionally holds a pathfinder and follows a list of waypoints instead of a single destination. `ShipsView` builds the pathfinder from the map's mask.

**Tech Stack:** Unity 6000.6.4f1, C#, Unity Test Framework (NUnit, EditMode).

**Spec:** `docs/superpowers/specs/2026-10-07-ship-pathfinding-design.md`

## Global Constraints

- All code, comments, log messages, asset and folder names are in English.
- `Core` contains no `MonoBehaviour` and nothing that needs a scene. It may use `UnityEngine` value types (`Vector2`, `Mathf`).
- Map positions are normalized (`(0, 0)` bottom-left, `(1, 1)` top-right) and go to world space only through `MapProjection`. Grid cell `(0, 0)` is the bottom-left one.
- ScriptableObjects are never written while the game runs.
- A map without a navigation mask stays valid: its ships sail in a straight line, as today. The existing `ShipTests` must pass **unchanged**.
- Ship speed stays in world units per second, the same in every direction.
- Movement rules (spec, "Movement rules"): a side move needs the neighbour navigable; a diagonal move needs the diagonal cell **and both side cells next to the move** navigable; a straight leg needs **every cell it touches** navigable, including by an edge or a corner. Costs: `1` for a side move, `√2` for a diagonal one.
- `WorldMapInteraction` and `WorldMapSetup` are not modified. No new asset, prefab or scene object.
- Never hand-write `.meta` files. Unity generates them on import; commit them together with their file.
- Never edit the generated `.sln` / `.csproj`. Exclude `Library/` from searches.
- Match the surrounding code: Allman braces, a blank line before `return` / `if` blocks as in the existing files, XML `<summary>` on public types, comments only where the reason is not obvious.
- Spec additions, intentional:
  - `NavigationLineOfSight` and `NavigationCellSearch` are public classes of their own, so the movement rules and the search are tested directly.
  - A leg must stay `0.001` cell away from land (`NavigationLineOfSight.Margin`). Normalized positions are `float`s, so a cell center is only known to about `0.0001` cell; without the margin, "touches a corner" would depend on rounding.
  - The search weighs its heuristic by `1.001`. In open water every cell on the way has the same estimate, and the plain heuristic would expand all of them; the weight makes it prefer cells nearer the goal. The grid route may be up to 0.1 % longer than the shortest one, before smoothing.
  - When two cells are equally near a point, the nearest-cell lookup returns the one with the lowest index (lowest row, then lowest column).
  - `NavigationPathfinder.TryFindPath` throws `ArgumentNullException` for a null list.
  - Smoothing is greedy: a waypoint is kept as soon as the point after it stops being visible from the last kept one. Routes are valid but not always the shortest any-angle ones.
- Work on branch `ship-pathfinding` (already created; it holds the spec and this plan).
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

To run one fixture, append `'-testFilter','DarkFantasyMerchant.Tests.EditMode.NavigationLineOfSightTests'` to the argument list.

Reading the result:

- Exit code `0` and `result="Passed"` in the `<test-run` line: all tests passed.
- Exit code `2`: at least one test failed. Details: `Select-String -Path Logs\TestResults.xml -Pattern 'result="Failed"' -Context 0,6`.
- Exit code `1` and no `TestResults.xml`: compilation failed. Details: `Select-String -Path Logs\test.log -Pattern 'error CS'`.

A test file that references a type or member that does not exist yet fails the **whole compilation** (exit code `1`), not just that test. That is the expected "red" for the first step of each task.

A run takes one to three minutes. The first run after adding files also generates their `.meta` files; add them to the commit.

## Review Focus

Inputs the spec implies but does not spell out, most likely first. Each one is pinned by a test in the task named.

1. **A new order while the ship is under way, from a position that is not a cell center** (the normal way to play: the player clicks again before the ship arrives). The route must be valid from wherever the ship is. Tests in Task 4 (`AStartThatIsNotACellCenter_…`) and Task 5 (`AnOrderUnderWay_…`).
2. **A frame that lasts very long** (the window was dragged, a breakpoint, an infinite delta): a ship on a route of several legs must end exactly on its last waypoint, without overshooting and without looping forever. Tests in Task 5 (`Advance_WithAHugeDeltaTime_…`).
3. **A click on the very edge or corner of the map**, where a normalized coordinate is exactly `1`: it belongs to the last cell and is reached, not treated as outside the map. Tests in Task 1 (`TheEdgeOfTheMap_IsNotLand`) and Task 4 (`ADestinationOnTheCornerOfTheMap_…`).
4. **A search across a full-size grid** (1024 × 879, the size of the real mask), in open water and around a wall that spans the map: it completes and returns a legal route. Tests in Task 2 (`AFullSizeGrid_…`) and Task 4 (`AFullSizeGrid_OfOpenWater_IsCrossedInOneLeg`).
5. **A ship in a small closed water region ordered to the open sea:** it sails to the point of its own region nearest to the click and stops there, instead of failing or crossing land. Tests in Task 4 (`ADestinationInAnotherRegion_…`) and Task 5 (`AnOrderOnLand_…`).

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/Core/NavigationLineOfSight.cs` | Create | Whether a straight leg stays on water. |
| `Assets/Scripts/Core/NavigationCellSearch.cs` | Create | A\* over the cells of a grid. |
| `Assets/Scripts/Core/NavigationPathfinder.cs` | Create | Regions, nearest water, route resolution and smoothing. |
| `Assets/Scripts/Core/Ship.cs` | Modify | Follows a route; optional pathfinder. |
| `Assets/Scripts/Game/ShipsView.cs` | Modify | Builds the pathfinder from the map's mask. |
| `Assets/Tests/EditMode/GridAssert.cs` | Modify | `FromRows`, `CellPoint` helpers. |
| `Assets/Tests/EditMode/NavigationLineOfSightTests.cs` | Create | |
| `Assets/Tests/EditMode/NavigationCellSearchTests.cs` | Create | |
| `Assets/Tests/EditMode/NavigationPathfinderTests.cs` | Create | |
| `Assets/Tests/EditMode/ShipNavigationTests.cs` | Create | `Ship` with a pathfinder. |
| `Assets/Tests/EditMode/NavigationContentTests.cs` | Create | Routes on the real mask, with timings. |
| `CLAUDE.md` | Modify | Ships and Navigation mask sections. |

---

### Task 1: Line of sight

**Files:**
- Modify: `Assets/Tests/EditMode/GridAssert.cs`
- Create: `Assets/Scripts/Core/NavigationLineOfSight.cs`
- Test: `Assets/Tests/EditMode/NavigationLineOfSightTests.cs`

**Interfaces:**
- Consumes: `NavigationGrid.IsNavigable(int x, int y)` (false outside the grid), `NavigationGrid.Width`, `NavigationGrid.Height`, `MapProjection.IsInsideMap(Vector2)` (static; false for NaN).
- Produces:
  - `public static bool NavigationLineOfSight.IsClear(NavigationGrid grid, Vector2 from, Vector2 to)` — normalized points.
  - Test helpers `GridAssert.FromRows(params string[] rows)` and `GridAssert.CellPoint(NavigationGrid grid, float x, float y)`.

- [ ] **Step 1: Add the test helpers**

In `Assets/Tests/EditMode/GridAssert.cs`, add these two methods after `CellCenter`:

```csharp
        /// <summary>
        /// Normalized position of a point given in cells: cell (x, y) spans x to x + 1.
        /// </summary>
        public static Vector2 CellPoint(NavigationGrid grid, float x, float y)
        {
            return new Vector2(x / grid.Width, y / grid.Height);
        }

        /// <summary>
        /// Builds a grid from rows of text, the top row first: '.' is water, any other
        /// character is land.
        /// </summary>
        public static NavigationGrid FromRows(params string[] rows)
        {
            var grid = new NavigationGrid(rows[0].Length, rows.Length);

            for (int row = 0; row < rows.Length; row++)
            {
                int y = rows.Length - 1 - row;

                for (int x = 0; x < rows[row].Length; x++)
                {
                    if (rows[row][x] == '.')
                    {
                        grid.SetNavigable(x, y, true);
                    }
                }
            }

            return grid;
        }
```

- [ ] **Step 2: Write the failing tests**

Create `Assets/Tests/EditMode/NavigationLineOfSightTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationLineOfSightTests
    {
        private static bool IsClear(NavigationGrid grid, int fromX, int fromY, int toX, int toY)
        {
            return NavigationLineOfSight.IsClear(
                grid,
                GridAssert.CellCenter(grid, fromX, fromY),
                GridAssert.CellCenter(grid, toX, toY));
        }

        [Test]
        public void ANullGrid_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => NavigationLineOfSight.IsClear(null, Vector2.zero, Vector2.one));
        }

        [Test]
        public void OpenWater_IsClear()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "....",
                "....",
                "....");

            Assert.IsTrue(IsClear(grid, 0, 0, 3, 2));
            Assert.IsTrue(IsClear(grid, 3, 2, 0, 0));
            Assert.IsTrue(IsClear(grid, 0, 2, 3, 0));
        }

        [Test]
        public void ALandCellOnTheWay_Blocks()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "....",
                "..#.",
                "....");

            Assert.IsFalse(IsClear(grid, 0, 1, 3, 1));
            Assert.IsFalse(IsClear(grid, 3, 1, 0, 1));
        }

        [Test]
        public void LandBesideTheLine_DoesNotBlock()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "####",
                "....",
                "####");

            Assert.IsTrue(IsClear(grid, 0, 1, 3, 1));
        }

        [Test]
        public void ADiagonalGapBetweenTwoLandCells_Blocks()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "#.",
                ".#");

            Assert.IsFalse(IsClear(grid, 0, 0, 1, 1));
            Assert.IsFalse(IsClear(grid, 1, 1, 0, 0));
        }

        [Test]
        public void GrazingTheCornerOfALandCell_Blocks()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "..",
                ".#");

            Assert.IsFalse(IsClear(grid, 0, 0, 1, 1));
            Assert.IsFalse(IsClear(grid, 1, 1, 0, 0));
        }

        [Test]
        public void ADiagonalThroughOpenWater_IsClear()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "..",
                "..");

            Assert.IsTrue(IsClear(grid, 0, 0, 1, 1));
            Assert.IsTrue(IsClear(grid, 0, 1, 1, 0));
        }

        [Test]
        public void ALineAlongTheEdgeOfLand_Blocks()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "####",
                "....");

            // On the border between the water row and the land row.
            Assert.IsFalse(NavigationLineOfSight.IsClear(
                grid, GridAssert.CellPoint(grid, 0.5f, 1f), GridAssert.CellPoint(grid, 3.5f, 1f)));
        }

        [Test]
        public void AVerticalLine_IsCheckedCellByCell()
        {
            NavigationGrid open = GridAssert.FromRows(".", ".", ".");
            NavigationGrid blocked = GridAssert.FromRows(".", "#", ".");

            Assert.IsTrue(IsClear(open, 0, 0, 0, 2));
            Assert.IsFalse(IsClear(blocked, 0, 0, 0, 2));
            Assert.IsFalse(IsClear(blocked, 0, 2, 0, 0));
        }

        [Test]
        public void ASteepLine_SeesLandInTheColumnsItCrosses()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "..",
                ".#",
                ".#",
                "..");

            // From the bottom-left cell to the top-right one: through the right column
            // only near the top, but it enters that column inside the land.
            Assert.IsFalse(IsClear(grid, 0, 0, 1, 3));
            Assert.IsTrue(IsClear(grid, 0, 0, 0, 3));
        }

        [Test]
        public void APoint_IsClearOnWater_AndNotOnLand()
        {
            NavigationGrid grid = GridAssert.FromRows(".#");
            Vector2 water = GridAssert.CellCenter(grid, 0, 0);
            Vector2 land = GridAssert.CellCenter(grid, 1, 0);

            Assert.IsTrue(NavigationLineOfSight.IsClear(grid, water, water));
            Assert.IsFalse(NavigationLineOfSight.IsClear(grid, land, land));
        }

        [Test]
        public void AnEndOutsideTheMap_IsNotClear()
        {
            NavigationGrid grid = GridAssert.FromRows("....");
            Vector2 inside = GridAssert.CellCenter(grid, 1, 0);

            Assert.IsFalse(NavigationLineOfSight.IsClear(grid, inside, new Vector2(1.5f, 0.5f)));
            Assert.IsFalse(NavigationLineOfSight.IsClear(grid, new Vector2(0.5f, -0.1f), inside));
        }

        [TestCase(float.NaN, 0.5f)]
        [TestCase(0.5f, float.NaN)]
        public void ANaNEnd_IsNotClear(float x, float y)
        {
            NavigationGrid grid = GridAssert.FromRows("....");
            Vector2 inside = GridAssert.CellCenter(grid, 1, 0);

            Assert.IsFalse(NavigationLineOfSight.IsClear(grid, inside, new Vector2(x, y)));
            Assert.IsFalse(NavigationLineOfSight.IsClear(grid, new Vector2(x, y), inside));
        }

        [Test]
        public void TheEdgeOfTheMap_IsNotLand()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "..",
                "..");

            Assert.IsTrue(NavigationLineOfSight.IsClear(grid, new Vector2(0f, 0f), new Vector2(1f, 1f)));
            Assert.IsTrue(NavigationLineOfSight.IsClear(grid, new Vector2(0f, 0f), new Vector2(1f, 0f)));
            Assert.IsTrue(NavigationLineOfSight.IsClear(grid, new Vector2(1f, 0f), new Vector2(1f, 1f)));
        }

        [Test]
        public void AGridThatIsNotSquare_IsMeasuredInCells()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "........",
                "...#....");

            Assert.IsTrue(IsClear(grid, 0, 1, 7, 1));
            Assert.IsFalse(IsClear(grid, 0, 0, 7, 0));
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run the EditMode tests (see Commands).
Expected: exit code `1`, and `Logs\test.log` contains `error CS0103: The name 'NavigationLineOfSight' does not exist in the current context`.

- [ ] **Step 4: Write the implementation**

Create `Assets/Scripts/Core/NavigationLineOfSight.cs`:

```csharp
using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Whether a ship can sail in a straight line between two points of the map: every
    /// cell the segment touches, even by an edge or a corner, must be navigable.
    /// </summary>
    public static class NavigationLineOfSight
    {
        // In cells. Positions are floats, so a cell center is only known to about a
        // ten-thousandth of a cell: without a margin, whether a segment touches a
        // corner would depend on rounding.
        private const double Margin = 1e-3;

        /// <param name="from">Normalized map position.</param>
        /// <param name="to">Normalized map position.</param>
        /// <returns>False when either point is outside the map or NaN.</returns>
        public static bool IsClear(NavigationGrid grid, Vector2 from, Vector2 to)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            // Also false for a NaN point.
            if (!MapProjection.IsInsideMap(from) || !MapProjection.IsInsideMap(to))
            {
                return false;
            }

            // In cells: cell (x, y) spans x to x + 1.
            double startX = (double)from.x * grid.Width;
            double startY = (double)from.y * grid.Height;
            double deltaX = (double)to.x * grid.Width - startX;
            double deltaY = (double)to.y * grid.Height - startY;

            // Cells outside the grid are skipped: the edge of the map is not land.
            int firstColumn = Math.Max((int)Math.Floor(Math.Min(startX, startX + deltaX) - Margin), 0);
            int lastColumn = Math.Min((int)Math.Floor(Math.Max(startX, startX + deltaX) + Margin), grid.Width - 1);

            for (int x = firstColumn; x <= lastColumn; x++)
            {
                // The part [first, last] of the segment that lies over this column.
                double first = 0.0;
                double last = 1.0;

                if (deltaX != 0.0)
                {
                    double enter = (x - Margin - startX) / deltaX;
                    double exit = (x + 1 + Margin - startX) / deltaX;

                    if (enter > exit)
                    {
                        double swap = enter;
                        enter = exit;
                        exit = swap;
                    }

                    first = Math.Max(first, enter);
                    last = Math.Min(last, exit);

                    if (first > last)
                    {
                        continue;
                    }
                }

                double firstY = startY + deltaY * first;
                double lastY = startY + deltaY * last;
                int firstRow = Math.Max((int)Math.Floor(Math.Min(firstY, lastY) - Margin), 0);
                int lastRow = Math.Min((int)Math.Floor(Math.Max(firstY, lastY) + Margin), grid.Height - 1);

                for (int y = firstRow; y <= lastRow; y++)
                {
                    if (!grid.IsNavigable(x, y))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run the EditMode tests with `'-testFilter','DarkFantasyMerchant.Tests.EditMode.NavigationLineOfSightTests'`.
Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Core/NavigationLineOfSight.cs Assets/Scripts/Core/NavigationLineOfSight.cs.meta Assets/Tests/EditMode/NavigationLineOfSightTests.cs Assets/Tests/EditMode/NavigationLineOfSightTests.cs.meta Assets/Tests/EditMode/GridAssert.cs
git commit -m "Ligne de vue sur le masque de navigation

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Cell search (A\*)

**Files:**
- Create: `Assets/Scripts/Core/NavigationCellSearch.cs`
- Test: `Assets/Tests/EditMode/NavigationCellSearchTests.cs`

**Interfaces:**
- Consumes: `NavigationGrid.IsNavigable(int x, int y)`, `Width`, `Height`, `Clear(bool)`, `SetNavigable(int, int, bool)`; `GridAssert.FromRows` from Task 1.
- Produces:
  - `public NavigationCellSearch(NavigationGrid grid)` — throws `ArgumentNullException` for null.
  - `public bool TryFindPath(int startX, int startY, int goalX, int goalY, List<int> cells)` — clears `cells`, then fills it with cell indexes (`y * grid.Width + x`) from the start cell to the goal cell, both included. False, with `cells` empty, when either cell is not navigable or the goal cannot be reached.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/NavigationCellSearchTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationCellSearchTests
    {
        private const float Sqrt2 = 1.41421356f;

        private static readonly string[] WallWithAGapAtTheTop =
        {
            ".....",
            "..#..",
            "..#..",
            "..#..",
            "..#..",
        };

        /// <summary>
        /// Checks that every step of a path follows the movement rules, and returns the
        /// path's cost.
        /// </summary>
        private static float AssertLegalPath(NavigationGrid grid, List<int> cells)
        {
            float cost = 0f;

            for (int i = 0; i < cells.Count; i++)
            {
                int x = cells[i] % grid.Width;
                int y = cells[i] / grid.Width;

                Assert.IsTrue(grid.IsNavigable(x, y), $"cell ({x}, {y}) is land");

                if (i == 0)
                {
                    continue;
                }

                int previousX = cells[i - 1] % grid.Width;
                int previousY = cells[i - 1] / grid.Width;
                int stepX = x - previousX;
                int stepY = y - previousY;

                Assert.LessOrEqual(Math.Abs(stepX), 1, "step x");
                Assert.LessOrEqual(Math.Abs(stepY), 1, "step y");
                Assert.IsTrue(stepX != 0 || stepY != 0, "a step that does not move");

                if (stepX != 0 && stepY != 0)
                {
                    Assert.IsTrue(grid.IsNavigable(previousX + stepX, previousY), "cut a corner");
                    Assert.IsTrue(grid.IsNavigable(previousX, previousY + stepY), "cut a corner");
                    cost += Sqrt2;
                }
                else
                {
                    cost += 1f;
                }
            }

            return cost;
        }

        [Test]
        public void ANullGrid_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NavigationCellSearch(null));
        }

        [Test]
        public void ANullList_Throws()
        {
            var search = new NavigationCellSearch(GridAssert.FromRows(".."));

            Assert.Throws<ArgumentNullException>(() => search.TryFindPath(0, 0, 1, 0, null));
        }

        [Test]
        public void AStraightCorridor_IsFollowedCellByCell()
        {
            NavigationGrid grid = GridAssert.FromRows(".....");
            var cells = new List<int>();

            bool found = new NavigationCellSearch(grid).TryFindPath(0, 0, 4, 0, cells);

            Assert.IsTrue(found);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, cells);
        }

        [Test]
        public void OpenWater_IsCrossedDiagonally()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "....",
                "....",
                "....",
                "....");
            var cells = new List<int>();

            bool found = new NavigationCellSearch(grid).TryFindPath(0, 0, 3, 3, cells);

            Assert.IsTrue(found);
            CollectionAssert.AreEqual(new[] { 0, 5, 10, 15 }, cells);
        }

        [Test]
        public void AWall_IsSailedAround_ByTheShortestWay()
        {
            NavigationGrid grid = GridAssert.FromRows(WallWithAGapAtTheTop);
            var cells = new List<int>();

            bool found = new NavigationCellSearch(grid).TryFindPath(0, 0, 4, 0, cells);

            Assert.IsTrue(found);
            Assert.AreEqual(0, cells[0], "starts on the start cell");
            Assert.AreEqual(4, cells[cells.Count - 1], "ends on the goal cell");

            // Up to (1, 4), east to (3, 4), down to (4, 0). The corners of the wall
            // cannot be cut, so the gap takes two side moves.
            Assert.AreEqual(8f + 2f * Sqrt2, AssertLegalPath(grid, cells), 1e-3f);
        }

        [Test]
        public void ADiagonalGap_IsNotCrossed()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "...",
                ".#.",
                "..#");
            var cells = new List<int>();

            // (1, 0) and (2, 1) touch by a corner, between two land cells.
            bool found = new NavigationCellSearch(grid).TryFindPath(1, 0, 2, 1, cells);

            Assert.IsTrue(found);
            Assert.AreEqual(6f, AssertLegalPath(grid, cells), 1e-3f);
        }

        [Test]
        public void AGoalBehindADiagonalGapOnly_IsNotReached()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "#.",
                ".#");
            var cells = new List<int> { 99 };

            bool found = new NavigationCellSearch(grid).TryFindPath(0, 0, 1, 1, cells);

            Assert.IsFalse(found);
            Assert.IsEmpty(cells);
        }

        [Test]
        public void AGoalInAnotherRegion_IsNotReached()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "..#..",
                "..#..");
            var cells = new List<int> { 99 };

            bool found = new NavigationCellSearch(grid).TryFindPath(0, 0, 4, 0, cells);

            Assert.IsFalse(found);
            Assert.IsEmpty(cells);
        }

        [Test]
        public void TheStartCellItself_IsAPathOfOneCell()
        {
            NavigationGrid grid = GridAssert.FromRows("...");
            var cells = new List<int>();

            bool found = new NavigationCellSearch(grid).TryFindPath(1, 0, 1, 0, cells);

            Assert.IsTrue(found);
            CollectionAssert.AreEqual(new[] { 1 }, cells);
        }

        [TestCase(1, 0, 0, 0)]
        [TestCase(0, 0, 1, 0)]
        [TestCase(-1, 0, 0, 0)]
        [TestCase(0, 0, 3, 0)]
        [TestCase(0, 0, 0, 1)]
        public void ALandOrOutsideCell_AsStartOrGoal_IsNotReached(int startX, int startY, int goalX, int goalY)
        {
            NavigationGrid grid = GridAssert.FromRows(".#.");
            var cells = new List<int> { 99 };

            bool found = new NavigationCellSearch(grid).TryFindPath(startX, startY, goalX, goalY, cells);

            Assert.IsFalse(found);
            Assert.IsEmpty(cells);
        }

        [Test]
        public void SearchesInARow_GiveTheSameResultsAsFreshSearches()
        {
            NavigationGrid grid = GridAssert.FromRows(WallWithAGapAtTheTop);
            var reused = new NavigationCellSearch(grid);
            var queries = new[]
            {
                new[] { 0, 0, 4, 0 },
                new[] { 4, 4, 0, 0 },
                new[] { 1, 1, 1, 1 },
                new[] { 0, 0, 2, 0 },
                new[] { 3, 0, 0, 3 },
                new[] { 0, 0, 4, 0 },
            };

            foreach (int[] query in queries)
            {
                var expected = new List<int>();
                var actual = new List<int>();

                bool expectedFound = new NavigationCellSearch(grid)
                    .TryFindPath(query[0], query[1], query[2], query[3], expected);
                bool actualFound = reused.TryFindPath(query[0], query[1], query[2], query[3], actual);

                Assert.AreEqual(expectedFound, actualFound);
                CollectionAssert.AreEqual(expected, actual);
            }
        }

        [Test]
        public void AFullSizeGrid_OfOpenWater_IsCrossedCornerToCorner()
        {
            var grid = new NavigationGrid(1024, 879);
            grid.Clear(true);
            var cells = new List<int>();

            bool found = new NavigationCellSearch(grid).TryFindPath(0, 0, 1023, 878, cells);

            Assert.IsTrue(found);

            // 878 diagonal moves and 145 side moves. The search may be 0.1 % off.
            float shortest = 878f * Sqrt2 + 145f;
            Assert.AreEqual(shortest, AssertLegalPath(grid, cells), shortest * 0.002f);
        }

        [Test]
        public void AFullSizeGrid_WithAWallAcrossIt_IsSailedAround()
        {
            var grid = new NavigationGrid(1024, 879);
            grid.Clear(true);

            // A wall from the bottom row up to the row below the top one.
            for (int y = 0; y < 878; y++)
            {
                grid.SetNavigable(512, y, false);
            }

            var cells = new List<int>();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            bool found = new NavigationCellSearch(grid).TryFindPath(0, 0, 1023, 0, cells);

            stopwatch.Stop();
            Debug.Log($"Cell search around a full-height wall: {stopwatch.ElapsedMilliseconds} ms, {cells.Count} cells.");

            Assert.IsTrue(found);
            Assert.AreEqual(0, cells[0]);
            Assert.AreEqual(1023, cells[cells.Count - 1]);

            // Up to (511, 878), east to (513, 878), down to (1023, 0).
            float shortest = (511f * Sqrt2 + 367f) + 2f + (510f * Sqrt2 + 368f);
            Assert.AreEqual(shortest, AssertLegalPath(grid, cells), shortest * 0.002f);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests.
Expected: exit code `1`, `error CS0246: The type or namespace name 'NavigationCellSearch' could not be found`.

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/Core/NavigationCellSearch.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Finds the shortest sequence of cells between two cells of a
    /// <see cref="NavigationGrid"/> (A*). A ship moves to a side neighbour, or to a
    /// diagonal one when both cells beside the move are navigable, so it never cuts
    /// the corner of a land cell. The grid must not change between searches.
    /// </summary>
    public sealed class NavigationCellSearch
    {
        private const float DiagonalCost = 1.41421356f;

        // Slightly above 1: in open water every cell on the way has the same estimate,
        // and the search would expand all of them. The weight makes it prefer the cells
        // nearer the goal, for a path at most 0.1 % longer than the shortest one.
        private const float HeuristicWeight = 1.001f;

        private readonly NavigationGrid grid;
        private readonly int width;

        // One entry per cell, allocated by the first search and reused by the next ones.
        private float[] costs;
        private int[] parents;

        // generation * 2 for a cell seen by the current search, + 1 once it is settled.
        // Anything else is left over from an earlier search.
        private int[] marks;
        private int generation;

        // Binary heap of cells ordered by estimated total cost. A cell whose cost
        // improves is pushed again; its older entries are skipped when popped.
        private float[] heapKeys = new float[256];
        private int[] heapCells = new int[256];
        private int heapCount;

        public NavigationCellSearch(NavigationGrid grid)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            width = grid.Width;
        }

        /// <param name="cells">
        /// Cleared, then filled with the indexes (y * width + x) of the cells from the
        /// start to the goal, both included.
        /// </param>
        /// <returns>
        /// False, leaving <paramref name="cells"/> empty, when either cell is not
        /// navigable or the goal cannot be reached.
        /// </returns>
        public bool TryFindPath(int startX, int startY, int goalX, int goalY, List<int> cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            cells.Clear();

            if (!grid.IsNavigable(startX, startY) || !grid.IsNavigable(goalX, goalY))
            {
                return false;
            }

            BeginSearch();

            int start = startY * width + startX;
            int goal = goalY * width + goalX;
            int settled = generation * 2 + 1;

            costs[start] = 0f;
            parents[start] = -1;
            marks[start] = generation * 2;
            Push(Estimate(startX, startY, goalX, goalY), start);

            while (heapCount > 0)
            {
                int cell = Pop();

                if (marks[cell] == settled)
                {
                    continue;
                }

                marks[cell] = settled;

                if (cell == goal)
                {
                    for (int step = goal; step >= 0; step = parents[step])
                    {
                        cells.Add(step);
                    }

                    cells.Reverse();
                    return true;
                }

                int x = cell % width;
                int y = cell / width;
                float cost = costs[cell];

                bool west = grid.IsNavigable(x - 1, y);
                bool east = grid.IsNavigable(x + 1, y);
                bool south = grid.IsNavigable(x, y - 1);
                bool north = grid.IsNavigable(x, y + 1);

                if (west)
                {
                    Reach(x - 1, y, cell, cost + 1f, goalX, goalY);
                }

                if (east)
                {
                    Reach(x + 1, y, cell, cost + 1f, goalX, goalY);
                }

                if (south)
                {
                    Reach(x, y - 1, cell, cost + 1f, goalX, goalY);
                }

                if (north)
                {
                    Reach(x, y + 1, cell, cost + 1f, goalX, goalY);
                }

                // A diagonal move needs both cells beside it.
                if (west && south && grid.IsNavigable(x - 1, y - 1))
                {
                    Reach(x - 1, y - 1, cell, cost + DiagonalCost, goalX, goalY);
                }

                if (east && south && grid.IsNavigable(x + 1, y - 1))
                {
                    Reach(x + 1, y - 1, cell, cost + DiagonalCost, goalX, goalY);
                }

                if (west && north && grid.IsNavigable(x - 1, y + 1))
                {
                    Reach(x - 1, y + 1, cell, cost + DiagonalCost, goalX, goalY);
                }

                if (east && north && grid.IsNavigable(x + 1, y + 1))
                {
                    Reach(x + 1, y + 1, cell, cost + DiagonalCost, goalX, goalY);
                }
            }

            return false;
        }

        private void BeginSearch()
        {
            if (costs == null)
            {
                int cellCount = width * grid.Height;

                costs = new float[cellCount];
                parents = new int[cellCount];
                marks = new int[cellCount];
            }

            // Marks hold generation * 2 + 1 at most.
            if (generation >= int.MaxValue / 2 - 1)
            {
                Array.Clear(marks, 0, marks.Length);
                generation = 0;
            }

            generation++;
            heapCount = 0;
        }

        /// <summary>Records a way to a navigable cell if it is the best one so far.</summary>
        private void Reach(int x, int y, int from, float cost, int goalX, int goalY)
        {
            int cell = y * width + x;
            int seen = generation * 2;
            int mark = marks[cell];

            if (mark == seen + 1 || (mark == seen && cost >= costs[cell]))
            {
                return;
            }

            costs[cell] = cost;
            parents[cell] = from;
            marks[cell] = seen;
            Push(cost + Estimate(x, y, goalX, goalY), cell);
        }

        /// <summary>Cost of the way to the goal over open water (octile distance).</summary>
        private static float Estimate(int x, int y, int goalX, int goalY)
        {
            int deltaX = Math.Abs(goalX - x);
            int deltaY = Math.Abs(goalY - y);
            int diagonal = Math.Min(deltaX, deltaY);

            return HeuristicWeight * (deltaX + deltaY - 2 * diagonal + diagonal * DiagonalCost);
        }

        private void Push(float key, int cell)
        {
            if (heapCount == heapKeys.Length)
            {
                Array.Resize(ref heapKeys, heapCount * 2);
                Array.Resize(ref heapCells, heapCount * 2);
            }

            int index = heapCount++;

            while (index > 0)
            {
                int parent = (index - 1) / 2;

                if (heapKeys[parent] <= key)
                {
                    break;
                }

                heapKeys[index] = heapKeys[parent];
                heapCells[index] = heapCells[parent];
                index = parent;
            }

            heapKeys[index] = key;
            heapCells[index] = cell;
        }

        private int Pop()
        {
            int top = heapCells[0];

            heapCount--;

            // The last entry is moved down from the root to its place.
            float key = heapKeys[heapCount];
            int cell = heapCells[heapCount];
            int index = 0;

            while (true)
            {
                int child = index * 2 + 1;

                if (child >= heapCount)
                {
                    break;
                }

                if (child + 1 < heapCount && heapKeys[child + 1] < heapKeys[child])
                {
                    child++;
                }

                if (heapKeys[child] >= key)
                {
                    break;
                }

                heapKeys[index] = heapKeys[child];
                heapCells[index] = heapCells[child];
                index = child;
            }

            heapKeys[index] = key;
            heapCells[index] = cell;
            return top;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the EditMode tests with `'-testFilter','DarkFantasyMerchant.Tests.EditMode.NavigationCellSearchTests'`.
Expected: exit code `0`, `result="Passed"`.

Note the time logged by `AFullSizeGrid_WithAWallAcrossIt_IsSailedAround` for the final report: `Select-String -Path Logs\test.log -Pattern 'Cell search around'`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/NavigationCellSearch.cs Assets/Scripts/Core/NavigationCellSearch.cs.meta Assets/Tests/EditMode/NavigationCellSearchTests.cs Assets/Tests/EditMode/NavigationCellSearchTests.cs.meta
git commit -m "Recherche du plus court chemin sur les cellules du masque

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Pathfinder — regions and nearest water

**Files:**
- Create: `Assets/Scripts/Core/NavigationPathfinder.cs`
- Test: `Assets/Tests/EditMode/NavigationPathfinderTests.cs`

**Interfaces:**
- Consumes: `NavigationGrid`; `GridAssert.FromRows`, `GridAssert.CellPoint`, `GridAssert.CellCenter`.
- Produces:
  - `public NavigationPathfinder(NavigationGrid grid)` — throws `ArgumentNullException` for null.
  - `public bool HasNavigableCells { get; }`
  - `public bool TryGetNearestNavigable(Vector2 point, out Vector2 nearest)`
  - Private members Task 4 builds on: `grid`, `width`, `height`, `regions` (`int[]`, `NoRegion` = `0` for land), `AnyRegion` (`-1`), `CellAt(Vector2)`, `CellCenter(int)`, `NearestCell(Vector2, int region)`, `ClampToMap(Vector2)`, `IsNaN(Vector2)`.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/NavigationPathfinderTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationPathfinderTests
    {
        [Test]
        public void ANullGrid_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NavigationPathfinder(null));
        }

        [Test]
        public void HasNavigableCells_TellsWhetherTheGridHasWater()
        {
            Assert.IsFalse(new NavigationPathfinder(new NavigationGrid(4, 3)).HasNavigableCells);
            Assert.IsTrue(new NavigationPathfinder(GridAssert.FromRows("###", "#.#")).HasNavigableCells);
        }

        [Test]
        public void NearestNavigable_OfAPointOnWater_IsThePointItself()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "#..",
                "###");
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 point = GridAssert.CellPoint(grid, 1.2f, 1.7f);

            bool found = pathfinder.TryGetNearestNavigable(point, out Vector2 nearest);

            Assert.IsTrue(found);
            Assert.AreEqual(point.x, nearest.x);
            Assert.AreEqual(point.y, nearest.y);
        }

        [Test]
        public void NearestNavigable_OfAPointOnLand_IsTheCenterOfTheNearestWaterCell()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "#..",
                "###");
            var pathfinder = new NavigationPathfinder(grid);

            bool found = pathfinder.TryGetNearestNavigable(
                GridAssert.CellPoint(grid, 0.8f, 1.5f), out Vector2 nearest);

            Assert.IsTrue(found);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 1, 1), nearest);
        }

        [Test]
        public void NearestNavigable_IsMeasuredInCells_NotInNormalizedSpace()
        {
            // Eight cells wide, two high: one cell up is 0.5 away in normalized space,
            // three cells to the right only 0.375.
            NavigationGrid grid = GridAssert.FromRows(
                ".#######",
                "###.####");
            var pathfinder = new NavigationPathfinder(grid);

            pathfinder.TryGetNearestNavigable(GridAssert.CellCenter(grid, 0, 0), out Vector2 nearest);

            TestAssert.AreEqual(GridAssert.CellCenter(grid, 0, 1), nearest);
        }

        [Test]
        public void NearestNavigable_OfAPointOutsideTheMap_StartsFromTheClampedPoint()
        {
            NavigationGrid grid = GridAssert.FromRows("#.");
            var pathfinder = new NavigationPathfinder(grid);

            bool found = pathfinder.TryGetNearestNavigable(new Vector2(5f, 0.25f), out Vector2 nearest);

            Assert.IsTrue(found);
            TestAssert.AreEqual(new Vector2(1f, 0.25f), nearest);
        }

        [TestCase(float.NaN, 0.5f)]
        [TestCase(0.5f, float.NaN)]
        public void NearestNavigable_OfANaNPoint_IsNotFound(float x, float y)
        {
            var pathfinder = new NavigationPathfinder(GridAssert.FromRows(".."));

            Assert.IsFalse(pathfinder.TryGetNearestNavigable(new Vector2(x, y), out _));
        }

        [Test]
        public void NearestNavigable_OnAGridWithoutWater_IsNotFound_AndGivesTheClampedPoint()
        {
            var pathfinder = new NavigationPathfinder(new NavigationGrid(4, 3));

            bool found = pathfinder.TryGetNearestNavigable(new Vector2(-2f, 0.5f), out Vector2 nearest);

            Assert.IsFalse(found);
            TestAssert.AreEqual(new Vector2(0f, 0.5f), nearest);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests.
Expected: exit code `1`, `error CS0246: The type or namespace name 'NavigationPathfinder' could not be found`.

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/Core/NavigationPathfinder.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Finds routes for ships over the navigable cells of a <see cref="NavigationGrid"/>.
    /// The grid must not be modified once the pathfinder is built.
    /// </summary>
    public sealed class NavigationPathfinder
    {
        private const int NoRegion = 0;
        private const int AnyRegion = -1;

        private readonly NavigationGrid grid;
        private readonly int width;
        private readonly int height;

        // Region of each cell, NoRegion for land. Two cells are in the same region when
        // a ship can sail from one to the other: regions are connected by cell sides,
        // because a diagonal move needs the cells beside it.
        private readonly int[] regions;

        public NavigationPathfinder(NavigationGrid grid)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            width = grid.Width;
            height = grid.Height;
            regions = new int[width * height];
            HasNavigableCells = LabelRegions() > 0;
        }

        /// <summary>False for a grid that is all land: no ship can be placed or moved.</summary>
        public bool HasNavigableCells { get; }

        /// <summary>
        /// The point itself when it is on water, otherwise the center of the nearest
        /// navigable cell. A point outside the map is clamped to it first.
        /// </summary>
        /// <returns>
        /// False, with <paramref name="nearest"/> set to the clamped point, for a NaN
        /// point and for a grid with no navigable cell.
        /// </returns>
        public bool TryGetNearestNavigable(Vector2 point, out Vector2 nearest)
        {
            nearest = ClampToMap(point);

            if (IsNaN(point) || !HasNavigableCells)
            {
                return false;
            }

            if (regions[CellAt(nearest)] == NoRegion)
            {
                nearest = CellCenter(NearestCell(nearest, AnyRegion));
            }

            return true;
        }

        /// <returns>The number of regions.</returns>
        private int LabelRegions()
        {
            int count = 0;
            var pending = new Stack<int>();

            for (int cell = 0; cell < regions.Length; cell++)
            {
                if (regions[cell] != NoRegion || !grid.IsNavigable(cell % width, cell / width))
                {
                    continue;
                }

                count++;
                regions[cell] = count;
                pending.Push(cell);

                // Iterative: a sea can hold hundreds of thousands of cells.
                while (pending.Count > 0)
                {
                    int current = pending.Pop();
                    int x = current % width;
                    int y = current / width;

                    Label(x - 1, y, count, pending);
                    Label(x + 1, y, count, pending);
                    Label(x, y - 1, count, pending);
                    Label(x, y + 1, count, pending);
                }
            }

            return count;
        }

        private void Label(int x, int y, int region, Stack<int> pending)
        {
            // False outside the grid.
            if (!grid.IsNavigable(x, y))
            {
                return;
            }

            int cell = y * width + x;

            if (regions[cell] == NoRegion)
            {
                regions[cell] = region;
                pending.Push(cell);
            }
        }

        /// <summary>
        /// Index of the cell of a region, or of any region, whose center is nearest to a
        /// point of the map, in cells. The lowest index wins a tie. -1 when there is none.
        /// </summary>
        private int NearestCell(Vector2 point, int region)
        {
            double pointX = (double)point.x * width;
            double pointY = (double)point.y * height;
            double nearestDistance = double.MaxValue;
            int nearest = -1;

            for (int y = 0; y < height; y++)
            {
                double offsetY = y + 0.5 - pointY;
                double squaredY = offsetY * offsetY;

                // No cell of a row this far away can be nearer.
                if (squaredY >= nearestDistance)
                {
                    continue;
                }

                int row = y * width;

                for (int x = 0; x < width; x++)
                {
                    int cellRegion = regions[row + x];

                    if (cellRegion == NoRegion || (region != AnyRegion && cellRegion != region))
                    {
                        continue;
                    }

                    double offsetX = x + 0.5 - pointX;
                    double distance = offsetX * offsetX + squaredY;

                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = row + x;
                    }
                }
            }

            return nearest;
        }

        /// <summary>Index of the cell under a point of the map.</summary>
        private int CellAt(Vector2 point)
        {
            // The same rounding as NavigationGrid: a coordinate of exactly 1 belongs to
            // the last cell.
            int x = Mathf.Min((int)(point.x * width), width - 1);
            int y = Mathf.Min((int)(point.y * height), height - 1);

            return y * width + x;
        }

        private Vector2 CellCenter(int cell)
        {
            return new Vector2((cell % width + 0.5f) / width, (cell / width + 0.5f) / height);
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

`using System.Collections.Generic;` is used by `Stack<int>` here and by the lists of Task 4.

- [ ] **Step 4: Run the tests to verify they pass**

Run the EditMode tests with `'-testFilter','DarkFantasyMerchant.Tests.EditMode.NavigationPathfinderTests'`.
Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/NavigationPathfinder.cs Assets/Scripts/Core/NavigationPathfinder.cs.meta Assets/Tests/EditMode/NavigationPathfinderTests.cs Assets/Tests/EditMode/NavigationPathfinderTests.cs.meta
git commit -m "Régions d'eau et point navigable le plus proche

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Pathfinder — routes

**Files:**
- Modify: `Assets/Scripts/Core/NavigationPathfinder.cs`
- Test: `Assets/Tests/EditMode/NavigationPathfinderTests.cs` (add to the fixture of Task 3)

**Interfaces:**
- Consumes: `NavigationCellSearch.TryFindPath(int, int, int, int, List<int>)` (Task 2), `NavigationLineOfSight.IsClear(NavigationGrid, Vector2, Vector2)` (Task 1), the private members of Task 3.
- Produces: `public bool TryFindPath(Vector2 from, Vector2 to, List<Vector2> waypoints)` — clears `waypoints`, fills it with the normalized points to sail through in order, **not including `from`**. True with an empty list when `from` is already the end point. False with an empty list for a NaN point or a grid with no navigable cell. Throws `ArgumentNullException` for a null list.

- [ ] **Step 1: Write the failing tests**

In `Assets/Tests/EditMode/NavigationPathfinderTests.cs`, add these members inside the class, after the last test:

```csharp
        private static readonly string[] WallWithAGapAtTheTop =
        {
            ".....",
            "..#..",
            "..#..",
            "..#..",
            "..#..",
        };

        private static void AssertSamePoint(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, "x");
            Assert.AreEqual(expected.y, actual.y, "y");
        }

        /// <summary>Checks that every leg of a route follows the movement rules.</summary>
        private static void AssertRouteIsOnWater(NavigationGrid grid, Vector2 from, List<Vector2> waypoints)
        {
            Vector2 previous = from;

            for (int i = 0; i < waypoints.Count; i++)
            {
                Assert.IsTrue(
                    NavigationLineOfSight.IsClear(grid, previous, waypoints[i]),
                    $"leg {i}, from {previous} to {waypoints[i]}, touches land");
                previous = waypoints[i];
            }
        }

        [Test]
        public void TryFindPath_RejectsANullList()
        {
            var pathfinder = new NavigationPathfinder(GridAssert.FromRows(".."));

            Assert.Throws<ArgumentNullException>(
                () => pathfinder.TryFindPath(Vector2.zero, Vector2.one, null));
        }

        [Test]
        public void OpenWater_IsOneLeg_ToTheDestinationItself()
        {
            var grid = new NavigationGrid(6, 6);
            grid.Clear(true);
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 to = GridAssert.CellPoint(grid, 4.3f, 2.7f);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(GridAssert.CellCenter(grid, 0, 0), to, waypoints);

            Assert.IsTrue(found);
            Assert.AreEqual(1, waypoints.Count);
            AssertSamePoint(to, waypoints[0]);
        }

        [Test]
        public void AWall_IsSailedAround_InStraightLegs()
        {
            NavigationGrid grid = GridAssert.FromRows(WallWithAGapAtTheTop);
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 from = GridAssert.CellCenter(grid, 0, 0);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(from, GridAssert.CellCenter(grid, 4, 0), waypoints);

            Assert.IsTrue(found);
            AssertRouteIsOnWater(grid, from, waypoints);

            // One turn on each side of the gap; everything else is skipped.
            Assert.AreEqual(3, waypoints.Count);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 1, 4), waypoints[0]);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 3, 4), waypoints[1]);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 4, 0), waypoints[2]);
        }

        [Test]
        public void ADiagonalGap_IsNotCrossed_WhenThereIsAWayAround()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "...",
                ".#.",
                "..#");
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 from = GridAssert.CellCenter(grid, 1, 0);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(from, GridAssert.CellCenter(grid, 2, 1), waypoints);

            Assert.IsTrue(found);
            AssertRouteIsOnWater(grid, from, waypoints);
            Assert.Greater(waypoints.Count, 1, "a single leg would cross the gap");
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 2, 1), waypoints[waypoints.Count - 1]);
        }

        [Test]
        public void ADiagonalGap_IsNotCrossed_WhenItIsTheOnlyWay()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "#.",
                ".#");
            var pathfinder = new NavigationPathfinder(grid);
            var waypoints = new List<Vector2>();

            // The two water cells are separate regions: the nearest cell the start can
            // reach is its own.
            bool found = pathfinder.TryFindPath(
                GridAssert.CellCenter(grid, 0, 0), GridAssert.CellCenter(grid, 1, 1), waypoints);

            Assert.IsTrue(found);
            Assert.IsEmpty(waypoints);
        }

        [Test]
        public void AChannelOneCellWide_IsFollowedThroughItsBend()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "#####",
                "#...#",
                "#.###",
                "#.###",
                "#####");
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 from = GridAssert.CellCenter(grid, 1, 1);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(from, GridAssert.CellCenter(grid, 3, 3), waypoints);

            Assert.IsTrue(found);
            AssertRouteIsOnWater(grid, from, waypoints);
            Assert.AreEqual(2, waypoints.Count);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 1, 3), waypoints[0]);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 3, 3), waypoints[1]);
        }

        [Test]
        public void ADestinationOnLand_EndsOnTheNearestWaterCell()
        {
            NavigationGrid grid = GridAssert.FromRows(WallWithAGapAtTheTop);
            var pathfinder = new NavigationPathfinder(grid);
            var waypoints = new List<Vector2>();

            // In the wall, nearer its west side.
            bool found = pathfinder.TryFindPath(
                GridAssert.CellCenter(grid, 0, 0), GridAssert.CellPoint(grid, 2.2f, 0.5f), waypoints);

            Assert.IsTrue(found);
            Assert.AreEqual(1, waypoints.Count);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 1, 0), waypoints[0]);
        }

        [Test]
        public void ADestinationInAnotherRegion_EndsOnTheNearestCellOfTheStartsRegion()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "..#..",
                "..#..");
            var pathfinder = new NavigationPathfinder(grid);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(
                GridAssert.CellCenter(grid, 0, 0), GridAssert.CellCenter(grid, 4, 0), waypoints);

            Assert.IsTrue(found);
            Assert.AreEqual(1, waypoints.Count);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 1, 0), waypoints[0]);
        }

        [Test]
        public void ADestinationOutsideTheMap_IsClampedToIt()
        {
            NavigationGrid grid = GridAssert.FromRows("....");
            var pathfinder = new NavigationPathfinder(grid);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(
                GridAssert.CellCenter(grid, 0, 0), new Vector2(3f, 0.5f), waypoints);

            Assert.IsTrue(found);
            Assert.AreEqual(1, waypoints.Count);
            AssertSamePoint(new Vector2(1f, 0.5f), waypoints[0]);
        }

        [Test]
        public void ADestinationOnTheCornerOfTheMap_IsReachedExactly()
        {
            var grid = new NavigationGrid(4, 4);
            grid.Clear(true);
            var pathfinder = new NavigationPathfinder(grid);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(
                GridAssert.CellCenter(grid, 0, 0), new Vector2(1f, 1f), waypoints);

            Assert.IsTrue(found);
            Assert.AreEqual(1, waypoints.Count);
            AssertSamePoint(new Vector2(1f, 1f), waypoints[0]);
        }

        [Test]
        public void AStartOnLand_FirstGoesToTheNearestWaterCell()
        {
            NavigationGrid grid = GridAssert.FromRows("#..");
            var pathfinder = new NavigationPathfinder(grid);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(
                GridAssert.CellCenter(grid, 0, 0), GridAssert.CellCenter(grid, 2, 0), waypoints);

            Assert.IsTrue(found);
            Assert.AreEqual(2, waypoints.Count);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 1, 0), waypoints[0]);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 2, 0), waypoints[1]);
        }

        [TestCase(0.2f, 0.3f)]
        [TestCase(1.9f, 2.6f)]
        [TestCase(0.05f, 4.95f)]
        [TestCase(1.5f, 0.999f)]
        public void AStartThatIsNotACellCenter_StillGivesARouteOnWater(float cellX, float cellY)
        {
            NavigationGrid grid = GridAssert.FromRows(WallWithAGapAtTheTop);
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 from = GridAssert.CellPoint(grid, cellX, cellY);
            Vector2 to = GridAssert.CellPoint(grid, 4.8f, 0.1f);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(from, to, waypoints);

            Assert.IsTrue(found);
            AssertRouteIsOnWater(grid, from, waypoints);
            AssertSamePoint(to, waypoints[waypoints.Count - 1]);
        }

        [Test]
        public void ARouteToTheStartItself_IsFound_AndEmpty()
        {
            NavigationGrid grid = GridAssert.FromRows("....");
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 point = GridAssert.CellPoint(grid, 1.3f, 0.4f);
            var waypoints = new List<Vector2> { Vector2.one };

            bool found = pathfinder.TryFindPath(point, point, waypoints);

            Assert.IsTrue(found);
            Assert.IsEmpty(waypoints);
        }

        [TestCase(float.NaN, 0.5f, 0.5f, 0.5f)]
        [TestCase(0.5f, float.NaN, 0.5f, 0.5f)]
        [TestCase(0.5f, 0.5f, float.NaN, 0.5f)]
        [TestCase(0.5f, 0.5f, 0.5f, float.NaN)]
        public void ANaNPoint_GivesNoRoute(float fromX, float fromY, float toX, float toY)
        {
            var pathfinder = new NavigationPathfinder(GridAssert.FromRows("...."));
            var waypoints = new List<Vector2> { Vector2.one };

            bool found = pathfinder.TryFindPath(
                new Vector2(fromX, fromY), new Vector2(toX, toY), waypoints);

            Assert.IsFalse(found);
            Assert.IsEmpty(waypoints);
        }

        [Test]
        public void AGridWithoutWater_GivesNoRoute()
        {
            var pathfinder = new NavigationPathfinder(new NavigationGrid(4, 3));
            var waypoints = new List<Vector2> { Vector2.one };

            bool found = pathfinder.TryFindPath(new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), waypoints);

            Assert.IsFalse(found);
            Assert.IsEmpty(waypoints);
        }

        [Test]
        public void RoutesInARow_AreTheSameAsRoutesFromFreshPathfinders()
        {
            NavigationGrid grid = GridAssert.FromRows(WallWithAGapAtTheTop);
            var reused = new NavigationPathfinder(grid);
            var points = new[]
            {
                GridAssert.CellCenter(grid, 0, 0),
                GridAssert.CellCenter(grid, 4, 0),
                GridAssert.CellPoint(grid, 2.2f, 0.5f),
                GridAssert.CellPoint(grid, 3.7f, 4.2f),
                GridAssert.CellCenter(grid, 0, 0),
            };

            for (int i = 1; i < points.Length; i++)
            {
                var expected = new List<Vector2>();
                var actual = new List<Vector2>();

                new NavigationPathfinder(grid).TryFindPath(points[i - 1], points[i], expected);
                reused.TryFindPath(points[i - 1], points[i], actual);

                CollectionAssert.AreEqual(expected, actual);
            }
        }

        [Test]
        public void AFullSizeGrid_OfOpenWater_IsCrossedInOneLeg()
        {
            var grid = new NavigationGrid(1024, 879);
            grid.Clear(true);
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 to = GridAssert.CellCenter(grid, 1023, 878);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(GridAssert.CellCenter(grid, 0, 0), to, waypoints);

            Assert.IsTrue(found);
            Assert.AreEqual(1, waypoints.Count);
            AssertSamePoint(to, waypoints[0]);
        }

        [Test]
        public void AGridThatIsNotSquare_IsSailedAroundItsLand()
        {
            NavigationGrid grid = GridAssert.FromRows(
                "........",
                "...#....");
            var pathfinder = new NavigationPathfinder(grid);
            Vector2 from = GridAssert.CellCenter(grid, 0, 0);
            var waypoints = new List<Vector2>();

            bool found = pathfinder.TryFindPath(from, GridAssert.CellCenter(grid, 7, 0), waypoints);

            Assert.IsTrue(found);
            AssertRouteIsOnWater(grid, from, waypoints);
            Assert.Greater(waypoints.Count, 1);
            TestAssert.AreEqual(GridAssert.CellCenter(grid, 7, 0), waypoints[waypoints.Count - 1]);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests.
Expected: exit code `1`, `error CS1061: 'NavigationPathfinder' does not contain a definition for 'TryFindPath'`.

- [ ] **Step 3: Write the implementation**

In `Assets/Scripts/Core/NavigationPathfinder.cs`:

Add these fields after the `regions` field:

```csharp
        private readonly NavigationCellSearch search;

        // Reused by every search.
        private readonly List<int> cells = new List<int>();
        private readonly List<Vector2> route = new List<Vector2>();
```

In the constructor, add as the last statement:

```csharp
            search = new NavigationCellSearch(grid);
```

Add this method after `TryGetNearestNavigable`:

```csharp
        /// <summary>
        /// Finds the route a ship sails from one point of the map to another. The route
        /// ends on the destination when the ship can reach it, otherwise on the nearest
        /// navigable cell it can reach. Points outside the map are clamped to it.
        /// </summary>
        /// <param name="waypoints">
        /// Cleared, then filled with the points to sail through in order, not including
        /// <paramref name="from"/>. Empty when the ship is already where the route ends.
        /// </param>
        /// <returns>
        /// False, leaving <paramref name="waypoints"/> empty, for a NaN point and for a
        /// grid with no navigable cell.
        /// </returns>
        public bool TryFindPath(Vector2 from, Vector2 to, List<Vector2> waypoints)
        {
            if (waypoints == null)
            {
                throw new ArgumentNullException(nameof(waypoints));
            }

            waypoints.Clear();

            if (IsNaN(from) || IsNaN(to) || !HasNavigableCells)
            {
                return false;
            }

            from = ClampToMap(from);
            to = ClampToMap(to);

            int fromCell = CellAt(from);
            int startCell = regions[fromCell] != NoRegion ? fromCell : NearestCell(from, AnyRegion);
            int region = regions[startCell];

            int toCell = CellAt(to);
            int goalCell = regions[toCell] == region ? toCell : NearestCell(to, region);

            // A destination on reachable water is reached exactly, not rounded to a cell.
            Vector2 end = goalCell == toCell ? to : CellCenter(goalCell);

            // Cannot fail: both cells are in the same region.
            if (!search.TryFindPath(
                startCell % width, startCell / width, goalCell % width, goalCell / width, cells))
            {
                return false;
            }

            route.Clear();
            route.Add(from);

            // A start that is not on water first sails to the nearest water.
            if (startCell != fromCell)
            {
                route.Add(CellCenter(startCell));
            }

            for (int i = 1; i < cells.Count; i++)
            {
                route.Add(CellCenter(cells[i]));
            }

            route.Add(end);
            Smooth(from, waypoints);
            return true;
        }

        /// <summary>
        /// Copies the route without the points a straight leg can skip. Two consecutive
        /// points of the route are always an acceptable leg, so a point is only skipped
        /// when the leg that replaces it is clear.
        /// </summary>
        private void Smooth(Vector2 from, List<Vector2> waypoints)
        {
            Vector2 anchor = from;

            for (int i = 1; i < route.Count - 1; i++)
            {
                if (!NavigationLineOfSight.IsClear(grid, anchor, route[i + 1]))
                {
                    AddWaypoint(waypoints, from, route[i]);
                    anchor = route[i];
                }
            }

            AddWaypoint(waypoints, from, route[route.Count - 1]);
        }

        private static void AddWaypoint(List<Vector2> waypoints, Vector2 from, Vector2 point)
        {
            Vector2 previous = waypoints.Count > 0 ? waypoints[waypoints.Count - 1] : from;

            // Compared per component: Vector2 equality is approximate and would drop a
            // very short leg.
            if (point.x != previous.x || point.y != previous.y)
            {
                waypoints.Add(point);
            }
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the EditMode tests with `'-testFilter','DarkFantasyMerchant.Tests.EditMode.NavigationPathfinderTests'`.
Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/NavigationPathfinder.cs Assets/Tests/EditMode/NavigationPathfinderTests.cs
git commit -m "Routes des navires sur les zones navigables

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Ship follows a route

**Files:**
- Modify: `Assets/Scripts/Core/Ship.cs` (whole file replaced)
- Test: `Assets/Tests/EditMode/ShipNavigationTests.cs` (new); `Assets/Tests/EditMode/ShipTests.cs` must pass **unchanged**

**Interfaces:**
- Consumes: `NavigationPathfinder.TryGetNearestNavigable(Vector2, out Vector2)`, `NavigationPathfinder.TryFindPath(Vector2, Vector2, List<Vector2>)`, `MapProjection.NormalizedToWorld` / `WorldToNormalized`, `CompassHeading.FromVector(Vector2 direction, CompassDirection fallback)`.
- Produces:
  - `public Ship(MapProjection projection, Vector2 position, float speed, NavigationPathfinder navigation = null)`
  - `public IReadOnlyList<Vector2> RemainingWaypoints { get; }` — next waypoint first; empty when idle.
  - `Destination` is the last waypoint, or null when idle. `Position`, `WorldPosition`, `IsMoving`, `Heading`, `SetDestination(Vector2)`, `Advance(float)` keep their signatures.

The tests use a map of 40 × 20 world units and a grid of 8 × 4 cells, so a cell is 5 × 5 world units:

```
"........"   y = 3
"...#...."   y = 2
"...#...."   y = 1
"...#...."   y = 0
```

From the center of cell `(1, 0)` to the center of cell `(5, 0)` the route is three legs, through the centers of `(2, 3)`, `(4, 3)` and `(5, 0)`. In world units: `(-12.5, -7.5)` → `(-7.5, 7.5)` → `(2.5, 7.5)` → `(7.5, -7.5)`. The legs are `√250 ≈ 15.811`, `10` and `15.811` long, heading N, E and S.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/EditMode/ShipNavigationTests.cs`:

```csharp
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>A ship that was given a pathfinder: it sails over water only.</summary>
    public class ShipNavigationTests
    {
        private const float Speed = 4f;
        private const float FirstLegLength = 15.8113883f;

        private MapProjection projection;
        private NavigationGrid grid;
        private NavigationPathfinder navigation;

        [SetUp]
        public void SetUp()
        {
            // 40 x 20 world units over 8 x 4 cells: a cell is 5 x 5 world units.
            projection = new MapProjection(40f, 2f);
            grid = GridAssert.FromRows(
                "........",
                "...#....",
                "...#....",
                "...#....");
            navigation = new NavigationPathfinder(grid);
        }

        private Vector2 Cell(int x, int y)
        {
            return GridAssert.CellCenter(grid, x, y);
        }

        private Ship CreateShip()
        {
            return new Ship(projection, Cell(1, 0), Speed, navigation);
        }

        [Test]
        public void AShipCreatedOnWater_StaysWhereItIs()
        {
            Vector2 position = GridAssert.CellPoint(grid, 1.2f, 0.7f);

            var ship = new Ship(projection, position, Speed, navigation);

            TestAssert.AreEqual(position, ship.Position);
            Assert.IsFalse(ship.IsMoving);
        }

        [Test]
        public void AShipCreatedOnLand_StartsOnTheNearestWater()
        {
            // In the wall, nearer its west side.
            var ship = new Ship(projection, GridAssert.CellPoint(grid, 3.2f, 1.5f), Speed, navigation);

            TestAssert.AreEqual(Cell(2, 1), ship.Position);
            Assert.IsFalse(ship.IsMoving);
        }

        [Test]
        public void AShipCreatedOutsideTheMap_IsClampedBeforeLookingForWater()
        {
            var ship = new Ship(projection, new Vector2(-3f, 0.1f), Speed, navigation);

            TestAssert.AreEqual(new Vector2(0f, 0.1f), ship.Position);
        }

        [Test]
        public void AnOrderAcrossLand_BecomesARouteAroundIt()
        {
            Ship ship = CreateShip();

            ship.SetDestination(Cell(5, 0));

            Assert.IsTrue(ship.IsMoving);
            Assert.AreEqual(3, ship.RemainingWaypoints.Count);
            TestAssert.AreEqual(Cell(2, 3), ship.RemainingWaypoints[0]);
            TestAssert.AreEqual(Cell(4, 3), ship.RemainingWaypoints[1]);
            TestAssert.AreEqual(Cell(5, 0), ship.RemainingWaypoints[2]);
            TestAssert.AreEqual(Cell(5, 0), ship.Destination.Value);
            Assert.AreEqual(CompassDirection.N, ship.Heading, "towards the first waypoint");
        }

        [Test]
        public void AnIdleShip_HasNoWaypoint()
        {
            Ship ship = CreateShip();

            Assert.IsEmpty(ship.RemainingWaypoints);
            Assert.IsNull(ship.Destination);
        }

        [Test]
        public void Advance_FollowsTheFirstLeg()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));

            ship.Advance(1f);

            // Four world units along (5, 15) from (-12.5, -7.5).
            float along = Speed / FirstLegLength;
            TestAssert.AreEqual(new Vector2(-12.5f + 5f * along, -7.5f + 15f * along), ship.WorldPosition);
            Assert.AreEqual(3, ship.RemainingWaypoints.Count);
        }

        [Test]
        public void Advance_PastAWaypoint_SpendsTheRestOfTheStepOnTheNextLeg()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));

            ship.Advance(5f);

            // Twenty world units: the whole first leg, then the rest eastwards.
            Assert.AreEqual(-7.5f + (20f - FirstLegLength), ship.WorldPosition.x, 1e-3f);
            Assert.AreEqual(7.5f, ship.WorldPosition.y, 1e-3f);
            Assert.AreEqual(CompassDirection.E, ship.Heading);
            Assert.AreEqual(2, ship.RemainingWaypoints.Count);
            TestAssert.AreEqual(Cell(4, 3), ship.RemainingWaypoints[0]);
            TestAssert.AreEqual(Cell(5, 0), ship.Destination.Value);
        }

        [Test]
        public void Advance_InSmallSteps_CoversTheSameDistanceAsOneStep()
        {
            Ship inOneStep = CreateShip();
            Ship inSmallSteps = CreateShip();
            inOneStep.SetDestination(Cell(5, 0));
            inSmallSteps.SetDestination(Cell(5, 0));

            inOneStep.Advance(8f);

            for (int i = 0; i < 80; i++)
            {
                inSmallSteps.Advance(0.1f);
            }

            Assert.AreEqual(inOneStep.WorldPosition.x, inSmallSteps.WorldPosition.x, 1e-2f);
            Assert.AreEqual(inOneStep.WorldPosition.y, inSmallSteps.WorldPosition.y, 1e-2f);
            Assert.AreEqual(CompassDirection.S, inSmallSteps.Heading, "on the last leg");
        }

        [Test]
        public void TheShip_StopsOnItsLastWaypoint_AndKeepsItsHeading()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));

            // The route is about 41.6 world units long: 10.4 seconds.
            ship.Advance(11f);

            Assert.AreEqual(Cell(5, 0), ship.Position);
            Assert.IsFalse(ship.IsMoving);
            Assert.IsNull(ship.Destination);
            Assert.IsEmpty(ship.RemainingWaypoints);
            Assert.AreEqual(CompassDirection.S, ship.Heading);
        }

        [TestCase(1e6f)]
        [TestCase(float.PositiveInfinity)]
        public void Advance_WithAHugeDeltaTime_StopsExactlyOnTheLastWaypoint(float deltaTime)
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));

            ship.Advance(deltaTime);

            Assert.AreEqual(Cell(5, 0), ship.Position);
            Assert.IsFalse(ship.IsMoving);
            Assert.AreEqual(CompassDirection.S, ship.Heading);
        }

        [Test]
        public void OverAWholeTrip_TheShipIsNeverOnLand()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));

            int steps = 0;

            while (ship.IsMoving)
            {
                ship.Advance(0.05f);
                steps++;

                Assert.IsTrue(grid.IsNavigable(ship.Position), $"on land at {ship.Position}, step {steps}");
                Assert.Less(steps, 1000, "the ship never arrives");
            }

            Assert.AreEqual(Cell(5, 0), ship.Position);
        }

        [Test]
        public void AnOrderOnLand_SendsTheShipToTheNearestWaterItCanReach()
        {
            Ship ship = CreateShip();

            // In the wall, nearer its west side.
            ship.SetDestination(GridAssert.CellPoint(grid, 3.2f, 0.5f));

            Assert.IsTrue(ship.IsMoving);
            TestAssert.AreEqual(Cell(2, 0), ship.Destination.Value);

            ship.Advance(100f);

            Assert.AreEqual(Cell(2, 0), ship.Position);
        }

        [Test]
        public void AnOrderUnderWay_ReplacesTheRoute_FromWhereTheShipIs()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));
            ship.Advance(1f);
            Vector2 turningPoint = ship.Position;

            ship.SetDestination(Cell(0, 3));

            Assert.AreEqual(1, ship.RemainingWaypoints.Count);
            TestAssert.AreEqual(Cell(0, 3), ship.Destination.Value);
            TestAssert.AreEqual(turningPoint, ship.Position);
            Assert.AreEqual(CompassDirection.NW, ship.Heading);

            int steps = 0;

            while (ship.IsMoving)
            {
                ship.Advance(0.05f);
                steps++;

                Assert.IsTrue(grid.IsNavigable(ship.Position), $"on land at {ship.Position}");
                Assert.Less(steps, 1000, "the ship never arrives");
            }

            Assert.AreEqual(Cell(0, 3), ship.Position);
        }

        [Test]
        public void AnOrderToWhereTheShipIs_LeavesItIdle_AndItsHeadingUnchanged()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(2, 0));
            ship.Advance(100f);

            ship.SetDestination(ship.Position);

            Assert.IsFalse(ship.IsMoving);
            Assert.AreEqual(CompassDirection.E, ship.Heading);
        }

        [Test]
        public void ANaNOrder_KeepsTheCurrentRoute()
        {
            Ship ship = CreateShip();
            ship.SetDestination(Cell(5, 0));

            ship.SetDestination(new Vector2(float.NaN, 0.5f));

            Assert.AreEqual(3, ship.RemainingWaypoints.Count);
            TestAssert.AreEqual(Cell(5, 0), ship.Destination.Value);
        }

        [Test]
        public void OnAGridWithoutWater_TheShipStaysWhereItIs_AndIgnoresOrders()
        {
            var noWater = new NavigationPathfinder(new NavigationGrid(8, 4));
            var ship = new Ship(projection, new Vector2(0.3f, 0.4f), Speed, noWater);

            ship.SetDestination(new Vector2(0.8f, 0.8f));
            ship.Advance(1f);

            Assert.IsFalse(ship.IsMoving);
            TestAssert.AreEqual(new Vector2(0.3f, 0.4f), ship.Position);
        }
    }
}
```

The heading expected in `AnOrderUnderWay_…`: after one second the ship is at about `(-11.24, -3.71)` in world units; cell `(0, 3)` is at `(-17.5, 7.5)`; the direction `(-6.26, 11.21)` is 29 degrees west of north, in the NW sector (22.5 to 67.5).

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests.
Expected: exit code `1`, with `error CS1729: 'Ship' does not contain a constructor that takes 4 arguments` and `error CS1061: 'Ship' does not contain a definition for 'RemainingWaypoints'`.

- [ ] **Step 3: Write the implementation**

Replace the whole content of `Assets/Scripts/Core/Ship.cs` with:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Runtime state of one ship: where it is, the route it follows and which way it
    /// faces. Sails at constant speed: over water only when it was given a pathfinder,
    /// otherwise in a straight line.
    /// </summary>
    public sealed class Ship
    {
        private readonly MapProjection projection;
        private readonly float speed;
        private readonly NavigationPathfinder navigation;

        // Waypoints not reached yet, the next one first.
        private readonly List<Vector2> waypoints = new List<Vector2>();

        // Where an order is worked out, so that an order that fails leaves the route alone.
        private readonly List<Vector2> orderedRoute = new List<Vector2>();

        /// <param name="position">
        /// Normalized map position; clamped to the map, then moved to the nearest water
        /// when the ship has a pathfinder.
        /// </param>
        /// <param name="speed">World units per second.</param>
        /// <param name="navigation">
        /// Where the ship can sail. Null for a map without a navigation mask: the ship
        /// then sails in a straight line.
        /// </param>
        public Ship(MapProjection projection, Vector2 position, float speed, NavigationPathfinder navigation = null)
        {
            this.projection = projection ?? throw new ArgumentNullException(nameof(projection));

            if (IsNaN(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            // The negated comparison also rejects NaN.
            if (!(speed > 0f) || float.IsInfinity(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed));
            }

            this.speed = speed;
            this.navigation = navigation;
            Position = ClampToMap(position);
            Heading = CompassDirection.S;

            // Cities are on land: a ship that starts on one starts on the water beside it.
            if (navigation != null && navigation.TryGetNearestNavigable(Position, out Vector2 onWater))
            {
                Position = onWater;
            }
        }

        /// <summary>Normalized map position.</summary>
        public Vector2 Position { get; private set; }

        public Vector2 WorldPosition => projection.NormalizedToWorld(Position);

        /// <summary>
        /// Normalized map position where the ship's route ends, or null when idle. With a
        /// pathfinder it can differ from the point that was ordered.
        /// </summary>
        public Vector2? Destination => IsMoving ? waypoints[waypoints.Count - 1] : (Vector2?)null;

        /// <summary>
        /// Normalized map positions the ship has yet to sail through, the next one first.
        /// Empty when idle.
        /// </summary>
        public IReadOnlyList<Vector2> RemainingWaypoints => waypoints;

        public bool IsMoving => waypoints.Count > 0;

        public CompassDirection Heading { get; private set; }

        /// <summary>
        /// Orders the ship to a normalized map position, replacing any order in progress.
        /// The point is clamped to the map; with a pathfinder, a point the ship cannot
        /// sail to is replaced by the nearest one it can. A NaN point is ignored, and so
        /// is an order for which there is no route.
        /// </summary>
        public void SetDestination(Vector2 destination)
        {
            if (IsNaN(destination))
            {
                return;
            }

            Vector2 clamped = ClampToMap(destination);

            orderedRoute.Clear();

            if (navigation == null)
            {
                orderedRoute.Add(clamped);
            }
            else if (!navigation.TryFindPath(Position, clamped, orderedRoute))
            {
                return;
            }

            waypoints.Clear();
            waypoints.AddRange(orderedRoute);
            SkipWaypointsAlreadyReached();
            FaceNextWaypoint();
        }

        public void Advance(float deltaTime)
        {
            // The negated comparison also rejects NaN.
            if (!IsMoving || !(deltaTime > 0f))
            {
                return;
            }

            // Steps are measured in world space: on a map that is not square, a
            // normalized step would be faster along one axis than the other.
            Vector2 worldPosition = WorldPosition;
            float step = speed * deltaTime;

            while (waypoints.Count > 0)
            {
                Vector2 toWaypoint = projection.NormalizedToWorld(waypoints[0]) - worldPosition;
                float distance = toWaypoint.magnitude;

                if (step < distance)
                {
                    Position = projection.WorldToNormalized(worldPosition + toWaypoint * (step / distance));
                    return;
                }

                // Also taken when the remaining distance underflows to zero. What is
                // left of the step is spent on the next leg, so that the ship does not
                // slow down at a turn.
                Position = waypoints[0];
                worldPosition = WorldPosition;
                step -= distance;
                waypoints.RemoveAt(0);
                FaceNextWaypoint();
            }
        }

        private void SkipWaypointsAlreadyReached()
        {
            while (waypoints.Count > 0)
            {
                Vector2 toWaypoint = projection.NormalizedToWorld(waypoints[0]) - WorldPosition;

                // Compared per component: Vector2 equality is approximate and would drop
                // a very short trip.
                if (toWaypoint.x != 0f || toWaypoint.y != 0f)
                {
                    return;
                }

                waypoints.RemoveAt(0);
            }
        }

        /// <summary>Turns the ship towards its next waypoint; keeps its heading when idle.</summary>
        private void FaceNextWaypoint()
        {
            if (waypoints.Count > 0)
            {
                Vector2 toWaypoint = projection.NormalizedToWorld(waypoints[0]) - WorldPosition;
                Heading = CompassHeading.FromVector(toWaypoint, Heading);
            }
        }

        private static Vector2 ClampToMap(Vector2 normalized)
        {
            return new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y));
        }

        private static bool IsNaN(Vector2 value)
        {
            return float.IsNaN(value.x) || float.IsNaN(value.y);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run **all** EditMode tests (no filter): the existing `ShipTests` must still pass unchanged.
Expected: exit code `0`, `result="Passed"`.

If a `ShipTests` case fails, fix `Ship.cs`, not the test: without a pathfinder the ship must behave exactly as before.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/Ship.cs Assets/Tests/EditMode/ShipNavigationTests.cs Assets/Tests/EditMode/ShipNavigationTests.cs.meta
git commit -m "Le navire suit une route de plusieurs segments sur l'eau

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Wire the mask into the game

**Files:**
- Modify: `Assets/Scripts/Game/ShipsView.cs`
- Create: `Assets/Tests/EditMode/NavigationContentTests.cs`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: `WorldMapView.Definition` (`WorldMapDefinition`), `WorldMapDefinition.NavigationMask` (`NavigationMaskDefinition`, may be null), `NavigationMaskDefinition.CreateGrid()`, `new NavigationPathfinder(NavigationGrid)`, `NavigationPathfinder.HasNavigableCells`, the four-argument `Ship` constructor.
- Produces: nothing other tasks use.

- [ ] **Step 1: Write the content test**

It checks routes on the real mask and logs how long the longest ones take. Create `Assets/Tests/EditMode/NavigationContentTests.cs`:

```csharp
using System.Collections.Generic;
using System.Diagnostics;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>Checks routes between the cities of the real map, on its real mask.</summary>
    public class NavigationContentTests
    {
        private const string MapPath = "Assets/Data/WorldMap/WorldMap.asset";

        [Test]
        public void RoutesBetweenCities_StayOnWater()
        {
            var map = AssetDatabase.LoadAssetAtPath<WorldMapDefinition>(MapPath);
            Assert.IsNotNull(map, MapPath);
            Assert.IsNotNull(map.NavigationMask, "the map has no navigation mask");

            NavigationGrid grid = map.NavigationMask.CreateGrid();
            var pathfinder = new NavigationPathfinder(grid);
            Assert.IsTrue(pathfinder.HasNavigableCells, "the navigation mask is empty");

            // Where a ship that starts on each city is put.
            var harbours = new List<Vector2>();
            var names = new List<string>();

            foreach (CityDefinition city in map.Cities)
            {
                Assert.IsTrue(pathfinder.TryGetNearestNavigable(city.MapPosition, out Vector2 harbour), city.name);
                harbours.Add(harbour);
                names.Add(city.name);
            }

            var waypoints = new List<Vector2>();

            // The first search allocates the pathfinder's buffers; keep it out of the timings.
            pathfinder.TryFindPath(harbours[0], harbours[harbours.Count - 1], waypoints);

            double longest = 0.0;
            string longestRoute = "";

            for (int from = 0; from < harbours.Count; from++)
            {
                for (int to = 0; to < harbours.Count; to++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    bool found = pathfinder.TryFindPath(harbours[from], harbours[to], waypoints);
                    stopwatch.Stop();

                    string route = $"{names[from]} to {names[to]}";
                    Assert.IsTrue(found, route);

                    Vector2 previous = harbours[from];

                    for (int i = 0; i < waypoints.Count; i++)
                    {
                        Assert.IsTrue(
                            NavigationLineOfSight.IsClear(grid, previous, waypoints[i]),
                            $"{route}: leg {i} touches land");
                        previous = waypoints[i];
                    }

                    if (stopwatch.Elapsed.TotalMilliseconds > longest)
                    {
                        longest = stopwatch.Elapsed.TotalMilliseconds;
                        longestRoute = $"{route} ({waypoints.Count} waypoints)";
                    }
                }
            }

            Debug.Log($"Slowest route between cities: {longest:F1} ms, {longestRoute}.");
        }
    }
}
```

- [ ] **Step 2: Run it**

Run the EditMode tests with `'-testFilter','DarkFantasyMerchant.Tests.EditMode.NavigationContentTests'`.
Expected: exit code `0`. (It passes already: it only uses `Core`, finished in Task 4.)

Read the timing: `Select-String -Path Logs\test.log -Pattern 'Slowest route between cities'`. Keep the line for the final report.

- [ ] **Step 3: Give the pathfinder to the ships**

In `Assets/Scripts/Game/ShipsView.cs`:

Add a field after `private readonly List<ShipView> views = new List<ShipView>();`:

```csharp

        // Null on a map without a navigation mask: ships then sail in a straight line.
        private NavigationPathfinder navigation;
```

In `Start`, replace

```csharp
            Spawn(playerShipDefinition, StartPosition());
```

with

```csharp
            navigation = CreateNavigation();
            Spawn(playerShipDefinition, StartPosition());
```

In `Spawn`, replace

```csharp
            var ship = new Ship(mapView.Projection, position, definition.Speed);
```

with

```csharp
            // With a pathfinder, a ship that starts on a city starts on the water beside it.
            var ship = new Ship(mapView.Projection, position, definition.Speed, navigation);
```

Add this method after `StartPosition`:

```csharp
        private NavigationPathfinder CreateNavigation()
        {
            // The definition is set: the map view is ready.
            NavigationMaskDefinition mask = mapView.Definition.NavigationMask;

            if (mask == null)
            {
                return null;
            }

            var pathfinder = new NavigationPathfinder(mask.CreateGrid());

            if (!pathfinder.HasNavigableCells)
            {
                Debug.LogWarning(
                    $"NavigationMaskDefinition '{mask.name}' has no navigable cell; ships cannot move.",
                    mask);
            }

            return pathfinder;
        }
```

- [ ] **Step 4: Run all the tests**

Run all EditMode tests (no filter).
Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 5: Check in the Editor**

This needs the Editor open on `Assets/Scenes/WorldMap.unity`. If the `unity-mcp` tools are available, enter Play mode with `Unity_RunCommand`, read `Unity_GetConsoleLogs` (expected: no error, no warning from `ShipsView`), and take a `Unity_Camera_Capture`: the ship is drawn on the water beside the first city of the map, not on its marker.

Mouse clicks cannot be injected through these tools. Drive the same code path from `Unity_RunCommand`, wait for the ship to sail, then capture:

```csharp
var ships = UnityEngine.Object.FindFirstObjectByType<DarkFantasyMerchant.Game.ShipsView>();
var map = UnityEngine.Object.FindFirstObjectByType<DarkFantasyMerchant.Game.WorldMapView>();
var ship = ships.Ships[0];
var stopwatch = System.Diagnostics.Stopwatch.StartNew();
ship.SetDestination(map.Cities[map.Cities.Count - 1].MapPosition);
stopwatch.Stop();
UnityEngine.Debug.Log($"Order: {stopwatch.Elapsed.TotalMilliseconds:F1} ms, {ship.RemainingWaypoints.Count} waypoints, ends at {ship.Destination}.");
```

Each item must hold, with no error or exception in the Console:

1. The ship starts on water next to its start city.
2. An order to another city sends it along the coast and stops it on the water beside that city, never across land.
3. An order to a point in the open sea is reached in one straight leg.
4. An order to a point far inland stops the ship on the nearest shore.
5. An order to the second sea (the 57 991-cell region, not connected to the main one) stops the ship on the shore of its own sea nearest to the click.
6. The sprite turns at each leg of the route.
7. Selecting, deselecting, panning and zooming behave as before.

If the tools are not available, ask the user to go through the list by right-clicking in Play mode.

- [ ] **Step 6: Report the timings**

Compare the two timings gathered (Step 2, and the `Order:` line of Step 5 for the longest trip tried) with the spec's threshold of **50 ms**.

- At or under 50 ms: say so in the final report, with the figures.
- Over 50 ms: do **not** add an optimisation. Report the figures to the user, with this remedy and its cost: search on a grid of half the resolution (a cell is navigable when its four source cells are), then smooth against the full-resolution grid. It divides the cells to explore by four, costs one more `NavigationGrid` built at startup and about half a day of work, and makes rivers narrower than two cells impassable unless they are searched at full resolution.

- [ ] **Step 7: Update `CLAUDE.md`**

In "Project state", replace

```
the player has one ship to sail on it, and the navigable areas of the map can be painted but are not used yet; nothing else of the game exists yet.
```

with

```
the player has one ship that sails on it over the navigable areas painted on the map; nothing else of the game exists yet.
```

In "Ships", replace the bullet

```
- Ships sail in a straight line and ignore land. Steps are measured in world space through `MapProjection`, not in normalized space, so the speed is the same in every direction on a map that is not square.
```

with

```
- A ship follows a route: a list of waypoints, exposed as `RemainingWaypoints`, with `Destination` the last one. `SetDestination` asks the ship's `NavigationPathfinder` for the route, so the ship sails over water only; without a pathfinder (a map with no mask) the route is one straight leg. What is left of a step after a waypoint is spent on the next leg, so the speed is constant through turns.
- Steps are measured in world space through `MapProjection`, not in normalized space, so the speed is the same in every direction on a map that is not square.
- A ship is created on the water nearest to the position it is given: cities are on land, so the player ship starts beside its start city, not on it.
```

In "Ships", replace

```
A right click sends the selected ship to the clicked point.
```

with

```
A right click sends the selected ship to the clicked point, or to the nearest water it can reach when that point is on land or in another sea.
```

In "Navigation mask", replace the bullet

```
- Nothing in the running game reads the mask yet: ships still ignore land.
```

with

```
- `ShipsView` builds one `NavigationPathfinder` from the mask at startup and gives it to its ships. The mask is not expected to change while the game runs.
```

After the "Navigation mask" subsection, add:

```

### Pathfinding

- Movement rules, shared by the search and by route smoothing: a ship moves to a side neighbour, or to a diagonal one only when both cells beside the move are navigable; a straight leg is allowed only when every cell it touches, even by a corner, is navigable (`NavigationLineOfSight`, with a margin of a thousandth of a cell because positions are floats). A diagonal coastline one cell thick therefore holds, as it does for the fill bucket, and two cells are reachable from one another exactly when they are in the same 4-connected region.
- `NavigationCellSearch` is an A* over the cells; its buffers are allocated once and reused, so an order allocates nothing. `NavigationPathfinder` labels the regions, picks the start and goal cells, runs the search and drops the waypoints a straight leg can skip.
- An order ends on the clicked point itself when it is on water the ship can reach, otherwise on the center of the nearest cell of the ship's own region.
- A search runs synchronously on the click. About 16 bytes per cell are held for the regions and the search, about 14 MB for a 1024-cell-wide grid.
```

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Game/ShipsView.cs Assets/Tests/EditMode/NavigationContentTests.cs Assets/Tests/EditMode/NavigationContentTests.cs.meta CLAUDE.md
git commit -m "Les navires ne naviguent plus que sur les zones navigables

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
