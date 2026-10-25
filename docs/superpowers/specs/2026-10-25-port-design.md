# Port — design

## Intent

Every city of the game is coastal: it trades by sea and by river. Its grid should say so. This work gives each city a **port**, a building that stands half on the water and half on the land, and makes the grid tell water from land. Like the town hall, the port is part of what a city is, not something anyone builds during the game: it is placed by hand in the City Layout window, and a city without one is not usable.

## Goals

### The port

- A building of **4×3** cells: 4 wide, 3 deep *(the user's decision)*.
- Every city has **exactly one** port. Neither the player nor the city builds it during the game: it is placed only in the City Layout window and saved in the city's layout *(the user's decision)*.
- A layout **without a port is not usable**, exactly like one without a town hall: `TryCreateGrid` fails, the city gets a market without grid and one warning at the start *(the user's decision)*.
- The port does **not** need a road to the town hall *(the user's decision)*. It is not part of the road network either: it is not a root of the network, and a road touching it is not a network road for that *(builder's proposal, accepted by the user)*. It is never darkened as an unconnected building.
- It has no room, no jobs, no cost, and no effect on the game for now: ships keep docking as they do today *(accepted by the user)*.

### Water and land

- The cells painted with the City Layout window's **Water** tool become real **water cells**; every other cell is **land** *(accepted by the user)*. The layouts' existing `forbiddenCells` are the water cells (there are none today in any layout).
- Only the port may cover water. Houses, workshops, roads and the town hall stay refused on water, as they are on forbidden cells today.
- The port's footprint is split by depth: its **two front rows** (2 rows of 4 cells) must all be **water**, its **back row** (1 row of 4 cells) must all be **land** *(the user's decision)*. Any other cell under it refuses the place. Like every building, it must also be inside the grid and on no other building.

### Facing

- The port has a **facing**: the side its water rows are on, one of **north, east, south, west** (north = +y, east = +x, the layout window's directions).
- Facing north or south, the port lies 4 on x and 3 on y (`Along`); facing east or west, 3 on x and 4 on y (`Across`). Facing north, the water rows are the two of largest y; facing south, the two of smallest y; east, the two of largest x; west, the two of smallest x.
- The facing is part of the ghost and turns with it *(the user's decision)*: in the City Layout window, **R** turns the port's ghost a quarter turn, north → east → south → west → north; two presses (180°) take water rows that were on the north to the south.
- The other buildings keep their two orientations (`Along`, `Across`); nothing changes for them.

### The City Layout window

- A new tool, **Port**: its ghost shows the 4×3 footprint at the facing chosen, its water rows drawn distinctly from its land row (so the facing reads at a glance), green where it can stand and red where it cannot. A left click places the port, or moves it when the layout already has one; a right click on it removes it, as the town hall tool does.
- The port is drawn on the grid with its own colour, its water side visible.
- The window's existing "something cannot stand" error covers a port that cannot stand. A layout without a port gets a warning like the one shown for a layout without a town hall.
- Painting or erasing water under the port is refused, as painting water under a house is today, so the port never ends up standing wrong through the brush *(builder's call)*. Resizing drops a port that falls outside, as it drops the town hall.
- "Town hall and roads" does not place a port: water must be painted first, which only the designer can decide.

### The city view

- Water cells are drawn as the forbidden cells are today.
- The port is drawn with a **placeholder** (no art yet), in a colour of its own, whose water side can be told from its land side, so that its facing reads in the view *(builder's proposal, accepted by the user; how is the builder's call: e.g. a block on the land row and flat tiles on the water rows, or one sprite per facing)*.
- The drawing order is checked for the port's sizes: `IsoProjection.DrawKeyOf`'s brute-force tests are extended to 4×3 and 3×4.
- The port is not in the build bar. It is never shown with the placement ghost of the city view, since nobody places it there.

### The scene setup

- A city the scene setup gives a default layout (a city without a layout asset) gets a band of water along one edge of its 40×40 grid and its port on that band, facing the water, along with its town hall, starting houses and roads as today, so that it comes usable *(accepted by the user; band width and edge are the builder's call)*.
- The setup creates the port's asset when the catalogue has none, as it does the town hall.
- The 24 existing layouts are **not** migrated: they stay refused until the designer paints their water and places their port by hand *(the user's decision)*.

## Data

- `BuildingKind` gains a **port** kind (`IsPort`), like the town hall. `CityGrid` gains `Port` (null when none) and refuses a second one, as it does a second town hall.
- `CityGrid` learns water: the cells given at its creation as forbidden become its water cells (`IsWater`, or `IsForbidden` kept with that meaning: builder's call). `CanPlace` stays as it is for everything but the port: no cell on water. The port is checked and placed through its own entry point that takes its facing and applies the water/land split *(builder's call on the API; the grid still knows footprints and places, never who builds)*.
- A new enum for the four facings; the port's place is its origin (covered cell of smallest x and y, as for every building) and its facing, from which its `BuildingOrientation` follows.
- The building catalogue (`BuildingsDefinition`) holds exactly **one port**: a `PortDefinition` (name "Port", footprint 4×3, no cost), validated like the town hall (`TryValidate` fails without one). The number of water rows is a value of the asset, **2**, validated between 1 and the depth minus 1 *(builder's call: values belong in the assets)*. `DefinitionOf` returns it for the port kind.
- `CityLayoutDefinition` gains the port (present or not; origin; facing). `TryCreateGrid` places the town hall, then the port, then the roads, then the houses, and fails with a problem naming the cause when the port is missing or cannot stand.

## Non-goals

- No gameplay for the port: no docks, no capacity, no link with the ships of the world map.
- No real art for the port, nor support for a `viewSprite` on it.
- No other building on water (no bridges, no piers built by the player).
- No change to the orientations of the other buildings.
- No automatic placement of water or ports in the existing layouts.

## Anti-goals

- A port standing with a land cell in its water rows, or a water cell in its land row, in any facing.
- A facing that changes when the ghost is placed: what the ghost shows is what is saved.
- A house, workshop, road or town hall on water.
- The port changing which houses are connected.

## Tests

- Grid: the port's place is refused unless its two front rows are water and its back row land, for each of the four facings; a second port is refused; the port is not a root of the network and a road touching only it is not a network road.
- Layout: `TryCreateGrid` fails without a port, and with a port that cannot stand; it places the port with its facing.
- Editing: placing, moving and removing the port; water brush refused under it; resize drops it when outside.
- Drawing order: 4×3 and 3×4 added to the brute-force tests.
- Content: the content tests that require every active city to have a usable grid (`EveryActiveCity_HasAUsableGrid`, `EveryActiveCity_HousesItsStartingPopulation`, `EveryStartingHouse_IsConnected`) would fail while the existing layouts have no port. They are kept unchanged but marked `[Ignore]`, with the reason (the layouts await their water and port), together with a new `EveryLayout_HasAPort` on the model of `EveryLayout_HasATownHall`, also ignored. They are switched back on once every port is placed, which is not part of this work *(the user's decision)*. The suite stays green.

## Calls made

My calls, to check while reading:

- Painting or erasing water under the port is refused.
- The number of water rows (2) is a value of the port's asset, validated.
- The water cells are the layouts' existing `forbiddenCells`; whether the code renames them is left to the builder.
- The default layouts of the scene setup get a band of water and a port, so that a new city comes usable.
- How the placeholder shows the water side is left to the builder.
