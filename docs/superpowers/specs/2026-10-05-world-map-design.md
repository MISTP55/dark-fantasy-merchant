# World Map — Design

Date: 2026-10-05
Status: awaiting review

## Purpose

The world map is the main screen of Dark Fantasy Merchant and the first real
subsystem of the project. This iteration delivers a map the player can pan and
zoom, with cities placed on it that can be hovered and selected to show an
information panel.

It also establishes the project's code layout (assemblies, folders, test
setup) that later systems will build on.

## Scope

In scope:

- Display the world map in its own scene.
- Pan and zoom with mouse and keyboard, clamped to the map edges.
- Cities defined as ScriptableObjects and shown as markers on the map.
- Hover and selection of cities, with a UI Toolkit information panel.
- An editor tool to place cities by dragging them on the map.
- Seven sample cities.

Out of scope:

- Sea and river routes, pathfinding, ships, travel.
- Economy, markets, factions, progression.
- Save and load.
- Pixel Perfect Camera and final pixel art.

## Constraints

- `Assets/WorldMap.jpg` (2048×1758, hand-drawn parchment style) is a
  **placeholder** used as a geographic reference. No code may depend on its
  resolution or style. Replacing it with an image of a different resolution
  must not move any city, provided the geographic framing is unchanged.
- Project conventions from `CLAUDE.md` apply: English code, UI Toolkit for
  runtime UI, content in ScriptableObjects holding static definitions only,
  simulation logic in plain C# covered by EditMode tests, MonoBehaviours as
  thin adapters, new Input System only.

## Approach

The map lives in world space. The map is a `SpriteRenderer`, city markers are
sprites instantiated from the definitions, and an orthographic camera handles
pan and zoom. Hover and click are resolved by a plain C# class that finds the
nearest city to the cursor in map coordinates; no colliders or physics are
involved. The information panel is a UI Toolkit overlay.

Rejected alternatives:

- **Everything in UI Toolkit.** Less code at first, but animated ships, 2D
  lights and effects would later have to be rebuilt in UI or force a
  migration.
- **World space with `Collider2D` and Physics2D raycasts.** Picking would
  depend on the scene and physics, so it could not be covered by EditMode
  tests.

## Code layout

| Assembly | Folder | Content |
|---|---|---|
| `DarkFantasyMerchant.Core` | `Assets/Scripts/Core` | Plain C#, no `MonoBehaviour`: projection, camera model, picking, selection state |
| `DarkFantasyMerchant.Game` | `Assets/Scripts/Game` | ScriptableObjects and MonoBehaviour adapters |
| `DarkFantasyMerchant.Editor` | `Assets/Scripts/Editor` | City placement tool (Editor only) |
| `DarkFantasyMerchant.Tests.EditMode` | `Assets/Tests/EditMode` | Tests for `Core` |

`Core` may use `UnityEngine` value types (`Vector2`, `Rect`, `Mathf`) but
nothing that requires a scene. `Game` references `Core`; `Editor` references
both; the test assembly references `Core`.

Other folders:

- `Assets/Data/Cities`, `Assets/Data/WorldMap` — content assets.
- `Assets/UI/WorldMap` — UXML and USS.
- `Assets/Art/WorldMap` — map image and marker sprites. `WorldMap.jpg` moves
  here together with its `.meta`.
- `Assets/Prefabs/WorldMap` — the city marker prefab.

## Data

ScriptableObjects hold static definitions only.

`CityDefinition`

| Field | Type | Notes |
|---|---|---|
| `displayName` | `string` | Shown on hover and in the panel |
| `description` | `string` | Multi-line |
| `mapPosition` | `Vector2` | Normalized, see Coordinates |
| `access` | `CityAccess` | `Coastal`, `River` |
| `size` | `CitySize` | `Village`, `Town`, `Capital` |

`WorldMapDefinition`

| Field | Type | Notes |
|---|---|---|
| `mapSprite` | `Sprite` | The map image |
| `worldWidth` | `float` | Width of the map in world units |
| `cities` | `List<CityDefinition>` | Cities shown on this map |
| `maxZoomInOrthographicSize` | `float` | Smallest orthographic size allowed |

There is no minimum-zoom field: the farthest zoom out is derived from the map
size and the screen aspect ratio.

## Coordinates

- `mapPosition` is normalized: `(0, 0)` is the bottom-left corner of the map
  and `(1, 1)` the top-right corner.
