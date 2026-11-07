## About the project

Dark Fantasy Merchant is a maritime trade management and simulation game, for PC only. The player is a merchant in a dark fantasy world made of coastal cities that trade with each other by sea and by river. The goals are to build a maritime trading empire and to rise through the social hierarchy.

## Project state

Unity **6000.6.4f1** project. What exists: the world map, the player's ship with its crew, ports, the date and fast forward, the treasury and weekly wages, the player's home city (where the player ship starts and the game opens, in its city view), notifications, the cities' economy (stocks, workshops, prices, population, buildings), trade, the player's trading posts (one per city, on a reserved site of its grid, with its inventory, the transfers between it and a ship, and the weekly rent above its capacity), the player's workshops (built only where the player has a trading post, producing into it from its inputs, with a weekly running cost and wages), the AI merchants, the city view (a city's grid of buildings, its water, its town hall, its walls, its port, its roads and the ships crossing its harbour, opened from the city panel) and the in-game debug tools. Nothing else of the game exists yet.

## Maintaining this file

Only record here what cannot be read from the code: invariants, pitfalls, the reasons for a choice, the entry points of a system. Values belong in the assets, formulas in the tests, the texts the player reads in the code.

What concerns one subsystem goes in its rule, `.claude/rules/<subsystem>.md`, which Claude Code loads only when a file of that subsystem is read; this file keeps what concerns the whole project.

## Architecture

Code is split into assemblies, each with its own `.asmdef`:

| Assembly | Folder | Role |
|---|---|---|
| `DarkFantasyMerchant.Core` | `Assets/Scripts/Core` | Plain C# logic and runtime state. No `MonoBehaviour`, nothing that needs a scene. |
| `DarkFantasyMerchant.Game` | `Assets/Scripts/Game` | ScriptableObject definitions and thin MonoBehaviour adapters over `Core`. |
| `DarkFantasyMerchant.Editor` | `Assets/Scripts/Editor` | Editor-only tools. |
| `DarkFantasyMerchant.Tests.EditMode` | `Assets/Tests/EditMode` | EditMode tests for `Core`. |

### Subsystems

Inside each assembly folder, scripts are in one subfolder per subsystem, with the same names everywhere. Each has its rule in `.claude/rules/`:

| Subfolder | Rule | Content |
|---|---|---|
| `WorldMap` | `world-map.md` | Map, camera, picking, cities, city panel |
| `Ships` | `ships.md` | Ships, routes, crew, ports, ship panel |
| `Navigation` | `navigation.md` | Navigation mask, its Editor tool, pathfinding |
| `Time` | `time.md` | Clock, calendar, fast forward |
| `Player` | `player.md` | Treasury, notifications, weekly expenses |
| `Economy` | `economy.md` | Goods, markets, prices, trade, holds, market tab |
| `City` | `city.md` | Cities in play, labour, beggars, houses, buildings and construction |
| `AiMerchants` | `ai-merchants.md` | AI merchant ships and their planning |
| `CityView` | `city-view.md` | City grid, placement, city view scene, build bar, layout Editor tool |
| `Debug` | `debug-tools.md` | In-game debug tools |
| `Common` | — | Shared helpers (`NameOrder`, `ListStripes`, `NumberText`, `UiPointer`, `SimulationServices`, `SceneEditing` and `SceneCheck` for the scenes, test helpers) |

A new script goes in the subfolder of its subsystem; a new subsystem gets its subfolder in every assembly that needs one, and its rule, with `paths` matching those subfolders.

### Scenes

