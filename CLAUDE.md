# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## The game

Dark Fantasy Merchant is a maritime trade management and simulation game. The player is a merchant in a dark fantasy world made of coastal cities that trade with each other by sea and by river. The goals are to build a maritime trading empire and to rise through the social hierarchy.

- **Presentation**: 2D pixel art.
- **Platform**: PC only (keyboard and mouse).

## Project state

Unity **6000.6.4f1** project, created from the URP 3D template and then converted in place to 2D. The world map is the first subsystem, the player has one ship that sails on it over the navigable areas painted on the map and can lie in the port of a city, the ship has a crew of sailors that sets its speed, the world has a date that passes, with a fast forward, and the player has a treasury of gold; nothing else of the game exists yet. `TutorialInfo/` and `SampleScene` are template leftovers and not part of the game. Update this file as the architecture grows.

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
- The panel of the selected city is centered on the screen (by its root, in `CityInfoPanel.uss`), over the map and possibly over the city itself: the top right corner belongs to the treasury.
- `WorldMapInput` is the only reader of the `WorldMap` action map; it raises click, drag and zoom events and ignores pointer input that starts over the UI.
- `MapCameraModel` is where input has sent the camera (the target); `MapCameraSmoother` is what is displayed, eased towards the target. Position and size share one easing factor on purpose: that keeps the zoom anchor fixed during the transition, so do not split it into separate pan and zoom settings. Hover, picking and marker size follow the displayed view.
- Camera clamping and zoom math are in `MapCameraModel`; picking is `CityPicker` (nearest city within a screen-pixel radius, no colliders).
- Cities are placed by dragging their handles in the Scene view (`CityPlacementTool`). It and the navigation mask tool find the map to work on through `WorldMapEditorContext`.
- `Tools > Dark Fantasy Merchant > Build World Map Scene` (`WorldMapSetup.Build`) recreates the scene, the marker, ship and ship route prefabs, the panel settings, the map definition, the ship definition, the calendar or the player start when one is missing, and leaves existing ones untouched. The exceptions are the `Ships` object with the `ShipRoute` object under it, the `Ship Panel`, `World Clock`, `Time HUD`, `Player Treasury` and `Treasury HUD` objects: they are also added to an existing scene that has none, as are the empty references to them (`shipsView` of `WorldMapInteraction`, `CityInfoPanelController` and `ShipInfoPanelController`, `routeView` and `playerStart` of `ShipsView`, `worldClock` of `ShipsView`, `WorldMapInteraction` and `WorldMapCameraController`, `playerTreasury` of `TreasuryHudController`), and that scene is saved unless it already had unsaved changes. The tool offers to save the open scene first, and adds `WorldMap.unity` to the build list without removing other scenes. Sample cities are only recreated together with a missing map definition.

### Ships

