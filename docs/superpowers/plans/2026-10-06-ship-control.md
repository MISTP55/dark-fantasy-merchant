# Ship Control Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A player ship on the world map that is selected with a left click and sent to a point with a right click, sailing in a straight line at constant speed with a sprite for each of eight headings.

**Architecture:** The ship's state and movement are a plain C# class in `Core` (`Ship`), with heading quantization in `CompassHeading`; both are covered by EditMode tests. In `Game`, `ShipDefinition` is the static ScriptableObject, `ShipView` shows one ship, and `ShipsView` owns the ships, their views and a `MapSelectionState<Ship>`. `WorldMapInput` gains a `Command` action (right mouse button), and `WorldMapInteraction` arbitrates hover and selection between the ship and the cities so only one thing is selected.

**Tech Stack:** Unity 6000.6.4f1, URP 17.6 2D Renderer, Input System (project-wide actions), Unity Test Framework (NUnit, EditMode).

**Spec:** `docs/superpowers/specs/2026-10-06-ship-control-design.md`

## Global Constraints

- All code, comments, log messages, asset and folder names are in English.
- `Core` contains no `MonoBehaviour` and nothing that needs a scene. It may use `UnityEngine` value types (`Vector2`, `Rect`, `Mathf`).
- ScriptableObjects hold static definitions only. Runtime state (`Ship`) is never written back to assets.
- Map positions are normalized (`(0, 0)` bottom-left, `(1, 1)` top-right) and go to world space only through `MapProjection`.
- Only the new Input System is used; `WorldMapInput` is the only reader of the `WorldMap` action map.
- The import settings of `Assets/Art/Ships/MerchantShip.png` are not changed. Its sprites are `MerchantShip_N`, `_NE`, `_E`, `_SE`, `_S`, `_SW`, `_W`, `_NW` (32×32, 16 pixels per unit).
- `CompassDirection` order is `N, NE, E, SE, S, SW, W, NW` (values 0 to 7).
- Ship speed is in world units per second. Default for the merchant ship: `1.5` (the map is 40 units wide).
- Ship sprite sorting order is `20` (city markers use `10`). `shipScreenPixelsPerUnit` defaults to `32`, `shipPickRadiusPixels` to `24`.
- Never hand-write `.meta` files. Unity generates them on import; commit them together with their asset.
- Never edit the generated `.sln` / `.csproj`. Exclude `Library/` from searches.
- Match the surrounding code: Allman braces, a blank line before `return`/`if` blocks as in the existing files, XML `<summary>` on public types, comments only where the reason is not obvious.
- Spec additions, intentional:
  - `Ship.SetDestination` ignores a point with a NaN component (the spec only covers clamping).
  - The `Ship` constructor clamps the start position to the map and rejects a NaN one.
  - The EditMode test assembly gains a reference to `Unity.InputSystem`, to test that the `Command` action exists.
- Work on branch `feature/ship-control`, created from `main` in Task 1.
- Commit messages are in French (matching the repository history) and end with:
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`

## Commands

Batch mode requires the project to be **closed** in the Unity Editor. Check first:

```powershell
Get-Process Unity -ErrorAction SilentlyContinue
```

If Unity is running with this project, use the `unity-mcp` tools instead: `Unity_RunCommand` to trigger a refresh/compile or run tests, then `Unity_GetConsoleLogs` to read errors.

**Run EditMode tests** (all, or add `-testFilter`). `Unity.exe` is a GUI executable, so it must be started with `-Wait` or PowerShell returns immediately:

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe"
Remove-Item Logs\TestResults.xml -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList '-batchmode','-projectPath','.','-runTests','-testPlatform','EditMode','-testResults','Logs\TestResults.xml','-logFile','Logs\test.log'
"exit code: $($p.ExitCode)"
Select-String -Path Logs\TestResults.xml -Pattern '<test-run ' | ForEach-Object { $_.Line }
```

To run one fixture, append `'-testFilter','DarkFantasyMerchant.Tests.EditMode.ShipTests'` to the argument list.

Reading the result:

- Exit code `0` and `result="Passed"` in the `<test-run` line: all tests passed.
- Exit code `2`: at least one test failed. Details: `Select-String -Path Logs\TestResults.xml -Pattern 'result="Failed"'`.
- Exit code `1` and no `TestResults.xml`: compilation failed. Details: `Select-String -Path Logs\test.log -Pattern 'error CS'`.

A test file that references a type that does not exist yet fails the **whole compilation** (exit code `1`), not just that test. That is the expected "red" for the first step of each task.

A run takes one to three minutes. The first run after adding files also generates their `.meta` files; add them to the commit.

## Review Focus

Inputs the spec implies but does not spell out, most likely first. Each one is pinned by a test in the task named.

