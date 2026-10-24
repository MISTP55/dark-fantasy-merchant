# Roads and town hall — design

## Intent

Today a building works as soon as it stands on the grid: a house gives its room, a player's workshop hires. This work makes a city a network: each city has a **town hall**, and a building works only when it is **connected** to it by **roads**. Roads are a new kind of building that both the player and the cities build; the player traces them with the mouse in the city view, the cities lay them when they build a house.

## Goals

### The town hall

- Every city has exactly one town hall, a building of **3×2** cells (placeable in either orientation, like the 2×1 houses).
- Nobody builds it during the game, neither the player nor the city: it is placed only in the City Layout window, and saved in the city's layout.
- It is part of the network: it is where every connection starts (see below).
- It has no room, no jobs and no cost.

### Roads

- A road covers **one cell**. It lodges nobody and produces nothing.
- A road costs **gold only** for now. Its cost is defined in a building asset like the others (gold and materials), so that materials can be added later through the usual rules (`BuildingDefinition.MaterialsOf`, switched-off materials left out).
- Roads are built by the player (in the city view), by the cities (when they build a house), and placed by hand in the City Layout window (the starting roads).

### Connection

- **Touching** is by side **or corner**: two cells touch when they are among each other's 8 neighbours. A building touches a cell when one of its cells does.
- The **network** of a city is the town hall and every road reachable from it by going from touching thing to touching thing (town hall to road, road to road, corners included).
- A house or workshop is **connected** when it touches a road of the network. A building that touches only the town hall and no road is **not** connected *(builder's call to revisit, see "Calls made")*.
- A building that is **not connected is inactive** and does not count at all:
  - an inactive **house** gives no room to its class;
  - an inactive **workshop** of the player does not hire (and, when the player's workshops produce, will not produce).
- The city's own workshops stay virtual (they do not stand on the grid) and are not concerned.
- There is no demolition in the game: a building, once connected, stays connected. A building is inactive from when it is placed until a road joins it to the network, and active from then on. When it becomes active, its effect starts: a house adds its room to its class then, a player's workshop starts hiring the next day.
- A city's starting room is its **connected** starting houses × the room of a house of the class. The content rule that the starting houses give at least `StartingRoomOf` now counts only the connected ones.

### What the cities build

- When a city builds a house (`CityMarket.BuildCityHouse`), it builds the roads that connect it, in the same day, at no cost (no gold, no material). The house is never left unconnected: when no place can be connected, the house is not built that day (nothing taken), as today when no place is found.
- Where it goes is the placement strategy's choice (`IPlacementStrategy`), which now returns a place **and the roads to lay**. The new strategy of this work, made the one in use: among every place where the footprint fits, those needing the **fewest new road cells** to touch the network (0 when the place already touches a road of the network); one of them drawn at random, seeded. The roads are the shortest path of free cells (8-neighbour moves) from the network to a cell touching the house. The random strategy without roads stays for what does not need connecting, or is removed if nothing uses it any more *(builder's call)*.

### What the player builds

- The build bar gets a third category, **Routes**, with one offer: the road, its cost per cell.
- The player places roads by **dragging**: press the left button on a cell, move, release. The trace goes from the pressed cell to the cell under the pointer, along x first then y (an L, the elbow at the end of the x leg). While dragging, the ghost shows the whole trace and its total cost.
- On release, the whole trace is built and **paid at once** (gold of the trace's new cells), through `PlayerBuilder` like the other buildings, with one notification. Cells of the trace that already hold a road are kept and not paid for. If any cell of the trace is off the grid, forbidden or covered by another building, or the gold is short, the trace is red and the release builds nothing.
- The player's road mode stays on after a trace, until cancelled (right button) or another offer is chosen, so that several traces can be drawn in a row.
- Since the left button both clicks and pans in the city view, the **middle button pans** the view, in every mode, and in road mode the left drag traces instead of panning *(builder's call)*.
- The player's houses and workshops can be placed anywhere they fit, connected or not; one placed unconnected is inactive until a road reaches it.

### Showing it

- In the city view: roads are flat tiles drawn on the ground, above the ground cells and below every building. The town hall has a 3×2 block of its own colour. An inactive house or workshop is drawn **darkened**, and turns to its normal colour when it becomes connected.
- The placement ghost in the city view says whether the building would work *(the user's decision)*: **green** when it can be built and its place is connected, **yellow** when it can be built but would be unconnected (inactive), **red** as today when it cannot be built.
- In the City Layout window: the town hall and the roads are drawn, and inactive starting houses are marked; the room lines ("whether the houses shelter the starting population") count only the connected houses.

### The City Layout window

- New tools: **Town hall** (places it, or moves it when the city already has one) and **Road** (paints road cells with the brush). The right click erases what the tool paints, as today.
- A button **"Town hall and roads"** places them automatically on the shown layout: when the layout has no town hall, it puts one on a free place as close as possible to the centre of its starting houses (of the grid when it has none); then it lays, for each starting house not yet connected, the shortest road path joining it to the network. A house that cannot be connected (walled in) stays as it is and the window reports it.
- The same automatic placement is run once on every existing layout as part of this work, and the scene setup runs it on every default layout it makes (the 40×40 grids of cities without a layout): it places the town hall first at the centre, then the starting houses with the road strategy, so that they come connected.

## Data

- `BuildingKind` gains two kinds: **road** and **town hall** (`IsHouse`/`IsWorkshop`-style tests; the accessors that throw keep throwing for the wrong kind).
- The building catalogue (`BuildingsDefinition`) holds exactly **one road** (`RoadDefinition`: name "Route", footprint 1×1, cost gold only) and **one town hall** (`TownHallDefinition`: name "Hôtel de ville", footprint 3×2, no cost); `WorldMapSetup.CompleteBuildings` creates them when missing, as it does the houses.
- `CityLayoutDefinition` gains the town hall (origin, orientation; may be absent) and the list of road cells. `TryCreateGrid` places the town hall, the roads, then the houses. A layout **without a town hall** is not usable: the city gets a market without grid and one warning, as for any unusable layout today.
- `WorldEconomy` exposes a `RoadOffer` alongside the house and workshop offers.

## Non-goals

- No demolition of roads or buildings.
- No upkeep, traffic, or walking distance: a building is connected or not.
- No real art: placeholder blocks and tiles, as for the other buildings.
- The AI merchants still build nothing.
- No road through forbidden cells (no bridges).

## Anti-goals

- A city losing its population at the start because its layout has no roads: every existing layout gets its town hall and roads.
- A city building a house it then leaves unconnected.
- A trace that half-builds: it is all or nothing.

## Calls made (mine, not the user's)

- The town hall alone does not connect a building touching it; only a road of the network does. *(Literal reading of "toucher à une route reliée à l'hôtel de ville".)*
- A building becomes active once connected and stays so (no demolition), its room added at that moment.
- The city's road strategy: fewest new road cells, then random; roads by shortest 8-neighbour path.
- The player's trace is an L (x then y); existing road cells in it are free; any blocked cell makes it impossible.
- Road mode stays on after a trace; the middle button pans.
- Inactive buildings are drawn darkened.
- The automatic placement's town hall goes near the centre of the starting houses.
- The roads of the player and of the city form one network: whoever built a road, it connects whatever touches it. A road on the grid is owned by whoever built it (the layout's and the city's are the city's); the owner changes nothing.
- The debug tools' free building makes roads free too.
- A road trace's ghost follows the same colours as a building's: yellow when the trace would not join the network, green when it would.
- The town hall gets a 3×2 placeholder block, and the `DrawKeyOf` brute-force tests are extended to 3×2 and 2×3.
