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

What concerns one subsystem goes in its rule, `.claude/rules/<subsystem>.md`, which Claude Code loads only when a file of that subsystem is read; this file keeps what concerns the whole project.

## Architecture

Code is split into assemblies, each with its own `.asmdef`:

| Assembly | Folder | Role |
|---|---|---|
| `DarkFantasyMerchant.Core` | `Assets/Scripts/Core` | Plain C# logic and runtime state. No `MonoBehaviour`, nothing that needs a scene. |
| `DarkFantasyMerchant.Game` | `Assets/Scripts/Game` | ScriptableObject definitions and thin MonoBehaviour adapters over `Core`. |
| `DarkFantasyMerchant.Editor` | `Assets/Scripts/Editor` | Editor-only tools. |
| `DarkFantasyMerchant.Tests.EditMode` | `Assets/Tests/EditMode` | EditMode tests for `Core`. |

Content lives in `Assets/Data`, runtime UI in `Assets/UI`, art in `Assets/Art`, prefabs in `Assets/Prefabs`.

### Subsystems

Inside each assembly folder, scripts are in one subfolder per subsystem, with the same names everywhere. Each has its rule in `.claude/rules/`:

| Subfolder | Rule | Content |
|---|---|---|
| `WorldMap` | `world-map.md` | Map, camera, picking, cities, city panel, scene setup |
| `Ships` | `ships.md` | Ships, routes, crew, ports, ship panel |
| `Navigation` | `navigation.md` | Navigation mask, its Editor tool, pathfinding |
| `Time` | `time.md` | Clock, calendar, fast forward |
| `Player` | `player.md` | Treasury, notifications, weekly expenses |
| `Economy` | `economy.md` | Goods, markets, prices, trade, holds, market tab |
| `Population` | `population.md` | Labour, beggars, houses, buildings and construction |
| `AiMerchants` | `ai-merchants.md` | AI merchant ships and their planning |
| `Debug` | `debug-tools.md` | In-game debug tools |
| `Common` | — | Shared helpers (`NameOrder`, `ListStripes`, test helpers) |

A new script goes in the subfolder of its subsystem; a new subsystem gets its subfolder in every assembly that needs one, and its rule, with `paths` matching those subfolders.

### Scene setup

- `Tools > Dark Fantasy Merchant > Build World Map Scene` (`WorldMapSetup.Build`) recreates whatever is missing (scene, prefabs, panel settings, data assets, scene objects and the references between them) and leaves what exists untouched. Every new scene object, data asset or reference between components must be added to it, so that an existing scene gets it too. It saves the scene unless it already had unsaved changes, and gives cities that produce nothing the productions of the sample content.
- Components that others read in `Awake`/`Start` are created first: `WorldClock`, `PlayerTreasury` and `PlayerNotifications` run at execution order -100.

### Runtime UI

- Every `UIDocument` of the game (city panel, ship panel, time HUD, treasury HUD, notifications, debug panel) shares one `PanelSettings`: that is what lets `CityInfoPanelController.IsPointerOverUi` see them all and the map take no click or wheel step on them. A new panel goes on the same `PanelSettings`.
- Labels drawn over the map (treasury, notifications) ignore the pointer, so the map under them stays reachable.
- Labels are rewritten on the event that changes them (`Changed`, `DayStarted`…), not every frame. The exception is the debug ships table.
- The city and ship panels share their frame, title and close button styles (`InfoPanel.uss`).

## Conventions

- **Language**: all code is in English — identifiers, comments, log messages, asset and folder names. Conversation with the user is in French.
- **Interface language**: everything the player reads is in French — UXML texts, the strings the controllers write, and the names and descriptions held by the data assets (and the defaults `WorldMapSetup` gives them). Numbers are written the French way (thousands separated by a no-break space). There is no localization system: the French text is written directly. City names are proper names and are not translated.
- **Alphabetical lists**: every list the player reads is in the alphabetical order of its names, accents and case aside ("Étoffe" between "Cuir" and "Fer"). Sort with `NameOrder`, never in the catalogue's order or the order things were added; the goods stay identified by their index, only what is shown is sorted. A fixed sequence that is not a list of items (tabs, columns, the parts of a sentence) and a chronological one (the notifications) keep their order.
- **Switched-off materials**: every construction, the player's or a city's, leaves out the materials that are not active goods and asks only for the others, its gold alone when none is active; a switched-off material never makes a building unavailable. Go through `BuildingsDefinition.MaterialsOf`.
- **UI**: UI Toolkit (UXML + USS) for all runtime UI. Do not build uGUI Canvas hierarchies, even though `com.unity.ugui` is installed.
- **Striped lists**: every list of rows on screen has a slightly lighter background on every other row, from the second. USS has no `:nth-child`: call `ListStripes.Apply` on the row container whenever rows are added, removed or moved. The notifications, labels over the map, are not striped.
- **Data**: game content (goods, cities, ships, prices, ranks, etc.) is defined in ScriptableObject assets, not hard-coded. ScriptableObjects hold static definitions only; mutable simulation state lives in separate runtime objects, never written back into the assets.
- **Testing**: keep simulation logic (economy, trade, progression) in plain C# classes with no `MonoBehaviour` or scene dependency, and cover it with EditMode tests. MonoBehaviours are thin adapters that drive the simulation and present its state.

## Commands

There is no CLI build or lint step; compilation happens in the Unity Editor. The `.sln` / `.csproj` files at the root are generated by Unity and git-ignored — never edit them by hand. To run the tests, use the `run-tests` skill. When the Editor is open, prefer the `unity-mcp` MCP tools (`Unity_RunCommand`, `Unity_GetConsoleLogs`, scene/camera captures) to compile, check console errors and inspect the scene.

## Project configuration

- **Rendering**: URP with the **2D Renderer**: sprites are lit by 2D lights (`Light2D`, a global one in the scene); 3D lights and 3D-only URP features do not apply. No Pixel Perfect Camera is set up yet.
- **Input**: the new Input System is the only active handler — the legacy `UnityEngine.Input` API will throw. The game uses the `WorldMap` action map of `Assets/InputSystem_Actions.inputactions`; its `Command` action is the right mouse button, which gives orders and never pans. The `Player` map is the template's and is unused.
- **Build scenes**: only `Assets/Scenes/WorldMap.unity`.

## Unity conventions

- Create, move, rename and delete assets together with their `.meta`, or references in scenes and prefabs break.
- Exclude `Library/` from searches: it contains the package cache (tens of thousands of files). Package sources under `Library/PackageCache/` are read-only.
