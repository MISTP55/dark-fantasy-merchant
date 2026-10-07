# Navigation Mask Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A grid of navigable / non-navigable cells covering the world map, stored in a ScriptableObject, and a Scene view tool to paint it with a brush, an eraser, a fill bucket and a colour-based detection.

**Architecture:** The grid and all painting logic are a plain C# class in `Core` (`NavigationGrid`), with the colour rule in `WaterColorClassifier`; both are covered by EditMode tests. In `Game`, `NavigationMaskDefinition` serializes the grid's bits and `WorldMapDefinition` references it. In `Editor`, `NavigationMaskAuthoring` holds the asset operations (apply with undo, create, detect), `NavigationMaskSession` holds the working copy and its preview texture, `NavigationMaskTool` is the Scene view `EditorTool` that turns pointer input into strokes, and `NavigationMaskOverlay` is its settings panel.

**Tech Stack:** Unity 6000.6.4f1, URP 17.6 2D Renderer, `UnityEditor.EditorTools`, `UnityEditor.Overlays`, UI Toolkit, Unity Test Framework (NUnit, EditMode).

**Spec:** `docs/superpowers/specs/2026-10-06-navigation-mask-design.md`

## Global Constraints

- All code, comments, log messages, asset and folder names are in English.
- `Core` contains no `MonoBehaviour` and nothing that needs a scene. It may use `UnityEngine` value types (`Vector2`, `Color32`, `Mathf`).
- ScriptableObjects hold static definitions only. `NavigationMaskDefinition` is written by the editing tool only, never while the game runs.
- Map positions are normalized (`(0, 0)` bottom-left, `(1, 1)` top-right). Grid cell `(0, 0)` is the bottom-left one.
- Ship behaviour does not change. Nothing in the running game reads the mask.
- The import settings of `Assets/Art/WorldMap/WorldMap.jpg` are not changed.
- Bits are packed row by row from the bottom row, eight cells per byte, least significant bit first; the array holds `ceil(width × height / 8)` bytes.
- Default grid width `1024`; the tool accepts `64` to `4096`. Height is `max(1, round(width / aspect ratio))`.
- Brush size `1` to `64` cells. Default overlay opacity `0.5`, default saturation threshold `0.2`.
- Mask asset path: `Assets/Data/WorldMap/NavigationMask.asset`.
- Never hand-write `.meta` files. Unity generates them on import; commit them together with their asset.
- Never edit the generated `.sln` / `.csproj`. Exclude `Library/` from searches.
- Match the surrounding code: Allman braces, a blank line before `return`/`if` blocks as in the existing files, XML `<summary>` on public types, comments only where the reason is not obvious.
- Editor UI built in C# with UI Toolkit elements; no uGUI.
- Spec additions, intentional:
  - `NavigationGrid.MaxSize` (`16384`): the constructors also reject a width or height above it, so cell indexes fit in an `int`.
  - `NavigationGrid.SetNavigable(int, int, bool)` and `NavigationGrid.ByteCount(int, int)` are public; detection and the definition need them.
  - Unused bits of the last byte are always zero, so two equal grids have equal bytes.
  - An infinite point or radius is treated like a NaN one: nothing changes.
  - The brush size is a **diameter** in cells; the radius passed to the grid is half of it.
  - `width` and `height` of `NavigationMaskDefinition` are hidden in the Inspector like `bits` (editing them by hand would discard the mask); a custom Inspector shows the size instead.
  - While the tool is active, `CityPlacementTool` draws nothing, so city handles cannot take a click meant for the brush.
  - The tool paints only when the Scene view is in 2D mode, and says so otherwise.
  - **Create mask** assigns an asset that already exists at the path instead of replacing it. Undo covers the assignment, not the creation of the asset file.
  - On press the tool calls `PaintStroke` with two identical points; the result is the same as `PaintDisc`.
- Work on branch `navigation-mask` (already created; it holds the spec and this plan).
- Commit messages are in French (matching the repository history) and end with:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`

## Commands

Batch mode requires the project to be **closed** in the Unity Editor. Check first:

```powershell
Get-Process Unity -ErrorAction SilentlyContinue
```

If Unity is running with this project, use the `unity-mcp` tools instead: `Unity_RunCommand` to trigger a refresh/compile or run tests, then `Unity_GetConsoleLogs` to read errors.

**Run EditMode tests** (all, or add `-testFilter`). `Unity.exe` is a GUI executable, so it must be started with `-Wait` or PowerShell returns immediately. Do **not** add `-nographics`: the detection tests need a graphics device.

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe"
Remove-Item Logs\TestResults.xml -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList '-batchmode','-projectPath','.','-runTests','-testPlatform','EditMode','-testResults','Logs\TestResults.xml','-logFile','Logs\test.log'
"exit code: $($p.ExitCode)"
Select-String -Path Logs\TestResults.xml -Pattern '<test-run ' | ForEach-Object { $_.Line }
```

To run one fixture, append `'-testFilter','DarkFantasyMerchant.Tests.EditMode.NavigationGridTests'` to the argument list.

Reading the result:

- Exit code `0` and `result="Passed"` in the `<test-run` line: all tests passed.
- Exit code `2`: at least one test failed. Details: `Select-String -Path Logs\TestResults.xml -Pattern 'result="Failed"'`.
- Exit code `1` and no `TestResults.xml`: compilation failed. Details: `Select-String -Path Logs\test.log -Pattern 'error CS'`.

A test file that references a type or member that does not exist yet fails the **whole compilation** (exit code `1`), not just that test. That is the expected "red" for the first step of each task.

A run takes one to three minutes. The first run after adding files also generates their `.meta` files; add them to the commit.

## Review Focus

Inputs the spec implies but does not spell out, most likely first. Each one is pinned by a test in the task named.

