# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## The game

Dark Fantasy Merchant is a maritime trade management and simulation game. The player is a merchant in a dark fantasy world made of coastal cities that trade with each other by sea and by river. The goals are to build a maritime trading empire and to rise through the social hierarchy.

- **Presentation**: 2D pixel art.
- **Platform**: PC only (keyboard and mouse).

## Project state

Unity **6000.6.4f1** project, created from the URP 3D template and then converted in place to 2D. The world map is the first subsystem, the player has one ship that sails on it over the navigable areas painted on the map; nothing else of the game exists yet. `TutorialInfo/` and `SampleScene` are template leftovers and not part of the game. Update this file as the architecture grows.

## Architecture

Code is split into assemblies, each with its own `.asmdef`:

| Assembly | Folder | Role |
|---|---|---|
| `DarkFantasyMerchant.Core` | `Assets/Scripts/Core` | Plain C# logic and runtime state. No `MonoBehaviour`, nothing that needs a scene. |
| `DarkFantasyMerchant.Game` | `Assets/Scripts/Game` | ScriptableObject definitions and thin MonoBehaviour adapters over `Core`. |
| `DarkFantasyMerchant.Editor` | `Assets/Scripts/Editor` | Editor-only tools. |
| `DarkFantasyMerchant.Tests.EditMode` | `Assets/Tests/EditMode` | EditMode tests for `Core`. |

Content lives in `Assets/Data`, runtime UI in `Assets/UI`, art in `Assets/Art`, prefabs in `Assets/Prefabs`.

### World map

- Positions on the map are **normalized** (`(0,0)` bottom-left, `(1,1)` top-right) and converted to world space only through `MapProjection`. The map is centered on the world origin and sized by `WorldMapDefinition.worldWidth`, so the map image can be replaced by one of another resolution without moving anything.
- `Assets/Art/WorldMap/WorldMap.jpg` is a placeholder for future pixel art.
- `WorldMapView` owns the scene's `MapSelectionState<CityDefinition>` (hovered and selected city). Markers and the UI panel both react to it and do not know about each other.
- `WorldMapInput` is the only reader of the `WorldMap` action map; it raises click, drag and zoom events and ignores pointer input that starts over the UI.
- `MapCameraModel` is where input has sent the camera (the target); `MapCameraSmoother` is what is displayed, eased towards the target. Position and size share one easing factor on purpose: that keeps the zoom anchor fixed during the transition, so do not split it into separate pan and zoom settings. Hover, picking and marker size follow the displayed view.
- Camera clamping and zoom math are in `MapCameraModel`; picking is `CityPicker` (nearest city within a screen-pixel radius, no colliders).
- Cities are placed by dragging their handles in the Scene view (`CityPlacementTool`). It and the navigation mask tool find the map to work on through `WorldMapEditorContext`.
- `Tools > Dark Fantasy Merchant > Build World Map Scene` (`WorldMapSetup.Build`) recreates the scene, the marker and ship prefabs, the panel settings, the map definition or the ship definition when one is missing, and leaves existing ones untouched. The one exception is the `Ships` object: it is also added to an existing scene that has none, and that scene is saved unless it already had unsaved changes. The tool offers to save the open scene first, and adds `WorldMap.unity` to the build list without removing other scenes. Sample cities are only recreated together with a missing map definition.

### Ships

- `Ship` (Core) is the runtime state of one ship: normalized position, destination, heading. `ShipDefinition` (Game) is its static definition: name, speed in world units per second, and eight sprites indexed by `CompassDirection` (`N, NE, E, SE, S, SW, W, NW`, the order of the sprite sheets).
- A ship follows a route: a list of waypoints, exposed as `RemainingWaypoints`, with `Destination` the last one. `SetDestination` asks the ship's `NavigationPathfinder` for the route, so the ship sails over water only; without a pathfinder (a map with no mask) the route is one straight leg. What is left of a step after a waypoint is spent on the next leg, so the speed is constant through turns.
- Steps are measured in world space through `MapProjection`, not in normalized space, so the speed is the same in every direction on a map that is not square.
- A ship is created on the water nearest to the position it is given: cities are on land, so the player ship starts beside its start city, not on it.
- `ShipsView` owns the ships, their `ShipView`s and a `MapSelectionState<Ship>`, and advances the ships with `Time.deltaTime`. It holds a list although there is a single player ship, which starts on `startCity` or the first city of the map.
- `WorldMapInteraction` arbitrates between the two selection states: a ship is picked before the cities, and selecting one clears the other, so at most one thing is hovered and one selected. A right click sends the selected ship to the clicked point, or to the nearest water it can reach when that point is on land or in another sea.
- Ships keep a constant on-screen size, like city markers, and are picked with `CityPicker` on their current world positions.

### Navigation mask

