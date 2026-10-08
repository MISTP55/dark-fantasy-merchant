# World Time — Design

Date: 2026-10-07
Status: awaiting review

## Purpose

Give the world a date that passes. This is the first piece of the world
simulation: everything that comes later (economy, prices, progression) will
be driven by the same clock and by its "a new day started" signal.

The player sees the date at the top of the screen and can fast forward the
world, in a mode where the map is only watched, not played.

## Scope

In scope:

- A simulation clock in `Core`, with two speeds, and a calendar date derived
  from it.
- A calendar defined in a ScriptableObject asset.
- Ships that sail in simulation time.
- The date and a fast forward button on screen.
- The fast forward mode: map fully zoomed out, camera locked, nothing hovered
  or selected, any player input going back to normal speed.

Out of scope:

- Pause, and any speed other than x1 and x60.
- A keyboard shortcut that starts fast forward.
- Anything that happens on a new day: the event exists, nothing listens yet.
- Seasons, weekdays, hours of the day, day and night.
- Saving and loading the date.
- Localization of the month names and of the UI text.

## Decisions

Agreed with the user before writing this spec:

| Topic | Decision |
|---|---|
| Start date | Day 1 of the first month of year 932. |
| Pace | One day every 30 real seconds at x1; fast forward is x60 (one day every half second). |
| Calendar | 12 months of 30 days, a year of 360 days. Month names are invented but resemble the Gregorian ones. |
| Ships | Sail 60 times faster in fast forward: the world's time speeds up, not only the date. |
| Camera in fast forward | Zooms out fully and is locked. Back at normal speed, it returns to the view it had before. |
| Leaving fast forward | Any player input: a mouse button (left, right, middle), the mouse wheel, any keyboard key, or the button itself. Moving the mouse is not an input. |
| The input that leaves fast forward | Does nothing else: a click on a city selects nothing, a right click gives no order, a wheel step does not zoom. One exception, added after play testing: a left or middle button that stays held can drag the map, as at normal speed. |
| Arrival of the awaited ship | Added after play testing: when a ship was selected and under way as fast forward started, fast forward ends by itself when that ship arrives, at its destination or in a port. |
| UI text | English, like the city panel: `1 Janus 932`, button `Fast forward` / `Normal speed`. |

Month names, in the calendar asset and editable there: Janus, Febrin, Martis,
Aprilis, Maius, Junis, Julis, Augustis, Septem, Octem, Novem, Decem.

## Approach

The simulation has its own clock; `Time.timeScale` stays at 1.

A `timeScale` of 60 would speed up everything that reads `Time.deltaTime`,
present and future (animations, effects, UI transitions), and cannot be
tested without a scene. With a clock in `Core`, the camera and the UI stay in
real time, only what asks the clock for its time speeds up, and the pace is
covered by EditMode tests.

## Core

### `GameDate`

An immutable value: `Year`, `Month` (1 to 12), `Day` (1 to days per month).
Built from a number of whole days elapsed since the start of the game, a
start year, the number of months per year and the number of days per month.
It holds no month name: names are content.

Equality is by value, so that the clock can tell when the date changed.

### `GameClock`

Constructed with the seconds per day, the fast forward multiplier, the start
year, months per year and days per month. Invalid values (not positive, NaN,
infinite) throw.

- `Advance(realDeltaSeconds)`: sets `DeltaTime` to the simulated seconds of
  this step (the real ones, times the multiplier when fast forwarding) and
  adds them to the elapsed simulated time. A negative, NaN or infinite delta
  is treated as zero.
- `DeltaTime`: simulated seconds of the last `Advance`. This is what the
  simulation is advanced with.
- `Date`: the current `GameDate`. `ElapsedDays`: whole days since the start.
- `DayStarted`: raised once per day that starts, in order, with its date. A
  step that crosses several days raises it for each one.
- `IsFastForward`, `SetFastForward(bool)`, and `FastForwardChanged` raised
  only when the value actually changes.

Elapsed time is kept as a `double` of simulated seconds, so that the date
stays exact over a long game.

### `MapCameraModel`

Two additions, both clamped like everything else in the model:

- `ZoomOutFully()`: sets the largest orthographic size and centers the view
  as the clamp requires.
- `SetView(position, orthographicSize)`: goes to a given view, clamped to the
  size limits and to the map. Used to return to the view saved before fast
  forward; if the window was resized meanwhile, the view is the nearest valid
  one.

## Game

### `CalendarDefinition`

A ScriptableObject, in `Assets/Data`: start year (932), month names (12),
days per month (30), real seconds per day (30), fast forward multiplier (60).
The number of months is the number of names. It formats a `GameDate` as
`<day> <month name> <year>`.

### `WorldClock`