- The world map scene, its prefabs and the data assets are versioned as they are: nothing regenerates them. A new scene object, data asset or reference between components is made once, in the Editor, by a one-off script run through `unity command eval_file` (or by hand), and only its result is committed; the script is not kept. Such a script goes through `SceneEditing` (`Editor/Common`): the scenes' paths, the shared panel settings, `CreateInGroup`, `FindInScene`, `FillReference`, `SetReference`. A new asset is filled in before `AssetDatabase.CreateAsset`, so its file is written complete.
- After editing a scene, run `Tools > Dark Fantasy Merchant > Check Scenes` (`SceneCheck.Run`, which changes nothing): it reports an object at the root outside the groups, a missing script, an empty or broken object reference on a component of the game, and a UI document without its UXML or the shared panel settings. A reference left empty on purpose has no place in the scenes: the check knows no exception.
- The city view scene, `Assets/Scenes/CityView.unity`, is built by `CityViewSetup` (`Tools > Dark Fantasy Merchant > Build City View Scene`): what is missing is created, what exists is left as it is. It also makes the view's placeholder sprites and its layer, and lists the scene after `WorldMap.unity` in the build list.
- A scene's root holds only empty groups at the origin (`Rendering`, `World`, `UI`, `Simulation`, `Debug`), which hold every object. `SceneEditing.CreateInGroup` puts an object last in its group, and makes the group when it is missing. Do not reorder the panels and HUDs of `UI`: UI documents of equal `sortingOrder` may be drawn in that order.
- Components that others read in `Awake`/`Start` are created first: `WorldClock`, `PlayerTreasury`, `PlayerNotifications` and `SimulationServices` run at execution order -100.
- `SimulationServices.Current` is the entry point to the components that run the world (clock, treasury, notifications, economy, ships, AI merchants). Code that has no serialized reference to them, in particular the objects of another scene, which cannot hold one, goes through it; the world map scene's own objects keep their serialized references. It is set in the world map scene's `Awake`: an object of a scene loaded with it, or before it, finds it null in its own `Awake`. A new service of the simulation is added to it.

### Runtime UI

- Every `UIDocument` of the game (city panel, ship panel, time HUD, treasury HUD, notifications, debug panel, city view) shares one `PanelSettings`: that is what lets `UiPointer.IsOver`, given any one of them (the city panel's for `WorldMapInput`, the view's for `CityViewInput`), see them all and the map take no click or wheel step on them. A new panel goes on the same `PanelSettings`.
- Labels drawn over the map (treasury, notifications) ignore the pointer, so the map under them stays reachable.
- Labels are rewritten on the event that changes them (`Changed`, `DayStarted`…), not every frame. The exceptions are the debug ships table, rewritten every frame, and the debug views of the past, rewritten at most once a frame.
- The city and ship panels share their frame, title and close button styles (`InfoPanel.uss`).

## Conventions

- **Language**: all code is in English — identifiers, comments, log messages, asset and folder names. Conversation with the user is in French.
- **Interface language**: everything the player reads is in French — UXML texts, the strings the controllers write, and the names and descriptions held by the data assets. Numbers are written the French way (thousands separated by a no-break space), always through `NumberText` (`Format`, `FormatGold`), and the quantities and fills of goods through `GoodUnitText`; a panel never calls another panel's controller to write a text. There is no localization system: the French text is written directly. City names are proper names and are not translated.
- **Alphabetical lists**: every list the player reads is in the alphabetical order of its names, accents and case aside ("Étoffe" between "Cuir" and "Fer"). Sort with `NameOrder`, never in the catalogue's order or the order things were added; the goods stay identified by their index, only what is shown is sorted. A fixed sequence that is not a list of items (tabs, columns, the parts of a sentence) and a chronological one (the notifications) keep their order.
- **Switched-off materials**: every construction, the player's or a city's, leaves out the materials that are not active goods and asks only for the others, its gold alone when none is active; a switched-off material never makes a building unavailable. Go through `BuildingDefinition.MaterialsOf`.
- **UI**: UI Toolkit (UXML + USS) for all runtime UI. Do not build uGUI Canvas hierarchies, even though `com.unity.ugui` is installed.
- **Good names**: a good's name is never followed by its unit in parentheses ("Bière", never "Bière (tonneaux)"), anywhere the player or the debug tools show it: titles, columns, drop-downs, charts. The unit goes with a quantity (`GoodUnitText.FormatQuantity`: "3 tonneaux").
- **Striped lists**: every list of rows on screen has a slightly lighter background on every other row, from the second. USS has no `:nth-child`: call `ListStripes.Apply` on the row container whenever rows are added, removed or moved. The notifications, labels over the map, are not striped.
- **Data**: game content (goods, cities, ships, prices, ranks, etc.) is defined in ScriptableObject assets, not hard-coded. ScriptableObjects hold static definitions only; mutable simulation state lives in separate runtime objects, never written back into the assets.
- **Testing**: keep simulation logic (economy, trade, progression) in plain C# classes with no `MonoBehaviour` or scene dependency, and cover it with EditMode tests. MonoBehaviours are thin adapters that drive the simulation and present its state.
- **No mention of Patrician III**: apart from this rule, nothing in this repository names Patrician III — not the code, the comments, the asset names, the rules and documentation (`.claude/rules/`, `docs/`), nor the commit messages. This covers its abbreviation "P3", the `patrician3-re` project, its files and the addresses of its code. The specs and plans already in `docs/superpowers/` and the commits already made are left as they are. A rule taken from it is described for what it does, in the game's own terms, never by where it comes from ("the price falls linearly above the threshold", not "Patrician III's price curve").