- `Ship` (Core) is the runtime state of one ship: normalized position, destination, heading. `ShipDefinition` (Game) is its static definition: name, speed in world units per second, crew capacity (see Crew), and eight sprites indexed by `CompassDirection` (`N, NE, E, SE, S, SW, W, NW`, the order of the sprite sheets).
- A ship follows a route: a list of waypoints, exposed as `RemainingWaypoints`, with `Destination` the last one. `SetDestination` asks the ship's `NavigationPathfinder` for the route, so the ship sails over water only; without a pathfinder (a map with no mask) the route is one straight leg. What is left of a step after a waypoint is spent on the next leg, so the speed is constant through turns.
- Steps are measured in world space through `MapProjection`, not in normalized space, so the speed is the same in every direction on a map that is not square.
- A ship is created on the water nearest to the position it is given: cities are on land, so the player ship starts beside its start city, not on it.
- `ShipsView` owns the ships, their `ShipView`s and a `MapSelectionState<Ship>`, and advances the ships with the world clock's `DeltaTime` (see Time), or with `Time.deltaTime` in a scene without a clock. It holds a list although there is a single player ship, which starts on `startCity` or the first city of the map.
- `WorldMapInteraction` arbitrates between the two selection states: a ship is picked before the cities, and selecting one clears the other, so at most one thing is hovered and one selected (the one exception is a ship in port, see below). Pressing the right button sends the selected ship to the pointer, at once and without waiting for the release (an order given while the pointer moves must not be lost), or to the nearest water it can reach when that point is on land or in another sea. When the pointer is on a city, the ship is sent to that city's port instead.
- `ShipInfoPanelController` shows the panel of the selected ship (`Assets/UI/WorldMap/ShipInfoPanel.uxml`: its name, its crew and a close button that deselects it), in a fourth `UIDocument` on the same `PanelSettings`, so the map does not take a click on it. It is at the bottom left of the screen, not centered like the city panel: the selected ship is ordered about on the map. It shows a ship selected in port too, beside the panel of its city. The two panels share their frame, title and close button styles (`InfoPanel.uss`).
- Ships keep a constant on-screen size, like city markers, and are picked with `CityPicker` on their current world positions.
- The route of the selected ship is drawn on the map while it is under way (`ShipRouteView`, driven by `ShipsView`): a solid line over what it has sailed, a dashed one over what is left, and a marker where the route ends, unless the route enters a city's port. `Ship.SailedWaypoints` is what the solid line follows: where the ship was when it was ordered, then the waypoints it reached. Every order the ship takes starts it again and it is empty when idle, so an idle ship, or one in port, has no route to show.
- The two lines are `LineRenderer`s with an unlit sprite material, drawn above the map and below the city markers, with widths and dashes in screen pixels. The dashes are a repeated two-pixel texture laid out from the destination, not from the ship (the dashed line's points run from the destination back to the ship), so they do not slide while it sails (they do rescale around the destination when zooming).

### Crew

- `ShipCrew` (Core) is the sailors aboard one ship (`Count`) out of those it has room for (`Capacity`); `Ship.Crew` holds it. Nothing changes a crew yet: sailors will be hired in cities.
- The crew sets the share of its speed a ship sails at (`SpeedFactor`), from the occupancy (`Count / Capacity`): half speed up to 10 % of the capacity, then rising in a straight line to full speed at 50 %. More sailors do not make it faster. A ship without a sailor does not move and keeps its route: it stays under way, so it never enters the port it was ordered to, and a fast forward that awaits it ends only on input. `Ship.Advance` reads the factor on every step, and `ShipDefinition.Speed` is the speed at a factor of 1.
- `ShipDefinition.crewCapacity` is the capacity of a kind of ship (28 for the merchant ship); `PlayerStartDefinition.startingCrew` (12) is what the player ship starts with, cut down to the capacity with a warning when it exceeds it (`ShipsView` also warns of a start without a sailor). `ShipsView` reads it through its `playerStart` reference, which is optional: without it the ship starts with a full crew.
- The panel of the selected ship shows the crew ("Équipage : 12 / 28", see Ships). It is written on selection: a crew that changes will need an event to refresh it.

### Ports

- `ShipDocking<TPort>` (Core, generic like `MapSelectionState<T>`) says which ships lie in which port and which are sailing to one. `ShipsView` owns the one of the map, with `CityDefinition` as the port, and updates it after the ships have advanced. Orders go through it (`OrderToPort`, `OrderToPoint`), not straight to `Ship.SetDestination`.
- A ship enters a port only when it was ordered to that city and has arrived: an order to the water beside a city does not dock, and any other order under way cancels the entry. A ship in port leaves on its next order, except one to its own city. The ship starts at sea, beside its start city.
- A port is entered from `Ship.AnchorageAt(city position)`, where a ship that starts on that city is put. A city the ship cannot sail to is not entered: the ship stops as near as it can and stays at sea.
- A ship in port stays where it anchored but is not on the map: its view is inactive, its entry in `ShipWorldPositions` is NaN (which `CityPicker` skips), and it is deselected when it arrives.
- The city panel lists the ships in port (`CityInfoPanelController`, which reads `ShipsView`). Clicking a row selects the ship while its city stays selected, the only case where two things are selected; `WorldMapInteraction` clears that ship selection as soon as another city, or none, is selected. A right click then orders the ship out, which closes the panel.

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

### Time

- `GameClock` (Core) is the time of the simulated world: `Advance(real seconds)` gives `DeltaTime`, the simulated seconds of the frame, multiplied in fast forward, and raises `DayStarted` once per day that starts, even when one step crosses several. Everything simulated is advanced with that `DeltaTime`. `Time.timeScale` stays at 1, so the camera and the UI stay in real time; do not speed the game up through it.
- `GameDate` (Core) is a day of the calendar, derived from the days elapsed. `CalendarDefinition` (Game, `Assets/Data/Calendar/Calendar.asset`) holds the start year (932), the month names (12 invented ones; their number is the number of months), the days per month (30), the real seconds per day (30) and the fast forward multiplier (60), and formats a date.
- `WorldClock` owns the clock and advances it first in the frame (execution order -100). It is optional everywhere: `ShipsView`, `WorldMapInteraction` and `WorldMapCameraController` work as before without it.
- Fast forward is a mode in which the map is only watched. `WorldMapInteraction` clears hover and selection when it starts, allows none while it lasts, and ends it on `WorldMapInput.AnyInput`: any keyboard key, or a mouse button or wheel step on the map. It then calls `ConsumeInput`, so that the same input gives no click, order or zoom, not even through the release of the button; a button that stays held can still drag the map (`PointerGesture.PressWithoutClick`). The HUD's button is over the UI: it does not raise `AnyInput` and toggles the mode itself. Fast forward also ends by itself when the ship that was selected and under way when it started arrives (`awaitedShip`). However it ends, the ship that was selected when it started is selected again, or the city whose port it has entered meanwhile.
- `WorldMapCameraController` saves the view when fast forward starts, shows the whole map, ignores the input while it lasts and returns to the saved view afterwards. When it is the arrival of the awaited ship that ends the fast forward, `WorldMapInteraction` then replaces that view by a close-up of where the ship arrived (`ShowCloseUp`: `MapCameraModel.ZoomInFullyOn`, the smallest size, centered on the ship as far as the map allows). These changes of view are instant (`MapCameraSmoother.SnapTo`), unlike every other move of the camera.
- `TimeHudController` shows the date and the button (`Assets/UI/WorldMap/TimeHud.uxml`), in a second `UIDocument` that uses the same `PanelSettings` as the city panel: one panel, so `CityInfoPanelController.IsPointerOverUi` sees the button too. The date label is rewritten when a day starts, not every frame.
- Keyboard keys are read from `Keyboard.current.allKeys`, not from an action: the "any key" control does not see a key pressed while another is held.

### Treasury

- `Treasury` (Core) is the player's gold, in whole coins. It can be negative: the player is then in debt (`IsInDebt`). `Withdraw` always takes the amount and is for what is paid whatever the gold left (wages); `TryWithdraw` takes it only when `CanAfford` and is for what a lack of gold must block (buying goods). `Changed` is raised with the new gold, only on an actual change. Nothing deposits or withdraws yet.
- `PlayerStartDefinition` (Game, `Assets/Data/Player/PlayerStart.asset`) holds the starting gold (10000) and the starting crew of the player ship (see Crew). `PlayerTreasury` owns the treasury and creates it in `Awake`, before the components that read it (execution order -100, like `WorldClock`).
- `TreasuryHudController` shows the gold at the top right of the screen (`Assets/UI/WorldMap/TreasuryHud.uxml`, "10 000 or", in red when in debt), in a third `UIDocument` on the same `PanelSettings`. Its label ignores the pointer, so the map under it stays reachable, and is rewritten on `Changed`, not every frame.

## Conventions

- **Language**: all code is in English — identifiers, comments, log messages, asset and folder names. Conversation with the user is in French.
- **Interface language**: everything the player reads is in French — UXML texts, the strings the controllers write, and the names and descriptions held by the data assets (and the defaults `WorldMapSetup` gives them). Numbers are written the French way (thousands separated by a no-break space). There is no localization system: the French text is written directly where the English one would be. City names are proper names and are not translated; the invented month names are French-sounding ("Janvis", "Févrin", …).
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
- **Input**: the new Input System is the only active handler — the legacy `UnityEngine.Input` API will throw. `Assets/InputSystem_Actions.inputactions` is registered as the project-wide actions asset. The game uses its `WorldMap` action map (Point, Click, PanDrag, PanMove, Zoom, Cancel, Command — the right mouse button, which gives orders and never pans); the `Player` map is the template's action-game default and is unused. `WorldMapInput` also reads the keyboard's keys directly, only to tell that some key was pressed.

- **Build scenes**: only `Assets/Scenes/WorldMap.unity`. Its sprites are lit by a global `Light2D`.


## Unity conventions

- Every asset has a sibling `.meta` file holding its GUID. Create, move, rename, and delete assets together with their `.meta`; otherwise references in scenes and prefabs break.
- `Library/`, `Temp/`, `Logs/`, and `UserSettings/` are generated and git-ignored. Exclude `Library/` from searches — it contains the package cache (tens of thousands of files).
- Packages are declared in `Packages/manifest.json`; package sources live read-only under `Library/PackageCache/`.
