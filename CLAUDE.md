# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## The game

Dark Fantasy Merchant is a maritime trade management and simulation game. The player is a merchant in a dark fantasy world made of coastal cities that trade with each other by sea and by river. The goals are to build a maritime trading empire and to rise through the social hierarchy.

- **Presentation**: 2D pixel art.
- **Platform**: PC only (keyboard and mouse).

## Project state

Unity **6000.6.4f1** project, created from the URP 3D template and converted in place to 2D. What exists: the world map, the player's ship with its crew, ports, the date and fast forward, the treasury and weekly wages, notifications, the cities' economy (stocks, workshops, prices, population, buildings), trade, the AI merchants and the in-game debug tools. Nothing else of the game exists yet. `TutorialInfo/` and `SampleScene` are template leftovers and not part of the game.

## Maintaining this file

Only record here what cannot be read from the code: invariants, pitfalls, the reasons for a choice, the entry points of a system. Values belong in the assets, formulas in the tests and in `patrician3-re\resultats\`, the texts the player reads in the code. When a feature is added, add the few lines a newcomer would need to work on it safely, not its specification.

## Architecture

Code is split into assemblies, each with its own `.asmdef`:

| Assembly | Folder | Role |
|---|---|---|
| `DarkFantasyMerchant.Core` | `Assets/Scripts/Core` | Plain C# logic and runtime state. No `MonoBehaviour`, nothing that needs a scene. |
| `DarkFantasyMerchant.Game` | `Assets/Scripts/Game` | ScriptableObject definitions and thin MonoBehaviour adapters over `Core`. |
| `DarkFantasyMerchant.Editor` | `Assets/Scripts/Editor` | Editor-only tools. |
| `DarkFantasyMerchant.Tests.EditMode` | `Assets/Tests/EditMode` | EditMode tests for `Core`. |

Content lives in `Assets/Data`, runtime UI in `Assets/UI`, art in `Assets/Art`, prefabs in `Assets/Prefabs`.

### Scene setup

- `Tools > Dark Fantasy Merchant > Build World Map Scene` (`WorldMapSetup.Build`) recreates whatever is missing (scene, prefabs, panel settings, data assets, scene objects and the references between them) and leaves what exists untouched. Every new scene object, data asset or reference between components must be added to it, so that an existing scene gets it too. It saves the scene unless it already had unsaved changes, and gives cities that produce nothing the productions of the sample content.
- Components that others read in `Awake`/`Start` are created first: `WorldClock`, `PlayerTreasury` and `PlayerNotifications` run at execution order -100.

### Runtime UI

- Every `UIDocument` of the game (city panel, ship panel, time HUD, treasury HUD, notifications, debug panel) shares one `PanelSettings`: that is what lets `CityInfoPanelController.IsPointerOverUi` see them all and the map take no click or wheel step on them. A new panel goes on the same `PanelSettings`.
- Labels drawn over the map (treasury, notifications) ignore the pointer, so the map under them stays reachable.
- Labels are rewritten on the event that changes them (`Changed`, `DayStarted`…), not every frame. The exception is the debug ships table.
- The city and ship panels share their frame, title and close button styles (`InfoPanel.uss`).
- The city panel's tabs: each page wraps the element its section shows and hides, so the tabs and the sections never write the same `display`. Its fixed height is chosen so that the market's active goods show without scrolling.
- Sections of the city panel listen to the market, the hold and the treasury only while they are shown.

### World map

- Positions on the map are **normalized** (`(0,0)` bottom-left, `(1,1)` top-right) and converted to world space only through `MapProjection`. The map is centered on the world origin and sized by `WorldMapDefinition.worldWidth`, so the map image can be replaced by one of another resolution without moving anything. `Assets/Art/WorldMap/WorldMap.jpg` is a placeholder.
- `WorldMapView` owns the scene's `MapSelectionState<CityDefinition>`. Markers and the city panel both react to it and do not know about each other.
- `WorldMapInput` is the only reader of the `WorldMap` action map; it raises click, drag and zoom events and ignores pointer input that starts over the UI.
- `MapCameraModel` is where input has sent the camera (the target); `MapCameraSmoother` is what is displayed, eased towards it. Position and size share one easing factor on purpose: that keeps the zoom anchor fixed during the transition, so do not split it into separate pan and zoom settings. Hover, picking and marker size follow the displayed view.
- Picking is `CityPicker` (nearest point within a screen-pixel radius, no colliders), for cities and ships alike.
- A city stands on a navigable cell of the mask, at the edge of the water beside its town on the map. The order of the map's cities is the order of the AI merchants' numbers, and the player starts in the first.
- Cities are placed by dragging their handles in the Scene view (`CityPlacementTool`); it and the navigation mask tool find the map through `WorldMapEditorContext`.

### Ships

- `Ship` (Core) is the runtime state of one ship; `ShipDefinition` (Game) its static definition, with eight sprites indexed by `CompassDirection` (the order of the sprite sheets).
- `SetDestination` asks the ship's `NavigationPathfinder` for a route over water; without one (a map with no mask) the route is one straight leg. What is left of a step after a waypoint is spent on the next leg, so the speed is constant through turns. Steps are measured in world space, not normalized space, so the speed is the same in every direction on a non-square map.
- A ship is created on the water nearest to the position it is given: cities are on land.
- `ShipsView` owns every ship of the map, the player's and the AI's, in parallel lists by index: `RemoveShip` takes a ship out of all of them at once. It advances them with the world clock's `DeltaTime`, or `Time.deltaTime` without a clock. A ship's name is its own (`DisplayNameOf`), not its definition's. Only the player's ships (`IsPlayerShip`) take orders, are awaited by a fast forward, are paid wages and have their arrivals announced.
- `WorldMapInteraction` arbitrates between the two selection states: a ship is picked before the cities, and at most one thing is hovered and one selected (except a ship in port, see Ports). The right button orders on press, not on release, so that an order given while the pointer moves is not lost; on a city it sends the ship to that city's port.
- The route of the selected ship (`ShipRouteView`): a solid line over `Ship.SailedWaypoints`, a dashed one over what is left. The dashes are laid out from the destination, not from the ship, so they do not slide while it sails. Widths and dashes are in screen pixels.

### Crew

- `ShipCrew` (Core) gives the share of its speed a ship sails at (`SpeedFactor`, from the occupancy). A ship without a sailor does not move but keeps its route: it stays under way, never enters the port it was ordered to, and a fast forward that awaits it ends only on input.
- Nothing changes a crew yet. The ship panel writes the crew on selection: a crew that changes will need an event to refresh it.
- `ShipsView` reads the starting crew from its optional `playerStart`; without it the ship starts with a full crew.

### Ports

- `ShipDocking<TPort>` (Core) says which ships lie in which port and which are sailing to one; `ShipsView` updates it after the ships have advanced. Orders go through it (`OrderToPort`, `OrderToPoint`), never straight to `Ship.SetDestination`.
- A ship enters a port only when it was ordered to that city and has arrived: an order to the water beside a city does not dock. A ship in port leaves on its next order, except one to its own city. A city the ship cannot reach is not entered: it stops as near as it can, at sea. A port is entered from `Ship.AnchorageAt`.
- A ship in port is not on the map: its view is inactive, its entry in `ShipWorldPositions` is NaN (which `CityPicker` skips), and it is deselected when it arrives.
- Selecting a ship from the city panel's "ships in port" tab keeps the city selected: the only case of two selections. `WorldMapInteraction` clears it as soon as another city, or none, is selected, and the panel follows a selected AI ship that leaves by itself (`ShipDocking.Undocked`).

### Navigation mask

- `NavigationGrid` (Core) is one bit per cell, cell `(0,0)` bottom-left, over normalized space, with square cells (its height follows the map's aspect ratio). It also holds the painting logic. Replacing the map image by one of another shape needs a **Resize** in the tool, not a repaint. The mask is not expected to change while the game runs.
- `NavigationMaskDefinition` stores the grid; its fields are hidden in the Inspector on purpose: size and bits only make sense together. A map without a mask is valid.
- The mask is painted in the Scene view with the **Navigation Mask** tool (`NavigationMaskTool`, `NavigationMaskOverlay`); while it is active, `CityPlacementTool` draws no handles. Its working copy, `NavigationMaskSession`, is written to the asset once per stroke through `NavigationMaskAuthoring.Apply` (one stroke, one undo step) and rebuilt from the asset after undo/redo or an outside change (`SyncWithAsset`). Its preview texture is rewritten only where a stroke passed; do not go back to rebuilding it whole on every drag event.

### Pathfinding

- Movement rules, shared by the search, route smoothing and the fill bucket: a move to a side neighbour, or to a diagonal one only when both cells beside it are navigable; a straight leg only when every cell it touches, even by a corner, is navigable (`NavigationLineOfSight`, with a margin of a thousandth of a cell because positions are floats). A diagonal coastline one cell thick therefore holds, and two cells are reachable exactly when they are in the same 4-connected region.
- `NavigationCellSearch` is an A* whose buffers are allocated once and reused: an order allocates nothing. Keep it that way. It runs synchronously on the click, with about 16 bytes per cell held.
- An order ends on the clicked point when the ship can reach it, otherwise on the nearest cell of the ship's own region.

### Time

- `GameClock` (Core) gives `DeltaTime`, the simulated seconds of the frame, multiplied in fast forward, and raises `DayStarted` once per day that starts, even when one step crosses several. Everything simulated advances with that `DeltaTime`. `Time.timeScale` stays at 1, so the camera and the UI stay in real time; do not speed the game up through it.
- `WorldClock` is optional for `ShipsView`, `WorldMapInteraction` and `WorldMapCameraController`; `PlayerExpenses`, `WorldEconomy` and `AiMerchants` need it. The calendar's settings are in `Assets/Data/Calendar/Calendar.asset`.
- Fast forward is a mode in which the map is only watched. `WorldMapInteraction` clears hover and selection when it starts and ends it on `WorldMapInput.AnyInput`, then calls `ConsumeInput`, so that the same input gives no click, order or zoom, not even through the release of the button (`PointerGesture.PressWithoutClick`). The HUD's button is over the UI: it does not raise `AnyInput`. Fast forward also ends when the ship selected and under way when it started arrives (`awaitedShip`); the camera then shows a close-up of it. However it ends, the previous selection is restored, or the city whose port the ship has entered.
- The camera's changes of view around a fast forward are instant (`MapCameraSmoother.SnapTo`), unlike every other move.
- Keyboard keys are read from `Keyboard.current.allKeys`, not from an action: the "any key" control does not see a key pressed while another is held.

### Treasury

- `Treasury` (Core) can be negative (debt). `Withdraw` always takes the amount, for what is paid whatever the gold left (wages); `TryWithdraw` only when `CanAfford`, for what a lack of gold must block (buying, building). `Changed` is raised only on an actual change.

### Notifications

- `NotificationFeed` (Core), newest first, aged by `PlayerNotifications` in real time, not with the world clock: a notification is read by the player, in fast forward too.
- The list's `top` in `NotificationsHud.uss` follows the height of the treasury label: change them together.
- Texts have no article and nothing that agrees with the ship's name or elides before the city's; keep new ones that way.
- `Ship.Arrived` is raised when a ship reaches the end of its route, not when an order replaces it. `ShipArrivals<TPort>` also counts a ship that enters a port without sailing, and `ShipsView` calls its `Report` after `Docking.Update`, so that the port is known.

### Expenses

- `WeeklyBilling` (Core) keeps no count of its own: the week is read from the clock's elapsed days, counted from the first day of the game (not within a month), so a day it is not told of shifts no week, and a step that crosses several weeks pays each. It pays with `Withdraw`, so a player without gold goes into debt.
- `WeeklyBill` holds one amount per kind of expense; a new recurring expense is a new amount added to its `Total`. The only one so far is the sailors' wages, counted when the week ends, at sea or in port.

### Economy

The rules follow Patrician III; their detail is in `patrician3-re\resultats\` (`Calcul des prix.md`, `Production et consommation des villes.md`, `Population des villes.md`, `Marchands IA.md`, `Villes de la carte.md`) and in the tests of the Core class that implements them. Every setting is a field of `Assets/Data/Economy/Economy.asset`, of the good assets (`Assets/Data/Goods`) or of `Buildings.asset`, meant to be tuned by playing; no test pins the base prices.

- **Goods and units.** Patrician III's twenty goods, in its order. Stocks, consumption, production, thresholds and prices are all in the good's unit, barrels or bushels (`GoodUnit`): the price of grain is that of a bushel. A bushel takes `barrelsPerBushel` barrels of room in a hold (`GoodUnits`).
- **Index order.** In `Core` a good is its index among the active goods, in the catalogue's order. What is shown is sorted by name (see Conventions), never the indices.
- **Active goods.** Each `CatalogueGood` is ticked active or not before Play mode. An inactive good does not exist in the game: `EconomyDefinition.Goods`, which everything reads, holds only the active ones; `Catalogue` is every good, for the Editor and the content tests. A city keeps its workshop for an inactive good in its `CityDefinition` but has none in the game. A catalogue with no active good is refused.
- **Cities' productions.** `CityDefinition` lists the goods the city produces efficiently and inefficiently, edited through `CityDefinitionEditor` (`CityProductionEditing.SetProduction`).
- **Markets.** `WorldEconomy` owns one `CityMarket` (Core) per city, created in `Start` because the map view lists its cities in `Awake`, and calls their `StartDay` on `DayStarted`.
- **Validation.** `EconomyDefinition.TryValidateSettings` checks every setting; one that breaks a rule leaves every city without a market, with a warning in the Inspector (`OnValidate`) and an error from `WorldEconomy`. `WorldEconomy` also needs a valid `BuildingsDefinition`.
- **Stocks.** There is no limit to what a city stores. Stocks are fractional; what is shown and can be bought is the whole part.
- **Thresholds and prices.** `ThresholdRule` computes the thresholds when a price is asked, so they follow the population. `PriceCurve` / `MarketPricing`. Units are priced one by one at the average factor over the stock they move, buying rounded up and selling rounded down, so buying and selling back never gains gold.
- **Workshops and population.** `WorkforceRule` (city workshops' hiring), `LabourRule` (see Population).
- **Starting population.** `EconomyDefinition.StartingPopulation` is **not** the population of the game's cities, only that of a market made without labour; the labour markets start with `CityPopulation.AtStart`.
- **Raw materials.** Raw and building materials are in large surplus for now, because nothing consumes them as inputs yet. This is expected until production chains are added.
- **Trade.** `Trade` (Core) moves goods between a market, a `CargoHold` and the `Treasury`. A trade of several units stops at the first that cannot be traded, and moves them at once, so the market and the hold raise `Changed` once per trade. A treasury in debt buys nothing but still sells. The overloads without a treasury are for the AI merchants.
- **Holds.** `CargoHold.RoomFor` is whole units: nothing ever splits a unit.
- **Market tab.** `TradingShipChoice` (Core) picks the ship that trades; choosing it does not select it on the map. A price is always a `Button`, disabled whenever its trade is impossible, and a disabled price is styled as plain text, as readable as the others (`.market__price`).
- **Goods' icons.** `Assets/Art/Goods/Icons/<good's asset name>.png`, cut from `icon-set-a.png` (painted, not pixel art), with mipmaps since they are shown much smaller.