1. **Dragging the brush far outside the map, or back in from far outside** (the Scene view is much larger than the map): the stroke paints only its part over the map, costs a bounded amount of work and never freezes the Editor. Tests in Task 2.
2. **Undo right after a stroke, then painting again:** the working copy must follow the asset, or the next stroke would bring back what was just undone. Tests in Task 7.
3. **A mask asset whose data does not match its size** (merge conflict, hand-edited YAML, half-written file): opening the tool gives an empty grid of the stored size instead of an exception, and painting works. Tests in Task 5.
4. **A detection that is upside down, or that reads the whole texture when the map sprite is only part of it:** cell `(0, 0)` must come from the bottom-left of the sprite's own rectangle. Tests in Task 6.
5. **Filling an open sea on the full-size grid** (about 900,000 cells in one region): the fill completes without a stack overflow. Test in Task 3.

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/Core/NavigationGrid.cs` | Create | The grid: storage, queries, disc, stroke, fill, resample. |
| `Assets/Scripts/Core/WaterColorClassifier.cs` | Create | Whether a colour is water. |
| `Assets/Scripts/Game/NavigationMaskDefinition.cs` | Create | ScriptableObject storing a grid's size and bits. |
| `Assets/Scripts/Game/WorldMapDefinition.cs` | Modify | `navigationMask` reference. |
| `Assets/Scripts/Editor/WorldMapEditorContext.cs` | Create | Finds the active map view and definition. |
| `Assets/Scripts/Editor/CityPlacementTool.cs` | Modify | Uses the shared context; idle while the mask tool is active. |
| `Assets/Scripts/Editor/NavigationMaskAuthoring.cs` | Create | Asset operations: height for a width, apply with undo, create, detect. |
| `Assets/Scripts/Editor/NavigationMaskSession.cs` | Create | Working copy of the grid, preview texture, commit. |
| `Assets/Scripts/Editor/NavigationMaskToolSettings.cs` | Create | Mode, brush size, opacity, threshold in `EditorPrefs`. |
| `Assets/Scripts/Editor/NavigationMaskTool.cs` | Create | Scene view `EditorTool`: input, overlay drawing, brush cursor. |
| `Assets/Scripts/Editor/NavigationMaskOverlay.cs` | Create | Scene view panel. |
| `Assets/Scripts/Editor/NavigationMaskDefinitionEditor.cs` | Create | Inspector summary of a mask asset. |
| `Assets/Tests/EditMode/GridAssert.cs` | Create | Test helpers for grids. |
| `Assets/Tests/EditMode/NavigationGridTests.cs` | Create | Storage and queries. |
| `Assets/Tests/EditMode/NavigationGridPaintTests.cs` | Create | Disc and stroke. |
| `Assets/Tests/EditMode/NavigationGridFillTests.cs` | Create | Fill and resample. |
| `Assets/Tests/EditMode/WaterColorClassifierTests.cs` | Create | |
| `Assets/Tests/EditMode/NavigationMaskDefinitionTests.cs` | Create | |
| `Assets/Tests/EditMode/NavigationMaskAuthoringTests.cs` | Create | |
| `Assets/Tests/EditMode/NavigationMaskSessionTests.cs` | Create | |
| `CLAUDE.md` | Modify | "Navigation mask" subsection. |

`Assets/Data/WorldMap/NavigationMask.asset` is **not** created by this plan: the designer creates it with the panel's **Create mask** button.

---

### Task 1: Navigation grid — storage and queries

**Files:**
- Create: `Assets/Scripts/Core/NavigationGrid.cs`
- Test: `Assets/Tests/EditMode/NavigationGridTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces, in `DarkFantasyMerchant.Core.NavigationGrid`:
  - `const int MaxSize = 16384`
  - `NavigationGrid(int width, int height)`
  - `NavigationGrid(int width, int height, byte[] bits)`
  - `int Width { get; }`, `int Height { get; }`
  - `static int ByteCount(int width, int height)`
  - `bool IsNavigable(int x, int y)`, `bool IsNavigable(Vector2 normalized)`
  - `bool SetNavigable(int x, int y, bool navigable)` — returns true when the cell changed
  - `void Clear(bool navigable)`
  - `byte[] ToBytes()`
  - private `bool TryGetCell(Vector2 normalized, out int x, out int y)` and `static bool IsFinite(float)` / `IsFinite(Vector2)`, used by Tasks 2 and 3 inside the same class.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/NavigationGridTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationGridTests
    {
        [TestCase(0, 4)]
        [TestCase(4, 0)]
        [TestCase(-1, 4)]
        [TestCase(4, -1)]
        [TestCase(NavigationGrid.MaxSize + 1, 4)]
        [TestCase(4, NavigationGrid.MaxSize + 1)]
        public void Constructors_RejectInvalidDimensions(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationGrid(width, height));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationGrid(width, height, new byte[1]));
        }

        [Test]
        public void Constructor_RejectsANullBitArray()
        {
            Assert.Throws<ArgumentNullException>(() => new NavigationGrid(4, 3, null));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        public void Constructor_RejectsABitArrayOfTheWrongLength(int length)
        {
            // 4 x 3 = 12 cells = 2 bytes.
            Assert.Throws<ArgumentException>(() => new NavigationGrid(4, 3, new byte[length]));
        }

        [TestCase(4, 3, 2)]
        [TestCase(8, 1, 1)]
        [TestCase(9, 1, 2)]
        [TestCase(1024, 879, 112512)]
        public void ByteCount_IsTheCellCountRoundedUpToBytes(int width, int height, int expected)
        {
            Assert.AreEqual(expected, NavigationGrid.ByteCount(width, height));
        }

        [Test]
        public void NewGrid_HasItsDimensions_AndIsEntirelyNonNavigable()
        {
            var grid = new NavigationGrid(4, 3);

            Assert.AreEqual(4, grid.Width);
            Assert.AreEqual(3, grid.Height);

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    Assert.IsFalse(grid.IsNavigable(x, y), $"({x}, {y})");
                }
            }
        }

        [Test]
        public void SetNavigable_SetsOneCell_AndReportsTheChange()
        {
            var grid = new NavigationGrid(4, 3);

            Assert.IsTrue(grid.SetNavigable(2, 1, true));
            Assert.IsFalse(grid.SetNavigable(2, 1, true), "already navigable");

            Assert.IsTrue(grid.IsNavigable(2, 1));
            Assert.IsFalse(grid.IsNavigable(1, 1));
            Assert.IsFalse(grid.IsNavigable(3, 1));
            Assert.IsFalse(grid.IsNavigable(2, 0));
            Assert.IsFalse(grid.IsNavigable(2, 2));

            Assert.IsTrue(grid.SetNavigable(2, 1, false));
            Assert.IsFalse(grid.IsNavigable(2, 1));
        }

        [TestCase(-1, 0)]
        [TestCase(4, 0)]
        [TestCase(0, -1)]
        [TestCase(0, 3)]
        public void CellsOutsideTheGrid_AreNotNavigable_AndCannotBeSet(int x, int y)
        {
            var grid = new NavigationGrid(4, 3);
            grid.Clear(true);

            Assert.IsFalse(grid.IsNavigable(x, y));
            Assert.IsFalse(grid.SetNavigable(x, y, false));
            CollectionAssert.AreEqual(new byte[] { 0xFF, 0x0F }, grid.ToBytes());
        }

        // A 4 x 2 grid: not square, so a swapped axis shows.
        [TestCase(0f, 0f, 0, 0)]
        [TestCase(1f, 1f, 3, 1)]
        [TestCase(1f, 0f, 3, 0)]
        [TestCase(0f, 1f, 0, 1)]
        [TestCase(0.49f, 0.49f, 1, 0)]
        [TestCase(0.5f, 0.5f, 2, 1)]
        [TestCase(0.99f, 0.2f, 3, 0)]
        public void NormalizedPoint_MapsToItsCell(float u, float v, int x, int y)
        {
            var only = new NavigationGrid(4, 2);
            only.SetNavigable(x, y, true);

            var allBut = new NavigationGrid(4, 2);
            allBut.Clear(true);
            allBut.SetNavigable(x, y, false);

            Assert.IsTrue(only.IsNavigable(new Vector2(u, v)));
            Assert.IsFalse(allBut.IsNavigable(new Vector2(u, v)));
        }

        [TestCase(-0.01f, 0.5f)]
        [TestCase(1.01f, 0.5f)]
        [TestCase(0.5f, -0.01f)]
        [TestCase(0.5f, 1.01f)]
        [TestCase(float.NaN, 0.5f)]
        [TestCase(0.5f, float.NaN)]
        [TestCase(float.PositiveInfinity, 0.5f)]
        [TestCase(0.5f, float.NegativeInfinity)]
        public void PointsOutsideTheMap_AreNotNavigable(float u, float v)
        {
            var grid = new NavigationGrid(4, 2);
            grid.Clear(true);

            Assert.IsFalse(grid.IsNavigable(new Vector2(u, v)));
        }

        [Test]
        public void Clear_SetsEveryCell()
        {
            var grid = new NavigationGrid(4, 3);

            grid.Clear(true);

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    Assert.IsTrue(grid.IsNavigable(x, y), $"({x}, {y})");
                }
            }

            grid.Clear(false);
            CollectionAssert.AreEqual(new byte[] { 0x00, 0x00 }, grid.ToBytes());
        }

        [Test]
        public void ToBytes_PacksRowsFromTheBottom_LeastSignificantBitFirst()
        {
            var grid = new NavigationGrid(4, 3);
            grid.SetNavigable(0, 0, true);   // cell index 0  -> byte 0, bit 0
            grid.SetNavigable(3, 2, true);   // cell index 11 -> byte 1, bit 3

            CollectionAssert.AreEqual(new byte[] { 0x01, 0x08 }, grid.ToBytes());
        }

        [Test]
        public void Clear_LeavesTheUnusedBitsOfTheLastByteAtZero()
        {
            var grid = new NavigationGrid(4, 3);

            grid.Clear(true);

            CollectionAssert.AreEqual(new byte[] { 0xFF, 0x0F }, grid.ToBytes());
        }

        [Test]
        public void Constructor_FromBytes_RestoresTheCells_AndDropsUnusedBits()
        {
            var grid = new NavigationGrid(4, 3, new byte[] { 0x01, 0xF8 });

            Assert.IsTrue(grid.IsNavigable(0, 0));
            Assert.IsTrue(grid.IsNavigable(3, 2));
            Assert.IsFalse(grid.IsNavigable(1, 0));
            CollectionAssert.AreEqual(new byte[] { 0x01, 0x08 }, grid.ToBytes());
        }

        [Test]
        public void Constructor_CopiesTheBitArray()
        {
            byte[] bits = { 0x01, 0x00 };
            var grid = new NavigationGrid(4, 3, bits);

            bits[0] = 0x00;

            Assert.IsTrue(grid.IsNavigable(0, 0));
        }

        [Test]
        public void ToBytes_ReturnsACopy()
        {
            var grid = new NavigationGrid(4, 3);

            grid.ToBytes()[0] = 0xFF;

            Assert.IsFalse(grid.IsNavigable(0, 0));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests (see Commands).
Expected: exit code `1`, and `Logs\test.log` contains `error CS0246` for `NavigationGrid`.

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/NavigationGrid.cs`:

```csharp
using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Which cells of the map ships can sail on. Cell (0, 0) is the bottom-left one,
    /// matching normalized map coordinates; the grid covers the whole map.
    /// </summary>
    public sealed class NavigationGrid
    {
        /// <summary>Largest width or height, so that cell indexes fit in an int.</summary>
        public const int MaxSize = 16384;

        // Row by row from the bottom row, eight cells per byte, least significant bit
        // first. Unused bits of the last byte stay at zero.
        private readonly byte[] bits;

        /// <summary>Creates a grid in which no cell is navigable.</summary>
        public NavigationGrid(int width, int height)
        {
            if (width < 1 || width > MaxSize)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height < 1 || height > MaxSize)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            Width = width;
            Height = height;
            bits = new byte[ByteCount(width, height)];
        }

        /// <param name="bits">Packed cells, as returned by <see cref="ToBytes"/>; copied.</param>
        public NavigationGrid(int width, int height, byte[] bits)
            : this(width, height)
        {
            if (bits == null)
            {
                throw new ArgumentNullException(nameof(bits));
            }

            if (bits.Length != this.bits.Length)
            {
                throw new ArgumentException(
                    $"Expected {this.bits.Length} bytes for a {width} x {height} grid, got {bits.Length}.",
                    nameof(bits));
            }

            Array.Copy(bits, this.bits, bits.Length);
            ClearUnusedBits();
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>Number of bytes that hold a grid of this size.</summary>
        public static int ByteCount(int width, int height)
        {
            return (width * height + 7) / 8;
        }

        /// <returns>False outside the grid.</returns>
        public bool IsNavigable(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return false;
            }

            int index = y * Width + x;

            return (bits[index >> 3] & (1 << (index & 7))) != 0;
        }

        /// <returns>False outside the map and for a NaN point.</returns>
        public bool IsNavigable(Vector2 normalized)
        {
            return TryGetCell(normalized, out int x, out int y) && IsNavigable(x, y);
        }

        /// <returns>True when the cell changed. A cell outside the grid is ignored.</returns>
        public bool SetNavigable(int x, int y, bool navigable)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return false;
            }

            int index = y * Width + x;
            int mask = 1 << (index & 7);
            bool current = (bits[index >> 3] & mask) != 0;

            if (current == navigable)
            {
                return false;
            }

            bits[index >> 3] ^= (byte)mask;
            return true;
        }

        public void Clear(bool navigable)
        {
            byte value = navigable ? (byte)0xFF : (byte)0x00;

            for (int i = 0; i < bits.Length; i++)
            {
                bits[i] = value;
            }

            ClearUnusedBits();
        }

        /// <returns>A copy of the packed cells.</returns>
        public byte[] ToBytes()
        {
            return (byte[])bits.Clone();
        }

        private bool TryGetCell(Vector2 normalized, out int x, out int y)
        {
            x = 0;
            y = 0;

            // Written so that a NaN component fails the test.
            if (!(normalized.x >= 0f && normalized.x <= 1f && normalized.y >= 0f && normalized.y <= 1f))
            {
                return false;
            }

            // A coordinate of exactly 1 belongs to the last cell.
            x = Mathf.Min((int)(normalized.x * Width), Width - 1);
            y = Mathf.Min((int)(normalized.y * Height), Height - 1);
            return true;
        }

        private void ClearUnusedBits()
        {
            int usedBits = (Width * Height) & 7;

            if (usedBits != 0)
            {
                bits[bits.Length - 1] &= (byte)((1 << usedBits) - 1);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }
    }
}
```

`IsFinite` is unused until Task 2; a compiler warning for it is expected and goes away there.

- [ ] **Step 4: Run the tests to verify they pass**

Run with `-testFilter DarkFantasyMerchant.Tests.EditMode.NavigationGridTests`.
Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Core Assets/Tests/EditMode
git commit -m "Grille de navigation : stockage et interrogation des cellules`n`nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Navigation grid — disc and stroke

**Files:**
- Modify: `Assets/Scripts/Core/NavigationGrid.cs`
- Create: `Assets/Tests/EditMode/GridAssert.cs`
- Test: `Assets/Tests/EditMode/NavigationGridPaintTests.cs`

**Interfaces:**
- Consumes: `NavigationGrid` from Task 1, including its private `TryGetCell`, `IsFinite` and public `SetNavigable`.
- Produces:
  - `bool NavigationGrid.PaintDisc(Vector2 center, float radiusInCells, bool navigable)`
  - `bool NavigationGrid.PaintStroke(Vector2 from, Vector2 to, float radiusInCells, bool navigable)`
  - Test helpers in `DarkFantasyMerchant.Tests.EditMode.GridAssert`:
    `static int CountNavigable(NavigationGrid grid)` and
    `static Vector2 CellCenter(NavigationGrid grid, int x, int y)` (normalized).

All tests below use 8 × 8 grids on purpose: cell centers are then exact binary fractions, so cells that lie exactly on the brush radius are decided the same way on every machine.

- [ ] **Step 1: Write the test helpers**

`Assets/Tests/EditMode/GridAssert.cs`:

```csharp
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    internal static class GridAssert
    {
        public static int CountNavigable(NavigationGrid grid)
        {
            int count = 0;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.IsNavigable(x, y))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>Normalized position of the center of a cell.</summary>
        public static Vector2 CellCenter(NavigationGrid grid, int x, int y)
        {
            return new Vector2((x + 0.5f) / grid.Width, (y + 0.5f) / grid.Height);
        }
    }
}
```

- [ ] **Step 2: Write the failing tests**

`Assets/Tests/EditMode/NavigationGridPaintTests.cs`:

```csharp
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationGridPaintTests
    {
        private NavigationGrid grid;

        [SetUp]
        public void SetUp()
        {
            grid = new NavigationGrid(8, 8);
        }

        [Test]
        public void PaintDisc_SetsTheCellsWhoseCenterIsWithinTheRadius()
        {
            bool changed = grid.PaintDisc(GridAssert.CellCenter(grid, 4, 4), 1f, true);

            Assert.IsTrue(changed);
            Assert.IsTrue(grid.IsNavigable(4, 4));
            Assert.IsTrue(grid.IsNavigable(3, 4));
            Assert.IsTrue(grid.IsNavigable(5, 4));
            Assert.IsTrue(grid.IsNavigable(4, 3));
            Assert.IsTrue(grid.IsNavigable(4, 5));
            Assert.AreEqual(5, GridAssert.CountNavigable(grid), "diagonals are 1.41 cells away");
        }

        [Test]
        public void PaintDisc_WithALargerRadius_ReachesTheDiagonals()
        {
            grid.PaintDisc(GridAssert.CellCenter(grid, 4, 4), 1.5f, true);

            Assert.IsTrue(grid.IsNavigable(3, 3));
            Assert.IsTrue(grid.IsNavigable(5, 5));
            Assert.AreEqual(9, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintDisc_IsClippedAtTheEdges()
        {
            // Bottom-left corner of the map. Cell centers at 0.71, 1.58, 1.58 and 2.12 cells.
            grid.PaintDisc(Vector2.zero, 2f, true);

            Assert.IsTrue(grid.IsNavigable(0, 0));
            Assert.IsTrue(grid.IsNavigable(1, 0));
            Assert.IsTrue(grid.IsNavigable(0, 1));
            Assert.AreEqual(3, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintDisc_CenteredOutsideTheMap_PaintsThePartThatOverlapsIt()
        {
            // One cell to the left of the map, level with the center of row 4.
            var center = new Vector2(-1f / 8f, 4.5f / 8f);

            bool changed = grid.PaintDisc(center, 2f, true);

            Assert.IsTrue(changed);
            Assert.IsTrue(grid.IsNavigable(0, 3));
            Assert.IsTrue(grid.IsNavigable(0, 4));
            Assert.IsTrue(grid.IsNavigable(0, 5));
            Assert.AreEqual(3, GridAssert.CountNavigable(grid));
        }

        [TestCase(0.01f)]
        [TestCase(0f)]
        [TestCase(-3f)]
        public void PaintDisc_WithATinyRadius_PaintsTheCellUnderThePoint(float radius)
        {
            // Not on a cell center: (3.25, 6.75) in cells.
            bool changed = grid.PaintDisc(new Vector2(3.25f / 8f, 6.75f / 8f), radius, true);

            Assert.IsTrue(changed);
            Assert.IsTrue(grid.IsNavigable(3, 6));
            Assert.AreEqual(1, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintDisc_OnTheTopRightCorner_PaintsTheLastCell()
        {
            grid.PaintDisc(Vector2.one, 0f, true);

            Assert.IsTrue(grid.IsNavigable(7, 7));
            Assert.AreEqual(1, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintDisc_ReportsNoChange_WhenTheCellsAlreadyHaveTheValue()
        {
            Vector2 center = GridAssert.CellCenter(grid, 4, 4);

            Assert.IsFalse(grid.PaintDisc(center, 1f, false), "the grid starts non-navigable");
            Assert.IsTrue(grid.PaintDisc(center, 1f, true));
            Assert.IsFalse(grid.PaintDisc(center, 1f, true));
        }

        [Test]
        public void PaintDisc_NonNavigable_Erases()
        {
            grid.Clear(true);

            bool changed = grid.PaintDisc(GridAssert.CellCenter(grid, 4, 4), 1f, false);

            Assert.IsTrue(changed);
            Assert.IsFalse(grid.IsNavigable(4, 4));
            Assert.AreEqual(64 - 5, GridAssert.CountNavigable(grid));
        }

        [TestCase(float.NaN, 0.5f, 1f)]
        [TestCase(0.5f, float.NaN, 1f)]
        [TestCase(0.5f, 0.5f, float.NaN)]
        [TestCase(float.PositiveInfinity, 0.5f, 1f)]
        [TestCase(0.5f, 0.5f, float.PositiveInfinity)]
        public void PaintDisc_IgnoresNonFiniteInput(float u, float v, float radius)
        {
            Assert.IsFalse(grid.PaintDisc(new Vector2(u, v), radius, true));
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintStroke_CoversTheSegmentWithTheBrushRadius()
        {
            Vector2 from = GridAssert.CellCenter(grid, 2, 4);
            Vector2 to = GridAssert.CellCenter(grid, 5, 4);

            bool changed = grid.PaintStroke(from, to, 1f, true);

            Assert.IsTrue(changed);

            for (int x = 1; x <= 6; x++)
            {
                Assert.IsTrue(grid.IsNavigable(x, 4), $"({x}, 4)");
            }

            for (int x = 2; x <= 5; x++)
            {
                Assert.IsTrue(grid.IsNavigable(x, 3), $"({x}, 3)");
                Assert.IsTrue(grid.IsNavigable(x, 5), $"({x}, 5)");
            }

            Assert.AreEqual(14, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintStroke_BetweenDistantPoints_LeavesNoGap()
        {
            var wide = new NavigationGrid(64, 32);

            // A brush much smaller than a cell: only the cells under the samples are painted.
            wide.PaintStroke(new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.8f), 0.1f, true);

            for (int x = 7; x <= 57; x++)
            {
                bool columnPainted = false;

                for (int y = 0; y < wide.Height; y++)
                {
                    columnPainted |= wide.IsNavigable(x, y);
                }

                Assert.IsTrue(columnPainted, $"column {x}");
            }
        }

        [Test]
        public void PaintStroke_OfZeroLength_IsADisc()
        {
            Vector2 point = GridAssert.CellCenter(grid, 4, 4);
            var disc = new NavigationGrid(8, 8);
            disc.PaintDisc(point, 1.5f, true);

            grid.PaintStroke(point, point, 1.5f, true);

            CollectionAssert.AreEqual(disc.ToBytes(), grid.ToBytes());
        }

        [Test]
        public void PaintStroke_FromFarOutsideTheMap_PaintsOnlyThePartOverTheMap()
        {
            // A million map widths to the left: without clipping this would be
            // sixteen million samples.
            var from = new Vector2(-1e6f, 4.5f / 8f);
            Vector2 to = GridAssert.CellCenter(grid, 4, 4);

            bool changed = grid.PaintStroke(from, to, 0.1f, true);

            Assert.IsTrue(changed);

            for (int x = 0; x <= 4; x++)
            {
                Assert.IsTrue(grid.IsNavigable(x, 4), $"({x}, 4)");
            }

            Assert.AreEqual(5, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintStroke_EntirelyOutsideTheMap_PaintsNothing()
        {
            bool changed = grid.PaintStroke(new Vector2(-5f, 3f), new Vector2(-2f, 9f), 1f, true);

            Assert.IsFalse(changed);
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void PaintStroke_ReportsNoChange_WhenTheCellsAlreadyHaveTheValue()
        {
            Vector2 from = GridAssert.CellCenter(grid, 2, 4);
            Vector2 to = GridAssert.CellCenter(grid, 5, 4);

            Assert.IsTrue(grid.PaintStroke(from, to, 1f, true));
            Assert.IsFalse(grid.PaintStroke(from, to, 1f, true));
        }

        [TestCase(float.NaN, 0.5f, 0.5f, 0.5f, 1f)]
        [TestCase(0.5f, 0.5f, 0.5f, float.NaN, 1f)]
        [TestCase(0.5f, 0.5f, 0.5f, 0.5f, float.NaN)]
        [TestCase(float.NegativeInfinity, 0.5f, 0.5f, 0.5f, 1f)]
        [TestCase(0.5f, 0.5f, float.PositiveInfinity, 0.5f, 1f)]
        public void PaintStroke_IgnoresNonFiniteInput(float fromU, float fromV, float toU, float toV, float radius)
        {
            bool changed = grid.PaintStroke(new Vector2(fromU, fromV), new Vector2(toU, toV), radius, true);

            Assert.IsFalse(changed);
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run the EditMode tests.
Expected: exit code `1`, `error CS1061` for `PaintDisc` and `PaintStroke`.

- [ ] **Step 4: Write the implementation**

In `Assets/Scripts/Core/NavigationGrid.cs`, add this constant under `MaxSize`:

```csharp
        // Distance between two discs of a stroke, in cells.
        private const double StrokeSpacing = 0.5;
```

Add these methods after `Clear`:

```csharp
        /// <summary>
        /// Sets every cell whose center is within the radius of a normalized point, and
        /// always the cell under the point.
        /// </summary>
        /// <returns>True when at least one cell changed.</returns>
        public bool PaintDisc(Vector2 center, float radiusInCells, bool navigable)
        {
            if (!IsFinite(center) || !IsFinite(radiusInCells))
            {
                return false;
            }

            return PaintDiscAt(center.x * Width, center.y * Height, radiusInCells, navigable);
        }

        /// <summary>Paints discs along a segment, close enough to leave no gap.</summary>
        /// <returns>True when at least one cell changed.</returns>
        public bool PaintStroke(Vector2 from, Vector2 to, float radiusInCells, bool navigable)
        {
            if (!IsFinite(from) || !IsFinite(to) || !IsFinite(radiusInCells))
            {
                return false;
            }

            // In doubles: a pointer dragged far outside the map gives coordinates whose
            // float precision is coarser than a cell.
            double startX = (double)from.x * Width;
            double startY = (double)from.y * Height;
            double deltaX = (double)to.x * Width - startX;
            double deltaY = (double)to.y * Height - startY;

            // Only the part of the segment within reach of the grid can paint anything;
            // walking the rest would cost time in proportion to the pointer's distance.
            double reach = Math.Max(radiusInCells, 0f) + 1.0;
            double first = 0.0;
            double last = 1.0;

            if (!ClipToRange(startX, deltaX, -reach, Width + reach, ref first, ref last)
                || !ClipToRange(startY, deltaY, -reach, Height + reach, ref first, ref last))
            {
                return false;
            }

            double length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY) * (last - first);
            int steps = Math.Max(1, (int)Math.Ceiling(length / StrokeSpacing));
            bool changed = false;

            for (int i = 0; i <= steps; i++)
            {
                double t = first + (last - first) * i / steps;

                changed |= PaintDiscAt(
                    (float)(startX + deltaX * t),
                    (float)(startY + deltaY * t),
                    radiusInCells,
                    navigable);
            }

            return changed;
        }
```

Add these private methods before `ClearUnusedBits`:

```csharp
        /// <param name="centerX">In cells: cell (x, y) spans x to x + 1.</param>
        private bool PaintDiscAt(float centerX, float centerY, float radiusInCells, bool navigable)
        {
            float radius = Mathf.Max(radiusInCells, 0f);
            bool changed = false;

            // The cell under the point is always painted, so a brush smaller than a cell
            // still draws.
            if (centerX >= 0f && centerX <= Width && centerY >= 0f && centerY <= Height)
            {
                changed |= SetNavigable(
                    Mathf.Min((int)centerX, Width - 1),
                    Mathf.Min((int)centerY, Height - 1),
                    navigable);
            }

            int minX = FloorClamped(centerX - radius, Width - 1);
            int maxX = FloorClamped(centerX + radius, Width - 1);
            int minY = FloorClamped(centerY - radius, Height - 1);
            int maxY = FloorClamped(centerY + radius, Height - 1);
            float squaredRadius = radius * radius;

            for (int y = minY; y <= maxY; y++)
            {
                float offsetY = y + 0.5f - centerY;

                for (int x = minX; x <= maxX; x++)
                {
                    float offsetX = x + 0.5f - centerX;

                    if (offsetX * offsetX + offsetY * offsetY <= squaredRadius)
                    {
                        changed |= SetNavigable(x, y, navigable);
                    }
                }
            }

            return changed;
        }

        // Clamped before the conversion: casting a float beyond the int range is undefined.
        private static int FloorClamped(float value, int max)
        {
            return (int)Mathf.Floor(Mathf.Clamp(value, 0f, max));
        }

        /// <summary>
        /// Narrows the part [first, last] of a segment to where one of its coordinates,
        /// start + delta * t, lies in [min, max]. False when no part remains.
        /// </summary>
        private static bool ClipToRange(
            double start, double delta, double min, double max, ref double first, ref double last)
        {
            if (delta == 0.0)
            {
                return start >= min && start <= max;
            }

            double enter = (min - start) / delta;
            double exit = (max - start) / delta;

            if (enter > exit)
            {
                double swap = enter;
                enter = exit;
                exit = swap;
            }

            first = Math.Max(first, enter);
            last = Math.Min(last, exit);
            return first <= last;
        }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run with `-testFilter DarkFantasyMerchant.Tests.EditMode.NavigationGridPaintTests`, then the whole suite.
Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/Core Assets/Tests/EditMode
git commit -m "Grille de navigation : pinceau en disque et trait continu`n`nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Navigation grid — fill and resample

**Files:**
- Modify: `Assets/Scripts/Core/NavigationGrid.cs`
- Test: `Assets/Tests/EditMode/NavigationGridFillTests.cs`

**Interfaces:**
- Consumes: `NavigationGrid` from Tasks 1 and 2 (private `TryGetCell`); `GridAssert.CountNavigable`, `GridAssert.CellCenter` from Task 2.
- Produces:
  - `bool NavigationGrid.Fill(Vector2 point, bool navigable)`
  - `NavigationGrid NavigationGrid.Resampled(int width, int height)`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/NavigationGridFillTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationGridFillTests
    {
        [Test]
        public void Fill_StopsAtACoastline()
        {
            var grid = new NavigationGrid(8, 8);

            // A painted coastline: column 4, top to bottom.
            for (int y = 0; y < 8; y++)
            {
                grid.SetNavigable(4, y, true);
            }

            bool changed = grid.Fill(GridAssert.CellCenter(grid, 1, 1), true);

            Assert.IsTrue(changed);
            Assert.IsTrue(grid.IsNavigable(0, 0));
            Assert.IsTrue(grid.IsNavigable(3, 7));
            Assert.IsFalse(grid.IsNavigable(5, 0), "the other side of the coastline");
            Assert.IsFalse(grid.IsNavigable(7, 7));
            Assert.AreEqual(5 * 8, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void Fill_DoesNotLeakThroughADiagonal()
        {
            var grid = new NavigationGrid(8, 8);

            // A one-cell-thick diagonal: its cells touch only by their corners.
            for (int i = 0; i < 8; i++)
            {
                grid.SetNavigable(i, i, true);
            }

            grid.Fill(GridAssert.CellCenter(grid, 0, 7), true);

            Assert.IsTrue(grid.IsNavigable(0, 1));
            Assert.IsTrue(grid.IsNavigable(6, 7));
            Assert.IsFalse(grid.IsNavigable(1, 0));
            Assert.IsFalse(grid.IsNavigable(7, 0));
            Assert.AreEqual(28 + 8, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void Fill_OnARegionAlreadyAtTheValue_ReportsNoChange()
        {
            var grid = new NavigationGrid(8, 8);

            Assert.IsFalse(grid.Fill(GridAssert.CellCenter(grid, 3, 3), false));
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void Fill_NonNavigable_ErasesARegion()
        {
            var grid = new NavigationGrid(8, 8);
            grid.Clear(true);

            Assert.IsTrue(grid.Fill(GridAssert.CellCenter(grid, 3, 3), false));
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void Fill_OfASingleEnclosedCell_FillsOnlyThatCell()
        {
            var grid = new NavigationGrid(8, 8);
            grid.Clear(true);
            grid.SetNavigable(3, 3, false);

            Assert.IsTrue(grid.Fill(GridAssert.CellCenter(grid, 3, 3), true));
            Assert.AreEqual(64, GridAssert.CountNavigable(grid));
        }

        [TestCase(-0.1f, 0.5f)]
        [TestCase(0.5f, 1.1f)]
        [TestCase(float.NaN, 0.5f)]
        [TestCase(0.5f, float.PositiveInfinity)]
        public void Fill_OutsideTheMap_DoesNothing(float u, float v)
        {
            var grid = new NavigationGrid(8, 8);

            Assert.IsFalse(grid.Fill(new Vector2(u, v), true));
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void Fill_HandlesAnOpenSeaOnAFullSizeGrid()
        {
            // The default size for the real map: 900,096 cells in one region.
            var grid = new NavigationGrid(1024, 879);

            Assert.IsTrue(grid.Fill(new Vector2(0.5f, 0.5f), true));

            foreach (byte packed in grid.ToBytes())
            {
                Assert.AreEqual(0xFF, packed);
            }
        }

        [Test]
        public void Resampled_ToTheSameSize_IsIdentical_AndIsANewGrid()
        {
            var grid = new NavigationGrid(8, 4);
            grid.PaintDisc(GridAssert.CellCenter(grid, 3, 2), 1.5f, true);

            NavigationGrid copy = grid.Resampled(8, 4);
            byte[] before = grid.ToBytes();
            copy.Clear(true);

            Assert.AreNotSame(grid, copy);
            CollectionAssert.AreEqual(before, grid.ToBytes(), "the source is untouched");
            CollectionAssert.AreEqual(before, grid.Resampled(8, 4).ToBytes());
        }

        [Test]
        public void Resampled_ToALargerGrid_KeepsTheShape()
        {
            var grid = new NavigationGrid(2, 2);
            grid.SetNavigable(0, 0, true);

            NavigationGrid larger = grid.Resampled(4, 4);

            Assert.AreEqual(4, larger.Width);
            Assert.AreEqual(4, larger.Height);
            Assert.IsTrue(larger.IsNavigable(0, 0));
            Assert.IsTrue(larger.IsNavigable(1, 0));
            Assert.IsTrue(larger.IsNavigable(0, 1));
            Assert.IsTrue(larger.IsNavigable(1, 1));
            Assert.AreEqual(4, GridAssert.CountNavigable(larger));
        }

        [Test]
        public void Resampled_ToASmallerGrid_KeepsTheShape()
        {
            var grid = new NavigationGrid(4, 4);

            // Left half navigable.
            for (int y = 0; y < 4; y++)
            {
                grid.SetNavigable(0, y, true);
                grid.SetNavigable(1, y, true);
            }

            NavigationGrid smaller = grid.Resampled(2, 2);

            Assert.IsTrue(smaller.IsNavigable(0, 0));
            Assert.IsTrue(smaller.IsNavigable(0, 1));
            Assert.IsFalse(smaller.IsNavigable(1, 0));
            Assert.IsFalse(smaller.IsNavigable(1, 1));
        }

        [Test]
        public void Resampled_ToAnotherAspectRatio_StretchesEachAxisSeparately()
        {
            var grid = new NavigationGrid(4, 2);

            // Top row navigable.
            for (int x = 0; x < 4; x++)
            {
                grid.SetNavigable(x, 1, true);
            }

            NavigationGrid tall = grid.Resampled(2, 4);

            Assert.AreEqual(4, GridAssert.CountNavigable(tall));
            Assert.IsTrue(tall.IsNavigable(0, 2));
            Assert.IsTrue(tall.IsNavigable(1, 3));
            Assert.IsFalse(tall.IsNavigable(0, 1));
        }

        [TestCase(0, 4)]
        [TestCase(4, 0)]
        [TestCase(NavigationGrid.MaxSize + 1, 4)]
        public void Resampled_RejectsInvalidDimensions(int width, int height)
        {
            var grid = new NavigationGrid(4, 4);

            Assert.Throws<ArgumentOutOfRangeException>(() => grid.Resampled(width, height));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests.
Expected: exit code `1`, `error CS1061` for `Fill` and `Resampled`.

- [ ] **Step 3: Write the implementation**

In `Assets/Scripts/Core/NavigationGrid.cs`, add `using System.Collections.Generic;` at the top, and these methods after `PaintStroke`:

```csharp
        /// <summary>
        /// Sets the region of cells connected to the one under a normalized point that
        /// share its value. Cells are connected by their sides, not their corners, so a
        /// one-cell-thick diagonal coastline holds.
        /// </summary>
        /// <returns>True when at least one cell changed.</returns>
        public bool Fill(Vector2 point, bool navigable)
        {
            if (!TryGetCell(point, out int startX, out int startY) || IsNavigable(startX, startY) == navigable)
            {
                return false;
            }

            // Iterative: a sea can hold hundreds of thousands of cells. A cell is set when
            // it is pushed, so it is never pushed twice.
            var pending = new Stack<int>();
            SetNavigable(startX, startY, navigable);
            pending.Push(startY * Width + startX);

            while (pending.Count > 0)
            {
                int index = pending.Pop();
                int x = index % Width;
                int y = index / Width;

                FillNeighbour(x - 1, y, navigable, pending);
                FillNeighbour(x + 1, y, navigable, pending);
                FillNeighbour(x, y - 1, navigable, pending);
                FillNeighbour(x, y + 1, navigable, pending);
            }

            return true;
        }

        /// <summary>
        /// A grid of another size in which each cell takes the value of the cell of this
        /// grid under its center.
        /// </summary>
        public NavigationGrid Resampled(int width, int height)
        {
            var result = new NavigationGrid(width, height);

            for (int y = 0; y < height; y++)
            {
                int sourceY = Math.Min((int)((y + 0.5) * Height / height), Height - 1);

                for (int x = 0; x < width; x++)
                {
                    int sourceX = Math.Min((int)((x + 0.5) * Width / width), Width - 1);

                    if (IsNavigable(sourceX, sourceY))
                    {
                        result.SetNavigable(x, y, true);
                    }
                }
            }

            return result;
        }
```

And this private method before `ClearUnusedBits`:

```csharp
        // SetNavigable ignores cells outside the grid and cells that already have the value.
        private void FillNeighbour(int x, int y, bool navigable, Stack<int> pending)
        {
            if (SetNavigable(x, y, navigable))
            {
                pending.Push(y * Width + x);
            }
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run with `-testFilter DarkFantasyMerchant.Tests.EditMode.NavigationGridFillTests`, then the whole suite.
Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Core Assets/Tests/EditMode
git commit -m "Grille de navigation : remplissage et rééchantillonnage`n`nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Water colour classifier

**Files:**
- Create: `Assets/Scripts/Core/WaterColorClassifier.cs`
- Test: `Assets/Tests/EditMode/WaterColorClassifierTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `static bool DarkFantasyMerchant.Core.WaterColorClassifier.IsWater(Color32 color, float maxSaturation)`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/WaterColorClassifierTests.cs`:

```csharp
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class WaterColorClassifierTests
    {
        private const float Threshold = 0.2f;

        [TestCase(128, 128, 128)]
        [TestCase(120, 125, 130)]
        [TestCase(200, 200, 190)]
        public void GreyishColours_AreWater(int r, int g, int b)
        {
            Assert.IsTrue(WaterColorClassifier.IsWater(Colour(r, g, b), Threshold));
        }

        [TestCase(150, 125, 70)]
        [TestCase(220, 140, 60)]
        [TestCase(90, 60, 30)]
        public void BrownAndOrangeColours_AreLand(int r, int g, int b)
        {
            Assert.IsFalse(WaterColorClassifier.IsWater(Colour(r, g, b), Threshold));
        }

        [Test]
        public void TheThreshold_IsInclusive()
        {
            // Saturation (200 - 100) / 200 = 0.5 exactly.
            Color32 colour = Colour(200, 100, 100);

            Assert.IsTrue(WaterColorClassifier.IsWater(colour, 0.5f));
            Assert.IsFalse(WaterColorClassifier.IsWater(colour, 0.49f));
        }

        [Test]
        public void BlackAndWhite_HaveNoSaturation()
        {
            Assert.IsTrue(WaterColorClassifier.IsWater(Colour(0, 0, 0), 0f));
            Assert.IsTrue(WaterColorClassifier.IsWater(Colour(255, 255, 255), 0f));
        }

        [Test]
        public void APureColour_IsWaterOnlyAtFullThreshold()
        {
            Assert.IsFalse(WaterColorClassifier.IsWater(Colour(255, 0, 0), 0.99f));
            Assert.IsTrue(WaterColorClassifier.IsWater(Colour(255, 0, 0), 1f));
        }

        [Test]
        public void Alpha_IsIgnored()
        {
            Assert.IsTrue(WaterColorClassifier.IsWater(new Color32(128, 128, 128, 0), Threshold));
            Assert.IsFalse(WaterColorClassifier.IsWater(new Color32(220, 140, 60, 0), Threshold));
        }

        [Test]
        public void ANaNThreshold_ClassifiesNothingAsWater()
        {
            Assert.IsFalse(WaterColorClassifier.IsWater(Colour(128, 128, 128), float.NaN));
        }

        private static Color32 Colour(int r, int g, int b)
        {
            return new Color32((byte)r, (byte)g, (byte)b, 255);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests.
Expected: exit code `1`, `error CS0103` or `CS0246` for `WaterColorClassifier`.

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/WaterColorClassifier.cs`:

```csharp
using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Tells water from land by colour: on the map the seas are grey and the land is
    /// brown or orange, so water is whatever has little saturation.
    /// </summary>
    public static class WaterColorClassifier
    {
        /// <param name="maxSaturation">Highest HSV saturation, 0 to 1, still counted as water.</param>
        public static bool IsWater(Color32 color, float maxSaturation)
        {
            int max = Math.Max(color.r, Math.Max(color.g, color.b));
            int min = Math.Min(color.r, Math.Min(color.g, color.b));
            float saturation = max == 0 ? 0f : (max - min) / (float)max;

            return saturation <= maxSaturation;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run with `-testFilter DarkFantasyMerchant.Tests.EditMode.WaterColorClassifierTests`.
Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Core Assets/Tests/EditMode
git commit -m "Classification de l'eau par la saturation de la couleur`n`nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Navigation mask definition

**Files:**
- Create: `Assets/Scripts/Game/NavigationMaskDefinition.cs`
- Modify: `Assets/Scripts/Game/WorldMapDefinition.cs`
- Test: `Assets/Tests/EditMode/NavigationMaskDefinitionTests.cs`

**Interfaces:**
- Consumes: `NavigationGrid` (constructors, `Width`, `Height`, `MaxSize`, `ByteCount`, `ToBytes`, `SetNavigable`, `IsNavigable`).
- Produces:
  - `DarkFantasyMerchant.Game.NavigationMaskDefinition : ScriptableObject` with `int Width`, `int Height`, `NavigationGrid CreateGrid()`, `void SetGrid(NavigationGrid grid)`. Serialized field names: `width`, `height`, `bits`.
  - `NavigationMaskDefinition WorldMapDefinition.NavigationMask { get; }`. Serialized field name: `navigationMask`.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/NavigationMaskDefinitionTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationMaskDefinitionTests
    {
        private NavigationMaskDefinition mask;

        [SetUp]
        public void SetUp()
        {
            mask = ScriptableObject.CreateInstance<NavigationMaskDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(mask);
        }

        private static NavigationGrid SampleGrid()
        {
            var grid = new NavigationGrid(5, 3);
            grid.SetNavigable(0, 0, true);
            grid.SetNavigable(4, 2, true);
            grid.SetNavigable(2, 1, true);
            return grid;
        }

        private void SetSerialized(int width, int height, int byteCount)
        {
            var serialized = new SerializedObject(mask);
            serialized.FindProperty("width").intValue = width;
            serialized.FindProperty("height").intValue = height;

            // Emptied and applied first: Unity does not apply a byte array that is only
            // shortened to a non-zero length.
            serialized.FindProperty("bits").ClearArray();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.FindProperty("bits").arraySize = byteCount;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void NewDefinition_CreatesAnEmptyGrid()
        {
            NavigationGrid grid = mask.CreateGrid();

            Assert.IsNotNull(grid);
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void SetGrid_ThenCreateGrid_RoundTrips()
        {
            NavigationGrid source = SampleGrid();

            mask.SetGrid(source);
            NavigationGrid restored = mask.CreateGrid();

            Assert.AreEqual(5, mask.Width);
            Assert.AreEqual(3, mask.Height);
            Assert.AreEqual(5, restored.Width);
            Assert.AreEqual(3, restored.Height);
            CollectionAssert.AreEqual(source.ToBytes(), restored.ToBytes());
        }

        [Test]
        public void SetGrid_CopiesTheGrid()
        {
            NavigationGrid source = SampleGrid();
            byte[] expected = source.ToBytes();

            mask.SetGrid(source);
            source.Clear(true);

            CollectionAssert.AreEqual(expected, mask.CreateGrid().ToBytes());
        }

        [Test]
        public void CreateGrid_ReturnsAnIndependentGridEachTime()
        {
            mask.SetGrid(SampleGrid());

            NavigationGrid first = mask.CreateGrid();
            first.Clear(true);

            Assert.AreNotSame(first, mask.CreateGrid());
            Assert.AreEqual(3, GridAssert.CountNavigable(mask.CreateGrid()));
        }

        [Test]
        public void SetGrid_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => mask.SetGrid(null));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        public void CreateGrid_WithBitsOfTheWrongLength_ReturnsAnEmptyGridOfTheStoredSize(int byteCount)
        {
            mask.SetGrid(SampleGrid());

            // 5 x 3 = 15 cells = 2 bytes.
            SetSerialized(5, 3, byteCount);
            NavigationGrid grid = mask.CreateGrid();

            Assert.AreEqual(5, grid.Width);
            Assert.AreEqual(3, grid.Height);
            Assert.AreEqual(0, GridAssert.CountNavigable(grid));
        }

        [TestCase(0, 3)]
        [TestCase(5, 0)]
        [TestCase(-2, 3)]
        [TestCase(NavigationGrid.MaxSize + 1, 3)]
        public void CreateGrid_WithAnInvalidStoredSize_ReturnsAnEmptyOneCellGrid(int width, int height)
        {
            SetSerialized(width, height, 2);

            NavigationGrid grid = mask.CreateGrid();

            Assert.AreEqual(1, grid.Width);
            Assert.AreEqual(1, grid.Height);
            Assert.IsFalse(grid.IsNavigable(0, 0));
        }

        [Test]
        public void WorldMapDefinition_HasNoMaskByDefault_AndExposesTheAssignedOne()
        {
            var map = ScriptableObject.CreateInstance<WorldMapDefinition>();

            try
            {
                Assert.IsNull(map.NavigationMask);

                var serialized = new SerializedObject(map);
                serialized.FindProperty("navigationMask").objectReferenceValue = mask;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.AreSame(mask, map.NavigationMask);
            }
            finally
            {
                Object.DestroyImmediate(map);
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests.
Expected: exit code `1`, `error CS0246` for `NavigationMaskDefinition`.

- [ ] **Step 3: Write the definition**

`Assets/Scripts/Game/NavigationMaskDefinition.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Static definition of the navigable areas of a map: the size and the packed cells
    /// of a <see cref="NavigationGrid"/>. Painted in the Scene view; never written while
    /// the game runs.
    /// </summary>
    [CreateAssetMenu(fileName = "NavigationMask", menuName = "Dark Fantasy Merchant/Navigation Mask")]
    public sealed class NavigationMaskDefinition : ScriptableObject
    {
        // Hidden: the three fields only make sense together, and changing one by hand
        // would discard the mask.
        [SerializeField, HideInInspector] private int width = 1;
        [SerializeField, HideInInspector] private int height = 1;
        [SerializeField, HideInInspector] private byte[] bits = new byte[0];

        public int Width => width;

        public int Height => height;

        /// <summary>
        /// Builds a grid from the asset. Data that does not fit its stored size, or an
        /// invalid size, gives an empty grid instead of an error, so a damaged asset can
        /// still be opened and repainted.
        /// </summary>
        public NavigationGrid CreateGrid()
        {
            if (width < 1 || width > NavigationGrid.MaxSize || height < 1 || height > NavigationGrid.MaxSize)
            {
                return new NavigationGrid(1, 1);
            }

            if (bits == null || bits.Length != NavigationGrid.ByteCount(width, height))
            {
                return new NavigationGrid(width, height);
            }

            return new NavigationGrid(width, height, bits);
        }

        /// <summary>Copies a grid into the asset. For editor tooling only.</summary>
        public void SetGrid(NavigationGrid grid)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            width = grid.Width;
            height = grid.Height;
            bits = grid.ToBytes();
        }
    }
}
```

- [ ] **Step 4: Reference the mask from the map definition**

In `Assets/Scripts/Game/WorldMapDefinition.cs`, add the field after `maxZoomInOrthographicSize`:

```csharp
        [Tooltip("Where ships can sail. Painted with the Navigation Mask tool of the Scene view.")]
        [SerializeField] private NavigationMaskDefinition navigationMask;
```

and the property after `MaxZoomInOrthographicSize`:

```csharp
        /// <summary>Navigable areas of the map, or null when none has been painted.</summary>
        public NavigationMaskDefinition NavigationMask => navigationMask;
```

- [ ] **Step 5: Run the tests to verify they pass**

Run with `-testFilter DarkFantasyMerchant.Tests.EditMode.NavigationMaskDefinitionTests`, then the whole suite.
Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/Game Assets/Tests/EditMode
git commit -m "Définition du masque de navigation, référencée par la carte`n`nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Editor context and mask authoring

**Files:**
- Create: `Assets/Scripts/Editor/WorldMapEditorContext.cs`
- Modify: `Assets/Scripts/Editor/CityPlacementTool.cs`
- Create: `Assets/Scripts/Editor/NavigationMaskAuthoring.cs`
- Test: `Assets/Tests/EditMode/NavigationMaskAuthoringTests.cs`

**Interfaces:**
- Consumes: `NavigationGrid`, `WaterColorClassifier.IsWater`, `NavigationMaskDefinition` (`CreateGrid`, `SetGrid`), `WorldMapDefinition` (`MapSprite`, `NavigationMask`, serialized `navigationMask` and `mapSprite`).
- Produces, in `DarkFantasyMerchant.Editor`:
  - `static WorldMapView WorldMapEditorContext.FindView()`
  - `static WorldMapDefinition WorldMapEditorContext.FindDefinition(WorldMapView view)`
  - `NavigationMaskAuthoring` constants `MaskAssetPath`, `DefaultWidth` (1024), `MinWidth` (64), `MaxWidth` (4096)
  - `static int NavigationMaskAuthoring.HeightFor(int width, float aspectRatio)`
  - `static bool NavigationMaskAuthoring.TryGetAspectRatio(WorldMapDefinition map, out float aspectRatio)`
  - `static bool NavigationMaskAuthoring.MatchesAspect(int width, int height, float aspectRatio)`
  - `static void NavigationMaskAuthoring.Apply(NavigationMaskDefinition mask, NavigationGrid grid, string undoName)`
  - `static NavigationMaskDefinition NavigationMaskAuthoring.CreateMask(WorldMapDefinition map, string assetPath, int width)`
  - `static NavigationGrid NavigationMaskAuthoring.Detect(Sprite sprite, int width, int height, float maxSaturation)`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/NavigationMaskAuthoringTests.cs`:

```csharp
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Editor;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationMaskAuthoringTests
    {
        private const string TempFolderName = "NavigationMaskAuthoringTestsTemp";
        private const string TempFolder = "Assets/" + TempFolderName;
        private const string TempMaskPath = TempFolder + "/Mask.asset";

        private static readonly Color32 Grey = new Color32(128, 128, 128, 255);
        private static readonly Color32 Orange = new Color32(220, 140, 60, 255);

        private NavigationMaskDefinition mask;
        private WorldMapDefinition map;
        private Texture2D texture;
        private Sprite sprite;

        [SetUp]
        public void SetUp()
        {
            mask = ScriptableObject.CreateInstance<NavigationMaskDefinition>();
            map = ScriptableObject.CreateInstance<WorldMapDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearUndo(mask);
            Undo.ClearUndo(map);
            Object.DestroyImmediate(mask);
            Object.DestroyImmediate(map);

            if (sprite != null)
            {
                Object.DestroyImmediate(sprite);
            }

            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.DeleteAsset(TempFolder);
        }

        /// <summary>Texture with one grey pixel; every other pixel is orange.</summary>
        private void CreateTexture(int width, int height, int greyX, int greyY)
        {
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
            };

            var pixels = new Color32[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Orange;
            }

            pixels[greyY * width + greyX] = Grey;
            texture.SetPixels32(pixels);
            texture.Apply();
        }

        private void CreateSprite(Rect rect)
        {
            sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 1f);
        }

        private void AssignSpriteToMap()
        {
            var serialized = new SerializedObject(map);
            serialized.FindProperty("mapSprite").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TestCase(1024, 2048f / 1758f, 879)]
        [TestCase(100, 1f, 100)]
        [TestCase(10, 3f, 3)]
        [TestCase(64, 1000f, 1)]
        [TestCase(4096, 0.001f, NavigationGrid.MaxSize)]
        public void HeightFor_FollowsTheAspectRatio_WithinTheGridLimits(int width, float aspect, int expected)
        {
            Assert.AreEqual(expected, NavigationMaskAuthoring.HeightFor(width, aspect));
        }

        [Test]
        public void MatchesAspect_IsTrueOnlyForTheHeightOfThatWidth()
        {
            Assert.IsTrue(NavigationMaskAuthoring.MatchesAspect(1024, 879, 2048f / 1758f));
            Assert.IsFalse(NavigationMaskAuthoring.MatchesAspect(1024, 1024, 2048f / 1758f));
        }

        [Test]
        public void TryGetAspectRatio_UsesTheSpriteRectangle_AndFailsWithoutASprite()
        {
            Assert.IsFalse(NavigationMaskAuthoring.TryGetAspectRatio(map, out _));
            Assert.IsFalse(NavigationMaskAuthoring.TryGetAspectRatio(null, out _));

            CreateTexture(8, 2, 0, 0);
            CreateSprite(new Rect(0f, 0f, 4f, 2f));
            AssignSpriteToMap();

            Assert.IsTrue(NavigationMaskAuthoring.TryGetAspectRatio(map, out float aspect));
            Assert.AreEqual(2f, aspect, 1e-6f);
        }

        [Test]
        public void Apply_WritesTheGrid_AndCanBeUndone()
        {
            var grid = new NavigationGrid(4, 4);
            grid.SetNavigable(1, 2, true);

            Undo.IncrementCurrentGroup();
            NavigationMaskAuthoring.Apply(mask, grid, "Test Navigation Mask");

            Assert.AreEqual(4, mask.Width);
            CollectionAssert.AreEqual(grid.ToBytes(), mask.CreateGrid().ToBytes());

            Undo.PerformUndo();

            Assert.AreEqual(1, mask.Width, "back to a new definition");
            Assert.AreEqual(0, GridAssert.CountNavigable(mask.CreateGrid()));

            Undo.PerformRedo();

            CollectionAssert.AreEqual(grid.ToBytes(), mask.CreateGrid().ToBytes());
        }

        [Test]
        public void Detect_PutsCellZeroAtTheBottomLeftOfTheImage()
        {
            CreateTexture(2, 2, 0, 0);
            CreateSprite(new Rect(0f, 0f, 2f, 2f));

            NavigationGrid grid = NavigationMaskAuthoring.Detect(sprite, 2, 2, 0.2f);

            Assert.IsTrue(grid.IsNavigable(0, 0), "the grey pixel");
            Assert.AreEqual(1, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void Detect_ReadsOnlyTheRectangleOfTheSprite()
        {
            // The sprite is the right half of a 4 x 2 texture; the grey pixel is the
            // top-left pixel of that half.
            CreateTexture(4, 2, 2, 1);
            CreateSprite(new Rect(2f, 0f, 2f, 2f));

            NavigationGrid grid = NavigationMaskAuthoring.Detect(sprite, 2, 2, 0.2f);

            Assert.IsTrue(grid.IsNavigable(0, 1));
            Assert.AreEqual(1, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void Detect_UsesTheThreshold()
        {
            CreateTexture(2, 2, 0, 0);
            CreateSprite(new Rect(0f, 0f, 2f, 2f));

            // Orange has a saturation of about 0.73.
            NavigationGrid everything = NavigationMaskAuthoring.Detect(sprite, 2, 2, 0.9f);

            Assert.AreEqual(4, GridAssert.CountNavigable(everything));
        }

        [Test]
        public void CreateMask_CreatesTheAsset_SizedForTheMap_AndAssignsIt()
        {
            AssetDatabase.CreateFolder("Assets", TempFolderName);
            CreateTexture(8, 2, 0, 0);
            CreateSprite(new Rect(0f, 0f, 8f, 2f));
            AssignSpriteToMap();

            NavigationMaskDefinition created = NavigationMaskAuthoring.CreateMask(map, TempMaskPath, 64);

            Assert.IsNotNull(created);
            Assert.AreEqual(TempMaskPath, AssetDatabase.GetAssetPath(created));
            Assert.AreEqual(64, created.Width);
            Assert.AreEqual(16, created.Height);
            Assert.AreEqual(0, GridAssert.CountNavigable(created.CreateGrid()));
            Assert.AreSame(created, map.NavigationMask);
        }

        [Test]
        public void CreateMask_KeepsAnAssetThatAlreadyExistsAtThePath()
        {
            AssetDatabase.CreateFolder("Assets", TempFolderName);
            CreateTexture(8, 2, 0, 0);
            CreateSprite(new Rect(0f, 0f, 8f, 2f));
            AssignSpriteToMap();

            NavigationMaskDefinition first = NavigationMaskAuthoring.CreateMask(map, TempMaskPath, 64);
            NavigationGrid painted = first.CreateGrid();
            painted.SetNavigable(3, 3, true);
            first.SetGrid(painted);

            NavigationMaskDefinition second = NavigationMaskAuthoring.CreateMask(map, TempMaskPath, 128);

            Assert.AreSame(first, second);
            Assert.AreEqual(64, second.Width);
            Assert.IsTrue(second.CreateGrid().IsNavigable(3, 3), "the painted mask is not replaced");
        }

        [Test]
        public void CreateMask_ForAMapWithoutASprite_CreatesNothing()
        {
            AssetDatabase.CreateFolder("Assets", TempFolderName);

            Assert.IsNull(NavigationMaskAuthoring.CreateMask(map, TempMaskPath, 64));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<NavigationMaskDefinition>(TempMaskPath));
            Assert.IsNull(map.NavigationMask);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests.
Expected: exit code `1`, `error CS0103` or `CS0246` for `NavigationMaskAuthoring`.

- [ ] **Step 3: Write the shared editor context**

`Assets/Scripts/Editor/WorldMapEditorContext.cs`:

```csharp
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>Finds the world map the Scene view tools should work on.</summary>
    public static class WorldMapEditorContext
    {
        public static WorldMapView FindView()
        {
            return Object.FindAnyObjectByType<WorldMapView>();
        }

        /// <summary>
        /// The selected map definition if there is one, otherwise the one shown by the
        /// scene's view. Null when there is neither.
        /// </summary>
        public static WorldMapDefinition FindDefinition(WorldMapView view)
        {
            if (Selection.activeObject is WorldMapDefinition selected)
            {
                return selected;
            }

            return view != null ? view.Definition : null;
        }
    }
}
```

- [ ] **Step 4: Make `CityPlacementTool` use it**

In `Assets/Scripts/Editor/CityPlacementTool.cs`, replace these two lines of `OnSceneGui`:

```csharp
            WorldMapView view = Object.FindAnyObjectByType<WorldMapView>();
            WorldMapDefinition definition = FindDefinition(view);
```

with:

```csharp
            WorldMapView view = WorldMapEditorContext.FindView();
            WorldMapDefinition definition = WorldMapEditorContext.FindDefinition(view);
```

and delete the whole private `FindDefinition` method of that file (its nine lines, plus the blank line after it).

- [ ] **Step 5: Write the authoring operations**

`Assets/Scripts/Editor/NavigationMaskAuthoring.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>Operations of the navigation mask tool on assets.</summary>
    public static class NavigationMaskAuthoring
    {
        public const string MaskAssetPath = "Assets/Data/WorldMap/NavigationMask.asset";
        public const int DefaultWidth = 1024;
        public const int MinWidth = 64;
        public const int MaxWidth = 4096;

        /// <summary>Height that keeps the cells of a grid of this width square on the map.</summary>
        public static int HeightFor(int width, float aspectRatio)
        {
            return Mathf.Clamp(Mathf.RoundToInt(width / aspectRatio), 1, NavigationGrid.MaxSize);
        }

        /// <returns>False when the map is null or has no usable sprite.</returns>
        public static bool TryGetAspectRatio(WorldMapDefinition map, out float aspectRatio)
        {
            aspectRatio = 1f;

            if (map == null || map.MapSprite == null)
            {
                return false;
            }

            Rect rect = map.MapSprite.rect;

            if (rect.width <= 0f || rect.height <= 0f)
            {
                return false;
            }

            aspectRatio = rect.width / rect.height;
            return true;
        }

        /// <summary>False after the map image was replaced by one of another shape.</summary>
        public static bool MatchesAspect(int width, int height, float aspectRatio)
        {
            return height == HeightFor(width, aspectRatio);
        }

        /// <summary>Writes a grid into a mask asset as one undo step.</summary>
        public static void Apply(NavigationMaskDefinition mask, NavigationGrid grid, string undoName)
        {
            Undo.RecordObject(mask, undoName);
            mask.SetGrid(grid);
            EditorUtility.SetDirty(mask);
        }

        /// <summary>
        /// Gives a map an empty mask of the given width, stored at <paramref name="assetPath"/>,
        /// whose folder must exist. An asset already at that path is assigned as it is,
        /// never replaced: it may hold hours of painting.
        /// </summary>
        /// <returns>The mask, or null when the map has no sprite to size it from.</returns>
        public static NavigationMaskDefinition CreateMask(WorldMapDefinition map, string assetPath, int width)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (!TryGetAspectRatio(map, out float aspectRatio))
            {
                return null;
            }

            var mask = AssetDatabase.LoadAssetAtPath<NavigationMaskDefinition>(assetPath);

            if (mask == null)
            {
                mask = ScriptableObject.CreateInstance<NavigationMaskDefinition>();
                mask.SetGrid(new NavigationGrid(width, HeightFor(width, aspectRatio)));
                AssetDatabase.CreateAsset(mask, assetPath);
            }

            // SerializedObject records undo and marks the map dirty.
            var serializedMap = new SerializedObject(map);
            serializedMap.FindProperty("navigationMask").objectReferenceValue = mask;
            serializedMap.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return mask;
        }

        /// <summary>
        /// Builds a grid in which a cell is navigable when the map image is water-coloured
        /// there. Reads the sprite through a render texture, so the texture does not have
        /// to be readable.
        /// </summary>
        public static NavigationGrid Detect(Sprite sprite, int width, int height, float maxSaturation)
        {
            if (sprite == null)
            {
                throw new ArgumentNullException(nameof(sprite));
            }

            Texture2D source = sprite.texture;
            Rect rect = sprite.rect;
            var scale = new Vector2(rect.width / source.width, rect.height / source.height);
            var offset = new Vector2(rect.x / source.width, rect.y / source.height);

            // sRGB on both sides, so the bytes read back are the colours of the image and
            // the threshold means what it means in an image editor.
            RenderTexture target = RenderTexture.GetTemporary(
                width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false);

            try
            {
                Graphics.Blit(source, target, scale, offset);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);

                // Row 0 is the bottom row, like the grid.
                Color32[] pixels = readback.GetPixels32();
                var grid = new NavigationGrid(width, height);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (WaterColorClassifier.IsWater(pixels[y * width + x], maxSaturation))
                        {
                            grid.SetNavigable(x, y, true);
                        }
                    }
                }

                return grid;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(readback);
            }
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run with `-testFilter DarkFantasyMerchant.Tests.EditMode.NavigationMaskAuthoringTests`, then the whole suite.
Expected: exit code `0`, `result="Passed"`.

If `Detect_PutsCellZeroAtTheBottomLeftOfTheImage` fails with cell `(0, 1)` navigable instead of `(0, 0)`, the readback is vertically flipped on this graphics API. Fix `Detect` by reading row `height - 1 - y` of `pixels`, keep the comment accurate, and leave the tests as they are. If the three `Detect` tests fail with every cell non-navigable or an exception about a null graphics device, the run was started with `-nographics`; remove it.

- [ ] **Step 7: Commit**

```powershell
git add Assets/Scripts/Editor Assets/Tests/EditMode
git commit -m "Opérations d'édition du masque : création, écriture avec undo, détection`n`nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Editing session

**Files:**
- Create: `Assets/Scripts/Editor/NavigationMaskSession.cs`
- Test: `Assets/Tests/EditMode/NavigationMaskSessionTests.cs`

**Interfaces:**
- Consumes: `NavigationGrid` (`PaintStroke`, `Fill`, `IsNavigable`, `ToBytes`, `Width`, `Height`), `NavigationMaskDefinition.CreateGrid`, `WorldMapDefinition.NavigationMask`, `NavigationMaskAuthoring.Apply`.
- Produces, in `DarkFantasyMerchant.Editor.NavigationMaskSession : IDisposable`:
  - `WorldMapDefinition Map { get; }`, `NavigationMaskDefinition Mask { get; }`
  - `NavigationGrid Grid { get; }` — the working copy, null without a mask
  - `bool HasMask { get; }`, `bool HasNavigableCells { get; }`
  - `void SetMap(WorldMapDefinition map)`
  - `void Reload()`
  - `void Paint(Vector2 from, Vector2 to, float radiusInCells, bool navigable)`
  - `void Fill(Vector2 point, bool navigable)`
  - `bool Commit(string undoName)` — true when something was written
  - `void Replace(NavigationGrid grid, string undoName)`
  - `Texture2D GetPreview()` — white opaque where navigable, transparent elsewhere; null without a mask
  - `void Dispose()`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/NavigationMaskSessionTests.cs`:

```csharp
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Editor;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationMaskSessionTests
    {
        private NavigationMaskDefinition mask;
        private WorldMapDefinition map;
        private NavigationMaskSession session;

        [SetUp]
        public void SetUp()
        {
            mask = ScriptableObject.CreateInstance<NavigationMaskDefinition>();
            mask.SetGrid(new NavigationGrid(8, 4));

            map = ScriptableObject.CreateInstance<WorldMapDefinition>();
            AssignMask(mask);

            session = new NavigationMaskSession();
            session.SetMap(map);
        }

        [TearDown]
        public void TearDown()
        {
            session.Dispose();
            Undo.ClearUndo(mask);
            Object.DestroyImmediate(mask);
            Object.DestroyImmediate(map);
        }

        private void AssignMask(NavigationMaskDefinition value)
        {
            var serialized = new SerializedObject(map);
            serialized.FindProperty("navigationMask").objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private Vector2 Cell(int x, int y)
        {
            return GridAssert.CellCenter(session.Grid, x, y);
        }

        [Test]
        public void SetMap_LoadsAWorkingCopyOfTheMask()
        {
            Assert.AreSame(map, session.Map);
            Assert.AreSame(mask, session.Mask);
            Assert.IsTrue(session.HasMask);
            Assert.AreEqual(8, session.Grid.Width);
            Assert.AreEqual(4, session.Grid.Height);
            Assert.IsFalse(session.HasNavigableCells);
        }

        [Test]
        public void WithoutAMask_ThereIsNoGrid_AndEditingDoesNothing()
        {
            AssignMask(null);
            session.SetMap(map);

            Assert.IsFalse(session.HasMask);
            Assert.IsNull(session.Grid);
            Assert.IsFalse(session.HasNavigableCells);
            Assert.IsNull(session.GetPreview());

            Assert.DoesNotThrow(() => session.Paint(Vector2.zero, Vector2.one, 1f, true));
            Assert.DoesNotThrow(() => session.Fill(Vector2.zero, true));
            Assert.IsFalse(session.Commit("Test"));
        }

        [Test]
        public void SetMap_WithNoMap_ClearsTheSession()
        {
            session.SetMap(null);

            Assert.IsNull(session.Map);
            Assert.IsFalse(session.HasMask);
            Assert.IsNull(session.Grid);
        }

        [Test]
        public void SetMap_PicksUpAMaskAssignedLater()
        {
            AssignMask(null);
            session.SetMap(map);
            Assert.IsNull(session.Grid);

            AssignMask(mask);
            session.SetMap(map);

            Assert.IsNotNull(session.Grid);
        }

        [Test]
        public void SetMap_WithTheSameMap_KeepsUncommittedPaint()
        {
            session.Paint(Cell(2, 1), Cell(2, 1), 0.5f, true);

            session.SetMap(map);

            Assert.IsTrue(session.Grid.IsNavigable(2, 1));
        }

        [Test]
        public void Paint_ChangesTheWorkingCopyOnly_UntilCommitted()
        {
            session.Paint(Cell(2, 1), Cell(5, 1), 0.5f, true);

            Assert.IsTrue(session.Grid.IsNavigable(2, 1));
            Assert.IsTrue(session.Grid.IsNavigable(5, 1));
            Assert.IsTrue(session.HasNavigableCells);
            Assert.AreEqual(0, GridAssert.CountNavigable(mask.CreateGrid()), "not written yet");

            Assert.IsTrue(session.Commit("Paint"));

            CollectionAssert.AreEqual(session.Grid.ToBytes(), mask.CreateGrid().ToBytes());
        }

        [Test]
        public void Commit_WithNothingChanged_WritesNothing()
        {
            Assert.IsFalse(session.Commit("Paint"));

            // Painting non-navigable on an empty grid changes no cell.
            session.Paint(Cell(2, 1), Cell(5, 1), 0.5f, false);
            Assert.IsFalse(session.Commit("Paint"));

            session.Paint(Cell(2, 1), Cell(2, 1), 0.5f, true);
            Assert.IsTrue(session.Commit("Paint"));
            Assert.IsFalse(session.Commit("Paint"), "already written");
        }

        [Test]
        public void Fill_ChangesTheWorkingCopy_AndIsCommitted()
        {
            session.Fill(Cell(0, 0), true);

            Assert.AreEqual(32, GridAssert.CountNavigable(session.Grid));
            Assert.IsTrue(session.Commit("Fill"));
            Assert.AreEqual(32, GridAssert.CountNavigable(mask.CreateGrid()));
        }

        [Test]
        public void AfterUndo_Reload_BringsTheWorkingCopyBackInStepWithTheAsset()
        {
            Undo.IncrementCurrentGroup();
            session.Paint(Cell(2, 1), Cell(2, 1), 0.5f, true);
            session.Commit("Paint");

            Undo.PerformUndo();
            session.Reload();

            Assert.AreEqual(0, GridAssert.CountNavigable(mask.CreateGrid()), "the asset is restored");
            Assert.IsFalse(session.Grid.IsNavigable(2, 1), "and so is the working copy");

            // The next stroke must not bring the undone one back.
            session.Paint(Cell(6, 3), Cell(6, 3), 0.5f, true);
            session.Commit("Paint");

            Assert.AreEqual(1, GridAssert.CountNavigable(mask.CreateGrid()));
        }

        [Test]
        public void Reload_DropsUncommittedPaint()
        {
            session.Paint(Cell(2, 1), Cell(2, 1), 0.5f, true);

            session.Reload();

            Assert.IsFalse(session.Grid.IsNavigable(2, 1));
            Assert.IsFalse(session.Commit("Paint"));
        }

        [Test]
        public void Replace_SwapsTheGrid_AndWritesItAtOnce()
        {
            var replacement = new NavigationGrid(16, 8);
            replacement.SetNavigable(15, 7, true);

            session.Replace(replacement, "Resize");

            Assert.AreSame(replacement, session.Grid);
            Assert.AreEqual(16, mask.Width);
            Assert.AreEqual(8, mask.Height);
            Assert.IsTrue(mask.CreateGrid().IsNavigable(15, 7));
        }

        [Test]
        public void Preview_HasOneOpaquePixelPerNavigableCell_BottomRowFirst()
        {
            session.Paint(Cell(1, 0), Cell(1, 0), 0.5f, true);
            session.Paint(Cell(7, 3), Cell(7, 3), 0.5f, true);

            Texture2D preview = session.GetPreview();

            Assert.AreEqual(8, preview.width);
            Assert.AreEqual(4, preview.height);
            Assert.AreEqual(FilterMode.Point, preview.filterMode);
            Assert.AreEqual(1f, preview.GetPixel(1, 0).a, 0.01f);
            Assert.AreEqual(1f, preview.GetPixel(7, 3).a, 0.01f);
            Assert.AreEqual(0f, preview.GetPixel(0, 0).a, 0.01f);
            Assert.AreEqual(0f, preview.GetPixel(7, 0).a, 0.01f);
        }

        [Test]
        public void Preview_FollowsLaterEdits_AndAGridOfAnotherSize()
        {
            Texture2D first = session.GetPreview();
            Assert.AreEqual(0f, first.GetPixel(2, 2).a, 0.01f);

            session.Paint(Cell(2, 2), Cell(2, 2), 0.5f, true);
            Assert.AreEqual(1f, session.GetPreview().GetPixel(2, 2).a, 0.01f);

            session.Replace(new NavigationGrid(16, 8), "Resize");
            Texture2D resized = session.GetPreview();

            Assert.AreEqual(16, resized.width);
            Assert.AreEqual(8, resized.height);
            Assert.AreEqual(0f, resized.GetPixel(2, 2).a, 0.01f);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests.
Expected: exit code `1`, `error CS0246` for `NavigationMaskSession`.

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Editor/NavigationMaskSession.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// The mask being edited: a working copy of its grid, painted on stroke by stroke and
    /// written back to the asset when a stroke ends, and a texture that shows it.
    /// </summary>
    public sealed class NavigationMaskSession : IDisposable
    {
        private static readonly Color32 NavigableColor = new Color32(255, 255, 255, 255);
        private static readonly Color32 EmptyColor = new Color32(0, 0, 0, 0);

        private Texture2D preview;
        private Color32[] previewPixels;
        private bool previewIsStale;
        private bool hasUncommittedChanges;

        public WorldMapDefinition Map { get; private set; }

        public NavigationMaskDefinition Mask { get; private set; }

        /// <summary>Working copy of the mask's grid; null when the map has no mask.</summary>
        public NavigationGrid Grid { get; private set; }

        public bool HasMask => Grid != null;

        public bool HasNavigableCells
        {
            get
            {
                if (Grid == null)
                {
                    return false;
                }

                // Unused bits are always zero, so any set bit is a navigable cell.
                foreach (byte packed in Grid.ToBytes())
                {
                    if (packed != 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Points the session at a map. Reloads the working copy only when the map or its
        /// mask changed, so it can be called on every Scene view event.
        /// </summary>
        public void SetMap(WorldMapDefinition map)
        {
            NavigationMaskDefinition mask = map != null ? map.NavigationMask : null;

            // The last test catches a mask asset deleted while it was being edited.
            if (map == Map && mask == Mask && (mask != null) == (Grid != null))
            {
                return;
            }

            Map = map;
            Mask = mask;
            Reload();
        }

        /// <summary>Rebuilds the working copy from the asset, dropping unwritten changes.</summary>
        public void Reload()
        {
            Grid = Mask != null ? Mask.CreateGrid() : null;
            hasUncommittedChanges = false;
            previewIsStale = true;
        }

        public void Paint(Vector2 from, Vector2 to, float radiusInCells, bool navigable)
        {
            if (Grid != null && Grid.PaintStroke(from, to, radiusInCells, navigable))
            {
                MarkChanged();
            }
        }

        public void Fill(Vector2 point, bool navigable)
        {
            if (Grid != null && Grid.Fill(point, navigable))
            {
                MarkChanged();
            }
        }

        /// <summary>Writes the working copy to the asset as one undo step.</summary>
        /// <returns>False when there was nothing to write.</returns>
        public bool Commit(string undoName)
        {
            if (!hasUncommittedChanges || Grid == null || Mask == null)
            {
                return false;
            }

            NavigationMaskAuthoring.Apply(Mask, Grid, undoName);
            hasUncommittedChanges = false;
            return true;
        }

        /// <summary>Replaces the whole grid and writes it to the asset at once.</summary>
        public void Replace(NavigationGrid grid, string undoName)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            if (Mask == null)
            {
                return;
            }

            Grid = grid;
            MarkChanged();
            Commit(undoName);
        }

        /// <summary>
        /// A texture of the grid's size, opaque white where a cell is navigable and
        /// transparent elsewhere, to be tinted when drawn. Null when there is no mask.
        /// </summary>
        public Texture2D GetPreview()
        {
            if (Grid == null)
            {
                return null;
            }

            if (preview == null || preview.width != Grid.Width || preview.height != Grid.Height)
            {
                DestroyPreview();

                preview = new Texture2D(Grid.Width, Grid.Height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                previewPixels = new Color32[Grid.Width * Grid.Height];
                previewIsStale = true;
            }

            if (previewIsStale)
            {
                for (int y = 0; y < Grid.Height; y++)
                {
                    int row = y * Grid.Width;

                    for (int x = 0; x < Grid.Width; x++)
                    {
                        previewPixels[row + x] = Grid.IsNavigable(x, y) ? NavigableColor : EmptyColor;
                    }
                }

                preview.SetPixels32(previewPixels);
                preview.Apply(false);
                previewIsStale = false;
            }

            return preview;
        }

        public void Dispose()
        {
            DestroyPreview();
        }

        private void MarkChanged()
        {
            hasUncommittedChanges = true;
            previewIsStale = true;
        }

        private void DestroyPreview()
        {
            if (preview != null)
            {
                Object.DestroyImmediate(preview);
            }

            preview = null;
            previewPixels = null;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run with `-testFilter DarkFantasyMerchant.Tests.EditMode.NavigationMaskSessionTests`, then the whole suite.
Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Editor Assets/Tests/EditMode
git commit -m "Session d'édition du masque : copie de travail, aperçu et écriture`n`nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Scene view tool

**Files:**
- Create: `Assets/Scripts/Editor/NavigationMaskToolSettings.cs`
- Create: `Assets/Scripts/Editor/NavigationMaskTool.cs`
- Modify: `Assets/Scripts/Editor/CityPlacementTool.cs`

**Interfaces:**
- Consumes: `NavigationMaskSession` (all of Task 7), `WorldMapEditorContext.FindView` / `FindDefinition`, `WorldMapDefinition.TryCreateProjection(out MapProjection)`, `MapProjection.WorldRect` / `WorldToNormalized`.
- Produces, in `DarkFantasyMerchant.Editor`:
  - `enum NavigationMaskToolMode { Brush, Eraser, Fill }`
  - `static class NavigationMaskToolSettings` with constants `MinBrushSize` (1), `MaxBrushSize` (64) and properties `NavigationMaskToolMode Mode`, `int BrushSize`, `float Opacity`, `float MaxSaturation` (all get/set, stored in `EditorPrefs`)
  - `sealed class NavigationMaskTool : EditorTool` with `static NavigationMaskSession ActiveSession { get; }` — the session of the active tool, or null

This task has no automated test: it is Scene view input and drawing over the session tested in Task 7. It is verified by compiling and by the checks of Task 10.

- [ ] **Step 1: Write the tool settings**

`Assets/Scripts/Editor/NavigationMaskToolSettings.cs`:

```csharp
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Editor
{
    public enum NavigationMaskToolMode
    {
        Brush,
        Eraser,
        Fill,
    }

    /// <summary>
    /// Settings of the navigation mask tool. They are preferences of this Editor, not
    /// part of the mask asset.
    /// </summary>
    public static class NavigationMaskToolSettings
    {
        public const int MinBrushSize = 1;
        public const int MaxBrushSize = 64;

        private const string Prefix = "DarkFantasyMerchant.NavigationMask.";
        private const string ModeKey = Prefix + "Mode";
        private const string BrushSizeKey = Prefix + "BrushSize";
        private const string OpacityKey = Prefix + "Opacity";
        private const string MaxSaturationKey = Prefix + "MaxSaturation";

        public static NavigationMaskToolMode Mode
        {
            get => (NavigationMaskToolMode)Mathf.Clamp(EditorPrefs.GetInt(ModeKey, 0), 0, 2);
            set => EditorPrefs.SetInt(ModeKey, (int)value);
        }

        /// <summary>Diameter of the brush, in cells.</summary>
        public static int BrushSize
        {
            get => Mathf.Clamp(EditorPrefs.GetInt(BrushSizeKey, 8), MinBrushSize, MaxBrushSize);
            set => EditorPrefs.SetInt(BrushSizeKey, Mathf.Clamp(value, MinBrushSize, MaxBrushSize));
        }

        /// <summary>Opacity of the mask drawn over the map, 0 to 1.</summary>
        public static float Opacity
        {
            get => Mathf.Clamp01(EditorPrefs.GetFloat(OpacityKey, 0.5f));
            set => EditorPrefs.SetFloat(OpacityKey, Mathf.Clamp01(value));
        }

        /// <summary>Highest saturation the detection still counts as water, 0 to 1.</summary>
        public static float MaxSaturation
        {
            get => Mathf.Clamp01(EditorPrefs.GetFloat(MaxSaturationKey, 0.2f));
            set => EditorPrefs.SetFloat(MaxSaturationKey, Mathf.Clamp01(value));
        }
    }
}
```

- [ ] **Step 2: Write the tool**

`Assets/Scripts/Editor/NavigationMaskTool.cs`:

```csharp
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// Scene view tool that paints the navigation mask of the active world map: where
    /// ships can sail. Its settings are in <see cref="NavigationMaskOverlay"/>.
    /// </summary>
    [EditorTool("Navigation Mask")]
    public sealed class NavigationMaskTool : EditorTool
    {
        private const string PaintUndoName = "Paint Navigation Mask";
        private const string FillUndoName = "Fill Navigation Mask";

        private static readonly Color MaskTint = new Color(0.2f, 0.6f, 1f);
        private static readonly Color BrushColor = Color.white;

        private NavigationMaskSession session;
        private GUIContent icon;
        private Vector2 lastPoint;
        private bool strokeNavigable;

        /// <summary>Session of the active tool, for its panel; null when the tool is not in use.</summary>
        public static NavigationMaskSession ActiveSession { get; private set; }

        public override GUIContent toolbarIcon
        {
            get
            {
                if (icon == null)
                {
                    icon = new GUIContent(
                        EditorGUIUtility.IconContent("Grid.PaintTool").image, "Navigation Mask");
                }

                return icon;
            }
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            ReleaseSession();
        }

        public override void OnWillBeDeactivated()
        {
            ReleaseSession();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            // The mask is authored at edit time only.
            if (!(window is SceneView sceneView) || Application.isPlaying)
            {
                return;
            }

            WorldMapView view = WorldMapEditorContext.FindView();
            WorldMapDefinition definition = WorldMapEditorContext.FindDefinition(view);

            // Created here and not on activation: a script reload keeps the tool active
            // but drops the session.
            if (session == null)
            {
                session = new NavigationMaskSession();
            }

            ActiveSession = session;

            if (definition == null || !definition.TryCreateProjection(out MapProjection projection))
            {
                session.SetMap(null);
                DrawMessage("No world map found. Open the WorldMap scene or select a World Map asset.");
                return;
            }

            session.SetMap(definition);

            if (!session.HasMask)
            {
                DrawMessage("This map has no navigation mask. Create one from the Navigation Mask panel.");
                return;
            }

            // The mask is drawn as a screen rectangle, which only lines up with the map
            // when the view looks straight at it.
            if (!sceneView.in2DMode)
            {
                DrawMessage("Switch the Scene view to 2D to paint the navigation mask.");
                return;
            }

            Event current = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            Vector2 world = HandleUtility.GUIPointToWorldRay(current.mousePosition).origin;
            Vector2 point = projection.WorldToNormalized(world);
            NavigationMaskToolMode mode = NavigationMaskToolSettings.Mode;
            float radiusInCells = NavigationMaskToolSettings.BrushSize * 0.5f;

            switch (current.GetTypeForControl(controlId))
            {
                case EventType.Layout:
                    // Keeps a click on empty space from selecting or deselecting objects.
                    HandleUtility.AddDefaultControl(controlId);
                    break;

                case EventType.MouseDown:
                    // Alt and the other buttons stay with Scene view navigation.
                    if (current.button != 0 || current.alt)
                    {
                        break;
                    }

                    if (mode == NavigationMaskToolMode.Fill)
                    {
                        session.Fill(point, !current.shift);
                        session.Commit(FillUndoName);
                    }
                    else
                    {
                        // Shift swaps brush and eraser, for the whole stroke.
                        strokeNavigable = (mode == NavigationMaskToolMode.Brush) != current.shift;
                        lastPoint = point;
                        GUIUtility.hotControl = controlId;
                        session.Paint(point, point, radiusInCells, strokeNavigable);
                    }

                    current.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        session.Paint(lastPoint, point, radiusInCells, strokeNavigable);
                        lastPoint = point;
                        current.Use();
                    }

                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        session.Commit(PaintUndoName);
                        current.Use();
                    }

                    break;

                case EventType.MouseMove:
                    // The brush outline follows the pointer.
                    sceneView.Repaint();
                    break;

                case EventType.KeyDown:
                    if (current.keyCode == KeyCode.LeftBracket)
                    {
                        NavigationMaskToolSettings.BrushSize--;
                        current.Use();
                    }
                    else if (current.keyCode == KeyCode.RightBracket)
                    {
                        NavigationMaskToolSettings.BrushSize++;
                        current.Use();
                    }

                    break;

                case EventType.Repaint:
                    DrawMask(projection.WorldRect);

                    if (mode != NavigationMaskToolMode.Fill)
                    {
                        DrawBrush(world, radiusInCells * projection.WorldRect.width / session.Grid.Width);
                    }

                    break;
            }
        }

        private void DrawMask(Rect worldRect)
        {
            Texture2D preview = session.GetPreview();

            if (preview == null)
            {
                return;
            }

            Vector2 topLeft = HandleUtility.WorldToGUIPoint(new Vector3(worldRect.xMin, worldRect.yMax, 0f));
            Vector2 bottomRight = HandleUtility.WorldToGUIPoint(new Vector3(worldRect.xMax, worldRect.yMin, 0f));
            Color previousColor = GUI.color;

            Handles.BeginGUI();
            GUI.color = new Color(MaskTint.r, MaskTint.g, MaskTint.b, NavigationMaskToolSettings.Opacity);

            // The texture's bottom row is drawn at the bottom of the rectangle, like row 0
            // of the grid on the map.
            GUI.DrawTexture(
                Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y),
                preview,
                ScaleMode.StretchToFill,
                true);

            GUI.color = previousColor;
            Handles.EndGUI();
        }

        private static void DrawBrush(Vector2 world, float worldRadius)
        {
            Handles.color = BrushColor;
            Handles.DrawWireDisc(world, Vector3.forward, worldRadius);
        }

        private static void DrawMessage(string text)
        {
            Handles.BeginGUI();
            GUI.Label(new Rect(12f, 12f, 520f, 38f), text, EditorStyles.helpBox);
            Handles.EndGUI();
        }

        private void OnUndoRedo()
        {
            if (session == null)
            {
                return;
            }

            // The asset changed under the working copy.
            session.Reload();
            SceneView.RepaintAll();
        }

        private void ReleaseSession()
        {
            if (session == null)
            {
                return;
            }

            // A stroke still in progress when the tool is switched away is kept.
            session.Commit(PaintUndoName);
            session.Dispose();

            if (ActiveSession == session)
            {
                ActiveSession = null;
            }

            session = null;
        }
    }
}
```

`NavigationMaskOverlay` is referenced only from a `<see cref>`; until Task 9 creates it, the compiler reports warning `CS1574` at most, not an error.

- [ ] **Step 3: Keep city handles out of the way while painting**

In `Assets/Scripts/Editor/CityPlacementTool.cs`, add `using UnityEditor.EditorTools;` to the usings, and extend the first test of `OnSceneGui`. Replace:

```csharp
            // Positions are authored at edit time only.
            if (Application.isPlaying)
            {
                return;
            }
```

with:

```csharp
            // Positions are authored at edit time only.
            if (Application.isPlaying)
            {
                return;
            }

            // A city handle under the brush would take the click meant for the mask.
            if (ToolManager.activeToolType == typeof(NavigationMaskTool))
            {
                return;
            }
```

- [ ] **Step 4: Verify it compiles and nothing regressed**

Run the whole EditMode suite.
Expected: exit code `0`, `result="Passed"`. Then check the log for the icon:

```powershell
Select-String -Path Logs\test.log -Pattern 'Unable to load the icon'
```

Expected: no match. If `Grid.PaintTool` is reported missing, use `EditorGUIUtility.IconContent("d_Grid.PaintTool")`, and if that is missing too, `new GUIContent("Nav", "Navigation Mask")` with no image.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Editor
git commit -m "Outil de la Scene view pour peindre le masque de navigation`n`nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Tool panel and mask Inspector

**Files:**
- Create: `Assets/Scripts/Editor/NavigationMaskOverlay.cs`
- Create: `Assets/Scripts/Editor/NavigationMaskDefinitionEditor.cs`

**Interfaces:**
- Consumes:
  - `NavigationMaskTool.ActiveSession`
  - `NavigationMaskSession`: `Map`, `HasMask`, `Grid`, `HasNavigableCells`, `Replace(NavigationGrid, string)`
  - `NavigationMaskToolSettings`: `Mode`, `BrushSize`, `Opacity`, `MaxSaturation`, `MinBrushSize`, `MaxBrushSize`
  - `NavigationMaskToolMode`
  - `NavigationMaskAuthoring`: `MaskAssetPath`, `DefaultWidth`, `MinWidth`, `MaxWidth`, `HeightFor`, `TryGetAspectRatio`, `MatchesAspect`, `CreateMask`, `Detect`
  - `NavigationGrid`: `Width`, `Height`, `Resampled`, constructor
  - `NavigationMaskDefinition`: `Width`, `Height`
- Produces: `NavigationMaskOverlay` (Scene view overlay), `NavigationMaskDefinitionEditor` (custom Inspector). Nothing else depends on them.

This task has no automated test; it is verified by compiling and by the checks of Task 10.

- [ ] **Step 1: Write the panel**

`Assets/Scripts/Editor/NavigationMaskOverlay.cs`:

```csharp
using DarkFantasyMerchant.Core;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// Scene view panel of <see cref="NavigationMaskTool"/>: mode, brush, detection and
    /// grid size. Shown only while that tool is active.
    /// </summary>
    [Overlay(typeof(SceneView), OverlayId, "Navigation Mask")]
    public sealed class NavigationMaskOverlay : Overlay, ITransientOverlay
    {
        private const string OverlayId = "dark-fantasy-merchant-navigation-mask";
        private const long RefreshIntervalMilliseconds = 200;

        private Label message;
        private Button createButton;
        private VisualElement editing;
        private EnumField modeField;
        private SliderInt brushSizeSlider;
        private Slider opacitySlider;
        private Slider saturationSlider;
        private Label gridLabel;
        private IntegerField widthField;
        private HelpBox aspectWarning;
        private int shownWidth;

        public bool visible => ToolManager.activeToolType == typeof(NavigationMaskTool);

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            root.style.minWidth = 280f;
            root.style.paddingLeft = 6f;
            root.style.paddingRight = 6f;
            root.style.paddingTop = 4f;
            root.style.paddingBottom = 6f;

            message = new Label("No world map found. Open the WorldMap scene or select a World Map asset.");
            message.style.whiteSpace = WhiteSpace.Normal;
            root.Add(message);

            createButton = new Button(CreateMask) { text = "Create mask" };
            root.Add(createButton);

            editing = new VisualElement();
            root.Add(editing);

            modeField = new EnumField("Mode", NavigationMaskToolSettings.Mode);
            modeField.RegisterValueChangedCallback(
                change => NavigationMaskToolSettings.Mode = (NavigationMaskToolMode)change.newValue);
            editing.Add(modeField);

            brushSizeSlider = new SliderInt(
                "Brush size", NavigationMaskToolSettings.MinBrushSize, NavigationMaskToolSettings.MaxBrushSize)
            {
                showInputField = true,
                tooltip = "Diameter in cells. Shortcuts: [ and ].",
            };
            brushSizeSlider.RegisterValueChangedCallback(change =>
            {
                NavigationMaskToolSettings.BrushSize = change.newValue;
                SceneView.RepaintAll();
            });
            editing.Add(brushSizeSlider);

            opacitySlider = new Slider("Opacity", 0f, 1f);
            opacitySlider.RegisterValueChangedCallback(change =>
            {
                NavigationMaskToolSettings.Opacity = change.newValue;
                SceneView.RepaintAll();
            });
            editing.Add(opacitySlider);

            editing.Add(CreateHeader("Detection"));

            saturationSlider = new Slider("Max saturation", 0f, 1f)
            {
                showInputField = true,
                tooltip = "Pixels of the map image with at most this saturation are water.",
            };
            saturationSlider.RegisterValueChangedCallback(
                change => NavigationMaskToolSettings.MaxSaturation = change.newValue);
            editing.Add(saturationSlider);
            editing.Add(new Button(Detect) { text = "Detect from map" });

            editing.Add(CreateHeader("Grid"));

            gridLabel = new Label();
            editing.Add(gridLabel);

            widthField = new IntegerField("Width")
            {
                tooltip = $"{NavigationMaskAuthoring.MinWidth} to {NavigationMaskAuthoring.MaxWidth} cells. "
                    + "The height follows the map.",
            };
            editing.Add(widthField);
            editing.Add(new Button(Resize) { text = "Resize" });

            aspectWarning = new HelpBox(
                "The grid no longer has the shape of the map image. Resize it to fit.",
                HelpBoxMessageType.Warning);
            editing.Add(aspectWarning);

            var clearButton = new Button(Clear) { text = "Clear" };
            clearButton.style.marginTop = 8f;
            editing.Add(clearButton);

            // The tool, its keyboard shortcuts and undo all change what is shown here,
            // and none of them knows about the panel.
            shownWidth = 0;
            root.schedule.Execute(Refresh).Every(RefreshIntervalMilliseconds);
            Refresh();
            return root;
        }

        private static Label CreateHeader(string text)
        {
            var header = new Label(text);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 8f;
            return header;
        }

        private static void SetShown(VisualElement element, bool shown)
        {
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Refresh()
        {
            NavigationMaskSession session = NavigationMaskTool.ActiveSession;
            bool hasMap = session != null && session.Map != null;
            bool hasMask = hasMap && session.HasMask;

            SetShown(message, !hasMap);
            SetShown(createButton, hasMap && !hasMask);
            SetShown(editing, hasMask);

            if (!hasMask)
            {
                shownWidth = 0;
                return;
            }

            modeField.SetValueWithoutNotify(NavigationMaskToolSettings.Mode);
            brushSizeSlider.SetValueWithoutNotify(NavigationMaskToolSettings.BrushSize);
            opacitySlider.SetValueWithoutNotify(NavigationMaskToolSettings.Opacity);
            saturationSlider.SetValueWithoutNotify(NavigationMaskToolSettings.MaxSaturation);

            NavigationGrid grid = session.Grid;
            gridLabel.text = $"{grid.Width} x {grid.Height} cells";

            // Only when the grid itself changes, so a width being typed is not overwritten.
            if (shownWidth != grid.Width)
            {
                shownWidth = grid.Width;
                widthField.SetValueWithoutNotify(grid.Width);
            }

            bool fitsMap = !NavigationMaskAuthoring.TryGetAspectRatio(session.Map, out float aspectRatio)
                || NavigationMaskAuthoring.MatchesAspect(grid.Width, grid.Height, aspectRatio);
            SetShown(aspectWarning, !fitsMap);
        }

        private static bool TryGetEditableSession(out NavigationMaskSession session)
        {
            session = NavigationMaskTool.ActiveSession;

            return session != null && session.Map != null && session.HasMask;
        }

        private void CreateMask()
        {
            NavigationMaskSession session = NavigationMaskTool.ActiveSession;

            if (session == null || session.Map == null)
            {
                return;
            }

            if (NavigationMaskAuthoring.CreateMask(
                    session.Map, NavigationMaskAuthoring.MaskAssetPath, NavigationMaskAuthoring.DefaultWidth) == null)
            {
                Debug.LogError(
                    $"Cannot create a navigation mask: world map '{session.Map.name}' has no map sprite.",
                    session.Map);
            }

            // The tool picks the new mask up on its next Scene view event.
            SceneView.RepaintAll();
            Refresh();
        }

        private void Detect()
        {
            if (!TryGetEditableSession(out NavigationMaskSession session) || session.Map.MapSprite == null)
            {
                return;
            }

            if (session.HasNavigableCells
                && !EditorUtility.DisplayDialog(
                    "Detect from map",
                    "This replaces the whole navigation mask with one detected from the map image. "
                        + "It can be undone.",
                    "Replace",
                    "Cancel"))
            {
                return;
            }

            NavigationGrid detected = NavigationMaskAuthoring.Detect(
                session.Map.MapSprite,
                session.Grid.Width,
                session.Grid.Height,
                NavigationMaskToolSettings.MaxSaturation);

            session.Replace(detected, "Detect Navigation Mask");
            SceneView.RepaintAll();
        }

        private void Resize()
        {
            if (!TryGetEditableSession(out NavigationMaskSession session)
                || !NavigationMaskAuthoring.TryGetAspectRatio(session.Map, out float aspectRatio))
            {
                return;
            }

            int width = Mathf.Clamp(
                widthField.value, NavigationMaskAuthoring.MinWidth, NavigationMaskAuthoring.MaxWidth);
            int height = NavigationMaskAuthoring.HeightFor(width, aspectRatio);

            // Show the width actually used when the typed one was out of range.
            widthField.SetValueWithoutNotify(width);

            if (width == session.Grid.Width && height == session.Grid.Height)
            {
                return;
            }

            session.Replace(session.Grid.Resampled(width, height), "Resize Navigation Mask");
            SceneView.RepaintAll();
            Refresh();
        }

        private void Clear()
        {
            if (!TryGetEditableSession(out NavigationMaskSession session) || !session.HasNavigableCells)
            {
                return;
            }

            session.Replace(
                new NavigationGrid(session.Grid.Width, session.Grid.Height), "Clear Navigation Mask");
            SceneView.RepaintAll();
        }
    }
}
```

- [ ] **Step 2: Write the mask Inspector**

`Assets/Scripts/Editor/NavigationMaskDefinitionEditor.cs`:

```csharp
using DarkFantasyMerchant.Game;
using UnityEditor;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// The mask's fields are hidden, so the Inspector says what the asset holds and where
    /// to edit it.
    /// </summary>
    [CustomEditor(typeof(NavigationMaskDefinition))]
    public sealed class NavigationMaskDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var mask = (NavigationMaskDefinition)target;

            EditorGUILayout.HelpBox(
                $"{mask.Width} x {mask.Height} cells.\n"
                    + "Paint it in the Scene view with the Navigation Mask tool.",
                MessageType.Info);
        }
    }
}
```

- [ ] **Step 3: Verify it compiles and nothing regressed**

Run the whole EditMode suite.
Expected: exit code `0`, `result="Passed"`, and no `error CS` or `warning CS1574` in `Logs\test.log`:

```powershell
Select-String -Path Logs\test.log -Pattern 'error CS|warning CS1574'
```

- [ ] **Step 4: Commit**

```powershell
git add Assets/Scripts/Editor
git commit -m "Panneau de l'outil de masque et inspecteur de l'asset`n`nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Documentation and Editor checks

**Files:**
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: everything above, by name only.
- Produces: nothing code depends on.

- [ ] **Step 1: Document the navigation mask**

In `CLAUDE.md`, under `## Architecture`, add this subsection after the `### Ships` subsection (before the blank lines that precede `## Conventions`):

```markdown
### Navigation mask

- `NavigationGrid` (Core) says which cells of the map ships can sail on: one bit per cell, cell `(0,0)` bottom-left like normalized positions, queried by cell or by normalized point. It also holds the painting logic (disc, stroke, 4-connected fill, resample). `NavigationMaskDefinition` (Game) stores a grid's size and bits and is referenced by `WorldMapDefinition.navigationMask`; a map without a mask is valid.
- Nothing in the running game reads the mask yet: ships still ignore land.
- The grid is defined over normalized space, 1024 cells wide by default, with a height that follows the map's aspect ratio so cells are square. Replacing the map image by one of another shape needs a **Resize** in the tool, not a repaint.
- The mask is painted in the Scene view (2D mode) with the **Navigation Mask** tool of the Scene view toolbar (`NavigationMaskTool`, settings in the `NavigationMaskOverlay` panel): brush, eraser, fill bucket, and a detection that marks low-saturation pixels of the map image as water (`WaterColorClassifier`). Shift swaps brush and eraser; `[` and `]` resize the brush. While this tool is active, `CityPlacementTool` draws no handles.
- `NavigationMaskSession` is the tool's working copy of the grid. It is written to the asset once per stroke through `NavigationMaskAuthoring.Apply`, which is what makes a stroke one undo step, and rebuilt from the asset after an undo or redo.
- The mask's fields are hidden in the Inspector on purpose: its size and bits only make sense together.
```

Also, in the `### World map` list, replace the line:

```markdown
- Cities are placed by dragging their handles in the Scene view (`CityPlacementTool`).
```

with:

```markdown
- Cities are placed by dragging their handles in the Scene view (`CityPlacementTool`). It and the navigation mask tool find the map to work on through `WorldMapEditorContext`.
```

And in `## Project state`, replace the sentence `The world map is the first subsystem, and the player has one ship to sail on it; nothing else of the game exists yet.` with:

```markdown
The world map is the first subsystem, the player has one ship to sail on it, and the navigable areas of the map can be painted but are not used yet; nothing else of the game exists yet.
```

- [ ] **Step 2: Run the whole suite one last time**

Run all EditMode tests.
Expected: exit code `0`, `result="Passed"`. Note the total in the `<test-run` line for the report.

- [ ] **Step 3: Check the tool in the Editor**

These checks need a person at the Editor with the project open: the tool is driven by the mouse in the Scene view, which `unity-mcp` cannot do. Ask the user to run them and report each line as passed, failed or **not checked**; do not report them as verified otherwise. With the Editor open, `Unity_GetConsoleLogs` can confirm there are no errors or warnings from the new scripts, and `Unity_SceneView_Capture2DScene` can capture the result.

1. Open `Assets/Scenes/WorldMap.unity`, Scene view in 2D mode. Select the **Navigation Mask** tool in the Scene view toolbar: the panel appears with only a **Create mask** button, and a message says the map has no mask.
2. **Create mask**: `Assets/Data/WorldMap/NavigationMask.asset` appears, `WorldMap.asset` references it, the panel shows `1024 x 879 cells` and the editing controls. The mask's Inspector shows its size.
3. Brush: drag over a sea; a blue tint follows the pointer with no gaps on fast moves. The white circle matches the painted width. `]` and `[` change it, and the slider follows.
4. Eraser, and Shift with the brush: both remove tint.
5. Draw a closed outline around a bay with the brush, switch to Fill, click inside: only the inside fills. Click outside a closed coast: everything connected fills, land enclosed by painted coasts does not.
6. Ctrl+Z after a stroke removes exactly that stroke; Ctrl+Y brings it back; painting after an undo does not bring the undone stroke back.
7. **Detect from map** on a non-empty mask asks for confirmation, then tints the seas; the tint is the right way up and lines up with the map. Lower and raise **Max saturation** and detect again to see it change. One Ctrl+Z restores the previous mask.
8. Type `512` in **Width**, **Resize**: the panel shows `512 x 440 cells` (or `439`, by rounding) and the painted shape is kept, coarser. Typing `10` resizes to `64`.
9. Alt+drag, the wheel, and middle or right drag still navigate the Scene view while the tool is active.
10. City handles are hidden while the tool is active and usable again after switching to the Move tool.
11. Save the project, close and reopen the Editor: the mask is as it was left.
12. Enter Play mode: the game runs as before; ships still sail in a straight line.

- [ ] **Step 4: Commit**

Commit the documentation. If the user created the mask during the checks and wants it in the repository, add `Assets/Data/WorldMap` too; otherwise leave their unsaved content alone.

```powershell
git add CLAUDE.md
git commit -m "Documentation du masque de navigation et de son outil`n`nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