## Commands

There is no build or lint step outside the Editor: compilation happens in the Unity Editor. The `.sln` / `.csproj` files at the root are generated by Unity and git-ignored — never edit them by hand. To run the tests, use the `run-tests` skill. Claude may exit Play mode without asking the user, even when the user started it.

When the Editor is open, drive it with the Unity CLI (`unity`, through Bash, from the project folder), which reaches it through the `com.unity.pipeline` package; `unity status` shows whether it is connected. `unity command` lists what the Editor exposes, `unity list --format json` with their parameters. The `unity-mcp` MCP tools are only a fallback.

- `unity recompile --format json`: refreshes the assets and recompiles, after a `.cs` or `.asset` file was edited on disk; it gives the compile errors with their file and line.
- `unity command console --level error` (or `warn`), and `console_status` for the counts and whether compilation failed.
- `unity command eval '<C#>'` and `unity command eval_file --file <path>`: the code is a method body that may `return` a value, with no `using` (names are written in full). It runs on the main thread and is stopped after 5 s: pass a longer limit to the Editor after `--` (`... -- --timeout 30000`, in ms), since a `--timeout` before `--` is the CLI's own, in seconds.
- `move_asset`, `rename_asset`, `delete_asset` (`--confirm true`) move and delete an asset with its `.meta`. Also `get_scene_hierarchy`, `editor_play` / `editor_stop`.
- `capture_scene_view` and `capture_game_view`: pass `--include_inline_image true` and decode the PNG's `base64` into the scratchpad. Their `--save_path` always writes under `Assets/`, which makes the capture an asset of the project.
- Add `--result-only` to get only the Editor's result, and filter long results (a test run is several hundred kB) before reading them.
- An Editor opened on code that does not compile starts in Safe Mode, without the package: the CLI cannot reach it (`unity recompile` exits 7). Fix the code from the files, then reopen the Editor.

## Project configuration

- **Rendering**: URP with the **2D Renderer**: sprites are lit by 2D lights (`Light2D`, a global one in the scene); 3D lights and 3D-only URP features do not apply.
- **Input**: the new Input System is the only active handler — the legacy `UnityEngine.Input` API will throw. The game uses two action maps of `Assets/InputSystem_Actions.inputactions`: `WorldMap` on the map, whose `Command` action is the right mouse button, which gives orders and never pans; and `CityView` in the city view, whose `Click` and `PanDrag` are both the left button (a press released where it started clicks, one moved pans; while a road is traced, the left button traces instead), `MiddlePan` the middle button, which always pans, `Zoom` the wheel, `Rotate` the R key and `Cancel` the right button. The `Player` map is the template's and is unused.
- **Build scenes**: `Assets/Scenes/WorldMap.unity`, first, and `Assets/Scenes/CityView.unity`, loaded additively over it.

## Unity conventions

- Create, move, rename and delete assets together with their `.meta`, or references in scenes and prefabs break.
- Exclude `Library/` from searches: it contains the package cache (tens of thousands of files). Package sources under `Library/PackageCache/` are read-only.