### Buildings

- `BuildingsDefinition` holds the player's house and workshop. `TryCreateCost` turns a cost into Core's `BuildingCost`. A cost that cannot be created only makes its building unavailable: `WorldEconomy.HouseCost` or `WorkshopCost` is then null and its buttons stay disabled.
- `Construction` (Core) quotes and builds. The materials come first from the hold of the market tab's ship, then are bought from the market, all or nothing. A button whose quote is impossible reads "Construire" with no amount: the gold of an impossible quote means nothing.

### Population

- `CityPopulation` (Core): housed inhabitants, beggars and room, held by `CityMarket.Population`. Only labour changes the population; food does not. Beggars are the only source of new inhabitants.
- The order of a day in `CityMarket.StartDay`, per city:
  1. consumption;
  2. the city's workshops, in the catalogue's order: hiring, layoffs into a reserve, production (`WorkforceRule`, `LabourRule`);
  3. the player's workshops, which hire from the beggars after the city, never lay off and produce nothing yet (`PlayerDailyOutputOf` is known, `MerchantOutputRule`; their production will need Patrician III's office, the "comptoir");
  4. the beggars, who come or go towards `BeggarTarget`;
  5. a house the city builds of its own when crowded (`CityHousingRule`, at most one a day).
- The rules are in `Population des villes.md` and in `CityMarketLabourTests` / `CityMarketHousingTests`.
- A city's own house takes its materials from its market, with no gold and no notification, and is not counted as consumption in the week's log.
- A market without a `LabourRule` keeps its population: it hires at once, laid-off workers vanish, it never builds, and the player's workshops in it hire nobody.