1. **A very long frame** (alt-tab, breakpoint, hitch; `deltaTime` far larger than the remaining trip, or infinite): the ship lands exactly on its destination and stops; it never overshoots or ends up at NaN. Tests in Task 2.
2. **Right click with the window minimized or a degenerate camera** (screen-to-world yields NaN): the order is ignored and the ship keeps its position and current order. Tests in Task 2.
3. **Right click on the spot the ship already occupies, or twice on the same destination:** no division by a zero distance; the ship stays idle with its heading unchanged, or keeps sailing to the same point. Tests in Task 2.
4. **A very short trip** (direction vector with a tiny magnitude, far below `Vector2`'s approximate-equality epsilon): the heading still follows the real direction instead of the fallback. Tests in Task 1 and Task 2.
5. **A `ShipDefinition` with missing sprites** (array shorter than eight, or an empty slot): `SpriteFor` returns null instead of throwing, and the view keeps the sprite it had. Tests in Task 3.

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/Core/CompassDirection.cs` | Create | The eight headings, in sprite-sheet order. |
| `Assets/Scripts/Core/CompassHeading.cs` | Create | Quantizes a vector to a `CompassDirection`. |
| `Assets/Scripts/Core/Ship.cs` | Create | Runtime state and movement of one ship. |
| `Assets/Scripts/Game/ShipDefinition.cs` | Create | Static definition: name, speed, eight sprites. |
| `Assets/Scripts/Game/ShipView.cs` | Create | One ship sprite on the map. |
| `Assets/Scripts/Game/ShipsView.cs` | Create | Owns ships, their views and ship selection; advances them. |
| `Assets/Scripts/Game/WorldMapInput.cs` | Modify | Reads the `Command` action, raises `Commanded`. |
| `Assets/Scripts/Game/WorldMapInteraction.cs` | Modify | Ship/city hover and selection arbitration, move orders. |
| `Assets/InputSystem_Actions.inputactions` | Modify | `Command` action bound to the right mouse button. |
| `Assets/Scripts/Editor/WorldMapSetup.cs` | Modify | Creates the ship definition, the prefab and the scene object. |
| `Assets/Data/Ships/MerchantShip.asset` | Generated | By `WorldMapSetup.Build`. |
| `Assets/Prefabs/Ships/Ship.prefab` | Generated | By `WorldMapSetup.Build`. |
| `Assets/Scenes/WorldMap.unity` | Generated change | `Ships` object added by `WorldMapSetup.Build`. |
| `Assets/Tests/EditMode/CompassHeadingTests.cs` | Create | |
| `Assets/Tests/EditMode/ShipTests.cs` | Create | |
| `Assets/Tests/EditMode/ShipDefinitionTests.cs` | Create | |
| `Assets/Tests/EditMode/WorldMapInputActionsTests.cs` | Create | The `Command` action exists and is bound. |
| `Assets/Tests/EditMode/ShipContentTests.cs` | Create | The generated merchant ship asset is complete and ordered. |
| `Assets/Tests/EditMode/DarkFantasyMerchant.Tests.EditMode.asmdef` | Modify | Reference `Unity.InputSystem`. |
| `CLAUDE.md` | Modify | Ship architecture, `Command` action, setup tool behaviour. |

---

### Task 1: Compass heading

**Files:**
- Create: `Assets/Scripts/Core/CompassDirection.cs`
- Create: `Assets/Scripts/Core/CompassHeading.cs`
- Test: `Assets/Tests/EditMode/CompassHeadingTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum DarkFantasyMerchant.Core.CompassDirection { N, NE, E, SE, S, SW, W, NW }`
  - `static CompassDirection CompassHeading.FromVector(Vector2 direction, CompassDirection fallback)`

- [ ] **Step 1: Create the branch**

```powershell
git checkout -b feature/ship-control
```

- [ ] **Step 2: Write the failing tests**

`Assets/Tests/EditMode/CompassHeadingTests.cs`:

```csharp
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CompassHeadingTests
    {
        [TestCase(0f, 1f, CompassDirection.N)]
        [TestCase(1f, 1f, CompassDirection.NE)]
        [TestCase(1f, 0f, CompassDirection.E)]
        [TestCase(1f, -1f, CompassDirection.SE)]
        [TestCase(0f, -1f, CompassDirection.S)]
        [TestCase(-1f, -1f, CompassDirection.SW)]
        [TestCase(-1f, 0f, CompassDirection.W)]
        [TestCase(-1f, 1f, CompassDirection.NW)]
        public void FromVector_ReturnsTheExactDirection(float x, float y, CompassDirection expected)
        {
            Assert.AreEqual(expected, CompassHeading.FromVector(new Vector2(x, y), CompassDirection.S));
        }

        // Angles are clockwise from north. Sector boundaries sit at 22.5 + 45 * n degrees.
        [TestCase(21.5f, CompassDirection.N)]
        [TestCase(23.5f, CompassDirection.NE)]
        [TestCase(66.5f, CompassDirection.NE)]
        [TestCase(68.5f, CompassDirection.E)]
        [TestCase(111.5f, CompassDirection.E)]
        [TestCase(113.5f, CompassDirection.SE)]
        [TestCase(156.5f, CompassDirection.SE)]
        [TestCase(158.5f, CompassDirection.S)]
        [TestCase(201.5f, CompassDirection.S)]
        [TestCase(203.5f, CompassDirection.SW)]
        [TestCase(246.5f, CompassDirection.SW)]
        [TestCase(248.5f, CompassDirection.W)]
        [TestCase(291.5f, CompassDirection.W)]
        [TestCase(293.5f, CompassDirection.NW)]
        [TestCase(336.5f, CompassDirection.NW)]
        [TestCase(338.5f, CompassDirection.N)]
        public void FromVector_SwitchesDirectionAtTheSectorBoundaries(float degrees, CompassDirection expected)
        {
            float radians = degrees * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));

            Assert.AreEqual(expected, CompassHeading.FromVector(direction, CompassDirection.S));
        }

        [TestCase(1e-20f)]
        [TestCase(1f)]
        [TestCase(1e20f)]
        public void FromVector_IgnoresMagnitude(float magnitude)
        {
            var direction = new Vector2(-magnitude, 0f);

            Assert.AreEqual(CompassDirection.W, CompassHeading.FromVector(direction, CompassDirection.S));
        }

        [TestCase(0f, 0f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(1f, float.NaN)]
        public void FromVector_ReturnsTheFallback_WhenThereIsNoDirection(float x, float y)
        {
            Assert.AreEqual(CompassDirection.NW, CompassHeading.FromVector(new Vector2(x, y), CompassDirection.NW));
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run the EditMode tests (see Commands).
Expected: exit code `1`, and `Logs\test.log` contains `error CS0246` or `CS0103` naming `CompassDirection` / `CompassHeading`.

- [ ] **Step 4: Write the implementation**

`Assets/Scripts/Core/CompassDirection.cs`:

```csharp
namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The eight headings of a ship, clockwise from north. The order matches the ship
    /// sprite sheets, so a value can index their sprites.
    /// </summary>
    public enum CompassDirection
    {
        N,
        NE,
        E,
        SE,
        S,
        SW,
        W,
        NW,
    }
}
```

`Assets/Scripts/Core/CompassHeading.cs`:

```csharp
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Turns a direction of travel into one of the eight headings.</summary>
    public static class CompassHeading
    {
        private const int DirectionCount = 8;
        private const float SectorDegrees = 360f / DirectionCount;

        /// <returns>
        /// The heading whose 45-degree sector contains <paramref name="direction"/>, or
        /// <paramref name="fallback"/> for a zero or NaN vector.
        /// </returns>
        public static CompassDirection FromVector(Vector2 direction, CompassDirection fallback)
        {
            // Compared per component: Vector2 equality is approximate and would treat a
            // very short vector as zero.
            if (direction.x == 0f && direction.y == 0f)
            {
                return fallback;
            }

            // Clockwise angle from north, hence x before y.
            float degrees = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;

            if (float.IsNaN(degrees))
            {
                return fallback;
            }

            int sector = Mathf.RoundToInt(degrees / SectorDegrees);
            return (CompassDirection)((sector % DirectionCount + DirectionCount) % DirectionCount);
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run the EditMode tests.
Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/Core/CompassDirection.cs Assets/Scripts/Core/CompassDirection.cs.meta Assets/Scripts/Core/CompassHeading.cs Assets/Scripts/Core/CompassHeading.cs.meta Assets/Tests/EditMode/CompassHeadingTests.cs Assets/Tests/EditMode/CompassHeadingTests.cs.meta docs/superpowers/specs/2026-10-06-ship-control-design.md docs/superpowers/plans/2026-10-06-ship-control.md
git commit -m @'
Cap d'un navire sur huit directions

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
'@
```

---

### Task 2: Ship runtime state and movement

**Files:**
- Create: `Assets/Scripts/Core/Ship.cs`
- Test: `Assets/Tests/EditMode/ShipTests.cs`

**Interfaces:**
- Consumes:
  - `CompassDirection`, `CompassHeading.FromVector(Vector2, CompassDirection)` from Task 1.
  - Existing `MapProjection`: `Vector2 NormalizedToWorld(Vector2)`, `Vector2 WorldToNormalized(Vector2)`.
- Produces `DarkFantasyMerchant.Core.Ship`:
  - `Ship(MapProjection projection, Vector2 position, float speed)`
  - `Vector2 Position { get; }` (normalized), `Vector2 WorldPosition { get; }`
  - `Vector2? Destination { get; }` (normalized, null when idle), `bool IsMoving { get; }`
  - `CompassDirection Heading { get; }`
  - `void SetDestination(Vector2 destination)`, `void Advance(float deltaTime)`

All tests use `new MapProjection(40f, 2f)`: a 40 × 20 world, so normalized `(0.5, 0.5)` is world `(0, 0)`, one normalized unit is 40 world units in x and 20 in y.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/ShipTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ShipTests
    {
        private const float Speed = 4f;

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        private MapProjection projection;

        [SetUp]
        public void SetUp()
        {
            // 40 x 20 world units: not square, so normalized steps differ per axis.
            projection = new MapProjection(40f, 2f);
        }

        private Ship CreateShip()
        {
            return new Ship(projection, Center, Speed);
        }

        [Test]
        public void NewShip_IsIdle_AtItsPosition_FacingSouth()
        {
            Ship ship = CreateShip();

            TestAssert.AreEqual(Center, ship.Position);
            TestAssert.AreEqual(Vector2.zero, ship.WorldPosition);
            Assert.IsFalse(ship.IsMoving);
            Assert.IsNull(ship.Destination);
            Assert.AreEqual(CompassDirection.S, ship.Heading);
        }

        [Test]
        public void Constructor_RejectsANullProjection()
        {
            Assert.Throws<ArgumentNullException>(() => new Ship(null, Center, Speed));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Constructor_RejectsAnInvalidSpeed(float speed)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Ship(projection, Center, speed));
        }

        [Test]
        public void Constructor_RejectsANaNPosition()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Ship(projection, new Vector2(float.NaN, 0.5f), Speed));
        }

        [Test]
        public void Constructor_ClampsThePositionToTheMap()
        {
            var ship = new Ship(projection, new Vector2(-1f, 3f), Speed);

            TestAssert.AreEqual(new Vector2(0f, 1f), ship.Position);
        }

        [Test]
        public void Advance_DoesNothing_WhenIdle()
        {
            Ship ship = CreateShip();

            ship.Advance(10f);

            TestAssert.AreEqual(Center, ship.Position);
            Assert.IsFalse(ship.IsMoving);
        }

        [Test]
        public void SetDestination_StartsTheTrip()
        {
            Ship ship = CreateShip();

            ship.SetDestination(new Vector2(1f, 0.5f));

            Assert.IsTrue(ship.IsMoving);
            Assert.IsTrue(ship.Destination.HasValue);
            TestAssert.AreEqual(new Vector2(1f, 0.5f), ship.Destination.Value);
            TestAssert.AreEqual(Center, ship.Position);
        }

        // East edge is 20 world units away, north edge 10, the north-east corner about 22.4.
        [TestCase(1f, 0.5f)]
        [TestCase(0.5f, 1f)]
        [TestCase(1f, 1f)]
        [TestCase(0f, 0f)]
        public void Advance_CoversSpeedTimesDeltaTime_InEveryDirection(float x, float y)
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(x, y));

            ship.Advance(1.5f);

            Assert.AreEqual(Speed * 1.5f, ship.WorldPosition.magnitude, 1e-3f);
            Assert.IsTrue(ship.IsMoving);
        }

        [Test]
        public void Advance_MovesTowardsTheDestination()
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(1f, 0.5f));

            ship.Advance(1f);

            TestAssert.AreEqual(new Vector2(4f, 0f), ship.WorldPosition);
            TestAssert.AreEqual(new Vector2(0.6f, 0.5f), ship.Position);
        }

        [Test]
        public void Advance_AccumulatesOverSeveralSteps()
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(1f, 0.5f));

            for (int i = 0; i < 10; i++)
            {
                ship.Advance(0.25f);
            }

            Assert.AreEqual(10f, ship.WorldPosition.x, 1e-3f);
            Assert.AreEqual(0f, ship.WorldPosition.y, 1e-3f);
        }

        // The destination is 4 world units away, one second of travel. Exactly one second
        // is left out: rounding decides whether that step reaches the point or the next one.
        [TestCase(1.01f)]
        [TestCase(2f)]
        [TestCase(1e6f)]
        [TestCase(float.PositiveInfinity)]
        public void Advance_StopsExactlyOnTheDestination_WithoutOvershooting(float deltaTime)
        {
            Ship ship = CreateShip();
            var destination = new Vector2(0.6f, 0.5f);
            ship.SetDestination(destination);

            ship.Advance(deltaTime);

            Assert.AreEqual(destination, ship.Position);
            Assert.IsFalse(ship.IsMoving);
            Assert.IsNull(ship.Destination);
        }

        [Test]
        public void Advance_DoesNotMoveAnArrivedShip()
        {
            Ship ship = CreateShip();
            var destination = new Vector2(0.6f, 0.5f);
            ship.SetDestination(destination);
            ship.Advance(5f);

            ship.Advance(5f);

            Assert.AreEqual(destination, ship.Position);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void Advance_IgnoresAnInvalidDeltaTime(float deltaTime)
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(1f, 0.5f));

            ship.Advance(deltaTime);

            TestAssert.AreEqual(Center, ship.Position);
            Assert.IsTrue(ship.IsMoving);
        }

        [TestCase(0.5f, 1f, CompassDirection.N)]
        [TestCase(1f, 1f, CompassDirection.NE)]
        [TestCase(1f, 0.5f, CompassDirection.E)]
        [TestCase(1f, 0f, CompassDirection.SE)]
        [TestCase(0.5f, 0f, CompassDirection.S)]
        [TestCase(0f, 0f, CompassDirection.SW)]
        [TestCase(0f, 0.5f, CompassDirection.W)]
        [TestCase(0f, 1f, CompassDirection.NW)]
        public void SetDestination_TurnsTheShipTowardsIt_AtOnce(float x, float y, CompassDirection expected)
        {
            Ship ship = CreateShip();

            // From the center, the corners are at 63 degrees from north in world space:
            // inside the diagonal sectors (22.5 to 67.5).
            ship.SetDestination(new Vector2(x, y));

            Assert.AreEqual(expected, ship.Heading);
        }

        [Test]
        public void Heading_UsesWorldSpace_NotNormalizedSpace()
        {
            Ship ship = CreateShip();

            // The normalized offset (0.15, 0.5) is 16.7 degrees from north, which would be
            // north. In world space it is (6, 10): 31 degrees, north-east.
            ship.SetDestination(new Vector2(0.65f, 1f));

            Assert.AreEqual(CompassDirection.NE, ship.Heading);
        }

        [Test]
        public void Heading_IsKeptOnArrival()
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(0.4f, 0.5f));

            ship.Advance(100f);

            Assert.IsFalse(ship.IsMoving);
            Assert.AreEqual(CompassDirection.W, ship.Heading);
        }

        [Test]
        public void SetDestination_RedirectsAShipUnderWay()
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(1f, 0.5f));
            ship.Advance(1f);

            ship.SetDestination(new Vector2(0.6f, 1f));
            ship.Advance(1f);

            Assert.AreEqual(CompassDirection.N, ship.Heading);
            TestAssert.AreEqual(new Vector2(4f, 4f), ship.WorldPosition);
        }

        [Test]
        public void SetDestination_ClampsToTheMap()
        {
            Ship ship = CreateShip();

            ship.SetDestination(new Vector2(2f, -1f));

            TestAssert.AreEqual(new Vector2(1f, 0f), ship.Destination.Value);
        }

        [Test]
        public void SetDestination_OnTheCurrentPosition_LeavesTheShipIdle_AndItsHeadingUnchanged()
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(0.6f, 0.5f));
            ship.Advance(100f);

            ship.SetDestination(ship.Position);
            ship.Advance(1f);

            Assert.IsFalse(ship.IsMoving);
            Assert.AreEqual(CompassDirection.E, ship.Heading);
            TestAssert.AreEqual(new Vector2(0.6f, 0.5f), ship.Position);
        }

        [Test]
        public void SetDestination_Twice_KeepsSailingToTheSamePoint()
        {
            Ship ship = CreateShip();
            var destination = new Vector2(1f, 0.5f);
            ship.SetDestination(destination);
            ship.Advance(1f);

            ship.SetDestination(destination);
            ship.Advance(1f);

            TestAssert.AreEqual(new Vector2(8f, 0f), ship.WorldPosition);
            Assert.AreEqual(CompassDirection.E, ship.Heading);
        }

        [TestCase(float.NaN, 0.5f)]
        [TestCase(0.5f, float.NaN)]
        public void SetDestination_IgnoresANaNPoint(float x, float y)
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(1f, 0.5f));

            ship.SetDestination(new Vector2(x, y));
            ship.Advance(1f);

            TestAssert.AreEqual(new Vector2(4f, 0f), ship.WorldPosition);
            TestAssert.AreEqual(new Vector2(1f, 0.5f), ship.Destination.Value);
        }

        [Test]
        public void AVeryShortTrip_StillTurnsTheShip()
        {
            Ship ship = CreateShip();

            // 0.004 world units to the west: far below Vector2's equality epsilon once squared.
            ship.SetDestination(new Vector2(0.4999f, 0.5f));

            Assert.IsTrue(ship.IsMoving);
            Assert.AreEqual(CompassDirection.W, ship.Heading);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests.
Expected: exit code `1`, `Logs\test.log` contains `error CS0246` naming `Ship`.

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/Ship.cs`:

```csharp
using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Runtime state of one ship: where it is, where it is going and which way it faces.
    /// Sails in a straight line at constant speed.
    /// </summary>
    public sealed class Ship
    {
        private readonly MapProjection projection;
        private readonly float speed;
        private Vector2 destination;

        /// <param name="position">Normalized map position; clamped to the map.</param>
        /// <param name="speed">World units per second.</param>
        public Ship(MapProjection projection, Vector2 position, float speed)
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
            Position = ClampToMap(position);
            Heading = CompassDirection.S;
        }

        /// <summary>Normalized map position.</summary>
        public Vector2 Position { get; private set; }

        public Vector2 WorldPosition => projection.NormalizedToWorld(Position);

        /// <summary>Normalized map position the ship is sailing to, or null when idle.</summary>
        public Vector2? Destination => IsMoving ? destination : (Vector2?)null;

        public bool IsMoving { get; private set; }

        public CompassDirection Heading { get; private set; }

        /// <summary>
        /// Orders the ship to a normalized map position, replacing any order in progress.
        /// The point is clamped to the map; a NaN point is ignored.
        /// </summary>
        public void SetDestination(Vector2 destination)
        {
            if (IsNaN(destination))
            {
                return;
            }

            Vector2 clamped = ClampToMap(destination);
            Vector2 toDestination = projection.NormalizedToWorld(clamped) - WorldPosition;

            // Compared per component: Vector2 equality is approximate and would drop a
            // very short trip.
            if (toDestination.x == 0f && toDestination.y == 0f)
            {
                IsMoving = false;
                return;
            }

            this.destination = clamped;
            IsMoving = true;
            Heading = CompassHeading.FromVector(toDestination, Heading);
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
            Vector2 toDestination = projection.NormalizedToWorld(destination) - worldPosition;
            float distance = toDestination.magnitude;
            float step = speed * deltaTime;

            // Also taken when the remaining distance underflows to zero.
            if (!(step < distance))
            {
                Position = destination;
                IsMoving = false;
                return;
            }

            Position = projection.WorldToNormalized(worldPosition + toDestination * (step / distance));
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

Run the EditMode tests.
Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Core/Ship.cs Assets/Scripts/Core/Ship.cs.meta Assets/Tests/EditMode/ShipTests.cs Assets/Tests/EditMode/ShipTests.cs.meta
git commit -m @'
État et déplacement en ligne droite d'un navire

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
'@
```

---

### Task 3: Ship definition and views

**Files:**
- Create: `Assets/Scripts/Game/ShipDefinition.cs`
- Create: `Assets/Scripts/Game/ShipView.cs`
- Create: `Assets/Scripts/Game/ShipsView.cs`
- Test: `Assets/Tests/EditMode/ShipDefinitionTests.cs`

**Interfaces:**
- Consumes:
  - `Ship`, `CompassDirection` from Tasks 1 and 2.
  - Existing `WorldMapView`: `bool IsReady`, `MapProjection Projection`, `IReadOnlyList<CityDefinition> Cities`.
  - Existing `CityDefinition.MapPosition` (`Vector2`, normalized), `MapSelectionState<T>` (`SetHovered`, `Select`, `ClearSelection`, `Selected`, `HoveredChanged`, `SelectedChanged`).
- Produces:
  - `ShipDefinition : ScriptableObject` with `string DisplayName`, `float Speed`, `Sprite SpriteFor(CompassDirection)`, `const int DirectionCount = 8`. Serialized fields: `displayName`, `speed`, `directionSprites`.
  - `ShipView : MonoBehaviour` with `void Initialize(Ship, ShipDefinition)`, `void Refresh()`, `void SetHovered(bool)`, `void SetSelected(bool)`, `void SetBaseScale(float)`. Serialized field: `spriteRenderer`.
  - `ShipsView : MonoBehaviour` with `MapSelectionState<Ship> Selection`, `IReadOnlyList<Ship> Ships`, `IReadOnlyList<Vector2> ShipWorldPositions`, `bool IsReady`, `void SetShipScale(float worldUnitsPerPixel)`. Serialized fields: `mapView`, `shipPrefab`, `playerShipDefinition`, `startCity`, `shipScreenPixelsPerUnit`.

`ShipView` and `ShipsView` are scene adapters: `Awake`/`Start`/`Update` do not run in EditMode, so they are verified in Play mode in Task 6. Only `ShipDefinition` gets an EditMode test here.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/ShipDefinitionTests.cs`:

```csharp
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ShipDefinitionTests
    {
        private Texture2D texture;
        private Sprite[] sprites;
        private ShipDefinition definition;

        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(4, 4);
            sprites = new Sprite[ShipDefinition.DirectionCount];

            for (int i = 0; i < sprites.Length; i++)
            {
                sprites[i] = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero, 4f);
                sprites[i].name = ((CompassDirection)i).ToString();
            }

            definition = ScriptableObject.CreateInstance<ShipDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(definition);

            foreach (Sprite sprite in sprites)
            {
                Object.DestroyImmediate(sprite);
            }

            Object.DestroyImmediate(texture);
        }

        [Test]
        public void NewDefinition_HasAPositiveSpeed()
        {
            Assert.Greater(definition.Speed, 0f);
        }

        [Test]
        public void SpriteFor_ReturnsTheSpriteOfEachDirection()
        {
            SetSprites(sprites.Length);

            for (int i = 0; i < ShipDefinition.DirectionCount; i++)
            {
                Assert.AreSame(sprites[i], definition.SpriteFor((CompassDirection)i));
            }
        }

        [Test]
        public void SpriteFor_ReturnsNull_WhenTheArrayIsTooShort()
        {
            SetSprites(3);

            Assert.AreSame(sprites[2], definition.SpriteFor(CompassDirection.E));
            Assert.IsNull(definition.SpriteFor(CompassDirection.SE));
            Assert.IsNull(definition.SpriteFor(CompassDirection.NW));
        }

        [Test]
        public void SpriteFor_ReturnsNull_ForAnEmptySlot()
        {
            SetSprites(sprites.Length);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("directionSprites").GetArrayElementAtIndex(4).objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsNull(definition.SpriteFor(CompassDirection.S));
        }

        [Test]
        public void SpriteFor_ReturnsNull_ForAValueOutsideTheEnum()
        {
            SetSprites(sprites.Length);

            Assert.IsNull(definition.SpriteFor((CompassDirection)8));
            Assert.IsNull(definition.SpriteFor((CompassDirection)(-1)));
        }

        private void SetSprites(int count)
        {
            var serialized = new SerializedObject(definition);
            SerializedProperty property = serialized.FindProperty("directionSprites");
            property.arraySize = count;

            for (int i = 0; i < count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run the EditMode tests.
Expected: exit code `1`, `Logs\test.log` contains `error CS0246` naming `ShipDefinition`.

- [ ] **Step 3: Write `ShipDefinition`**

`Assets/Scripts/Game/ShipDefinition.cs`:

```csharp
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>Static definition of a kind of ship. Holds no runtime state.</summary>
    [CreateAssetMenu(fileName = "Ship", menuName = "Dark Fantasy Merchant/Ship")]
    public sealed class ShipDefinition : ScriptableObject
    {
        public const int DirectionCount = 8;

        [SerializeField] private string displayName;

        [Tooltip("Sailing speed, in world units per second.")]
        [SerializeField, Min(0.01f)] private float speed = 1.5f;

        [Tooltip("One sprite per heading, clockwise from north: N, NE, E, SE, S, SW, W, NW.")]
        [SerializeField] private Sprite[] directionSprites = new Sprite[DirectionCount];

        public string DisplayName => displayName;

        public float Speed => speed;

        /// <returns>The sprite for a heading, or null when none is assigned.</returns>
        public Sprite SpriteFor(CompassDirection direction)
        {
            int index = (int)direction;

            return directionSprites != null && index >= 0 && index < directionSprites.Length
                ? directionSprites[index]
                : null;
        }
    }
}
```

- [ ] **Step 4: Write `ShipView`**

`Assets/Scripts/Game/ShipView.cs`:

```csharp
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>One ship on the world map.</summary>
    public sealed class ShipView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoveredColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.55f, 0.2f);
        [SerializeField] private float selectedScale = 1.25f;

        private Ship ship;
        private ShipDefinition definition;
        private bool isHovered;
        private bool isSelected;
        private float baseScale = 1f;

        public void Initialize(Ship ship, ShipDefinition definition)
        {
            this.ship = ship;
            this.definition = definition;
            name = $"Ship ({definition.DisplayName})";
            Refresh();
        }

        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
            Refresh();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            Refresh();
        }

        /// <summary>Scale that keeps the ship at a constant on-screen size.</summary>
        public void SetBaseScale(float scale)
        {
            baseScale = scale;
            Refresh();
        }

        /// <summary>Shows the ship where it is now, facing its heading.</summary>
        public void Refresh()
        {
            if (ship == null)
            {
                return;
            }

            Vector2 worldPosition = ship.WorldPosition;
            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);

            // A definition with a missing sprite keeps showing the previous heading.
            Sprite sprite = definition.SpriteFor(ship.Heading);

            if (sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }

            spriteRenderer.color = isSelected ? selectedColor : isHovered ? hoveredColor : normalColor;

            float scale = baseScale * (isSelected ? selectedScale : 1f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
```

- [ ] **Step 5: Write `ShipsView`**

`Assets/Scripts/Game/ShipsView.cs`:

```csharp
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Owns the ships of the map, their views and the ship selection state, and sails
    /// the ships every frame.
    /// </summary>
    public sealed class ShipsView : MonoBehaviour
    {
        private static readonly Vector2 MapCenter = new Vector2(0.5f, 0.5f);

        [SerializeField] private WorldMapView mapView;
        [SerializeField] private ShipView shipPrefab;
        [SerializeField] private ShipDefinition playerShipDefinition;

        [Tooltip("City the player ship starts on. Empty uses the first city of the map.")]
        [SerializeField] private CityDefinition startCity;

        [Tooltip("On-screen size, in pixels, of one world unit of ship sprite.")]
        [SerializeField] private float shipScreenPixelsPerUnit = 32f;

        private readonly List<Ship> ships = new List<Ship>();
        private readonly List<Vector2> shipWorldPositions = new List<Vector2>();
        private readonly List<ShipView> views = new List<ShipView>();

        private Ship hoveredShip;
        private Ship selectedShip;

        public MapSelectionState<Ship> Selection { get; } = new MapSelectionState<Ship>();

        public IReadOnlyList<Ship> Ships => ships;

        /// <summary>World position of each entry of <see cref="Ships"/>, in the same order.</summary>
        public IReadOnlyList<Vector2> ShipWorldPositions => shipWorldPositions;

        public bool IsReady => ships.Count > 0;

        // Start, not Awake: WorldMapView lays the map out in its Awake.
        private void Start()
        {
            if (mapView == null || shipPrefab == null || playerShipDefinition == null)
            {
                Debug.LogError("ShipsView needs a map view, a ship prefab and a player ship definition.", this);
                enabled = false;
                return;
            }

            if (!mapView.IsReady)
            {
                // WorldMapView already logged why the map could not be laid out.
                enabled = false;
                return;
            }

            // The Min attribute only constrains the Inspector, not the asset file.
            float speed = playerShipDefinition.Speed;

            if (!(speed > 0f) || float.IsInfinity(speed))
            {
                Debug.LogError(
                    $"ShipDefinition '{playerShipDefinition.name}' has an invalid speed ({speed}).",
                    playerShipDefinition);
                enabled = false;
                return;
            }

            Spawn(playerShipDefinition, StartPosition());
            Selection.HoveredChanged += OnHoveredChanged;
            Selection.SelectedChanged += OnSelectedChanged;
        }

        private void OnDestroy()
        {
            Selection.HoveredChanged -= OnHoveredChanged;
            Selection.SelectedChanged -= OnSelectedChanged;
        }

        private void Update()
        {
            for (int i = 0; i < ships.Count; i++)
            {
                ships[i].Advance(Time.deltaTime);
                shipWorldPositions[i] = ships[i].WorldPosition;
                views[i].Refresh();
            }
        }

        public void SetShipScale(float worldUnitsPerPixel)
        {
            float scale = worldUnitsPerPixel * shipScreenPixelsPerUnit;

            foreach (ShipView view in views)
            {
                view.SetBaseScale(scale);
            }
        }

        private Vector2 StartPosition()
        {
            if (startCity != null)
            {
                return startCity.MapPosition;
            }

            if (mapView.Cities.Count > 0)
            {
                return mapView.Cities[0].MapPosition;
            }

            Debug.LogWarning("ShipsView found no city to start on; the player ship starts at the map center.", this);
            return MapCenter;
        }

        private void Spawn(ShipDefinition definition, Vector2 position)
        {
            // A NaN city position would be rejected by the ship.
            if (float.IsNaN(position.x) || float.IsNaN(position.y))
            {
                position = MapCenter;
            }

            var ship = new Ship(mapView.Projection, position, definition.Speed);
            ShipView view = Instantiate(shipPrefab, transform);
            view.Initialize(ship, definition);

            ships.Add(ship);
            shipWorldPositions.Add(ship.WorldPosition);
            views.Add(view);
        }

        private void OnHoveredChanged(Ship ship)
        {
            ViewOf(hoveredShip)?.SetHovered(false);
            hoveredShip = ship;
            ViewOf(hoveredShip)?.SetHovered(true);
        }

        private void OnSelectedChanged(Ship ship)
        {
            ViewOf(selectedShip)?.SetSelected(false);
            selectedShip = ship;
            ViewOf(selectedShip)?.SetSelected(true);
        }

        private ShipView ViewOf(Ship ship)
        {
            int index = ship != null ? ships.IndexOf(ship) : -1;

            return index >= 0 ? views[index] : null;
        }
    }
}
```

Note: `ViewOf(...)?.` uses `?.` on a `MonoBehaviour`. That is safe here because `ViewOf` returns a real C# null, and views live as long as this component; do not copy the pattern to references that Unity may destroy.

- [ ] **Step 6: Run the tests to verify they pass**

Run the EditMode tests.
Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 7: Commit**

```powershell
git add Assets/Scripts/Game/ShipDefinition.cs Assets/Scripts/Game/ShipDefinition.cs.meta Assets/Scripts/Game/ShipView.cs Assets/Scripts/Game/ShipView.cs.meta Assets/Scripts/Game/ShipsView.cs Assets/Scripts/Game/ShipsView.cs.meta Assets/Tests/EditMode/ShipDefinitionTests.cs Assets/Tests/EditMode/ShipDefinitionTests.cs.meta
git commit -m @'
Définition et affichage des navires sur la carte

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
'@
```

---

### Task 4: `Command` input action

**Files:**
- Modify: `Assets/InputSystem_Actions.inputactions` (the `WorldMap` map, around lines 1043–1108)
- Modify: `Assets/Scripts/Game/WorldMapInput.cs`
- Modify: `Assets/Tests/EditMode/DarkFantasyMerchant.Tests.EditMode.asmdef`
- Test: `Assets/Tests/EditMode/WorldMapInputActionsTests.cs`

**Interfaces:**
- Consumes: existing `PointerGesture` (`Press`, `Move`, `Release`, `Cancel`).
- Produces: `event Action<Vector2> WorldMapInput.Commanded`, raised with the screen position of a right click on the map that did not start over the UI and was not a drag.

- [ ] **Step 1: Let the test assembly see the Input System**

In `Assets/Tests/EditMode/DarkFantasyMerchant.Tests.EditMode.asmdef`, add `Unity.InputSystem` to `references`:

```json
    "references": [
        "DarkFantasyMerchant.Core",
        "DarkFantasyMerchant.Game",
        "DarkFantasyMerchant.Editor",
        "Unity.InputSystem",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
```

- [ ] **Step 2: Write the failing test**

`Assets/Tests/EditMode/WorldMapInputActionsTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class WorldMapInputActionsTests
    {
        [Test]
        public void WorldMap_HasACommandAction_BoundToTheRightMouseButton()
        {
            Assert.IsNotNull(InputSystem.actions, "No project-wide input actions asset.");

            InputActionMap map = InputSystem.actions.FindActionMap("WorldMap");
            Assert.IsNotNull(map, "WorldMap action map");

            InputAction command = map.FindAction("Command");
            Assert.IsNotNull(command, "Command action");
            Assert.AreEqual(InputActionType.Button, command.type);
            Assert.AreEqual(1, command.bindings.Count);
            Assert.AreEqual("<Mouse>/rightButton", command.bindings[0].path);
        }

        [Test]
        public void TheRightMouseButton_IsNotBoundToAnotherWorldMapAction()
        {
            InputActionMap map = InputSystem.actions.FindActionMap("WorldMap", true);

            foreach (InputBinding binding in map.bindings)
            {
                if (binding.path == "<Mouse>/rightButton")
                {
                    Assert.AreEqual("Command", binding.action);
                }
            }
        }
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run the EditMode tests with `'-testFilter','DarkFantasyMerchant.Tests.EditMode.WorldMapInputActionsTests'`.
Expected: exit code `2`; `WorldMap_HasACommandAction_BoundToTheRightMouseButton` fails with `Command action`.

- [ ] **Step 4: Add the action to the asset**

In `Assets/InputSystem_Actions.inputactions`, in the `WorldMap` map's `actions` array, add a `Command` entry after `Cancel`. Replace:

```json
                {
                    "name": "Cancel",
                    "type": "Button",
                    "id": "5a1e6c20-3f4b-4d7a-9c11-0a6b2d8e4f07",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                }
            ],
```

with:

```json
                {
                    "name": "Cancel",
                    "type": "Button",
                    "id": "5a1e6c20-3f4b-4d7a-9c11-0a6b2d8e4f07",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                },
                {
                    "name": "Command",
                    "type": "Button",
                    "id": "5a1e6c20-3f4b-4d7a-9c11-0a6b2d8e4f08",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                }
            ],
```

The `Cancel` action with id `…4f07` exists only in the `WorldMap` map, so the match is unique.

In the same map's `bindings` array, add the binding after the `<Keyboard>/escape` one. Replace:

```json
                    "id": "5a1e6c20-3f4b-4d7a-9c11-0a6b2d8e4f14",
                    "path": "<Keyboard>/escape",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard&Mouse",
                    "action": "Cancel",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
```

with:

```json
                    "id": "5a1e6c20-3f4b-4d7a-9c11-0a6b2d8e4f14",
                    "path": "<Keyboard>/escape",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard&Mouse",
                    "action": "Cancel",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
                {
                    "name": "",
                    "id": "5a1e6c20-3f4b-4d7a-9c11-0a6b2d8e4f15",
                    "path": "<Mouse>/rightButton",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard&Mouse",
                    "action": "Command",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
```

Ids `…4f08` and `…4f15` are unused in the file.

- [ ] **Step 5: Read the action in `WorldMapInput`**

In `Assets/Scripts/Game/WorldMapInput.cs`:

Add the field after `cancelAction`:

```csharp
        private InputAction cancelAction;
        private InputAction commandAction;
```

Add the gesture after `panGesture`:

```csharp
        private PointerGesture clickGesture;
        private PointerGesture panGesture;
        private PointerGesture commandGesture;
```

Add the event after `Cancelled`:

```csharp
        public event Action Cancelled;

        /// <summary>Raised with the screen position of a right click on the map.</summary>
        public event Action<Vector2> Commanded;
```

Replace `Awake`:

```csharp
        private void Awake()
        {
            clickGesture = new PointerGesture(dragThresholdPixels);
            panGesture = new PointerGesture(0f);
            commandGesture = new PointerGesture(dragThresholdPixels);
        }
```

In `OnEnable`, after the `cancelAction` line:

```csharp
            cancelAction = actionMap.FindAction("Cancel", true);
            commandAction = actionMap.FindAction("Command", true);
            actionMap.Enable();
```

In `Update`, replace the two `UpdateGesture` calls:

```csharp
            UpdateGesture(clickAction, clickGesture, true, Clicked);
            UpdateGesture(panDragAction, panGesture, true, null);

            // The right button gives orders: holding it and moving must not pan the map.
            UpdateGesture(commandAction, commandGesture, false, Commanded);
```

Replace `UpdateGesture`:

```csharp
        private void UpdateGesture(InputAction button, PointerGesture gesture, bool pansMap, Action<Vector2> clicked)
        {
            if (button.WasPressedThisFrame() && !IsPointerOverUi)
            {
                gesture.Press(PointerPosition);
            }

            Vector2 delta = gesture.Move(PointerPosition);

            if (pansMap && delta != Vector2.zero)
            {
                Dragged?.Invoke(delta);
            }

            if (button.WasReleasedThisFrame() && gesture.Release())
            {
                clicked?.Invoke(PointerPosition);
            }
        }
```

Replace `CancelGestures`:

```csharp
        private void CancelGestures()
        {
            clickGesture?.Cancel();
            panGesture?.Cancel();
            commandGesture?.Cancel();
        }
```

`IsDragging` stays as it is: a held right button must not suppress hover.

- [ ] **Step 6: Run the tests to verify they pass**

Run all EditMode tests.
Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 7: Commit**

```powershell
git add Assets/InputSystem_Actions.inputactions Assets/Scripts/Game/WorldMapInput.cs Assets/Tests/EditMode/DarkFantasyMerchant.Tests.EditMode.asmdef Assets/Tests/EditMode/WorldMapInputActionsTests.cs Assets/Tests/EditMode/WorldMapInputActionsTests.cs.meta
git commit -m @'
Action Command (clic droit) sur la carte du monde

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
'@
```

---

### Task 5: Ship hover, selection and move orders

**Files:**
- Modify: `Assets/Scripts/Game/WorldMapInteraction.cs` (whole file)

**Interfaces:**
- Consumes:
  - `ShipsView` from Task 3: `Selection`, `Ships`, `ShipWorldPositions`, `IsReady`, `SetShipScale(float)`.
  - `WorldMapInput.Commanded` from Task 4.
  - `Ship.SetDestination(Vector2)` from Task 2.
  - Existing `CityPicker.PickNearest(IReadOnlyList<Vector2>, Vector2, float)`, `WorldMapCameraController.ScreenToWorld(Vector2)` and `WorldUnitsPerPixel`, `MapProjection.WorldToNormalized(Vector2)`.
- Produces: serialized field `shipsView` on `WorldMapInteraction` (wired by the setup tool in Task 6), and `shipPickRadiusPixels`.

This component is a scene adapter and has no EditMode test; its behaviour is verified in Play mode in Task 6. The gate for this task is a clean compile and the existing tests staying green.

- [ ] **Step 1: Rewrite `WorldMapInteraction`**

`Assets/Scripts/Game/WorldMapInteraction.cs`:

```csharp
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Turns the cursor and clicks into hover and selection of ships and cities, and
    /// right clicks into move orders. At most one thing is hovered, and one selected.
    /// </summary>
    public sealed class WorldMapInteraction : MonoBehaviour
    {
        [SerializeField] private WorldMapView mapView;
        [SerializeField] private WorldMapInput input;
        [SerializeField] private WorldMapCameraController cameraController;

        [Tooltip("Optional. Without it, only cities can be hovered and selected.")]
        [SerializeField] private ShipsView shipsView;

        [Tooltip("How close to a city the cursor must be, in screen pixels, at any zoom level.")]
        [SerializeField, Min(0f)] private float pickRadiusPixels = 20f;

        [Tooltip("How close to a ship the cursor must be, in screen pixels, at any zoom level.")]
        [SerializeField, Min(0f)] private float shipPickRadiusPixels = 24f;

        private bool isBound;

        private void Start()
        {
            if (mapView == null || input == null || cameraController == null)
            {
                Debug.LogError("WorldMapInteraction needs a map view, an input component and a camera controller.", this);
                enabled = false;
                return;
            }

            input.Clicked += OnClicked;
            input.Commanded += OnCommanded;
            input.Cancelled += OnCancelled;
            isBound = true;
        }

        private void OnDestroy()
        {
            if (isBound && input != null)
            {
                input.Clicked -= OnClicked;
                input.Commanded -= OnCommanded;
                input.Cancelled -= OnCancelled;
            }
        }

        private void Update()
        {
            if (!CanPick())
            {
                return;
            }

            bool pointerIsBusy = input.IsPointerOverUi || input.IsDragging;

            // A ship hides the city it sails over.
            Ship ship = pointerIsBusy ? null : PickShip(input.PointerPosition);
            CityDefinition city = pointerIsBusy || ship != null ? null : PickCity(input.PointerPosition);

            if (shipsView != null)
            {
                shipsView.Selection.SetHovered(ship);
            }

            mapView.Selection.SetHovered(city);
        }

        private void LateUpdate()
        {
            if (!CanPick())
            {
                return;
            }

            mapView.SetMarkerScale(cameraController.WorldUnitsPerPixel);

            if (HasShips())
            {
                shipsView.SetShipScale(cameraController.WorldUnitsPerPixel);
            }
        }

        private void OnClicked(Vector2 screenPosition)
        {
            if (!CanPick())
            {
                return;
            }

            Ship ship = PickShip(screenPosition);

            if (ship != null)
            {
                mapView.Selection.ClearSelection();
                shipsView.Selection.Select(ship);
                return;
            }

            ClearShipSelection();

            CityDefinition city = PickCity(screenPosition);

            if (city != null)
            {
                mapView.Selection.Select(city);
            }
            else
            {
                mapView.Selection.ClearSelection();
            }
        }

        private void OnCommanded(Vector2 screenPosition)
        {
            if (!CanPick() || !HasShips())
            {
                return;
            }

            Ship ship = shipsView.Selection.Selected;

            if (ship == null)
            {
                return;
            }

            // The ship clamps the point to the map and ignores an unusable one.
            Vector2 worldPosition = cameraController.ScreenToWorld(screenPosition);
            ship.SetDestination(mapView.Projection.WorldToNormalized(worldPosition));
        }

        private void OnCancelled()
        {
            mapView.Selection.ClearSelection();
            ClearShipSelection();
        }

        private void ClearShipSelection()
        {
            if (shipsView != null)
            {
                shipsView.Selection.ClearSelection();
            }
        }

        // Screen height is zero while the window is minimized.
        private bool CanPick()
        {
            return mapView.IsReady && cameraController.IsReady && Screen.height > 0;
        }

        private bool HasShips()
        {
            return shipsView != null && shipsView.IsReady;
        }

        private Ship PickShip(Vector2 screenPosition)
        {
            if (!HasShips())
            {
                return null;
            }

            int index = Pick(shipsView.ShipWorldPositions, screenPosition, shipPickRadiusPixels);

            return index >= 0 ? shipsView.Ships[index] : null;
        }

        private CityDefinition PickCity(Vector2 screenPosition)
        {
            int index = Pick(mapView.CityWorldPositions, screenPosition, pickRadiusPixels);

            return index >= 0 ? mapView.Cities[index] : null;
        }

        private int Pick(IReadOnlyList<Vector2> worldPositions, Vector2 screenPosition, float radiusPixels)
        {
            Vector2 worldPosition = cameraController.ScreenToWorld(screenPosition);
            float worldRadius = radiusPixels * cameraController.WorldUnitsPerPixel;

            return CityPicker.PickNearest(worldPositions, worldPosition, worldRadius);
        }
    }
}
```

- [ ] **Step 2: Verify it compiles and nothing regressed**

Run all EditMode tests.
Expected: exit code `0`, `result="Passed"`. Exit code `1` means a compile error: `Select-String -Path Logs\test.log -Pattern 'error CS'`.

- [ ] **Step 3: Commit**

```powershell
git add Assets/Scripts/Game/WorldMapInteraction.cs
git commit -m @'
Sélection du navire et ordre de déplacement au clic droit

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
'@
```

---

### Task 6: Setup tool, generated content, Play-mode check, documentation

**Files:**
- Modify: `Assets/Scripts/Editor/WorldMapSetup.cs`
- Generated: `Assets/Data/Ships/MerchantShip.asset`, `Assets/Prefabs/Ships/Ship.prefab`, change to `Assets/Scenes/WorldMap.unity`
- Test: `Assets/Tests/EditMode/ShipContentTests.cs`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes:
  - `ShipDefinition` (serialized fields `displayName`, `speed`, `directionSprites`), `ShipView` (serialized field `spriteRenderer`), `ShipsView` (serialized fields `mapView`, `shipPrefab`, `playerShipDefinition`) from Task 3.
  - `WorldMapInteraction` serialized field `shipsView` from Task 5.
  - `CompassDirection` from Task 1; its value names are the sprite name suffixes.
- Produces: the playable feature.

- [ ] **Step 1: Write the failing content test**

`Assets/Tests/EditMode/ShipContentTests.cs`:

```csharp
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>Checks the ship content generated by the world map setup tool.</summary>
    public class ShipContentTests
    {
        private const string DefinitionPath = "Assets/Data/Ships/MerchantShip.asset";
        private const string PrefabPath = "Assets/Prefabs/Ships/Ship.prefab";

        [Test]
        public void MerchantShip_HasTheRightSpriteForEveryDirection()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ShipDefinition>(DefinitionPath);
            Assert.IsNotNull(definition, DefinitionPath);

            for (int i = 0; i < ShipDefinition.DirectionCount; i++)
            {
                var direction = (CompassDirection)i;
                Sprite sprite = definition.SpriteFor(direction);

                Assert.IsNotNull(sprite, direction.ToString());
                Assert.AreEqual($"MerchantShip_{direction}", sprite.name);
            }
        }

        [Test]
        public void MerchantShip_HasANameAndAPositiveSpeed()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ShipDefinition>(DefinitionPath);
            Assert.IsNotNull(definition, DefinitionPath);

            Assert.IsNotEmpty(definition.DisplayName);
            Assert.Greater(definition.Speed, 0f);
        }

        [Test]
        public void ShipPrefab_DrawsAboveCityMarkers()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<ShipView>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath);

            var spriteRenderer = prefab.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(spriteRenderer);
            Assert.Greater(spriteRenderer.sortingOrder, 10);
            Assert.IsNotNull(spriteRenderer.sprite);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run the EditMode tests with `'-testFilter','DarkFantasyMerchant.Tests.EditMode.ShipContentTests'`.
Expected: exit code `2`; the three tests fail on the `IsNotNull` of the asset path.

- [ ] **Step 3: Extend `WorldMapSetup`**

In `Assets/Scripts/Editor/WorldMapSetup.cs`:

Add `using DarkFantasyMerchant.Core;` as the second `using` (after `System.IO`).

Update the class summary:

```csharp
    /// <summary>
    /// Generates the world map scene, the city marker and ship prefabs and the sample content.
    /// Safe to run again: existing assets are left untouched, and an existing scene only
    /// gains the ships object when it has none.
    /// </summary>
```

Add constants after `ScenePath`:

```csharp
        private const string ShipTexturePath = "Assets/Art/Ships/MerchantShip.png";
        private const string ShipSpritePrefix = "MerchantShip_";
        private const string ShipDataFolder = "Assets/Data/Ships";
        private const string ShipPrefabFolder = "Assets/Prefabs/Ships";
        private const string ShipDefinitionPath = ShipDataFolder + "/MerchantShip.asset";
        private const string ShipPrefabPath = ShipPrefabFolder + "/Ship.prefab";

        private const float MerchantShipSpeed = 1.5f;
        private const int ShipSortingOrder = 20;
```

In `Build`, create the ship assets and add the ships to the scene. Replace:

```csharp
            CreateMarkerPrefab(villageSprite, townSprite, capitalSprite);
            CreateDefinition(mapSprite);
            CreatePanelSettings();

            AssetDatabase.SaveAssets();
            BuildScene();
            AssetDatabase.SaveAssets();
```

with:

```csharp
            CreateMarkerPrefab(villageSprite, townSprite, capitalSprite);
            CreateDefinition(mapSprite);
            CreatePanelSettings();

            ShipDefinition shipDefinition = CreateShipDefinition();
            CreateShipPrefab(shipDefinition);

            AssetDatabase.SaveAssets();
            BuildScene();
            AddShipsToScene();
            AssetDatabase.SaveAssets();
```

In `EnsureFolders`, add the two ship folders to the array:

```csharp
            foreach (string folder in new[]
            {
                ArtFolder, CitiesFolder, MapDataFolder, PrefabFolder, UiFolder,
                ShipDataFolder, ShipPrefabFolder, "Assets/Scenes",
            })
            {
                Directory.CreateDirectory(folder);
            }
```

Add these methods after `CreatePanelSettings`:

```csharp
        private static ShipDefinition CreateShipDefinition()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShipDefinition>(ShipDefinitionPath);

            if (existing != null)
            {
                return existing;
            }

            Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(ShipTexturePath);
            string[] directionNames = System.Enum.GetNames(typeof(CompassDirection));

            var definition = ScriptableObject.CreateInstance<ShipDefinition>();
            AssetDatabase.CreateAsset(definition, ShipDefinitionPath);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("displayName").stringValue = "Merchant Ship";
            serialized.FindProperty("speed").floatValue = MerchantShipSpeed;

            SerializedProperty sprites = serialized.FindProperty("directionSprites");
            sprites.arraySize = directionNames.Length;

            for (int i = 0; i < directionNames.Length; i++)
            {
                sprites.GetArrayElementAtIndex(i).objectReferenceValue =
                    FindShipSprite(subAssets, ShipSpritePrefix + directionNames[i]);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static Sprite FindShipSprite(Object[] subAssets, string spriteName)
        {
            foreach (Object subAsset in subAssets)
            {
                if (subAsset is Sprite sprite && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            throw new FileNotFoundException($"Sprite '{spriteName}' is missing from the ship sprite sheet.", ShipTexturePath);
        }

        private static ShipView CreateShipPrefab(ShipDefinition definition)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShipView>(ShipPrefabPath);

            if (existing != null)
            {
                return existing;
            }

            // ObjectFactory applies the render pipeline's default sprite material.
            GameObject instance = ObjectFactory.CreateGameObject("Ship", typeof(SpriteRenderer), typeof(ShipView));

            var spriteRenderer = instance.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = definition.SpriteFor(CompassDirection.S);
            spriteRenderer.sortingOrder = ShipSortingOrder;

            SetReference(instance.GetComponent<ShipView>(), "spriteRenderer", spriteRenderer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, ShipPrefabPath);
            Object.DestroyImmediate(instance);
            return prefab.GetComponent<ShipView>();
        }
```

Add this method after `BuildScene`:

```csharp
        // Unlike the rest of the scene, the ships object is also added to a scene that
        // already exists, so a map built before ships existed gains them.
        private static void AddShipsToScene()
        {
            // The scene is missing when its creation was cancelled.
            if (!File.Exists(ScenePath))
            {
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);

            if (!scene.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.Log("Ships not added to the world map scene: the open scene has unsaved changes.");
                    return;
                }

                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var mapView = FindInScene<WorldMapView>(scene);

            if (mapView == null)
            {
                Debug.LogWarning($"{ScenePath} has no WorldMapView; ships not added.");
                return;
            }

            bool wasDirty = scene.isDirty;
            bool changed = false;
            var shipsView = FindInScene<ShipsView>(scene);

            if (shipsView == null)
            {
                // Loaded only now: opening a scene unloads unreferenced assets.
                var shipDefinition = AssetDatabase.LoadAssetAtPath<ShipDefinition>(ShipDefinitionPath);
                var shipPrefab = AssetDatabase.LoadAssetAtPath<ShipView>(ShipPrefabPath);

                if (shipDefinition == null || shipPrefab == null)
                {
                    throw new FileNotFoundException("A generated ship asset could not be loaded.");
                }

                var shipsObject = new GameObject("Ships");
                SceneManager.MoveGameObjectToScene(shipsObject, scene);

                shipsView = shipsObject.AddComponent<ShipsView>();
                SetReference(shipsView, "mapView", mapView);
                SetReference(shipsView, "shipPrefab", shipPrefab);
                SetReference(shipsView, "playerShipDefinition", shipDefinition);
                changed = true;
            }

            var interaction = FindInScene<WorldMapInteraction>(scene);

            if (interaction != null && IsReferenceEmpty(interaction, "shipsView"))
            {
                SetReference(interaction, "shipsView", shipsView);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);

            // Saving would also write the user's own pending edits; leave that to them.
            if (wasDirty)
            {
                Debug.Log($"Ships added to {ScenePath}. The scene had unsaved changes: save it to keep them.");
                return;
            }

            EditorSceneManager.SaveScene(scene);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);

                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static bool IsReferenceEmpty(Object target, string propertyName)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);

            if (property == null)
            {
                throw new System.MissingFieldException(target.GetType().Name, propertyName);
            }

            return property.objectReferenceValue == null;
        }
```

- [ ] **Step 4: Run the setup tool**

With the Editor **closed**:

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe"
$p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList '-batchmode','-projectPath','.','-executeMethod','DarkFantasyMerchant.Editor.WorldMapSetup.Build','-quit','-logFile','Logs\setup.log'
"exit code: $($p.ExitCode)"
Select-String -Path Logs\setup.log -Pattern 'World map setup finished|error CS|Exception'
```

Expected: exit code `0` and the line `World map setup finished.`, no `error CS`, no `Exception`.

With the Editor **open**: run the menu `Tools > Dark Fantasy Merchant > Build World Map Scene` through `Unity_RunCommand` (`DarkFantasyMerchant.Editor.WorldMapSetup.Build();`), then check `Unity_GetConsoleLogs` for the same line and no error.

Then confirm what was generated:

```powershell
git status --short
```

Expected new files: `Assets/Data/Ships.meta`, `Assets/Data/Ships/MerchantShip.asset` (+ `.meta`), `Assets/Prefabs/Ships.meta`, `Assets/Prefabs/Ships/Ship.prefab` (+ `.meta`); modified: `Assets/Scenes/WorldMap.unity`. Nothing else under `Assets/Data` or `Assets/Prefabs/WorldMap` may be modified.

```powershell
Select-String -Path Assets\Scenes\WorldMap.unity -Pattern 'm_Name: Ships|shipsView: \{fileID: [1-9]'
```

Expected: both patterns match (the `Ships` object exists and `WorldMapInteraction.shipsView` is set).

- [ ] **Step 5: Run the tool a second time to check it is idempotent**

Run the same command again, then:

```powershell
git status --short
git diff --stat
```

Expected: the same file list as after Step 4 and an unchanged diff for `WorldMap.unity` (one `Ships` object, not two):

```powershell
(Select-String -Path Assets\Scenes\WorldMap.unity -Pattern 'm_Name: Ships').Count
```

Expected: `1`.

- [ ] **Step 6: Run all tests**

Run all EditMode tests.
Expected: exit code `0`, `result="Passed"`, including `ShipContentTests`.

- [ ] **Step 7: Check the feature in Play mode**

This needs the Editor open on `Assets/Scenes/WorldMap.unity`. If the `unity-mcp` tools are available, enter Play mode with `Unity_RunCommand`, read `Unity_GetConsoleLogs` (expected: no error, no warning from `ShipsView`), and take a `Unity_Camera_Capture` to confirm the ship is drawn on the first city of the map (Sparia, on the western cape) above its marker.

Mouse clicks cannot be injected through these tools. Drive the same code path from `Unity_RunCommand` to check movement and heading, then capture:

```csharp
var ships = UnityEngine.Object.FindFirstObjectByType<DarkFantasyMerchant.Game.ShipsView>();
var ship = ships.Ships[0];
ships.Selection.Select(ship);
ship.SetDestination(new UnityEngine.Vector2(0.4f, 0.3f));
UnityEngine.Debug.Log($"heading {ship.Heading}, moving {ship.IsMoving}, destination {ship.Destination}");
```

Expected log: `heading SE, moving True`. A capture a few seconds later shows the ship tinted as selected, with the south-east sprite, away from Sparia.

The remaining checks need a person at the mouse. Hand this list to the user and record their answers; do not report them as verified otherwise:

1. Hovering the ship tints it; hovering a city under the ship does not tint the city.
2. Left click on the ship selects it; with a city selected first, its panel closes.
3. Left click on a city deselects the ship; left click on empty sea, or Escape, deselects everything.
4. Right click with the ship selected sends it to the point; it stops exactly there and keeps its last sprite.
5. Each of the eight directions shows the matching sprite.
6. Right click during a trip redirects at once; the ship keeps sailing after being deselected.
7. Right click with nothing selected does nothing.
8. Holding the right button and dragging neither pans the map nor gives an order.
9. Zooming in and out keeps the ship the same size on screen, and it stays clickable.
10. Right click over the city panel does nothing.

- [ ] **Step 8: Update `CLAUDE.md`**

In the "Project state" paragraph, replace `The first subsystem is the world map; nothing else of the game exists yet.` with:

```markdown
The world map is the first subsystem, and the player has one ship to sail on it; nothing else of the game exists yet.
```

Replace the last bullet of the "World map" section (the one starting with `Tools > Dark Fantasy Merchant > Build World Map Scene`) with:

```markdown
- `Tools > Dark Fantasy Merchant > Build World Map Scene` (`WorldMapSetup.Build`) recreates the scene, the marker and ship prefabs, the panel settings, the map definition or the ship definition when one is missing, and leaves existing ones untouched. The one exception is the `Ships` object: it is also added to an existing scene that has none. The tool offers to save the open scene first, and adds `WorldMap.unity` to the build list without removing other scenes. Sample cities are only recreated together with a missing map definition.
```

Add a new section right after the "World map" section:

```markdown
### Ships

- `Ship` (Core) is the runtime state of one ship: normalized position, destination, heading. `ShipDefinition` (Game) is its static definition: name, speed in world units per second, and eight sprites indexed by `CompassDirection` (`N, NE, E, SE, S, SW, W, NW`, the order of the sprite sheets).
- Ships sail in a straight line and ignore land. Steps are measured in world space through `MapProjection`, not in normalized space, so the speed is the same in every direction on a map that is not square.
- `ShipsView` owns the ships, their `ShipView`s and a `MapSelectionState<Ship>`, and advances the ships with `Time.deltaTime`. It holds a list although there is a single player ship, which starts on `startCity` or the first city of the map.
- `WorldMapInteraction` arbitrates between the two selection states: a ship is picked before the cities, and selecting one clears the other, so at most one thing is hovered and one selected. A right click sends the selected ship to the clicked point.
- Ships keep a constant on-screen size, like city markers, and are picked with `CityPicker` on their current world positions.
```

In "Project configuration", in the **Input** bullet, replace `(Point, Click, PanDrag, PanMove, Zoom, Cancel)` with:

```markdown
(Point, Click, PanDrag, PanMove, Zoom, Cancel, Command — the right mouse button, which gives orders and never pans)
```

- [ ] **Step 9: Commit**

```powershell
git add Assets/Scripts/Editor/WorldMapSetup.cs Assets/Data/Ships.meta Assets/Data/Ships Assets/Prefabs/Ships.meta Assets/Prefabs/Ships Assets/Scenes/WorldMap.unity Assets/Tests/EditMode/ShipContentTests.cs Assets/Tests/EditMode/ShipContentTests.cs.meta CLAUDE.md
git status --short
git commit -m @'
Navire marchand ajouté à la scène de la carte du monde

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
'@
```

`git status --short` before the commit must show nothing unstaged under `Assets/`. If Unity rewrote unrelated files (`ProjectSettings/`, `Packages/packages-lock.json`), inspect the diff and leave them out unless the change is explained by this task.

---

## Deviations made during execution

- **Task 2:** the `Advance_StopsExactlyOnTheDestination_WithoutOvershooting(1f)` case became `1.01f`. At exactly distance / speed, float rounding decides whether that step or the next one reaches the point.
- **Task 6, `CreateShipDefinition`:** the instance is filled in *before* `AssetDatabase.CreateAsset`. Applied after it, as written above, the values were not persisted and `ShipContentTests` failed on an empty asset.
- **Task 6, `AddShipsToScene`:** it takes a `bool sceneHadUnsavedChanges` that `Build` reads before creating anything. Creating the ship prefab instantiates a temporary object in the open scene and marks it as modified, so reading `scene.isDirty` inside `AddShipsToScene`, as written above, always found the scene modified and never saved it.
- Tests were run through the `unity-mcp` tools (`TestRunnerApi`), the Editor being open, instead of batch mode.
