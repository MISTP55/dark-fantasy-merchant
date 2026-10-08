# World Time Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The world has a date that passes, one day every 30 real seconds, shown at the top of the screen, with a fast forward mode (x60) in which the map is only watched and any player input goes back to normal speed.

**Architecture:** A `GameClock` in `Core` turns real seconds into simulated ones and raises an event per day; `GameDate` is the calendar date derived from it. A `CalendarDefinition` asset holds the calendar and the pace. A `WorldClock` MonoBehaviour owns the clock and advances it before everything else; `ShipsView` sails its ships with the clock's delta. `WorldMapInteraction` decides what fast forward means for the map, `WorldMapCameraController` zooms out, locks and restores the view, and `TimeHudController` shows the date and the button.

**Tech Stack:** Unity 6000.6.4f1, C#, UI Toolkit (UXML + USS), Input System, Unity Test Framework (NUnit, EditMode).

**Spec:** `docs/superpowers/specs/2026-10-07-world-time-design.md`

## Global Constraints

- All code, comments, log messages, asset and folder names are in English.
- `Core` contains no `MonoBehaviour` and nothing that needs a scene. It may use `UnityEngine` value types (`Vector2`, `Mathf`).
- `Time.timeScale` stays at 1. Only what asks the clock for its time speeds up; the camera and the UI stay in real time.
- Start date: day 1 of month 1 of year 932. One day lasts 30 real seconds at normal speed. Fast forward is x60.
- Calendar: 12 months of 30 days. Month names, in order: `Janus`, `Febrin`, `Martis`, `Aprilis`, `Maius`, `Junis`, `Julis`, `Augustis`, `Septem`, `Octem`, `Novem`, `Decem`.
- UI text is English: the date reads `1 Janus 932`; the button reads `Fast forward`, and `Normal speed` while fast forwarding.
- Runtime UI is UI Toolkit only. No uGUI.
- ScriptableObjects are never written while the game runs.
- Every reference to the `WorldClock` is optional: a scene without one behaves exactly as before. The existing tests must pass **unchanged**.
- Never hand-write `.meta` files. Unity generates them on import; commit them together with their file.
- Never edit the generated `.sln` / `.csproj`. Exclude `Library/` from searches.
- Match the surrounding code: Allman braces, a blank line before `return` / `if` blocks as in the existing files, XML `<summary>` on public types, comments only where the reason is not obvious.
- Spec changes, intentional:
  - **No new input action.** The spec says the inputs are read through a new action of the `WorldMap` map bound to any key, the mouse buttons and the wheel. Two things prevent it: `WorldMapInputActionsTests` forbids binding the right button to a second action, and a button action bound to `<Keyboard>/anyKey` does not see a key pressed while another one is held. `WorldMapInput` instead reuses the `Click`, `PanDrag`, `Command` and `Zoom` actions for the mouse, and scans `Keyboard.current.allKeys` for a key pressed this frame. It stays the only component that reads input.
  - **`WorldMapInput.ConsumeInput()`** replaces the per-frame bookkeeping the spec gives to `WorldMapInteraction`: when a listener of `AnyInput` consumes the input, `WorldMapInput` raises nothing else that frame and starts no gesture, so the release of the same button is not a click. `CancelGestures()` is public as well.
  - **A real step longer than an hour is clamped to an hour** in `GameClock.Advance`. Unity already caps `Time.deltaTime`; the clamp keeps the day loop bounded for any caller.
  - The time HUD forces its document root to cover the screen (`position: absolute`, all four edges at 0), so its layout does not depend on how two documents share a panel.
- Work on a new branch `world-time`, created from `main` (which holds the spec and this plan).
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

To run one fixture, append `'-testFilter','DarkFantasyMerchant.Tests.EditMode.GameClockTests'` to the argument list.

Reading the result:

- Exit code `0` and `result="Passed"` in the `<test-run` line: all tests passed.
- Exit code `2`: at least one test failed. Details: `Select-String -Path Logs\TestResults.xml -Pattern 'result="Failed"' -Context 0,6`.
- Exit code `1` and no `TestResults.xml`: compilation failed. Details: `Select-String -Path Logs\test.log -Pattern 'error CS'`.

A test file that references a type or member that does not exist yet fails the **whole compilation** (exit code `1`), not just that test. That is the expected "red" for the first step of each task.

A run takes one to three minutes. The first run after adding files also generates their `.meta` files; add them to the commit.

**Run the world map setup tool** (Task 7):

```powershell
$p = Start-Process -FilePath $unity -Wait -PassThru -ArgumentList '-batchmode','-projectPath','.','-executeMethod','DarkFantasyMerchant.Editor.WorldMapSetup.Build','-quit','-logFile','Logs\setup.log'
"exit code: $($p.ExitCode)"
Select-String -Path Logs\setup.log -Pattern 'World map setup finished|Exception|error CS'
```

## Review Focus

Conditions the spec implies but does not spell out, most likely first.

1. **The press that leaves fast forward is released over a city or a ship.** Nothing must be selected: the player only stopped the fast forward. Owned by `WorldMapInput.ConsumeInput` (Task 5); it needs real input, so it is a manual check in Task 7, step 7.
2. **A key is pressed while another one is held** (the player was panning with `W` when fast forward started). It must leave fast forward like any key. Owned by `WorldMapInput` (Task 5); manual check in Task 7, step 7.
3. **A step far longer than a frame** (a stall, a breakpoint, an infinite or NaN delta). Every day crossed is announced once and in order, the loop is bounded, and a bad delta moves nothing. Tests in Task 2 (`SeveralDaysInOneStep_…`, `AStepLongerThanAnHour_…`, `AnInvalidDelta_…`).
4. **The window is resized during fast forward.** The map stays fully visible, and the view restored afterwards is a valid one for the new shape. Tests in Task 3 (`SetView_AfterTheAspectChanged_…`); `WorldMapCameraController` zooms out again after an aspect change while locked (Task 6).
5. **A calendar asset edited into something unusable** (no month name, a blank name, zero days per month). The game must say so in the console and keep running without a clock, not throw every frame; a blank name must still give a readable date. Tests in Task 4 (`ACalendarWithoutMonths_…`, `ABlankMonthName_…`); `WorldClock` logs and disables itself (Task 6).

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `Assets/Scripts/Core/GameDate.cs` | Create | A day of the calendar, built from elapsed days. |
| `Assets/Scripts/Core/GameClock.cs` | Create | Simulated time, the two speeds, the day event. |
| `Assets/Scripts/Core/MapCameraModel.cs` | Modify | `ZoomOutFully`, `SetView`. |
| `Assets/Scripts/Game/CalendarDefinition.cs` | Create | Calendar and pace content; formats a date. |
| `Assets/Scripts/Game/WorldClock.cs` | Create | Owns the clock, advances it first each frame. |
| `Assets/Scripts/Game/WorldMapInput.cs` | Modify | `AnyInput`, `ConsumeInput`, public `CancelGestures`. |
| `Assets/Scripts/Game/ShipsView.cs` | Modify | Sails ships in simulated time. |
| `Assets/Scripts/Game/WorldMapCameraController.cs` | Modify | Zooms out, locks, restores the view. |
| `Assets/Scripts/Game/WorldMapInteraction.cs` | Modify | Fast forward rules of the map. |
| `Assets/Scripts/Game/TimeHudController.cs` | Create | Date label and fast forward button. |
| `Assets/UI/WorldMap/TimeHud.uxml`, `TimeHud.uss` | Create | The HUD's layout and style. |
| `Assets/Scripts/Editor/WorldMapSetup.cs` | Modify | Calendar asset, clock and HUD objects, references. |
| `Assets/Data/Calendar/Calendar.asset` | Generated | By the setup tool. |
| `Assets/Scenes/WorldMap.unity` | Generated | Gains the clock and the HUD, by the setup tool. |
| `Assets/Tests/EditMode/GameDateTests.cs` | Create | |
| `Assets/Tests/EditMode/GameClockTests.cs` | Create | |
| `Assets/Tests/EditMode/MapCameraModelTests.cs` | Modify | The two new methods. |
| `Assets/Tests/EditMode/CalendarDefinitionTests.cs` | Create | Defaults and formatting, on an in-memory instance. |
| `Assets/Tests/EditMode/CalendarContentTests.cs` | Create | The generated asset. |
| `CLAUDE.md` | Modify | Time section, project state, input, setup tool. |

---

### Task 1: `GameDate`

