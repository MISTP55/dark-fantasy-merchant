# City walls — design

## Intent

A city's grid has no limit today but its edge: the player and the city build wherever a footprint fits. This work gives each city **walls**, which close in the space where anything may be built. They serve two purposes *(the user's words)*:

- they **restrict where one can build**; a later mechanic will let a city build larger walls, and so grow;
- they will **protect the city against sieges**, which do not exist yet.

For now, walls only bound the buildable space. Like the town hall and the port, they are part of what a city is, not something anyone builds during the game.

## Goals

### The wall

- A new building kind, the **wall**: a **1×1** footprint *(the user's decision)*.
- It is **2 cells high** *(the user's decision)*: this is its drawing only, a block twice as tall as a cell is wide, with no effect on the rules *(accepted by the user)*.
- Its placeholder is **dark grey** *(the user's decision)*.
- **Neither the player nor the cities build walls** *(the user's decision)*: they are not in the build bar, no strategy places them. They come only from the layout: hand-painted in the City Layout window, or generated.
- A wall has no room, no jobs, no cost. It is not a road, not part of the road network, never connected nor darkened.

### The enclosure

- The **enclosure** of a grid is the set of land cells reached from the town hall by stepping from cell to cell **by sides only**, never onto a wall nor onto water; cells under other buildings are crossed *(accepted by the user)*. Two walls touching only by a corner therefore close the way.
- **Water counts as a wall** for the enclosure, since it cannot be crossed *(the user's decision)*. So the generated walls are only on land, and on the shore side the water closes the enclosure *(accepted by the user)*.
- If the flood reaches a cell on the **edge of the grid**, the enclosure is **open**: the city has no closed walls *(accepted by the user)*.
- A grid is **usable** only when its enclosure is closed and **every building other than the walls stands entirely inside it** *(the user's decision)*: houses, workshops, roads and the town hall on enclosed cells; for the port, its land rows (its water rows are water, not part of any enclosure). Otherwise `TryCreateGrid` fails with a problem naming the cause, and the city is unusable as for a missing town hall (`WorldMapView.IsUsable`). Berths and ships are not concerned.

### Building inside the walls

- During the game, **nothing is built outside the enclosure** *(accepted by the user)*:
  - the player's ghost in the city view is **red** over a place with a cell outside the enclosure, as where the footprint does not fit; roads traced across the wall are impossible (red);
  - the cities place their houses and the roads that join them only inside (`RoadsidePlacement`, `RoadPlanner`); a city whose enclosure is full simply stops building, as it does today when no place can be connected.
- *Builder's call on where:* the simplest is that `CityGrid.CanPlace` (and whatever free-cell test `RoadPlanner` uses) refuses cells outside the enclosure, so every caller gets the rule from one place. The grid computes the enclosure itself (from its walls, water and town hall) and keeps it up to date when a wall is placed or removed; it never changes during a game today.
- The refusal applies to the game's placement only: the City Layout window (`CityLayoutEditing.TryAddHouse`, `TryMakeGrid`) and `TryCreateGrid` place without it, so that the window can hold a building outside the walls and `TryCreateGrid` reports "a building is outside the walls" rather than a vague "cannot stand". The enclosure is still computed in one place, the grid. Walls themselves are never refused for being outside the enclosure, and a grid with no town hall has no enclosure *(builder's call, after review)*.
- `CityMarket.TryCollectNewRoads` has its own free-cell test: it must refuse the outside too.

### Generation

- `StartingLayout.TryBuild` places, after the town hall and before the houses, walls on the outline of the **square of 45 cells of side centred on the town hall** *(the user's decision)*, one cell more on the far side when it cannot be exactly centred (as `HouseArea` does). A wall goes on every cell of that outline that is inside the grid, land and free; none on water *(accepted by the user)*. The inside of the square is 43×43; the houses' random area (`GeneratedHouseArea`, 40) stays inside it.
- The side of the square is a constant next to the other generation values (`GeneratedWallSide` = 45 in `CityLayoutEditing`, passed to `TryBuild`) *(builder's call)*.
- The starting houses and their roads must all stand inside the enclosure: `RoadsidePlacement`'s second pass places them inside only. If they do not fit, or the enclosure is open (the square leaves the grid on a land side, e.g. on a small hand-sized layout), generation **fails** with a problem saying so *(accepted by the user)*.
- On today's 80×80 grids, the town hall sits 16 land rows from a 12-row band of water, so the square's shore side falls in the water and the three others on land: the enclosure is closed.
- Since what makes a layout usable changes, **every city's layout is regenerated** in the same work (`Regenerate All City Layouts`), as the city-view rule demands. `CityViewContentTests` keeps checking every layout is usable, under both contacts.

### The City Layout window

- A new tool, **Wall** *(the user's decision)*: a brush like Water; the left click paints walls, the right click erases them. The Erase tool erases walls too.
- Painting a wall is refused on water, under the port, under any other building and on a berth; painting water over a wall is refused as it is under a house *(builder's call: a cell is one thing at a time)*. Removing a wall is always allowed.
- Walls are drawn in the window in a dark grey of their own.
- Hand-placing a house, road or town hall outside the enclosure is **allowed** in the window, which then shows its usual "unusable" warning with the problem (open enclosure, or a building outside the walls) *(builder's call: the designer may paint walls after the buildings, so the window must not refuse a state it will pass through)*. Equally, painting or erasing a wall that leaves buildings outside only brings the warning back.
- Resizing drops the walls that fall outside, as it drops roads. Every edit goes through `CityLayoutEditing.Apply`, as today.

### The city view

- Walls are drawn as buildings, ranked by `BuildingDrawOrder` like any 1×1 block (the 1×1 drawing order is already covered by the brute-force tests).
- A new placeholder sprite (`WallBlock.png` or similar, made by `CityViewSetup`): a 1×1 cell raised to 2 cells high, its pivot at the centre of the cell on the ground, following the projection, tinted by a `wallColor` (dark grey) of `CityGridView`, scaled by `buildingScale` like the other blocks *(builder's call on the exact name and the scale)*.
- Never darkened, never selectable, never with a ghost.

## Data

- `BuildingKind` gains a **wall** kind (`Wall()`, `IsWall`).
- `CityGrid` places walls through `Place` (1×1, owner city), refused on water like every building but the port. It exposes the enclosure (e.g. `IsEnclosed(cell)`, `IsClosed`) and the check `TryCreateGrid` needs *(builder's call on the API)*.
- The building catalogue (`BuildingsDefinition`) holds exactly **one wall**: a `WallDefinition` (name "Mur", footprint 1×1, no cost), validated like the town hall; `DefinitionOf` returns it for the wall kind. The scene setup creates its asset when the catalogue has none.
- `CityLayoutDefinition` gains its **walls** (a list of cells). `TryCreateGrid` places the walls first among the buildings (before the town hall, or right after it: builder's call, as long as the houses and roads are placed knowing the enclosure), and fails when a wall cannot stand, when the enclosure is open, or when a building is outside it. `SetContent` takes them.

## Non-goals

- Building or enlarging walls during the game (the later mechanic).
- Sieges, wall strength, gates, roads crossing the walls.
- Any effect of walls on ships, berths or the harbour.
- Final art.

## Anti-goals

- A city that builds, or lets the player build, outside its walls.
- A layout left unusable by the new rule for the designer to fix by hand: the regeneration fixes them all.
- A switched check: the rule of the enclosure computed in one place and copied in another (the grid's placement and `TryCreateGrid` must use the same enclosure).

## Left to the builder

The API of the enclosure on `CityGrid`, where exactly placement refuses the outside, the order inside `TryCreateGrid`, the sprite's name and drawing details, the window's wall colour, and the tests (enclosure flood, open enclosure, building outside, generation of the square, refusal of the ghost and of the cities' placement outside).