- `NavigationGrid` (Core) says which cells of the map ships can sail on: one bit per cell, cell `(0,0)` bottom-left like normalized positions, queried by cell or by normalized point. It also holds the painting logic (disc, stroke, 4-connected fill, resample). `NavigationMaskDefinition` (Game) stores a grid's size and bits and is referenced by `WorldMapDefinition.navigationMask`; a map without a mask is valid.
- `ShipsView` builds one `NavigationPathfinder` from the mask at startup and gives it to its ships. The mask is not expected to change while the game runs.
- The grid is defined over normalized space, 1024 cells wide by default, with a height that follows the map's aspect ratio so cells are square. Replacing the map image by one of another shape needs a **Resize** in the tool, not a repaint.
- The mask is painted in the Scene view (2D mode) with the **Navigation Mask** tool of the Scene view toolbar (`NavigationMaskTool`, settings in the `NavigationMaskOverlay` panel): brush, eraser, fill bucket, and a detection that marks low-saturation pixels of the map image as water (`WaterColorClassifier`). Shift swaps brush and eraser; `[` and `]` resize the brush. While this tool is active, `CityPlacementTool` draws no handles.
- `NavigationMaskSession` is the tool's working copy of the grid. It is written to the asset once per stroke through `NavigationMaskAuthoring.Apply`, which is what makes a stroke one undo step, and rebuilt from the asset after an undo or redo, or at the start of an edit when the asset was changed by something else (`SyncWithAsset`). Its preview texture is rewritten only where a stroke passed; do not go back to rebuilding it whole on every drag event.
- The mask's fields are hidden in the Inspector on purpose: its size and bits only make sense together.

### Pathfinding

- Movement rules, shared by the search and by route smoothing: a ship moves to a side neighbour, or to a diagonal one only when both cells beside the move are navigable; a straight leg is allowed only when every cell it touches, even by a corner, is navigable (`NavigationLineOfSight`, with a margin of a thousandth of a cell because positions are floats). A diagonal coastline one cell thick therefore holds, as it does for the fill bucket, and two cells are reachable from one another exactly when they are in the same 4-connected region.
- `NavigationCellSearch` is an A* over the cells; its buffers are allocated once and reused, so an order allocates nothing. `NavigationPathfinder` labels the regions, picks the start and goal cells, runs the search and drops the waypoints a straight leg can skip.
- An order ends on the clicked point itself when it is on water the ship can reach, otherwise on the center of the nearest cell of the ship's own region.
- A search runs synchronously on the click. About 16 bytes per cell are held for the regions and the search, about 14 MB for a 1024-cell-wide grid.


## Conventions

- **Language**: all code is in English — identifiers, comments, log messages, asset and folder names. Conversation with the user is in French.
- **UI**: UI Toolkit (UXML + USS) for all runtime UI. Do not build uGUI Canvas hierarchies, even though `com.unity.ugui` is installed.
- **Data**: game content (goods, cities, ships, prices, ranks, etc.) is defined in ScriptableObject assets, not hard-coded. ScriptableObjects hold static definitions only; mutable simulation state lives in separate runtime objects, never written back into the assets.
- **Testing**: keep simulation logic (economy, trade, progression) in plain C# classes with no `MonoBehaviour` or scene dependency, and cover it with EditMode tests. MonoBehaviours are thin adapters that drive the simulation and present its state.

## Commands

There is no CLI build or lint step; compilation happens in the Unity Editor. The `.sln` / `.csproj` files at the root are generated by Unity and git-ignored — never edit them by hand.

Editor path: `C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe`

Tests use the Unity Test Framework (`com.unity.test-framework`); EditMode tests are in `Assets/Tests/EditMode`. Batch-mode runs require the project to be closed in the Editor. `Unity.exe` is a GUI executable, so `& $unity ...` returns before the run ends; wrap it in `Start-Process -Wait -PassThru` and read `ExitCode` (`0` passed, `2` test failures, `1` compile error) when the result matters:

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe"

# All EditMode tests (use -testPlatform PlayMode for PlayMode tests)
& $unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs\TestResults.xml -logFile Logs\test.log

# Single test, fixture, or namespace
& $unity -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter "Namespace.ClassName.MethodName" -testResults Logs\TestResults.xml -logFile Logs\test.log
```

When the Editor is open, prefer the `unity-mcp` MCP tools (`Unity_RunCommand`, `Unity_GetConsoleLogs`, scene/camera captures) to compile, check console errors, and inspect the scene.

## Project configuration

- **Rendering**: URP 17.6 with the **2D Renderer**, linear color space. A single quality level, `PC`, uses `Assets/Settings/PC_RPAsset` → `Renderer2D`. Sprites are lit by 2D lights (`Light2D`); 3D lights and 3D-only URP features (SSAO, GPU Resident Drawer, shadow cascades) do not apply.
- **2D**: the Editor's default behavior mode is 2D (textures import as sprites). The `com.unity.feature.2d` feature set is installed (tilemaps, sprite tooling, Aseprite/PSD importers, 2D animation). No Pixel Perfect Camera is set up yet.
- **Input**: the new Input System is the only active handler — the legacy `UnityEngine.Input` API will throw. `Assets/InputSystem_Actions.inputactions` is registered as the project-wide actions asset. The game uses its `WorldMap` action map (Point, Click, PanDrag, PanMove, Zoom, Cancel, Command — the right mouse button, which gives orders and never pans); the `Player` map is the template's action-game default and is unused.

- **Build scenes**: only `Assets/Scenes/WorldMap.unity`. Its sprites are lit by a global `Light2D`.


## Unity conventions

- Every asset has a sibling `.meta` file holding its GUID. Create, move, rename, and delete assets together with their `.meta`; otherwise references in scenes and prefabs break.
- `Library/`, `Temp/`, `Logs/`, and `UserSettings/` are generated and git-ignored. Exclude `Library/` from searches — it contains the package cache (tens of thousands of files).
- Packages are declared in `Packages/manifest.json`; package sources live read-only under `Library/PackageCache/`.