**Files:**
- Create: `Assets/Scripts/Core/GameDate.cs`
- Test: `Assets/Tests/EditMode/GameDateTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `public readonly struct GameDate : IEquatable<GameDate>` with `int Year`, `int Month` (from 1), `int Day` (from 1), constructor `GameDate(int year, int month, int day)`, `==` / `!=`.
  - `public static GameDate GameDate.FromElapsedDays(int elapsedDays, int startYear, int monthsPerYear, int daysPerMonth)`; throws `ArgumentOutOfRangeException` for a negative `elapsedDays` or a count below 1.

- [ ] **Step 1: Create the branch**

```powershell
git checkout -b world-time
```

- [ ] **Step 2: Write the failing tests**

`Assets/Tests/EditMode/GameDateTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class GameDateTests
    {
        private static GameDate After(int elapsedDays)
        {
            return GameDate.FromElapsedDays(elapsedDays, 932, 12, 30);
        }

        [Test]
        public void NoElapsedDay_IsTheFirstDayOfTheStartYear()
        {
            Assert.AreEqual(new GameDate(932, 1, 1), After(0));
        }

        [Test]
        public void TheLastDayOfAMonth_IsFollowedByTheFirstOfTheNext()
        {
            Assert.AreEqual(new GameDate(932, 1, 30), After(29));
            Assert.AreEqual(new GameDate(932, 2, 1), After(30));
        }

        [Test]
        public void TheLastDayOfAYear_IsFollowedByTheFirstOfTheNextYear()
        {
            Assert.AreEqual(new GameDate(932, 12, 30), After(359));
            Assert.AreEqual(new GameDate(933, 1, 1), After(360));
        }

        [Test]
        public void ManyYears_AreCounted()
        {
            Assert.AreEqual(new GameDate(942, 6, 15), After(10 * 360 + 5 * 30 + 14));
        }

        [Test]
        public void AnotherCalendarShape_IsFollowed()
        {
            // 4 months of 10 days: day 45 is the sixth day of the first month of the next year.
            Assert.AreEqual(new GameDate(2, 1, 6), GameDate.FromElapsedDays(45, 1, 4, 10));
        }

        [Test]
        public void Dates_AreEqualByValue()
        {
            Assert.IsTrue(new GameDate(932, 3, 4) == new GameDate(932, 3, 4));
            Assert.IsTrue(new GameDate(932, 3, 4) != new GameDate(932, 3, 5));
            Assert.AreEqual(new GameDate(932, 3, 4).GetHashCode(), new GameDate(932, 3, 4).GetHashCode());
        }

        [TestCase(-1, 12, 30)]
        [TestCase(0, 0, 30)]
        [TestCase(0, 12, 0)]
        public void InvalidArguments_AreRejected(int elapsedDays, int monthsPerYear, int daysPerMonth)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GameDate.FromElapsedDays(elapsedDays, 932, monthsPerYear, daysPerMonth));
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run the EditMode tests with `-testFilter DarkFantasyMerchant.Tests.EditMode.GameDateTests` (see Commands).
Expected: exit code `1`, `error CS0246` for `GameDate` in `Logs\test.log`.

- [ ] **Step 4: Write the implementation**

`Assets/Scripts/Core/GameDate.cs`:

```csharp
using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// A day of the game's calendar. Months and days are counted from 1. Holds no month
    /// name: names are content.
    /// </summary>
    public readonly struct GameDate : IEquatable<GameDate>
    {
        public GameDate(int year, int month, int day)
        {
            Year = year;
            Month = month;
            Day = day;
        }

        public int Year { get; }

        public int Month { get; }

        public int Day { get; }

        /// <param name="elapsedDays">Whole days since the first day of <paramref name="startYear"/>.</param>
        public static GameDate FromElapsedDays(int elapsedDays, int startYear, int monthsPerYear, int daysPerMonth)
        {
            if (elapsedDays < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedDays));
            }

            if (monthsPerYear < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(monthsPerYear));
            }

            if (daysPerMonth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysPerMonth));
            }

            int daysPerYear = monthsPerYear * daysPerMonth;
            int dayOfYear = elapsedDays % daysPerYear;

            return new GameDate(
                startYear + elapsedDays / daysPerYear,
                dayOfYear / daysPerMonth + 1,
                dayOfYear % daysPerMonth + 1);
        }

        public bool Equals(GameDate other)
        {
            return Year == other.Year && Month == other.Month && Day == other.Day;
        }

        public override bool Equals(object obj)
        {
            return obj is GameDate other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Year * 397 ^ Month) * 397 ^ Day;
        }

        public override string ToString()
        {
            return $"{Year}-{Month}-{Day}";
        }

        public static bool operator ==(GameDate left, GameDate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameDate left, GameDate right)
        {
            return !left.Equals(right);
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Same command as step 3. Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Scripts/Core/GameDate.cs Assets/Scripts/Core/GameDate.cs.meta Assets/Tests/EditMode/GameDateTests.cs Assets/Tests/EditMode/GameDateTests.cs.meta
git commit -m "Date du calendrier du jeu" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `GameClock`

**Files:**
- Create: `Assets/Scripts/Core/GameClock.cs`
- Test: `Assets/Tests/EditMode/GameClockTests.cs`

**Interfaces:**
- Consumes: `GameDate.FromElapsedDays(int elapsedDays, int startYear, int monthsPerYear, int daysPerMonth)`.
- Produces, on `public sealed class GameClock`:
  - `GameClock(float secondsPerDay, float fastForwardMultiplier, int startYear, int monthsPerYear, int daysPerMonth)`; throws `ArgumentOutOfRangeException` for a value that is not positive and finite, or a count below 1.
  - `void Advance(float realDeltaSeconds)`
  - `float DeltaTime { get; }` — simulated seconds of the last `Advance`.
  - `int ElapsedDays { get; }`, `GameDate Date { get; }`
  - `bool IsFastForward { get; }`, `void SetFastForward(bool isFastForward)`
  - `event Action<GameDate> DayStarted`, `event Action<bool> FastForwardChanged`
  - `public const float MaxRealDeltaSeconds = 3600f`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/GameClockTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class GameClockTests
    {
        // 30 seconds per day, fast forward x60, the game's calendar.
        private static GameClock NewClock()
        {
            return new GameClock(30f, 60f, 932, 12, 30);
        }

        [Test]
        public void StartsOnTheFirstDay_AtNormalSpeed()
        {
            GameClock clock = NewClock();

            Assert.AreEqual(new GameDate(932, 1, 1), clock.Date);
            Assert.AreEqual(0, clock.ElapsedDays);
            Assert.AreEqual(0f, clock.DeltaTime);
            Assert.IsFalse(clock.IsFastForward);
        }

        [Test]
        public void ADay_LastsTheConfiguredSeconds()
        {
            GameClock clock = NewClock();

            clock.Advance(29f);
            Assert.AreEqual(new GameDate(932, 1, 1), clock.Date);

            clock.Advance(1f);
            Assert.AreEqual(new GameDate(932, 1, 2), clock.Date);
            Assert.AreEqual(1, clock.ElapsedDays);
        }

        [Test]
        public void DeltaTime_IsTheRealDelta_AtNormalSpeed()
        {
            GameClock clock = NewClock();

            clock.Advance(0.25f);

            Assert.AreEqual(0.25f, clock.DeltaTime, 1e-6f);
        }

        [Test]
        public void FastForward_MultipliesDeltaTimeAndThePace()
        {
            GameClock clock = NewClock();
            clock.SetFastForward(true);

            clock.Advance(0.5f);

            Assert.AreEqual(30f, clock.DeltaTime, 1e-4f);
            Assert.AreEqual(new GameDate(932, 1, 2), clock.Date);
        }

        [Test]
        public void ChangingSpeed_KeepsTheTimeAlreadyElapsed()
        {
            GameClock clock = NewClock();
            clock.Advance(15f);
            clock.SetFastForward(true);

            // Half a day was left: a quarter of a second at x60.
            clock.Advance(0.25f);

            Assert.AreEqual(new GameDate(932, 1, 2), clock.Date);
        }

        [Test]
        public void AYear_PassesInThreeMinutesOfFastForward()
        {
            GameClock clock = NewClock();
            clock.SetFastForward(true);

            clock.Advance(180f);

            Assert.AreEqual(new GameDate(933, 1, 1), clock.Date);
        }

        [Test]
        public void SeveralDaysInOneStep_AreEachAnnounced_InOrder()
        {
            GameClock clock = NewClock();
            var announced = new List<GameDate>();
            var datesSeenByTheListener = new List<GameDate>();
            clock.DayStarted += date =>
            {
                announced.Add(date);
                datesSeenByTheListener.Add(clock.Date);
            };

            clock.Advance(95f);

            var expected = new[] { new GameDate(932, 1, 2), new GameDate(932, 1, 3), new GameDate(932, 1, 4) };
            CollectionAssert.AreEqual(expected, announced);

            // The clock is on the announced day while it announces it.
            CollectionAssert.AreEqual(expected, datesSeenByTheListener);
        }

        [Test]
        public void NoDayIsAnnounced_WithoutADayChange()
        {
            GameClock clock = NewClock();
            int announced = 0;
            clock.DayStarted += _ => announced++;

            clock.Advance(10f);
            clock.Advance(10f);

            Assert.AreEqual(0, announced);
        }

        [TestCase(0f)]
        [TestCase(-5f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void AnInvalidDelta_MovesNothing(float realDeltaSeconds)
        {
            GameClock clock = NewClock();
            clock.Advance(12f);

            clock.Advance(realDeltaSeconds);

            Assert.AreEqual(0f, clock.DeltaTime);
            Assert.AreEqual(new GameDate(932, 1, 1), clock.Date);

            // The 12 seconds are still there: 18 more end the day.
            clock.Advance(18f);
            Assert.AreEqual(new GameDate(932, 1, 2), clock.Date);
        }

        [Test]
        public void AStepLongerThanAnHour_IsClampedToAnHour()
        {
            GameClock clock = NewClock();
            int announced = 0;
            clock.DayStarted += _ => announced++;

            clock.Advance(float.MaxValue);

            // An hour of real time is 120 days of 30 seconds.
            Assert.AreEqual(GameClock.MaxRealDeltaSeconds, clock.DeltaTime, 1e-3f);
            Assert.AreEqual(120, clock.ElapsedDays);
            Assert.AreEqual(120, announced);
        }

        [Test]
        public void FastForwardChanged_IsRaisedOnlyOnAnActualChange()
        {
            GameClock clock = NewClock();
            var changes = new List<bool>();
            clock.FastForwardChanged += changes.Add;

            clock.SetFastForward(false);
            clock.SetFastForward(true);
            clock.SetFastForward(true);
            clock.SetFastForward(false);

            CollectionAssert.AreEqual(new[] { true, false }, changes);
            Assert.IsFalse(clock.IsFastForward);
        }

        [Test]
        public void AListener_CanChangeTheSpeedWhileADayIsAnnounced()
        {
            GameClock clock = NewClock();
            clock.SetFastForward(true);
            clock.DayStarted += _ => clock.SetFastForward(false);

            // Two days at x60: both are announced, the speed changes for the next step.
            clock.Advance(1f);

            Assert.AreEqual(2, clock.ElapsedDays);
            Assert.IsFalse(clock.IsFastForward);

            clock.Advance(1f);
            Assert.AreEqual(1f, clock.DeltaTime, 1e-6f);
        }

        [TestCase(0f, 60f, 12, 30)]
        [TestCase(-30f, 60f, 12, 30)]
        [TestCase(float.NaN, 60f, 12, 30)]
        [TestCase(float.PositiveInfinity, 60f, 12, 30)]
        [TestCase(30f, 0f, 12, 30)]
        [TestCase(30f, float.NaN, 12, 30)]
        [TestCase(30f, float.PositiveInfinity, 12, 30)]
        [TestCase(30f, 60f, 0, 30)]
        [TestCase(30f, 60f, 12, 0)]
        public void InvalidSettings_AreRejected(
            float secondsPerDay, float fastForwardMultiplier, int monthsPerYear, int daysPerMonth)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GameClock(secondsPerDay, fastForwardMultiplier, 932, monthsPerYear, daysPerMonth));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run with `-testFilter DarkFantasyMerchant.Tests.EditMode.GameClockTests`.
Expected: exit code `1`, `error CS0246` for `GameClock`.

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Core/GameClock.cs`:

