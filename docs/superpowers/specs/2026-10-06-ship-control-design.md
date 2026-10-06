# Ship Control — Design

Date: 2026-10-06
Status: awaiting review

## Purpose

Give the player a ship to command on the world map. This is the first moving
entity of the game and the first piece of the trading loop: the player selects
the ship with a left click, then right-clicks the map to send it there. The
ship sails in a straight line at constant speed and shows the sprite matching
its heading.

It also establishes how a mutable game entity is split between a ScriptableObject
definition, a plain C# runtime object and a scene view, which later entities
will follow.

## Scope

In scope:

- One player ship shown on the world map, using `Assets/Art/Ships/MerchantShip.png`.
- Hover and selection of the ship with the left mouse button.
- A move order with the right mouse button while the ship is selected.
- Straight-line movement at constant speed, with an 8-direction sprite.
- Setup tooling so the ship exists in the `WorldMap` scene.

Out of scope:

- Land avoidance, sea/land data, pathfinding, river routes.
- Destination marker, route line, ship information panel.
- Docking in a city, cargo, trade.
- Game clock, pause, time acceleration.
- Several ships, fleets, multi-selection.
- Save and load.

## Decisions

Agreed with the user before writing this spec:

| Topic | Decision |
|---|---|
| Land | Ignored. The ship sails straight to the clicked point, across land if needed. |
| Selection | Exclusive. At most one thing is selected: a city or the ship. |
| On-screen size | Constant in screen pixels at every zoom level, like city markers. |
| Start position | On a start city; by default the first city of the map. |

## Constraints

- Conventions from `CLAUDE.md` apply: English code, static definitions in
  ScriptableObjects, mutable state in runtime objects, simulation logic in
  plain C# in `Core` covered by EditMode tests, MonoBehaviours as thin
  adapters, new Input System only, `WorldMapInput` as the single reader of the
  `WorldMap` action map.
- Map positions stay **normalized** and go to world space only through
  `MapProjection`.
- `MerchantShip.png` is already sliced into eight 32×32 sprites named
  `MerchantShip_N`, `_NE`, `_E`, `_SE`, `_S`, `_SW`, `_W`, `_NW`, at 16 pixels
  per unit with point filtering. Its import settings are not changed.

## Approach

The ship's behaviour is a plain C# class in `Core`. A second, ship-typed
`MapSelectionState` holds ship hover and selection, and `WorldMapInteraction`,
which already turns the cursor into city hover and selection, arbitrates
between the two so that only one thing is hovered or selected at a time.

Rejected alternatives:

- **One `MapSelectionState<object>` shared by cities and ships.** Exclusivity
  would come for free, but the city panel and the markers would have to
  type-test every event.
- **Movement written in a MonoBehaviour.** Shorter, but untestable in EditMode
  and against the `Core`/`Game` split.

## Core

### `CompassDirection`

Enum `N, NE, E, SE, S, SW, W, NW`, in that order (values 0 to 7), which is the
order of the sprite sheet and of the sprite array in `ShipDefinition`.

### `CompassHeading`

Static class with one function:

```csharp
public static CompassDirection FromVector(Vector2 direction, CompassDirection fallback)
```

- Returns the direction whose 45° sector contains `direction`. `(0, 1)` is
  `N`, `(1, 0)` is `E`; sectors are centered on the eight directions.
- Returns `fallback` for a zero vector or one with a NaN component.
- A vector exactly on a sector boundary (22.5° from two directions) may
  resolve to either neighbour; nothing relies on it.

### `Ship`

Runtime state of one ship. No Unity object, no scene dependency.

```csharp
public sealed class Ship
{
    public Ship(MapProjection projection, Vector2 position, float speed);

    public Vector2 Position { get; }          // normalized
    public Vector2 WorldPosition { get; }     // through the projection
    public Vector2? Destination { get; }      // normalized, null when idle
    public bool IsMoving { get; }
    public CompassDirection Heading { get; }

    public void SetDestination(Vector2 destination);
    public void Advance(float deltaTime);
}
```

- `speed` is in **world units per second** and must be finite and positive;
  the constructor throws `ArgumentOutOfRangeException` otherwise, and
  `ArgumentNullException` for a null projection.
- `Position` is stored normalized, like city positions. The step is computed
  in world space through the projection: on a map that is not square, a step
  measured in normalized space would be faster along one axis than the other.
- The initial heading is `S`, the sprite facing the viewer.
- `SetDestination` clamps the point to the map (`0..1` on both axes) and
  replaces any order in progress. The heading turns towards the new
  destination at once. A destination equal to the current position leaves the
  ship idle and its heading unchanged.
- `Advance` moves the ship `speed × deltaTime` world units towards the
  destination. If that reaches or passes the destination, the ship is placed
  exactly on it and becomes idle. The heading is kept on arrival.
- `Advance` does nothing when idle, or when `deltaTime` is zero, negative or
  NaN.

## Game

### `ShipDefinition` (ScriptableObject)

Static definition of a kind of ship. Menu: `Dark Fantasy Merchant/Ship`.

- `displayName`
- `speed`, world units per second, minimum above zero.
- `directionSprites`, eight sprites indexed by `CompassDirection`.
- `SpriteFor(CompassDirection)` returns the matching sprite, or null when the
  array is incomplete.

Asset: `Assets/Data/Ships/MerchantShip.asset`.

### `ShipView` (MonoBehaviour)