A MonoBehaviour that owns the `GameClock`, built from the
`CalendarDefinition` it references, and advances it once per frame with
`Time.deltaTime`, before the other components of the map update (execution
order). It exposes the clock and the definition. Without a valid definition it
logs an error and disables itself, like the other views of the scene.

### `ShipsView`

Advances its ships with the clock's `DeltaTime` instead of `Time.deltaTime`.
The reference to the `WorldClock` is optional, like the route view: without
it, ships keep sailing in real time.

`Ship.Advance` already spends what is left of a step on the next legs, so a
step 60 times longer follows the route exactly.

### `WorldMapInput`

Raises a new event, `AnyInput`, when a mouse button or a keyboard key is
pressed or the wheel is turned. A mouse button pressed or a wheel turned over
the UI does not raise it: the fast forward button must be able to turn the
mode off, and on, by itself.

It gets a way to drop the gestures in progress (`CancelGestures`, made
public), so that the press that leaves fast forward is not followed by a
click when the button is released.

The inputs are read through the `WorldMap` action map, of which
`WorldMapInput` stays the only reader: a new action bound to any key, the
mouse buttons and the wheel.

### `WorldMapInteraction`

It is the one that decides what fast forward means for the map. It gets an
optional reference to the `WorldClock`; without it, nothing changes.

- When fast forward starts: both selections and both hovers are cleared.
- While it lasts: no hover is computed, clicks select nothing, right clicks
  give no order, Escape clears nothing.
- On `AnyInput` while it lasts: the clock goes back to normal speed and the
  gestures in progress are cancelled.

The order of events within a frame must not let the same input both leave
fast forward and act: `WorldMapInput` raises `AnyInput` before the click,
command, zoom and cancel events of the same frame, and
`WorldMapInteraction` ignores those for the rest of that frame.

### `WorldMapCameraController`

It gets an optional reference to the `WorldClock`.

- When fast forward starts: it saves the model's position and size, then
  calls `ZoomOutFully()`.
- While it lasts: drags, wheel steps and keyboard pans are ignored.
- When it ends: `SetView` with the saved view. The wheel step or the key that
  ended it is not applied in that frame.

Both transitions are eased by the existing smoother. A key that is still
held once back at normal speed pans as usual.

### `TimeHudController`

A MonoBehaviour on its own `UIDocument`, which uses the same `PanelSettings`
as the city panel: both documents are then in one panel, and the existing
`IsPointerOverUi` already sees the new button.

- `Assets/UI/WorldMap/TimeHud.uxml` and `.uss`: a container at the top
  center of the screen, with the date label and, under it, the button. The
  root ignores the pointer; only the button picks it.
- The label is rewritten when a day starts, not every frame.
- The button toggles fast forward. Its text and a USS class follow
  `FastForwardChanged`. It is not focusable: the keyboard belongs to the map.

## Editor

`WorldMapSetup.Build` creates the calendar asset, the time HUD and the
`WorldClock` object when one is missing, and leaves existing ones untouched.
Like the `Ships` object, the `WorldClock` object and the HUD are also added
to an existing scene that has none, with the empty references to the clock of
`ShipsView`, `WorldMapInteraction` and `WorldMapCameraController`.

## Edge cases

- **A long frame**: `Time.deltaTime` is capped by Unity's maximum delta time,
  so a hitch in fast forward moves the world by 20 simulated seconds at most.
- **Several days in one step**: counted and announced one by one.
- **A ship that enters a port during fast forward**: it docks as usual.
  Nothing is selected, so no panel is concerned.
- **The window is resized during fast forward**: the model stays fully zoomed
  out for the new aspect; the saved view is clamped when it is restored.
- **The window loses focus**: fast forward goes on. Coming back with a click
  ends it, like any click.
- **Fast forward toggled twice in a frame**: `FastForwardChanged` is raised
  for each change; the camera saves and restores the same view.

## Testing

EditMode tests, in `Assets/Tests/EditMode`:

- `GameDateTests`: first day, last day of a month, first day of the next,
  last day of a year, first day of the next year.
- `GameClockTests`: a day lasts the configured seconds; fast forward
  multiplies `DeltaTime` and the pace; several days in one step each raise
  `DayStarted`, in order; no event without a day change; invalid deltas are
  ignored; `FastForwardChanged` only on a real change; invalid constructor
  arguments throw.
- `MapCameraModelTests`: `ZoomOutFully` and `SetView`, including a view
  outside the limits.
- `CalendarContentTests`: the calendar asset has 12 non-empty month names and
  positive values, and formats the start date as `1 Janus 932`.
- `WorldMapInputActionsTests`: the new action exists in the `WorldMap` map.

The scene wiring, the HUD and the fast forward mode are checked by hand in
Play mode: the date advances every 30 seconds, the button speeds it up, the
map zooms out and locks, and every kind of input brings back the previous
view without selecting or ordering anything.

## Documentation

`CLAUDE.md` gets a **Time** section, and its project state and input
paragraphs are updated.