### AI merchants

- Their gold is unlimited: they have no treasury and pay no wages, and only their cumulated profit is counted, for the debug tool.
- `AiMerchants` only wires the Core classes to the scene. It measures the travel time between every pair of cities a few pairs per frame (`PortTravelTable`, one for all ships since they share a definition and a crew), then spawns the ships in their home ports: the AI ships appear a moment after the game starts. A city that reaches no other is warned about and ignored: most likely a mistake of the mask.
- `AiMerchantFleet<TPort>` (Core, listens to `ShipDocking`) is the life of a ship on Patrician III's tour (`Marchands IA.md`). `SupplyPlanner` and `ShipmentQuantity` decide what to carry where.
- What a ship loads for a port is recorded as under way to it until it enters that port (`UnderWayTo`). Every ship's records count in every decision, the deciding ship's own included, so that it does not buy again what it carries and two ships do not supply the same lack.
- At the start every city holds the same days of every good, so no trade pays: the AI ships wait in port for the first days. This is expected.

### Debug tools

- In the game, not in Editor windows, and only in the Editor and development builds (`Debug.isDebugBuild`). They read the simulation and change nothing, except the ships tab's buttons that add and remove AI ships. Their `UIDocument` has a sorting order of 100, over the game's panels.
- F1 toggles them: `WorldMapInput` reads it (`DebugKey`) and leaves it out of `AnyInput`, so that they can be watched during fast forward. A new debug key goes the same way.
- `MarketWeekLog` (Core) is what a city actually produced and consumed over the last week; what the player buys or delivers is not counted.
- The cities table (`DebugCitiesTable`) is built the first time the panel is opened, since the economy starts after the panel is enabled. UI Toolkit has no sticky position: the cities' columns and the goods' are two containers side by side, built row by row, only the goods' in a horizontal `ScrollView`; rows have one height, so the two sides line up.
- The ships table (`DebugShipsTable`) is rewritten every frame while it is shown, and not otherwise.
- Removing an AI ship goes through `AiMerchants.RemoveShip`, which refuses a ship that is not its own: the fleet forgets it, then `ShipsView.RemoveShip` deselects it, takes it out of its port (raising `Undocked`), stops reporting its arrivals and destroys its view. A new ship's number is never one a removed ship had.