One ship on the map; the counterpart of `CityMarkerView`.

- `Initialize(Ship, ShipDefinition)`.
- `Refresh()` puts the transform on `Ship.WorldPosition` and shows the sprite
  of `Ship.Heading`.
- `SetHovered`, `SetSelected`, `SetBaseScale`, with the same tint and scale
  treatment as `CityMarkerView`.
- Its `SpriteRenderer` sorts above city markers (order 20; markers use 10).

Prefab: `Assets/Prefabs/Ships/Ship.prefab`.

### `ShipsView` (MonoBehaviour)

Owns the ships of the scene, their views and their selection state; the
counterpart of `WorldMapView` for ships.

- Serialized: `WorldMapView mapView`, `ShipView shipPrefab`,
  `ShipDefinition playerShipDefinition`, `CityDefinition startCity`,
  `shipScreenPixelsPerUnit` (default 32, so the 32-pixel sprite is shown at
  twice its size).
- In `Start`, once the map is laid out, creates the player ship on
  `startCity`. With no start city, it uses the first city of
  `WorldMapView.Cities`; with no city at all, the map center, with a warning.
- `Selection` is a `MapSelectionState<Ship>`; hover and selection changes are
  forwarded to the views.
- `Ships` and `ShipWorldPositions` are parallel lists, as `Cities` and
  `CityWorldPositions` are, so `CityPicker.PickNearest` is reused unchanged.
- `Update` advances every ship by `Time.deltaTime`, then refreshes views and
  world positions.
- `SetShipScale(worldUnitsPerPixel)` keeps ships at a constant on-screen size.
- `IsReady` is false until the ship exists, or if setup failed (missing
  reference, map not laid out); failures are logged once and disable the
  component, as the other map components do.

Ships are held in a list although only one is created: picking already works
on lists, and it costs nothing.

### `WorldMapInput`

- New action **`Command`** in the `WorldMap` action map: a button bound to
  `<Mouse>/rightButton` in the `Keyboard&Mouse` group.
- New event `Commanded(Vector2 screenPosition)`.
- It follows the left-click rules: the press must not start over the UI, and a
  press that moves past `dragThresholdPixels` before release is not a command.
- Unlike the other two buttons, moving the pointer with the right button held
  does **not** raise `Dragged`, so it never pans the map.
- `IsDragging` is unchanged: only the left and middle buttons count.

### `WorldMapInteraction`

Gains a `ShipsView` reference and a `shipPickRadiusPixels` field (default 24).

- **Hover**: the ship is picked first. If one is under the cursor it is
  hovered and no city is; otherwise cities are picked as today.
- **Left click**: on a ship, selects it and clears the city selection. On a
  city, selects it and clears the ship selection. On neither, clears both.
- **Cancel** (Escape): clears both.
- **Right click**: with a ship selected, converts the screen position to world
  then normalized coordinates and calls `SetDestination`. With no ship
  selected, nothing happens.
- `LateUpdate` also calls `ShipsView.SetShipScale`.
- If `ShipsView` is missing or not ready, city interaction keeps working.

Picking uses the ship's current world position and a screen-pixel radius, so
it follows the ship while it moves and behaves the same at every zoom level.

## Behaviour summary

- The ship keeps sailing after it is deselected.
- A right click during a trip redirects the ship at once.
- A right click outside the map sends the ship to the nearest point of the
  map edge.
- Selecting the ship closes the city panel, since the city is deselected.
- The ship has priority over a city it overlaps, for hover and for clicks.

## Tooling

`Tools > Dark Fantasy Merchant > Build World Map Scene` keeps its rule: create
what is missing, leave what exists untouched.

- Creates `Assets/Data/Ships/MerchantShip.asset` when missing, with the eight
  sprites assigned by name and a default speed.
- Creates `Assets/Prefabs/Ships/Ship.prefab` when missing.
- When the `WorldMap` scene has no `ShipsView`, adds a `Ships` object with its
  references, and sets the `ShipsView` reference of `WorldMapInteraction` if
  it is empty. This applies both to a newly built scene and to the existing
  one.

The `Command` action is added to `Assets/InputSystem_Actions.inputactions`.

## Testing

EditMode tests in `Assets/Tests/EditMode`:

- `CompassHeadingTests`: the eight exact directions; vectors just inside each
  side of every sector boundary; magnitude does not matter; zero and NaN
  vectors return the fallback.
- `ShipTests`:
  - invalid speed or null projection throws;
  - idle ship does not move;
  - one step covers `speed × deltaTime` world units;
  - the same speed horizontally, vertically and diagonally on a non-square map;
  - no overshoot, exact arrival, idle afterwards;
  - heading matches the direction of travel and is kept on arrival;
  - a new destination mid-trip redirects and updates the heading;
  - destinations outside the map are clamped;
  - zero, negative and NaN `deltaTime` are ignored.

`MapSelectionState` and `CityPicker` are already covered and are not changed.

Checked in the Editor (Play mode, through `unity-mcp` when the Editor is
open): the ship appears on the start city, tints on hover, selects on left
click and deselects the city; right click sends it off with the right sprite
for each of the eight directions; it stops on the point; its on-screen size
holds while zooming; right-dragging does not pan; clicks over the panel are
ignored.

## Documentation

`CLAUDE.md` gets a ship section under Architecture, the `Command` action in
the input description, and the updated behaviour of the setup tool.