- The map's world height is `worldWidth` divided by the sprite's aspect ratio.
- The map is centered on the world origin.
- `MapProjection` (plain C#) converts between normalized and world
  coordinates. Every other class goes through it.

Import change: `WorldMap.jpg` switches from sprite mode `Multiple` to
`Single`. Its `pixelsPerUnit` is irrelevant because the displayed size comes
from `WorldMapDefinition.worldWidth`; `WorldMapView` scales the renderer to
match.

## Runtime state

`MapSelectionState` (plain C#) holds the hovered city and the selected city
and raises an event when either changes. Setting a value equal to the current
one raises nothing. Nothing is written back to the assets.

## Camera

`MapCameraModel` (plain C#) owns the camera's logical state: center position
and orthographic size. It is given the map's world rectangle, the viewport
aspect ratio and the zoom limit, and exposes operations for panning by a world
delta and zooming around a world point.

Rules:

- The view never extends beyond the map. Position is clamped after every
  operation.
- Maximum zoom out is the largest orthographic size at which the view still
  fits inside the map on both axes (the map fills the screen).
- Maximum zoom in is `maxZoomInOrthographicSize`.
- Zoom is anchored on the cursor: the world point under the cursor stays under
  the cursor, except where clamping to the edges prevents it.
- When the viewport aspect ratio changes, limits are recomputed and the state
  is re-clamped.

`WorldMapCameraController` (MonoBehaviour) reads input, calls the model and
applies its position and orthographic size to the `Camera`. It contains no
clamping or zoom math.

## Input

A new action map `WorldMap` is added to
`Assets/InputSystem_Actions.inputactions`. The template's `Player` map is left
untouched.

| Action | Binding | Use |
|---|---|---|
| `Point` | Mouse position | Cursor position |
| `Click` | Left mouse button | Select a city, start a drag |
| `PanDrag` | Middle mouse button | Alternative drag button |
| `PanMove` | WASD and arrow keys | Keyboard pan |
| `Zoom` | Mouse scroll | Zoom |
| `Cancel` | Escape | Clear selection |

A press that moves more than a few screen pixels before release is a pan and
does not select anything. A press released within that threshold is a click.

## Cities on the map

- `WorldMapView` (MonoBehaviour) shows the map sprite and instantiates one
  `CityMarker` prefab per city, positioned through `MapProjection`.
- `CityMarkerView` (MonoBehaviour, on the prefab) shows one of three
  placeholder sprites depending on `CitySize`, and has normal, hovered and
  selected visual states.
- Markers keep a constant on-screen size regardless of zoom.
- `CityPicker` (plain C#) returns the city nearest to a world point within a
  given world radius, or nothing. The caller converts a fixed screen-pixel
  radius to world units at the current zoom, so click tolerance does not
  change with zoom. Ties resolve to the first city in list order.
- `WorldMapInteraction` (MonoBehaviour) converts the cursor to world
  coordinates, asks `CityPicker`, and updates `MapSelectionState`:
  - Cursor over a city: that city is hovered; its marker is highlighted and
    its name is shown next to it.
  - Click on a city: it becomes selected.
  - Click on empty map, or `Cancel`: selection is cleared.

## Information panel

- `CityInfoPanel.uxml` and `CityInfoPanel.uss` in `Assets/UI/WorldMap`,
  hosted by a `UIDocument`.
- Anchored to the right edge of the screen, hidden when no city is selected.
- Shows the city's name, size, access type and description, and a close
  button that clears the selection.
- `CityInfoPanelController` (MonoBehaviour) subscribes to
  `MapSelectionState`. It has no reference to markers.
- Pointer input over the panel does not reach the map: no hover, selection,
  pan or zoom happens while the cursor is over it.
- The hover name label is also UI Toolkit, positioned from the hovered city's
  screen position.

## Editor placement tool

- With a `WorldMapDefinition` selected, or a `WorldMapView` present in the
  open scene, the Scene view draws a labeled, draggable handle for each city.
- Dragging a handle writes the new `mapPosition` to the `CityDefinition`
  asset, with Undo support and the asset marked dirty.
- This is content authoring at edit time, not simulation state, so it is
  consistent with the ScriptableObject rule.
- `CityDefinition`'s inspector shows a warning when `mapPosition` is outside
  `[0, 1]`.

## Scene and delivered content

- New scene `Assets/Scenes/WorldMap.unity`, first in the build settings.
  `SampleScene` is removed from the build but kept in the project.
- The scene contains: an orthographic camera with `WorldMapCameraController`,
  a `WorldMapView`, a `WorldMapInteraction`, and a `UIDocument` with
  `CityInfoPanelController`. A global `Light2D` lights the sprites.
- One `WorldMapDefinition` asset referencing the placeholder map.
- Seven sample `CityDefinition` assets, positioned approximately from the
  placeholder image and adjusted with the placement tool:

| City | Access | Size |
|---|---|---|
| Sparia | Coastal | Town |
| Elforth | Coastal | Village |
| Bactfied | Coastal | Town |
| Hitrun | Coastal | Village |
| Cerbias | Coastal | Village |
| Hazer Empire | Coastal | Capital |
| Liveria | River | Capital |

- Three placeholder marker sprites (village, town, capital).

## Error handling

- `WorldMapDefinition` without a sprite: an error is logged and nothing is
  displayed; no exception propagates.
- Null entry in `cities`: an error naming the index is logged and the entry is
  skipped; the other cities are shown.
- `mapPosition` outside `[0, 1]`: inspector warning; the city is still
  displayed at its projected position.

## Testing

EditMode tests in `DarkFantasyMerchant.Tests.EditMode`:

- `MapProjection`: normalized-to-world and back round-trips; corners and
  center; non-square aspect ratios.
- `MapCameraModel`: clamping at all four edges; zoom-in and zoom-out limits;
  cursor-anchored zoom keeps the anchor point fixed; re-clamping after an
  aspect ratio change; a viewport wider or taller than the map's ratio.
- `CityPicker`: nearest city wins; nothing returned outside the radius; tie
  resolves to list order; empty list.
- `MapSelectionState`: hover and selection transitions; one event per actual
  change; no event when the value is unchanged.

MonoBehaviours, the UI panel and the editor tool are verified manually in the
Editor: pan, zoom, edge clamping, hover, selection, panel content, input
blocking over the panel, and dragging a city handle.

## Documentation

`CLAUDE.md` is updated to describe the new code layout, the `WorldMap` scene
and action map, and the existence of the EditMode test assembly.