## Conventions

- **Language**: all code is in English — identifiers, comments, log messages, asset and folder names. Conversation with the user is in French.
- **Interface language**: everything the player reads is in French — UXML texts, the strings the controllers write, and the names and descriptions held by the data assets (and the defaults `WorldMapSetup` gives them). Numbers are written the French way (thousands separated by a no-break space). There is no localization system: the French text is written directly where the English one would be. City names are proper names and are not translated; the invented month names are French-sounding.
- **Alphabetical lists**: every list the player reads is in the alphabetical order of its names, accents and case aside ("Étoffe" between "Cuir" and "Fer"). Sort with `NameOrder` (`Of` for indices, `Sort` for a list, `Compare` for two names), never in the catalogue's order or the order things were added. The goods stay identified by their index; only what is shown is sorted. A fixed sequence that is not a list of items (tabs, columns, the parts of a sentence) and a chronological one (the notifications) keep their order.
- **Switched-off materials**: every construction, the player's or a city's, leaves out the materials that are not active goods and asks only for the others, its gold alone when none is active; a switched-off material never makes a building unavailable. Go through `BuildingsDefinition.MaterialsOf`.
- **UI**: UI Toolkit (UXML + USS) for all runtime UI. Do not build uGUI Canvas hierarchies, even though `com.unity.ugui` is installed.
- **Striped lists**: every list of rows on screen has a slightly lighter background on every other row, from the second (both sides of a debug cities row alike). USS has no `:nth-child`: call `ListStripes.Apply` on the row container whenever rows are added, removed or moved; its class, `list-row--alternate`, is styled in `InfoPanel.uss` (and for the ship buttons, before their hover, in `CityInfoPanel.uss`). The notifications, labels over the map, are not striped.
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
- **Input**: the new Input System is the only active handler — the legacy `UnityEngine.Input` API will throw. `Assets/InputSystem_Actions.inputactions` is registered as the project-wide actions asset. The game uses its `WorldMap` action map (Point, Click, PanDrag, PanMove, Zoom, Cancel, Command — the right mouse button, which gives orders and never pans); the `Player` map is the template's action-game default and is unused. `WorldMapInput` also reads the keyboard's keys directly (see Time and Debug tools).
- **Build scenes**: only `Assets/Scenes/WorldMap.unity`. Its sprites are lit by a global `Light2D`.

## Unity conventions

- Every asset has a sibling `.meta` file holding its GUID. Create, move, rename, and delete assets together with their `.meta`; otherwise references in scenes and prefabs break.
- `Library/`, `Temp/`, `Logs/`, and `UserSettings/` are generated and git-ignored. Exclude `Library/` from searches — it contains the package cache (tens of thousands of files).
- Packages are declared in `Packages/manifest.json`; package sources live read-only under `Library/PackageCache/`.
