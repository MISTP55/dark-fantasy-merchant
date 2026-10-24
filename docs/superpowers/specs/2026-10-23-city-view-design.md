# City view — design

## Intent

Today a city's buildings are numbers: `CityMarket` knows the room of each class and counts the player's houses and workshops, and the player builds from the Buildings tab of the city panel without choosing where. This work gives each city a **place**: an isometric grid on which its houses stand, and a **city view**, a screen of its own where the player sees the city and places buildings on the grid, as in a classic city builder.

The world keeps running while the player is in a city view: the clock, the economy, the AI merchants and the ships go on, and the houses the city builds appear on its grid as they are built.

This is the first step of the city view. It lays the ground (the grid, its data, the placement rules, the screen, the Editor tool to design a city) on which later work adds real art, more building types and the AI merchants' constructions.

## Goals

### The city's grid

- Every city has a grid of cells, of a size set per city. Some cells are **forbidden** (water, streets, anything that cannot be built on), also set per city.
- A building covers a rectangle of cells, its **footprint**:
  - every house (poor, wealthy, rich) is **2×1**;
  - every workshop is **2×2**.
- A 2×1 footprint can be placed in either orientation (2 wide and 1 deep, or 1 wide and 2 deep).
- A building can only stand on cells that are inside the grid, not forbidden and not covered by another building.
- Each building on the grid records its kind (a house of a class, or a workshop of a good), its owner (the city or the player), its cells and its orientation.

### What stands on the grid

- **The city's starting houses** are placed by hand, per city, with an Editor tool (below), and saved in the city's data. They **are** the city's starting room: a class's starting room is the sum of the rooms of its starting houses (the room of a house of the class, from the buildings catalogue). This replaces the room the city gets today at the start from its population (`CityPopulation.AtStart`, rounded up to whole houses).
- **The starting population does not change**: it is still worked out from the city's workers, as today. The starting houses must give each class at least the room that population needs, which is the room `CityPopulation.AtStart` gives today (a whole number of houses). A city whose starting houses give a class less is an error of content: the content tests fail on it, and the Editor tool shows it. More room than needed is allowed (the beggars fill it as they are hired).
- **The city's workshops stay virtual**: they exist in the simulation only (jobs, production) and never stand on the grid.
- **The houses the city builds during the game** (`CityMarket.BuildCityHouses`, one per crowded class a day at most) are placed on the grid by a **placement strategy** (below). When the strategy finds no free place, the city **does not build** the house that day: no room added, no material taken. A full grid stops the city's growth.
- **The player's houses and workshops** are placed by the player, in the city view.
- **The AI merchants' buildings**, when they build some (they do not yet), will be placed by the same placement strategy. Nothing of their construction is part of this work, but the strategy is not written for houses or for the city alone: it is given a footprint and the grid, whoever builds.

### Placement strategies

- Where the city (and later the AI merchants) builds is decided by a **placement strategy**, behind one interface, so that several algorithms can exist side by side.
- The strategy used is **chosen in the Unity Editor**, and switching from one to another must be easy: one asset per strategy, the one in use referenced from one place, so that switching is dragging another asset into a field.
- The first and only strategy of this work: **a random free place**. Among every position (and orientation, for a 2×1) where the footprint fits, one is drawn at random.
- The randomness is seeded, so that a test can expect a given place.

### The city view

- The city panel gets a button that opens the **city view** of its city. The city view has a **back** button that returns to the world map.
- The view shows the city's grid in **2D isometric**, with **placeholder shapes**: the cells as flat diamonds, forbidden cells in another colour, buildings as simple blocks over their footprint. The player's buildings are told apart from the city's by their colour.
- The date with fast forward (time HUD) and the treasury (treasury HUD) stay at the top of the screen, as on the map. Notifications keep showing.
- A **build bar** at the bottom of the screen: one button per category (**Maisons**, **Ateliers**). A category opens a row of its buildings above the bar, each with its name and its cost, in the alphabetical order of the names.
- Choosing a building starts placing it: a ghost of its footprint follows the pointer over the grid, **green** where it can stand, **red** where it cannot. **R** turns it (for a 2×1). **Left click** on a green place builds it there; **right click** cancels the placing.
- Building from the city view follows today's rules (`Construction`): gold from the treasury, materials first from the hold of the player's ship in that port when there is one, then bought from the city's market; a building that cannot be paid for cannot be chosen. A switched-off material is left out (`BuildingDefinition.MaterialsOf`). The player gets the same notification as today.
- The view's camera can be moved and zoomed over the grid.

### Fast forward and the city view

- Fast forward is still a mode in which the **world map** is only watched. Starting fast forward from the city view (its button in the time HUD) **closes the city view and opens the world map**, where fast forward runs as it does today.
- When fast forward ends because the ship selected and under way when it started arrives in a port, the game **opens the city view of the city it arrives in**, instead of showing a close-up of the ship on the map. This holds however fast forward was started, from the map or from a city view.
- When fast forward ends otherwise (an input of the player), it ends as today, on the map, with the previous selection restored.
- The houses the city builds while the view is open appear on the grid as they are built.

### The Editor tool

- A tool in the Unity Editor designs a city's grid, outside the game: its size, its forbidden cells and its starting houses (each with its class, its place and its orientation).
- It shows, for each class, the room the placed houses give against the room the city's starting population needs, and how many houses of the class that makes, so the designer sees when a city is short of houses.