```csharp
using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The time of the simulated world. Turns real seconds into simulated ones, at normal
    /// speed or in fast forward, and tells when a day starts. The simulation is advanced
    /// with <see cref="DeltaTime"/>, never with real time.
    /// </summary>
    public sealed class GameClock
    {
        /// <summary>A longer real step is a stall, not time played, and is clamped to this.</summary>
        public const float MaxRealDeltaSeconds = 3600f;

        private readonly double secondsPerDay;
        private readonly float fastForwardMultiplier;
        private readonly int startYear;
        private readonly int monthsPerYear;
        private readonly int daysPerMonth;

        // A double: a float would lose the seconds after a few months of play.
        private double elapsedSeconds;

        /// <param name="secondsPerDay">Real seconds a day lasts at normal speed.</param>
        /// <param name="fastForwardMultiplier">How many times faster the world runs in fast forward.</param>
        public GameClock(
            float secondsPerDay, float fastForwardMultiplier, int startYear, int monthsPerYear, int daysPerMonth)
        {
            if (!IsFinitePositive(secondsPerDay))
            {
                throw new ArgumentOutOfRangeException(nameof(secondsPerDay));
            }

            if (!IsFinitePositive(fastForwardMultiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(fastForwardMultiplier));
            }

            if (monthsPerYear < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(monthsPerYear));
            }

            if (daysPerMonth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysPerMonth));
            }

            this.secondsPerDay = secondsPerDay;
            this.fastForwardMultiplier = fastForwardMultiplier;
            this.startYear = startYear;
            this.monthsPerYear = monthsPerYear;
            this.daysPerMonth = daysPerMonth;

            Date = DateAfter(0);
        }

        /// <summary>Simulated seconds of the last <see cref="Advance"/>.</summary>
        public float DeltaTime { get; private set; }

        /// <summary>Whole days since the start of the game.</summary>
        public int ElapsedDays { get; private set; }

        public GameDate Date { get; private set; }

        public bool IsFastForward { get; private set; }

        /// <summary>
        /// Raised once for every day that starts, in order, with its date. A step that
        /// crosses several days raises it for each of them.
        /// </summary>
        public event Action<GameDate> DayStarted;

        /// <summary>Raised with the new value, only on an actual change.</summary>
        public event Action<bool> FastForwardChanged;

        public void Advance(float realDeltaSeconds)
        {
            // The negated comparison also rejects NaN.
            if (!(realDeltaSeconds > 0f) || float.IsInfinity(realDeltaSeconds))
            {
                DeltaTime = 0f;
                return;
            }

            float realDelta = Math.Min(realDeltaSeconds, MaxRealDeltaSeconds);
            DeltaTime = IsFastForward ? realDelta * fastForwardMultiplier : realDelta;
            elapsedSeconds += DeltaTime;

            int days = (int)Math.Floor(elapsedSeconds / secondsPerDay);

            while (ElapsedDays < days)
            {
                ElapsedDays++;
                Date = DateAfter(ElapsedDays);
                DayStarted?.Invoke(Date);
            }
        }

        public void SetFastForward(bool isFastForward)
        {
            if (IsFastForward == isFastForward)
            {
                return;
            }

            IsFastForward = isFastForward;
            FastForwardChanged?.Invoke(isFastForward);
        }

        private GameDate DateAfter(int elapsedDays)
        {
            return GameDate.FromElapsedDays(elapsedDays, startYear, monthsPerYear, daysPerMonth);
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsInfinity(value);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Same command as step 2. Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Core/GameClock.cs Assets/Scripts/Core/GameClock.cs.meta Assets/Tests/EditMode/GameClockTests.cs Assets/Tests/EditMode/GameClockTests.cs.meta
git commit -m "Horloge de la simulation : deux vitesses et un événement par jour" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `MapCameraModel` — zoom out fully and go to a view

**Files:**
- Modify: `Assets/Scripts/Core/MapCameraModel.cs`
- Test: `Assets/Tests/EditMode/MapCameraModelTests.cs`

**Interfaces:**
- Consumes: the existing `MapCameraModel` (`Position`, `OrthographicSize`, `MinOrthographicSize`, `MaxOrthographicSize`, `Zoom`, `Pan`, `SetAspect`, private `ClampPosition`, `IsFinite`, `IsFinitePositive`).
- Produces:
  - `public void MapCameraModel.ZoomOutFully()`
  - `public void MapCameraModel.SetView(Vector2 position, float orthographicSize)` — clamped; an invalid argument leaves the view unchanged.

- [ ] **Step 1: Write the failing tests**

Add these tests at the end of the `MapCameraModelTests` class in `Assets/Tests/EditMode/MapCameraModelTests.cs`, after `WorldUnitsPerPixel_RejectsInvalidScreenHeight`. The fixture's `Map` is 40 x 20 centered on the origin; with an aspect of 2 the largest size is 10 and the constructor's third argument, 2, is the smallest.

```csharp
        [Test]
        public void ZoomOutFully_ShowsTheWholeMap_FromAnyView()
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.Zoom(0.5f, Vector2.zero);
            model.Pan(new Vector2(5f, 2f));

            model.ZoomOutFully();

            Assert.AreEqual(10f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(Vector2.zero, model.Position);
        }

        [Test]
        public void SetView_GoesToTheGivenView()
        {
            var model = new MapCameraModel(Map, 2f, 2f);

            model.SetView(new Vector2(3f, 1f), 5f);

            Assert.AreEqual(5f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(new Vector2(3f, 1f), model.Position);
        }

        [Test]
        public void SetView_RestoresTheViewLeftByZoomOutFully()
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.Zoom(0.3f, new Vector2(4f, -2f));
            model.Pan(new Vector2(-6f, 1f));
            Vector2 savedPosition = model.Position;
            float savedSize = model.OrthographicSize;

            model.ZoomOutFully();
            model.SetView(savedPosition, savedSize);

            Assert.AreEqual(savedSize, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(savedPosition, model.Position);
        }

        [TestCase(0.5f, 2f)]
        [TestCase(100f, 10f)]
        public void SetView_ClampsTheSize(float requestedSize, float expectedSize)
        {
            var model = new MapCameraModel(Map, 2f, 2f);

            model.SetView(Vector2.zero, requestedSize);

            Assert.AreEqual(expectedSize, model.OrthographicSize, 1e-4f);
        }

        [Test]
        public void SetView_ClampsThePositionToTheMap()
        {
            var model = new MapCameraModel(Map, 2f, 2f);

            // Size 5: half extents are 10 x 5, so the center stays within (-10..10, -5..5).
            model.SetView(new Vector2(100f, -100f), 5f);

            TestAssert.AreEqual(new Vector2(10f, -5f), model.Position);
        }

        [Test]
        public void SetView_AfterTheAspectChanged_GivesTheNearestValidView()
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.SetView(new Vector2(8f, 0f), 8f);
            Vector2 savedPosition = model.Position;
            float savedSize = model.OrthographicSize;

            // A wider window: the largest size becomes 40 / (2 * 4) = 5.
            model.ZoomOutFully();
            model.SetAspect(4f);
            model.SetView(savedPosition, savedSize);

            // The view is as wide as the map: it can only be centered.
            Assert.AreEqual(5f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(Vector2.zero, model.Position);
        }

        [Test]
        public void SetView_IgnoresAnInvalidView()
        {
            var model = new MapCameraModel(Map, 2f, 2f);
            model.SetView(new Vector2(3f, 1f), 5f);

            model.SetView(new Vector2(float.NaN, 0f), 4f);
            model.SetView(Vector2.zero, 0f);
            model.SetView(Vector2.zero, float.NaN);
            model.SetView(Vector2.zero, float.PositiveInfinity);

            Assert.AreEqual(5f, model.OrthographicSize, 1e-4f);
            TestAssert.AreEqual(new Vector2(3f, 1f), model.Position);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run with `-testFilter DarkFantasyMerchant.Tests.EditMode.MapCameraModelTests`.
Expected: exit code `1`, `error CS1061` for `ZoomOutFully` and `SetView`.

- [ ] **Step 3: Write the implementation**

In `Assets/Scripts/Core/MapCameraModel.cs`, insert between the `Zoom` method and the `ViewportToWorld` summary:

```csharp
        /// <summary>Shows as much of the map as the view can.</summary>
        public void ZoomOutFully()
        {
            OrthographicSize = MaxOrthographicSize;
            ClampPosition();
        }

        /// <summary>
        /// Goes to a view, clamped to the size limits and to the map: the nearest valid
        /// view when the given one is not. An invalid view is ignored.
        /// </summary>
        public void SetView(Vector2 position, float orthographicSize)
        {
            if (!IsFinite(position) || !IsFinitePositive(orthographicSize))
            {
                return;
            }

            OrthographicSize = Mathf.Clamp(orthographicSize, MinOrthographicSize, MaxOrthographicSize);
            Position = position;
            ClampPosition();
        }

```

- [ ] **Step 4: Run the tests to verify they pass**

Same command as step 2. Expected: exit code `0`, `result="Passed"`, the existing tests of the fixture included.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Core/MapCameraModel.cs Assets/Tests/EditMode/MapCameraModelTests.cs
git commit -m "Caméra de la carte : dézoom complet et retour à une vue donnée" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `CalendarDefinition`

**Files:**
- Create: `Assets/Scripts/Game/CalendarDefinition.cs`
- Test: `Assets/Tests/EditMode/CalendarDefinitionTests.cs`

**Interfaces:**
- Consumes: `GameClock(float secondsPerDay, float fastForwardMultiplier, int startYear, int monthsPerYear, int daysPerMonth)`, `GameDate`.
- Produces, on `public sealed class CalendarDefinition : ScriptableObject`:
  - serialized fields `startYear` (int), `monthNames` (string[]), `daysPerMonth` (int), `secondsPerDay` (float), `fastForwardMultiplier` (float), whose **default values are the game's calendar**: a new instance is already correct content.
  - `int StartYear`, `int MonthsPerYear`, `int DaysPerMonth`, `float SecondsPerDay`, `float FastForwardMultiplier`
  - `GameClock CreateClock()` — throws `ArgumentOutOfRangeException` when the values are unusable.
  - `string MonthName(int month)` — month from 1; `Month <n>` when there is no usable name.
  - `string Format(GameDate date)` — `<day> <month name> <year>`.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/EditMode/CalendarDefinitionTests.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CalendarDefinitionTests
    {
        private CalendarDefinition calendar;

        [SetUp]
        public void SetUp()
        {
            calendar = ScriptableObject.CreateInstance<CalendarDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(calendar);
        }

        [Test]
        public void ANewCalendar_IsTheGamesCalendar()
        {
            Assert.AreEqual(932, calendar.StartYear);
            Assert.AreEqual(12, calendar.MonthsPerYear);
            Assert.AreEqual(30, calendar.DaysPerMonth);
            Assert.AreEqual(30f, calendar.SecondsPerDay);
            Assert.AreEqual(60f, calendar.FastForwardMultiplier);
        }

        [Test]
        public void MonthNames_AreTheInventedOnes_InOrder()
        {
            string[] expected =
            {
                "Janus", "Febrin", "Martis", "Aprilis", "Maius", "Junis",
                "Julis", "Augustis", "Septem", "Octem", "Novem", "Decem",
            };

            for (int month = 1; month <= expected.Length; month++)
            {
                Assert.AreEqual(expected[month - 1], calendar.MonthName(month));
            }
        }

        [Test]
        public void Format_IsDayMonthNameYear()
        {
            Assert.AreEqual("1 Janus 932", calendar.Format(new GameDate(932, 1, 1)));
            Assert.AreEqual("30 Decem 933", calendar.Format(new GameDate(933, 12, 30)));
        }

        [Test]
        public void CreateClock_StartsOnTheFirstDayOfTheStartYear_AtTheCalendarsPace()
        {
            GameClock clock = calendar.CreateClock();

            Assert.AreEqual("1 Janus 932", calendar.Format(clock.Date));

            clock.Advance(30f);
            Assert.AreEqual("2 Janus 932", calendar.Format(clock.Date));

            clock.SetFastForward(true);
            clock.Advance(0.5f);
            Assert.AreEqual("3 Janus 932", calendar.Format(clock.Date));
        }

        [TestCase(0)]
        [TestCase(13)]
        [TestCase(-1)]
        public void AMonthWithoutAName_IsNamedByItsNumber(int month)
        {
            Assert.AreEqual($"Month {month}", calendar.MonthName(month));
        }

        [Test]
        public void ABlankMonthName_IsReplacedByTheMonthNumber()
        {
            var serialized = new SerializedObject(calendar);
            serialized.FindProperty("monthNames").GetArrayElementAtIndex(2).stringValue = "  ";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual("4 Month 3 932", calendar.Format(new GameDate(932, 3, 4)));
        }

        [Test]
        public void ACalendarWithoutMonths_CannotCreateAClock()
        {
            var serialized = new SerializedObject(calendar);
            serialized.FindProperty("monthNames").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(0, calendar.MonthsPerYear);
            Assert.Throws<ArgumentOutOfRangeException>(() => calendar.CreateClock());
        }

        [Test]
        public void ACalendarWithoutDaysInAMonth_CannotCreateAClock()
        {
            var serialized = new SerializedObject(calendar);
            serialized.FindProperty("daysPerMonth").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<ArgumentOutOfRangeException>(() => calendar.CreateClock());
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run with `-testFilter DarkFantasyMerchant.Tests.EditMode.CalendarDefinitionTests`.
Expected: exit code `1`, `error CS0246` for `CalendarDefinition`.

- [ ] **Step 3: Write the implementation**

`Assets/Scripts/Game/CalendarDefinition.cs`:

```csharp
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Static definition of the world's calendar and of the pace of its time. Holds no
    /// runtime state: the current date is in a <see cref="GameClock"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Calendar", menuName = "Dark Fantasy Merchant/Calendar")]
    public sealed class CalendarDefinition : ScriptableObject
    {
        [Tooltip("Year of the first day of the game.")]
        [SerializeField] private int startYear = 932;

        [Tooltip("One name per month, in order. Their number is the number of months of a year.")]
        [SerializeField] private string[] monthNames =
        {
            "Janus", "Febrin", "Martis", "Aprilis", "Maius", "Junis",
            "Julis", "Augustis", "Septem", "Octem", "Novem", "Decem",
        };

        [SerializeField, Min(1)] private int daysPerMonth = 30;

        [Tooltip("Real seconds a day lasts at normal speed.")]
        [SerializeField, Min(0.01f)] private float secondsPerDay = 30f;

        [Tooltip("How many times faster the world runs in fast forward.")]
        [SerializeField, Min(1f)] private float fastForwardMultiplier = 60f;

        public int StartYear => startYear;

        public int MonthsPerYear => monthNames != null ? monthNames.Length : 0;

        public int DaysPerMonth => daysPerMonth;

        public float SecondsPerDay => secondsPerDay;

        public float FastForwardMultiplier => fastForwardMultiplier;

        /// <summary>
        /// A clock on the first day of the start year. Throws when the values of the asset
        /// are unusable: the Min attributes only constrain the Inspector, not the file.
        /// </summary>
        public GameClock CreateClock()
        {
            return new GameClock(secondsPerDay, fastForwardMultiplier, startYear, MonthsPerYear, daysPerMonth);
        }

        /// <param name="month">Month of the year, from 1.</param>
        /// <returns>The month's name, or its number when it has no usable name.</returns>
        public string MonthName(int month)
        {
            int index = month - 1;
            string monthName = index >= 0 && index < MonthsPerYear ? monthNames[index] : null;

            return string.IsNullOrWhiteSpace(monthName) ? $"Month {month}" : monthName;
        }

        public string Format(GameDate date)
        {
            return $"{date.Day} {MonthName(date.Month)} {date.Year}";
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Same command as step 2. Expected: exit code `0`, `result="Passed"`.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Game/CalendarDefinition.cs Assets/Scripts/Game/CalendarDefinition.cs.meta Assets/Tests/EditMode/CalendarDefinitionTests.cs Assets/Tests/EditMode/CalendarDefinitionTests.cs.meta
git commit -m "Définition du calendrier et du rythme du temps" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `WorldMapInput` — any input, and consuming it

**Files:**
- Modify: `Assets/Scripts/Game/WorldMapInput.cs`

**Interfaces:**
- Consumes: the existing `WorldMapInput` and `PointerGesture`.
- Produces, on `WorldMapInput`:
  - `public event Action AnyInput` — raised at the start of the frame's input, before `Clicked`, `Dragged`, `Zoomed`, `Commanded` and `Cancelled`, when a keyboard key is pressed (anywhere), or a mouse button is pressed or the wheel is turned while the pointer is **not** over the UI.
  - `public void ConsumeInput()` — called by a listener of `AnyInput`: nothing else is raised this frame, no gesture starts, `MoveAxis` is zero this frame.
  - `public void CancelGestures()` — now public.

`WorldMapInput` is a MonoBehaviour over live input devices; it has no EditMode test, like today. It is verified by compiling, by the existing test suite, and by hand in Task 7.

- [ ] **Step 1: Add the event and the flag**

In `Assets/Scripts/Game/WorldMapInput.cs`, add `using UnityEngine.InputSystem.Controls;` after `using UnityEngine.InputSystem;`.

After the `private PointerGesture panGesture;` field, add:

```csharp

        private bool inputConsumed;
```

After the `Commanded` event, add:

```csharp

        /// <summary>
        /// Raised when the player does anything but move the pointer: a keyboard key is
        /// pressed, or a mouse button is pressed or the wheel turned on the map. Raised
        /// before the other events of the frame, so that a listener can consume the input.
        /// </summary>
        public event Action AnyInput;
```

- [ ] **Step 2: Replace `Update`**

Replace the whole `Update` method with:

```csharp
        private void Update()
        {
            PointerPosition = pointAction.ReadValue<Vector2>();
            MoveAxis = panMoveAction.ReadValue<Vector2>();
            IsPointerOverUi = ui != null && ui.IsPointerOverUi(PointerPosition);

            // Scroll magnitude differs between devices and platforms; only its direction is used.
            float scroll = zoomAction.ReadValue<Vector2>().y;

            // The UI handles the pointer input that starts over it; the keyboard is the map's.
            bool pointerInput = clickAction.WasPressedThisFrame() || panDragAction.WasPressedThisFrame()
                || commandAction.WasPressedThisFrame() || scroll != 0f;

            inputConsumed = false;

            if (WasAnyKeyPressedThisFrame() || (pointerInput && !IsPointerOverUi))
            {
                AnyInput?.Invoke();
            }

            if (inputConsumed)
            {
                return;
            }

            UpdateGesture(clickAction, clickGesture, Clicked);
            UpdateGesture(panDragAction, panGesture, null);

            // The right button gives its order as soon as it is pressed, wherever it is
            // released: an order given while the pointer is moving must not be lost.
            if (commandAction.WasPressedThisFrame() && !IsPointerOverUi)
            {
                Commanded?.Invoke(PointerPosition);
            }

            if (scroll != 0f && !IsPointerOverUi)
            {
                Zoomed?.Invoke(Mathf.Sign(scroll), PointerPosition);
            }

            if (cancelAction.WasPressedThisFrame())
            {
                Cancelled?.Invoke();
            }
        }

        /// <summary>
        /// For a listener of <see cref="AnyInput"/> that used the input up: nothing else is
        /// raised this frame and no gesture starts, so the release of the button that was
        /// just pressed is not a click.
        /// </summary>
        public void ConsumeInput()
        {
            inputConsumed = true;
            MoveAxis = Vector2.zero;
            CancelGestures();
        }

        /// <summary>Drops the click and the drag in progress.</summary>
        public void CancelGestures()
        {
            clickGesture?.Cancel();
            panGesture?.Cancel();
        }

        // Not the keyboard's "any key" control: it stays pressed while one key is held,
        // and would miss a second key pressed meanwhile.
        private static bool WasAnyKeyPressedThisFrame()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return false;
            }

            foreach (KeyControl key in keyboard.allKeys)
            {
                if (key != null && key.wasPressedThisFrame)
                {
                    return true;
                }
            }

            return false;
        }
```

- [ ] **Step 3: Remove the old private `CancelGestures`**

Delete the original method at the end of the class (it is now the public one above):

```csharp
        private void CancelGestures()
        {
            clickGesture?.Cancel();
            panGesture?.Cancel();
        }
```

`OnDisable` and `OnApplicationFocus` keep calling `CancelGestures()` unchanged.

- [ ] **Step 4: Compile and run the whole suite**

Run all EditMode tests (no filter).
Expected: exit code `0`, `result="Passed"`. An exit code of `1` is a compile error: read `Logs\test.log`.

- [ ] **Step 5: Commit**

```powershell
git add Assets/Scripts/Game/WorldMapInput.cs
git commit -m "Entrées de la carte : signal « n'importe quelle entrée », qui peut être consommée" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `WorldClock`, the HUD, and the fast forward mode

One task: the clock component, the three components that follow it and the HUD only make sense, and can only be seen working, together. Nothing here is reachable by an EditMode test; the logic they rely on was tested in Tasks 1 to 4. The scene still has no clock after this task, so the game behaves as before until Task 7.

**Files:**
- Create: `Assets/Scripts/Game/WorldClock.cs`
- Create: `Assets/Scripts/Game/TimeHudController.cs`
- Create: `Assets/UI/WorldMap/TimeHud.uxml`
- Create: `Assets/UI/WorldMap/TimeHud.uss`
- Modify: `Assets/Scripts/Game/ShipsView.cs`
- Modify: `Assets/Scripts/Game/WorldMapCameraController.cs`
- Modify: `Assets/Scripts/Game/WorldMapInteraction.cs`

**Interfaces:**
- Consumes:
  - `GameClock`: `Advance(float)`, `DeltaTime`, `Date`, `IsFastForward`, `SetFastForward(bool)`, `event Action<GameDate> DayStarted`, `event Action<bool> FastForwardChanged`.
  - `CalendarDefinition`: `CreateClock()` (throws `ArgumentOutOfRangeException`), `Format(GameDate)`.
  - `MapCameraModel`: `ZoomOutFully()`, `SetView(Vector2, float)`, `Position`, `OrthographicSize`.
  - `WorldMapInput`: `event Action AnyInput`, `ConsumeInput()`, `CancelGestures()`.
- Produces:
  - `public sealed class WorldClock : MonoBehaviour` with serialized field `calendar` (`CalendarDefinition`), `GameClock Clock { get; }` (null when the calendar is unusable), `CalendarDefinition Calendar { get; }`, `bool IsReady { get; }`.
  - `public sealed class TimeHudController : MonoBehaviour` with serialized field `worldClock` (`WorldClock`); it needs a `UIDocument` whose tree has a `Label` named `date-label` and a `Button` named `fast-forward-button`.
  - A serialized field named `worldClock` (`WorldClock`, optional) on `ShipsView`, `WorldMapCameraController` and `WorldMapInteraction`. Task 7 sets these by name.

- [ ] **Step 1: Create `WorldClock`**

`Assets/Scripts/Game/WorldClock.cs`:

```csharp
using System;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Owns the world's <see cref="GameClock"/> and advances it every frame, before the
    /// components that read its time.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class WorldClock : MonoBehaviour
    {
        [SerializeField] private CalendarDefinition calendar;

        /// <summary>Null when the calendar is missing or unusable.</summary>
        public GameClock Clock { get; private set; }

        public CalendarDefinition Calendar => calendar;

        public bool IsReady => Clock != null;

        // Awake, not Start: the other components look for the clock in their OnEnable or Start.
        private void Awake()
        {
            if (calendar == null)
            {
                Debug.LogError("WorldClock needs a calendar definition.", this);
                enabled = false;
                return;
            }

            try
            {
                Clock = calendar.CreateClock();
            }
            catch (ArgumentOutOfRangeException exception)
            {
                Debug.LogError(
                    $"CalendarDefinition '{calendar.name}' has an invalid value ({exception.ParamName}); time does not pass.",
                    calendar);
                enabled = false;
            }
        }

        private void Update()
        {
            // Real time: the clock is what turns it into the world's time.
            Clock.Advance(Time.deltaTime);
        }
    }
}
```

- [ ] **Step 2: `ShipsView` sails in simulated time**

In `Assets/Scripts/Game/ShipsView.cs`, after the `routeView` field, add:

```csharp

        [Tooltip("Optional. Without it, ships sail in real time.")]
        [SerializeField] private WorldClock worldClock;
```

In `Update`, replace:

```csharp
            foreach (Ship ship in ships)
            {
                ship.Advance(Time.deltaTime);
            }
```

with:

```csharp
            // The world's time: ships sail faster in fast forward.
            float deltaTime = worldClock != null && worldClock.IsReady ? worldClock.Clock.DeltaTime : Time.deltaTime;

            foreach (Ship ship in ships)
            {
                ship.Advance(deltaTime);
            }
```

- [ ] **Step 3: `WorldMapCameraController` zooms out, locks and restores**

In `Assets/Scripts/Game/WorldMapCameraController.cs`:

After the `input` field, add:

```csharp

        [Tooltip("Optional. With it, the view shows the whole map and is locked during fast forward.")]
        [SerializeField] private WorldClock worldClock;
```

After the `private float appliedAspect;` field, add:

```csharp

        // The view to return to after fast forward, during which the camera is locked.
        private GameClock clock;
        private bool isLocked;
        private Vector2 viewBeforeLockPosition;
        private float viewBeforeLockSize;
```

In `Start`, replace:

```csharp
            input.Dragged += OnDragged;
            input.Zoomed += OnZoomed;
            Apply();
```

with:

```csharp
            input.Dragged += OnDragged;
            input.Zoomed += OnZoomed;

            if (worldClock != null && worldClock.IsReady)
            {
                clock = worldClock.Clock;
                clock.FastForwardChanged += OnFastForwardChanged;
                OnFastForwardChanged(clock.IsFastForward);
            }

            Apply();
```

In `OnDestroy`, after the closing brace of `if (input != null) { ... }`, add:

```csharp

            if (clock != null)
            {
                clock.FastForwardChanged -= OnFastForwardChanged;
            }
```

In `Update`, replace:

```csharp
                model.SetAspect(aspect);
                appliedAspect = aspect;
```

with:

```csharp
                model.SetAspect(aspect);
                appliedAspect = aspect;

                // A locked view shows the whole map, whatever the window's shape.
                if (isLocked)
                {
                    model.ZoomOutFully();
                }
```

and replace:

```csharp
            if (move != Vector2.zero)
            {
```

with:

```csharp
            if (move != Vector2.zero && !isLocked)
            {
```

In `OnDragged` and in `OnZoomed`, replace the condition:

```csharp
            if (model == null || Screen.height <= 0)
```

with (in both methods):

```csharp
            if (model == null || isLocked || Screen.height <= 0)
```

Add this method after `OnZoomed`:

```csharp
        // Both ways are eased by the smoother, like any other move of the camera.
        private void OnFastForwardChanged(bool isFastForward)
        {
            if (isFastForward && !isLocked)
            {
                viewBeforeLockPosition = model.Position;
                viewBeforeLockSize = model.OrthographicSize;
                model.ZoomOutFully();
                isLocked = true;
            }
            else if (!isFastForward && isLocked)
            {
                // Clamped: the window may have been resized meanwhile.
                model.SetView(viewBeforeLockPosition, viewBeforeLockSize);
                isLocked = false;
            }
        }
```

Update the class summary to:

```csharp
    /// <summary>
    /// Feeds input to a <see cref="MapCameraModel"/> and shows its state on the camera,
    /// eased by a <see cref="MapCameraSmoother"/>. Holds no clamping, zoom or easing math of its own.
    /// During fast forward it shows the whole map and ignores the input.
    /// </summary>
```

- [ ] **Step 4: `WorldMapInteraction` applies the fast forward rules**

In `Assets/Scripts/Game/WorldMapInteraction.cs`:

After the `shipsView` field, add:

```csharp

        [Tooltip("Optional. With it, nothing can be hovered, selected or ordered during fast forward, and any input ends it.")]
        [SerializeField] private WorldClock worldClock;
```

After the `private bool isBound;` field, add:

```csharp
        private GameClock clock;

        private bool IsFastForward => clock != null && clock.IsFastForward;
```

In `Start`, replace:

```csharp
            mapView.Selection.SelectedChanged += OnCitySelected;
            isBound = true;
```

with:

```csharp
            mapView.Selection.SelectedChanged += OnCitySelected;

            if (worldClock != null && worldClock.IsReady)
            {
                clock = worldClock.Clock;
                clock.FastForwardChanged += OnFastForwardChanged;
                input.AnyInput += OnAnyInput;
            }

            isBound = true;
```

In `OnDestroy`, inside `if (input != null) { ... }`, after `input.Cancelled -= OnCancelled;`, add:

```csharp
                input.AnyInput -= OnAnyInput;
```

and after the closing brace of `if (mapView != null) { ... }`, add:

```csharp

            if (clock != null)
            {
                clock.FastForwardChanged -= OnFastForwardChanged;
            }
```

In `Update`, replace:

```csharp
            bool pointerIsBusy = input.IsPointerOverUi || input.IsDragging;
```

with:

```csharp
            // Nothing is hovered during fast forward: the map is only watched.
            bool pointerIsBusy = input.IsPointerOverUi || input.IsDragging || IsFastForward;
```

In `OnClicked`, replace:

```csharp
            if (!CanPick())
            {
                return;
            }

            Ship ship = PickShip(screenPosition);
```

with:

```csharp
            if (!CanPick() || IsFastForward)
            {
                return;
            }

            Ship ship = PickShip(screenPosition);
```

In `OnCommanded`, replace:

```csharp
            if (!CanPick() || !HasShips())
```

with:

```csharp
            if (!CanPick() || !HasShips() || IsFastForward)
```

Replace `OnCancelled` with:

```csharp
        private void OnCancelled()
        {
            if (IsFastForward)
            {
                return;
            }

            mapView.Selection.ClearSelection();
            ClearShipSelection();
        }

        // The map is only watched during fast forward: nothing stays hovered or selected,
        // and a click that was started before it is dropped.
        private void OnFastForwardChanged(bool isFastForward)
        {
            if (!isFastForward)
            {
                return;
            }

            input.CancelGestures();
            mapView.Selection.SetHovered(null);
            mapView.Selection.ClearSelection();

            if (shipsView != null)
            {
                shipsView.Selection.SetHovered(null);
                shipsView.Selection.ClearSelection();
            }
        }

        // Any input ends the fast forward, and does nothing else: a click on a city only
        // goes back to normal speed.
        private void OnAnyInput()
        {
            if (!IsFastForward)
            {
                return;
            }

            clock.SetFastForward(false);
            input.ConsumeInput();
        }
```

Update the class summary to:

```csharp
    /// <summary>
    /// Turns the cursor and clicks into hover and selection of ships and cities, and
    /// right clicks into move orders. At most one thing is hovered, and one selected,
    /// except that a ship in port is selected together with the city it lies in.
    /// During fast forward nothing is hovered, selected or ordered, and any input goes
    /// back to normal speed.
    /// </summary>
```

- [ ] **Step 5: Create the HUD layout and style**

`Assets/UI/WorldMap/TimeHud.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="TimeHud.uss" />
    <ui:VisualElement name="time-hud" picking-mode="Ignore" class="time-hud">
        <ui:Label name="date-label" picking-mode="Ignore" class="time-hud__date" />
        <ui:Button name="fast-forward-button" text="Fast forward" class="time-hud__button" />
    </ui:VisualElement>
</ui:UXML>
```

`Assets/UI/WorldMap/TimeHud.uss` (same palette as `CityInfoPanel.uss`):

```css
.time-hud {
    position: absolute;
    top: 16px;
    left: 0;
    right: 0;
    align-items: center;
}

.time-hud__date {
    padding: 4px 18px;
    color: rgb(240, 226, 196);
    background-color: rgba(24, 17, 12, 0.94);
    border-width: 2px;
    border-color: rgb(110, 84, 52);
    font-size: 22px;
    -unity-font-style: bold;
    -unity-text-align: middle-center;
}

.time-hud__button {
    margin: 6px 0 0 0;
    padding: 4px 14px;
    color: rgb(222, 210, 188);
    background-color: rgba(60, 44, 30, 0.9);
    border-width: 1px;
    border-radius: 0;
    border-color: rgb(110, 84, 52);
    font-size: 15px;
}

.time-hud__button:hover {
    background-color: rgb(84, 62, 40);
}

.time-hud__button:active {
    background-color: rgb(110, 84, 52);
}

.time-hud__button--active,
.time-hud__button--active:hover {
    color: rgb(24, 17, 12);
    background-color: rgb(255, 140, 51);
    border-color: rgb(240, 226, 196);
}
```

- [ ] **Step 6: Create `TimeHudController`**

`Assets/Scripts/Game/TimeHudController.cs`:

```csharp
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Shows the world's date at the top of the screen, with the button that starts and
    /// stops the fast forward. Reads the world clock; knows nothing about the map.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TimeHudController : MonoBehaviour
    {
        private const string ActiveButtonClass = "time-hud__button--active";
        private const string FastForwardText = "Fast forward";
        private const string NormalSpeedText = "Normal speed";

        [SerializeField] private WorldClock worldClock;

        private GameClock clock;
        private Label dateLabel;
        private Button fastForwardButton;
        private bool isBound;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            if (root == null || worldClock == null)
            {
                Debug.LogError("TimeHudController needs a UIDocument with a source asset and a world clock.", this);
                return;
            }

            dateLabel = root.Q<Label>("date-label");
            fastForwardButton = root.Q<Button>("fast-forward-button");

            if (dateLabel == null || fastForwardButton == null)
            {
                Debug.LogError("TimeHud.uxml is missing an expected element.", this);
                return;
            }

            // Only the button should block the pointer, not the full-screen root.
            root.pickingMode = PickingMode.Ignore;

            // Covers the screen whatever else shares the panel.
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;

            if (!worldClock.IsReady)
            {
                // WorldClock already logged why time does not pass.
                root.style.display = DisplayStyle.None;
                return;
            }

            // The button is clicked, not navigated: the keyboard belongs to the map.
            fastForwardButton.focusable = false;

            clock = worldClock.Clock;
            fastForwardButton.clicked += OnFastForwardClicked;
            clock.DayStarted += ShowDate;
            clock.FastForwardChanged += ShowSpeed;
            isBound = true;

            ShowDate(clock.Date);
            ShowSpeed(clock.IsFastForward);
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            fastForwardButton.clicked -= OnFastForwardClicked;
            clock.DayStarted -= ShowDate;
            clock.FastForwardChanged -= ShowSpeed;
            isBound = false;
        }

        private void ShowDate(GameDate date)
        {
            dateLabel.text = worldClock.Calendar.Format(date);
        }

        private void ShowSpeed(bool isFastForward)
        {
            fastForwardButton.text = isFastForward ? NormalSpeedText : FastForwardText;
            fastForwardButton.EnableInClassList(ActiveButtonClass, isFastForward);
        }

        private void OnFastForwardClicked()
        {
            clock.SetFastForward(!clock.IsFastForward);
        }
    }
}
```

- [ ] **Step 7: Compile and run the whole suite**

Run all EditMode tests (no filter).
Expected: exit code `0`, `result="Passed"`. Then check that the import raised no error for the new UXML and USS:

```powershell
Select-String -Path Logs\test.log -Pattern 'TimeHud' | Select-Object -First 10
```

Expected: no line mentioning an error or a warning for `TimeHud.uxml` or `TimeHud.uss`.

- [ ] **Step 8: Commit**

```powershell
git add Assets/Scripts/Game/WorldClock.cs Assets/Scripts/Game/WorldClock.cs.meta Assets/Scripts/Game/TimeHudController.cs Assets/Scripts/Game/TimeHudController.cs.meta Assets/UI/WorldMap/TimeHud.uxml Assets/UI/WorldMap/TimeHud.uxml.meta Assets/UI/WorldMap/TimeHud.uss Assets/UI/WorldMap/TimeHud.uss.meta Assets/Scripts/Game/ShipsView.cs Assets/Scripts/Game/WorldMapCameraController.cs Assets/Scripts/Game/WorldMapInteraction.cs
git commit -m "Horloge du monde, affichage de la date et mode avance rapide" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Setup tool, scene, content test and documentation

**Files:**
- Modify: `Assets/Scripts/Editor/WorldMapSetup.cs`
- Create: `Assets/Tests/EditMode/CalendarContentTests.cs`
- Generated by the tool: `Assets/Data/Calendar/Calendar.asset`, changes to `Assets/Scenes/WorldMap.unity`
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: `CalendarDefinition` (a new instance is the game's calendar), `WorldClock` (serialized `calendar`), `TimeHudController` (serialized `worldClock`, needs a `UIDocument`), the serialized `worldClock` fields of `ShipsView`, `WorldMapInteraction` and `WorldMapCameraController`; in `WorldMapSetup`: `FindInScene<T>(Scene)`, `IsReferenceEmpty(Object, string)`, `SetReference(Object, string, Object)`, `ScenePath`, `PanelSettingsPath`, `UiFolder`.
- Produces: `Assets/Data/Calendar/Calendar.asset`; a `World Clock` object and a `Time HUD` object in `Assets/Scenes/WorldMap.unity`.

- [ ] **Step 1: Write the failing content test**

`Assets/Tests/EditMode/CalendarContentTests.cs`:

```csharp
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>Checks the calendar generated by the world map setup tool.</summary>
    public class CalendarContentTests
    {
        private const string CalendarPath = "Assets/Data/Calendar/Calendar.asset";

        [Test]
        public void Calendar_HasTwelveNamedMonthsAndPositiveValues()
        {
            var calendar = AssetDatabase.LoadAssetAtPath<CalendarDefinition>(CalendarPath);
            Assert.IsNotNull(calendar, CalendarPath);

            Assert.AreEqual(12, calendar.MonthsPerYear);
            Assert.Greater(calendar.DaysPerMonth, 0);
            Assert.Greater(calendar.SecondsPerDay, 0f);
            Assert.Greater(calendar.FastForwardMultiplier, 1f);

            for (int month = 1; month <= calendar.MonthsPerYear; month++)
            {
                // A month without a name falls back to "Month <n>".
                Assert.IsFalse(calendar.MonthName(month).StartsWith("Month "), $"month {month} has no name");
            }
        }

        [Test]
        public void Calendar_StartsTheGameOnTheFirstOfJanus932()
        {
            var calendar = AssetDatabase.LoadAssetAtPath<CalendarDefinition>(CalendarPath);
            Assert.IsNotNull(calendar, CalendarPath);

            GameClock clock = calendar.CreateClock();

            Assert.AreEqual("1 Janus 932", calendar.Format(clock.Date));
        }
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run with `-testFilter DarkFantasyMerchant.Tests.EditMode.CalendarContentTests`.
Expected: exit code `2`, both tests failed with `Assets/Data/Calendar/Calendar.asset` in the message (the asset does not exist yet).

- [ ] **Step 3: Extend the setup tool**

In `Assets/Scripts/Editor/WorldMapSetup.cs`:

**a.** Replace the class summary with:

```csharp
    /// <summary>
    /// Generates the world map scene, the city marker, ship and ship route prefabs and the
    /// sample content. Safe to run again: existing assets are left untouched, and an
    /// existing scene only gains the ships, ship route, world clock and time HUD objects
    /// when it has none, and the references to them that are empty.
    /// </summary>
```

**b.** After the `ScenePath` constant, add:

```csharp

        private const string CalendarFolder = "Assets/Data/Calendar";
        private const string CalendarPath = CalendarFolder + "/Calendar.asset";
        private const string TimeHudTemplatePath = UiFolder + "/TimeHud.uxml";
```

**c.** In `Build`, replace:

```csharp
            CreateShipRoutePrefab();

            AssetDatabase.SaveAssets();
            BuildScene();
            AddShipsToScene(mapSceneHadUnsavedChanges);
            AssetDatabase.SaveAssets();
```

with:

```csharp
            CreateShipRoutePrefab();
            CreateCalendar();

            AssetDatabase.SaveAssets();
            BuildScene();
            AddShipsToScene(mapSceneHadUnsavedChanges);
            AddTimeToScene(mapSceneHadUnsavedChanges);
            AssetDatabase.SaveAssets();
```

**d.** In `EnsureFolders`, replace:

```csharp
                ShipArtFolder, ShipDataFolder, ShipPrefabFolder, "Assets/Scenes",
```

with:

```csharp
                ShipArtFolder, ShipDataFolder, ShipPrefabFolder, CalendarFolder, "Assets/Scenes",
```

**e.** After the `CreatePanelSettings` method, add:

```csharp
        // A new calendar definition is the game's calendar: its defaults are the content.
        private static CalendarDefinition CreateCalendar()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CalendarDefinition>(CalendarPath);

            if (existing != null)
            {
                return existing;
            }

            var calendar = ScriptableObject.CreateInstance<CalendarDefinition>();
            AssetDatabase.CreateAsset(calendar, CalendarPath);
            return calendar;
        }
```

**f.** In `AddShipsToScene`, replace the opening of the scene:

```csharp
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
```

with:

```csharp
            if (!TryOpenMapScene("Ships", out Scene scene))
            {
                return;
            }

            var mapView = FindInScene<WorldMapView>(scene);
```

and replace its end:

```csharp
            if (!changed)
            {
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);

            // Saving would also write the user's own pending edits; leave that to them.
            if (sceneHadUnsavedChanges)
            {
                Debug.Log($"Ships or their route added to {ScenePath}. The scene had unsaved changes: save it to keep them.");
                return;
            }

            EditorSceneManager.SaveScene(scene);
        }
```

with:

```csharp
            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "Ships or their route");
            }
        }

        // Like the ships, the world clock and the time HUD are added to a scene built
        // before they existed.
        private static void AddTimeToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The world clock", out Scene scene))
            {
                return;
            }

            bool changed = false;
            var worldClock = FindInScene<WorldClock>(scene);

            if (worldClock == null)
            {
                // Loaded only now: opening a scene unloads unreferenced assets.
                var calendar = AssetDatabase.LoadAssetAtPath<CalendarDefinition>(CalendarPath);

                if (calendar == null)
                {
                    throw new FileNotFoundException("The calendar could not be loaded.", CalendarPath);
                }

                var clockObject = new GameObject("World Clock");
                SceneManager.MoveGameObjectToScene(clockObject, scene);

                worldClock = clockObject.AddComponent<WorldClock>();
                SetReference(worldClock, "calendar", calendar);
                changed = true;
            }

            if (FindInScene<TimeHudController>(scene) == null)
            {
                var hudTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TimeHudTemplatePath);

                if (hudTemplate == null)
                {
                    throw new FileNotFoundException("The time HUD UXML is missing.", TimeHudTemplatePath);
                }

                // The same panel as the city panel, so that the map sees the pointer over
                // the HUD's button as it does over the panel.
                var cityPanel = FindInScene<CityInfoPanelController>(scene);
                PanelSettings panelSettings = cityPanel != null
                    ? cityPanel.GetComponent<UIDocument>().panelSettings
                    : null;

                if (panelSettings == null)
                {
                    panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
                }

                if (panelSettings == null)
                {
                    throw new FileNotFoundException("The panel settings could not be loaded.", PanelSettingsPath);
                }

                var hudObject = new GameObject("Time HUD");
                SceneManager.MoveGameObjectToScene(hudObject, scene);

                var document = hudObject.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = hudTemplate;

                SetReference(hudObject.AddComponent<TimeHudController>(), "worldClock", worldClock);
                changed = true;
            }

            // Ships sail in the world's time; the map and its camera follow the fast forward.
            foreach (Component follower in new Component[]
            {
                FindInScene<ShipsView>(scene),
                FindInScene<WorldMapInteraction>(scene),
                FindInScene<WorldMapCameraController>(scene),
            })
            {
                if (follower != null && IsReferenceEmpty(follower, "worldClock"))
                {
                    SetReference(follower, "worldClock", worldClock);
                    changed = true;
                }
            }

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The world clock or the time HUD");
            }
        }

        /// <returns>False when the scene does not exist or could not be opened.</returns>
        private static bool TryOpenMapScene(string what, out Scene scene)
        {
            scene = default;

            // The scene is missing when its creation was cancelled.
            if (!File.Exists(ScenePath))
            {
                return false;
            }

            scene = SceneManager.GetSceneByPath(ScenePath);

            if (scene.isLoaded)
            {
                return true;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log($"{what} not added to the world map scene: the open scene has unsaved changes.");
                return false;
            }

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return true;
        }

        private static void SaveSceneChanges(Scene scene, bool sceneHadUnsavedChanges, string what)
        {
            EditorSceneManager.MarkSceneDirty(scene);

            // Saving would also write the user's own pending edits; leave that to them.
            if (sceneHadUnsavedChanges)
            {
                Debug.Log($"{what} added to {ScenePath}. The scene had unsaved changes: save it to keep them.");
                return;
            }

            EditorSceneManager.SaveScene(scene);
        }
```

- [ ] **Step 4: Run the setup tool**

Run the world map setup tool (see Commands).
Expected: exit code `0` and a `World map setup finished.` line; no `Exception`.

Then check what it changed:

```powershell
git status --short
Select-String -Path Assets\Scenes\WorldMap.unity -Pattern 'm_Name: (World Clock|Time HUD)'
Select-String -Path Assets\Scenes\WorldMap.unity -Pattern 'worldClock: \{fileID: 0\}'
```

Expected: `Assets/Data/Calendar/` (asset and `.meta` files) is new and `Assets/Scenes/WorldMap.unity` is modified, nothing else under `Assets/`; the scene has one `World Clock` and one `Time HUD` object; the last command prints **nothing** (every `worldClock` reference is set).

- [ ] **Step 5: Run the whole suite**

Run all EditMode tests (no filter).
Expected: exit code `0`, `result="Passed"`, `CalendarContentTests` included.

- [ ] **Step 6: Update `CLAUDE.md`**

**a.** In "Project state", replace the sentence that starts with `The world map is the first subsystem` and ends with `nothing else of the game exists yet.` by:

```markdown
The world map is the first subsystem, the player has one ship that sails on it over the navigable areas painted on the map and can lie in the port of a city, and the world has a date that passes, with a fast forward; nothing else of the game exists yet.
```

**b.** In the "World map" section, replace the bullet about `Tools > Dark Fantasy Merchant > Build World Map Scene` by:

```markdown
- `Tools > Dark Fantasy Merchant > Build World Map Scene` (`WorldMapSetup.Build`) recreates the scene, the marker, ship and ship route prefabs, the panel settings, the map definition, the ship definition or the calendar when one is missing, and leaves existing ones untouched. The exceptions are the `Ships` object with the `ShipRoute` object under it, the `World Clock` object and the `Time HUD` object: they are also added to an existing scene that has none, as are the empty references to them (`shipsView` of `WorldMapInteraction` and `CityInfoPanelController`, `routeView` of `ShipsView`, `worldClock` of `ShipsView`, `WorldMapInteraction` and `WorldMapCameraController`), and that scene is saved unless it already had unsaved changes. The tool offers to save the open scene first, and adds `WorldMap.unity` to the build list without removing other scenes. Sample cities are only recreated together with a missing map definition.
```

**c.** In the "Ships" section, replace `and advances the ships with `Time.deltaTime`.` by:

```markdown
and advances the ships with the world clock's `DeltaTime` (see Time), or with `Time.deltaTime` in a scene without a clock.
```

**d.** Insert this section between "Pathfinding" and "Conventions":

```markdown
### Time

- `GameClock` (Core) is the time of the simulated world: `Advance(real seconds)` gives `DeltaTime`, the simulated seconds of the frame, multiplied in fast forward, and raises `DayStarted` once per day that starts, even when one step crosses several. Everything simulated is advanced with that `DeltaTime`. `Time.timeScale` stays at 1, so the camera and the UI stay in real time; do not speed the game up through it.
- `GameDate` (Core) is a day of the calendar, derived from the days elapsed. `CalendarDefinition` (Game, `Assets/Data/Calendar/Calendar.asset`) holds the start year (932), the month names (12 invented ones; their number is the number of months), the days per month (30), the real seconds per day (30) and the fast forward multiplier (60), and formats a date.
- `WorldClock` owns the clock and advances it first in the frame (execution order -100). It is optional everywhere: `ShipsView`, `WorldMapInteraction` and `WorldMapCameraController` work as before without it.
- Fast forward is a mode in which the map is only watched. `WorldMapInteraction` clears hover and selection when it starts, allows none while it lasts, and ends it on `WorldMapInput.AnyInput`: any keyboard key, or a mouse button or wheel step on the map. It then calls `ConsumeInput`, so that the same input does nothing else, not even through the release of the button. The HUD's button is over the UI: it does not raise `AnyInput` and toggles the mode itself.
- `WorldMapCameraController` saves the view when fast forward starts, shows the whole map, ignores the input while it lasts and returns to the saved view afterwards, eased like any other move.
- `TimeHudController` shows the date and the button (`Assets/UI/WorldMap/TimeHud.uxml`), in a second `UIDocument` that uses the same `PanelSettings` as the city panel: one panel, so `CityInfoPanelController.IsPointerOverUi` sees the button too. The date label is rewritten when a day starts, not every frame.
- Keyboard keys are read from `Keyboard.current.allKeys`, not from an action: the "any key" control does not see a key pressed while another is held.
```

**e.** In "Project configuration", in the **Input** bullet, replace `the `Player` map is the template's action-game default and is unused.` by:

```markdown
the `Player` map is the template's action-game default and is unused. `WorldMapInput` also reads the keyboard's keys directly, only to tell that some key was pressed.
```

- [ ] **Step 7: Check the game by hand**

Open the project in the Unity Editor, open `Assets/Scenes/WorldMap.unity` and enter Play mode. Check each line; a failure is a bug to fix before committing.

1. The date `1 Janus 932` is at the top center, the `Fast forward` button under it. After 30 seconds the date reads `2 Janus 932`.
2. Zoom in on a city, select it, then click `Fast forward`: the panel closes, the map eases out to its full view, the button reads `Normal speed` and is highlighted, the date changes twice per second.
3. During fast forward, moving the pointer over cities and the ship shows no hover label and no highlight. Moving the mouse does not end the fast forward.
4. A ship ordered to a far point before fast forward visibly sails much faster during it, along the same route.
5. End the fast forward, each time from a fresh fast forward, with: a left click on the sea; a left click **on a city** (the panel must not open); a left click **on the ship** (it must not be selected); a right click; a middle click; a wheel step (the view must not zoom beyond the restored view); the `Escape` key; a letter key; the `Normal speed` button. Each time the speed returns to normal and the camera eases back to the view it had before.
6. Press and hold the left button on the sea during fast forward, move the pointer onto a city, release: nothing is selected and the map did not move.
7. Hold `W` (the map pans), click `Fast forward` while holding it, then press `D` without releasing `W`: the fast forward ends.
8. During fast forward, resize the Game view: the whole map stays visible. End the fast forward: the view is a valid one, inside the map.
9. With a ship in port and its city selected, start the fast forward: the panel closes; the ship stays in port.
10. Ordering a ship into a port, then fast forwarding: the ship enters the port and disappears from the map as usual.
11. The Console shows no error or warning from the game's scripts.

- [ ] **Step 8: Commit**

```powershell
git add Assets/Scripts/Editor/WorldMapSetup.cs Assets/Data/Calendar.meta Assets/Data/Calendar Assets/Scenes/WorldMap.unity Assets/Tests/EditMode/CalendarContentTests.cs Assets/Tests/EditMode/CalendarContentTests.cs.meta CLAUDE.md
git status --short
git commit -m "Le temps s'écoule : date affichée et avance rapide dans la scène" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

`git status --short` must show nothing left unstaged under `Assets/` before the commit.