### Default grids

- None of the 24 active cities has a grid yet. The scene setup (`WorldMapSetup.Build`) gives every city that has none a **default grid**: a fixed size, no forbidden cell, and just enough starting houses of each class for its starting population, placed at random free places by the same placement code as the city's houses. A city that already has a grid is never touched, as the setup does with the cities' productions. The designer then reworks each city in the Editor tool when they wish.

### What goes away

- The Buildings tab of the city panel: building is done only from the city view.

## Non-goals

- Real art. Placeholder shapes only; no isometric sprites are made or imported.
- Demolition, moving a building, upgrading one.
- Clicking a building to see what it is or holds.
- Roads, connectivity, distances, zoning or any effect of a building's place on the simulation. Where a building stands changes nothing but which cells are free.
- Public buildings, walls, decorations.
- The AI merchants' constructions.
- Placement strategies other than the random one.
- Saving and loading a game (there is none yet).

## Anti-goals

- A city that adds room it has no place for. If no place is free, the house is not built.
- A grid that the simulation does not trust: the room of a class is always the sum of the rooms of the houses of that class on the grid, never a number that drifts apart from it.
- Placement code that knows who builds. The strategy places a footprint on a grid; it does not know of cities, merchants or classes.
- A placement strategy chosen by code or by a hidden constant. It is chosen in the Editor.
- Map input reaching through the city view: while the city view is open, nothing of the world map reacts to the pointer or the keyboard (no picking, no panning, no city or ship selection, no right-click order).

## Constraints

- The project's conventions apply (`CLAUDE.md`): Core in plain C# covered by EditMode tests, Game as thin adapters, ScriptableObjects for static definitions only, UI Toolkit for every runtime UI, French for what the player reads, English for the code, alphabetical lists with `NameOrder`, striped lists, the new Input System only.
- The grid, its occupancy, the footprints, the placement rules and the placement strategies are Core logic, with EditMode tests. The city's building of houses goes through the grid in Core.
- The city view's `UIDocument`s share the game's one `PanelSettings`.
- Whatever the city view needs in the scene (objects, references, data assets) is created by the scene setup, so that an existing project gets it by running `Tools > Dark Fantasy Merchant > Build World Map Scene` (or a setup of its own for a scene of its own).
- Every active city must have a usable grid. A city whose grid is missing or unusable (no starting house, for example) is reported by the content tests, as the other content rules are.

## Decided by the user

- 2D isometric grid, a build bar at the bottom with categories (layout "A" of `2026-10-23-city-view-mockups/city-view-layout.html`; `city-view-a-v2.html` is the screen as agreed).
- Placeholder shapes for now.
- Houses of every class 2×1, workshops 2×2; R turns a building while placing it.
- City workshops are virtual and never on the grid.
- Starting houses placed per city with an Editor tool, which also sets the grid's size and forbidden cells; they are the city's starting room.
- City (and later AI merchant) buildings placed by a strategy, chosen in the Editor; the first one draws a random free place.
- No free place: the city does not build.
- The starting population stays as today; the starting houses must give at least its room, checked by the content tests and shown by the Editor tool.
- The scene setup gives every city without a grid a default one.
- Fast forward started from the city view closes it and opens the map; the awaited ship's arrival opens the city view of its port instead of the close-up on the map.
- Entering by a button in the city panel; leaving by a back button.
- The Buildings tab of the city panel goes away.

## Calls made by Claude (to check)

- **A scene of its own.** The city view is a scene, `CityView.unity`, loaded additively over `WorldMap.unity`, which stays loaded so the simulation runs on. The city view reaches the simulation through `SimulationServices.Current`. While it is open, the world map's camera, input, picking and panels are switched off, and switched back on when it closes. Only one `AudioListener` and one global 2D light are active at a time.
- **Camera of the city view:** the same gestures as on the map (drag to pan, wheel to zoom), within the grid.
- **Placing:** green/red ghost, left click builds, right click cancels, as listed above. After a building is built, the player stays in placing mode with the same building, until a right click or another choice. *(Builder's call if this proves awkward.)*
- **Colours:** the player's buildings in blue, the city's in brown, forbidden cells in dark blue.
- **Workshop list:** the build bar's Ateliers category lists the player's workshops as the Buildings tab lists them today (`WorldEconomy.WorkshopOffers`); a workshop of a good the city does not produce cannot be chosen, as today.

## Left to the builder

- The data model of a city's grid: a field of `CityDefinition` or an asset of its own per city, and how cells, forbidden cells and starting houses are stored.
- How the placement strategy is chosen in the Editor (a field on the economy, on the buildings catalogue, or on an asset of its own) and how a strategy asset makes its Core strategy.
- How `CityMarket` gets its grid and its strategy, and how the room of a class is kept equal to the houses on the grid.
- The Editor tool's form (an EditorWindow, a Scene view tool like `CityPlacementTool`, or a custom inspector) and how it draws the grid.
- How the isometric grid is drawn at runtime (Unity's `Grid` in isometric layout with a `Tilemap`, generated meshes, sprites) and how the pointer is turned into a cell.
- The city view's input: a new action map beside `WorldMap` in `Assets/InputSystem_Actions.inputactions`, its bindings (R included).
- The size of a default grid, and whether it depends on the city's size (`CitySize`).
- Where fast forward's arbitration lives once the city view exists (it is in `WorldMapInteraction` today), and how the city view is opened at the end of fast forward.
