# Harbour traffic — design

## Intent

Until now a ship that reaches a city vanishes from the map into its port at once, and leaves it at once on its next order. This work gives the arrival and the departure a body: the ship is seen on the city's grid, coming in from the edge of the water, sailing to a free **berth**, lying there, then sailing back to the edge when it leaves. A city can hold only as many ships as it has berths; the others wait their turn.

This crossing is **simulation, not decoration**: it takes time, for every ship (the player's and the AI's) in every city, whether the city view is open or not. The view only shows what is happening. Otherwise a ship would take more or less time to leave depending on whether the player looks at its city.

## Goals

### Berths

- A new element of the layout, the **berth**: one cell (1×1), on a **water** cell, not under the port *(the user's decision)*.
- It is placed **only in the City Layout window**, never during the game, by nobody in the game *(the user's decision)*.
- A city's **capacity** is its number of berths: at most that many ships are in the city at once *(the user's decision)*.
- Berths are **not drawn in the city view**; they are seen only in the City Layout window *(the user's decision)*.
- Ships pass through berths (an empty one, or one another ship holds) like any other water cell.

### A usable layout

On top of what is already required (town hall, port that can stand, etc.), a layout is usable only when:

- it has **at least one berth** *(the user's decision)*;
- the **port is joined to the edge of the grid by water**: some water cell beside the port's water side is reachable from a water cell on the edge of the grid *(the user's decision)*;
- **every berth is reachable** from a water cell on the edge of the grid *(builder's proposal, accepted by the user)*;
- every berth is inside the grid, on water, not under the port, and no two berths share a cell (the builder's call, for a well-formed layout).

`TryCreateGrid` (or what replaces it) fails with a problem naming the cause, as it does today.

### A city without a usable layout is inactive

- A city whose layout is missing or unusable is treated **exactly as a city switched off in the `WorldMap` asset** (`MapCity.Active` false): it does not exist in the game. It is not on the map, has no market, the AI ignores it, the player cannot start there *(the user's decision)*. One warning names the city and the problem at the start.
- This replaces today's fallback of a city "with a market without grid": there is no city without a grid any more.
- `CityViewContentTests` keeps checking that every active city of the map has a usable layout, now with the new conditions.

### Generated berths

- When a layout is (re)generated (`CityLayoutEditing.TryGenerate`, used by "Regenerate layout" and by the scene setup's default layout), it gets **10 berths around the port, on water cells** *(the user's decision)*. The number 10 is a value of an asset, not a constant in the code (builder's call on which asset: e.g. `BuildingsDefinition` next to `GeneratedWaterRows`).
- **When there are not enough suitable water cells, the generator places as many as it can and stops there**; generation does not fail for it *(the user's decision)*. It fails (as it does when the port does not fit) only when it cannot place a single berth or the port is not joined to the edge, since the layout would be unusable.
- The generator places only berths that are reachable from the edge. Which cells "around the port" means is the builder's call; a sensible default: water cells not under the port, the nearest to the port's water side first (by path length from it), ties broken in a fixed order so that a seed always gives the same layout.

### The City Layout window

- A new tool, **Berth** ("Berth" in the toolbar, like the window's other English labels): a left click on a water cell not under the port and without a berth places one; a right click on a berth removes it. The ghost is green where a berth can go, red elsewhere.
- Berths are drawn on the grid in a colour of their own.
- The window shows the number of berths, and warns, as it does for a missing town hall or port, when the layout has no berth, when the port is not joined to the edge by water, and when a berth cannot be reached from the edge.
- Edits must not leave a berth standing wrong: painting land over a berth's cell removes the berth (or is refused, the builder's call, consistent with how the brush treats the port today); placing or moving the port over berths removes them; resizing drops the berths that fall outside. All in the same undo step as the edit.

### A ship's way through a city

States of a ship relative to a city (names are the builder's call):

1. **At sea**: on the world map, as today.
2. **Waiting** (in the city's queue): it has reached the city's anchorage on the map and all the berths are taken. It is **not on the world map** (like a docked ship today) and not on the grid. It is **selectable from the city panel's ships tab**, and the player **can give it another order**: it then leaves the queue and sails from the anchorage on the map at once *(the user's decision)*. The queue is first come, first served.
3. **Arriving**: on the grid. On entering the city it **takes (reserves) a free berth** and appears on the edge of the grid, on a water cell, then sails to its berth. Not on the world map.
4. **Docked**: at its berth. **Only now is it in port**: it can trade, the AI's stopover starts, `ShipDocking.Docked` is raised, the arrival is reported *(the user's decision)*.
5. **Leaving**: on its next order (any order other than one to this same city), it sails from its berth back to the edge of the grid. It is no longer in port: `Undocked` is raised when it starts leaving (it can no longer trade). Not on the world map.
6. When it reaches the edge, it **reappears on the world map at the city's anchorage** and carries out its order *(the user's decision)*: the departure is no longer instant.

- **The berth stays reserved for the ship from the moment it enters the grid until it leaves it by the edge**, on the way in as on the way out *(the user's decision)*. A waiting ship enters only when a berth is freed, that is when a ship leaves the grid.
- A ship of the player ordered elsewhere while **arriving** turns round, sails back to the edge, and only then reappears on the map and follows its order *(builder's proposal, accepted by the user)*. It never docked: no `Docked`, no `Undocked`.
- A ship ordered again while **leaving** keeps leaving and follows the latest order when it reaches the edge *(builder's proposal, accepted)*. An order back to this same city while arriving, docked or leaving: the builder's call, sensible default: arriving or docked, nothing changes; leaving, it turns round to its berth (still reserved) and docks again.
- **Edge and route** *(builder's proposal, accepted)*: the ship enters by the water cell on the edge of the grid that gives the shortest path to its berth, and leaves by the same way (the edge cell nearest its berth by path). Which free berth it reserves: the builder's call, sensible default the free berth with the shortest path from the edge, ties in a fixed order.
- **Speed**: a fixed **2 cells per second at normal speed**, that is 60 cells per game day with a day of 30 s *(the user's decision)*. It is a value of an asset (builder's call which, e.g. the ship definition or a harbour asset), expressed in game time so that it follows the clock: faster in fast forward, advanced by `GameClock.Stepped` like every simulated thing, never per frame. The crew's `SpeedFactor` does not apply in the harbour (builder's call: the user asked for a fixed speed; a ship without sailors still crosses the harbour).
- **Pathfinding**: an **A\*** over the city grid's cells *(the user's decision)*. Passable cells: water cells not under the port. Land, the port and every building are impassable. Berths and other ships are passable: **ships pass through each other** *(the user's decision)*. Moves in 8 directions without cutting a corner between two impassable cells (builder's call: natural-looking paths on a grid); the path is followed through cell centres at constant speed.
- This all lives in **Core** (plain C#, EditMode-tested): the berths and their reservations, the queue, the ships' positions on the grid and their movement. `ShipsView` (or a sibling the builder chooses) advances it at each step with the ships, before the docking's update.

### What the rest of the game sees

- `ShipDocking` keeps its meaning: a ship **in port** is a docked ship at its berth. Every current listener of `Docked`/`Undocked` (AI fleet, arrivals, build bar, city panel, interaction) keeps working on that meaning. A ship that is waiting, arriving or leaving is neither in port nor on the map.
- **On the map**: a waiting, arriving or leaving ship is not shown, cannot be hovered nor picked (its `ShipWorldPositions` entry is NaN), and is deselected when it leaves the map, as a docked ship is today.
- **The city panel's ships tab** lists every ship that is in the city — waiting, arriving, docked or leaving — each with its state ("à quai", "en attente", "arrive", "repart"), in alphabetical order of their names as every list *(builder's proposal, accepted)*. Each is selectable as a docked ship is today.
- **Trading** (market tab, build bar's hold) only with docked ships, as today.
- **The AI**: a ship's stopover starts when it docks. Its departure order is given while it is docked, as today; it is carried out when the ship reaches the edge. The cached route starts on the anchorage, where the ship reappears, so it still applies. The travel time table does not count the harbour crossing (builder's call: acceptable for now; noted as a known gap).
- **Arrival notifications** for the player's ships: when the ship docks at its berth. Entering the queue gets a notification too, in French ("<navire> attend une place au port de <ville>"; the builder's call on the wording).
- **Fast forward** awaiting the player's ship *(the user's decision)*: it ends when the ship **docks at its berth**, and the city view of that city opens, as today when the ship docks. If the ship **enters the queue**, fast forward ends and **the city's panel opens** (the city selected on the map) instead. Entering the grid (arriving) does not end it. A fast forward started on a player's ship that is leaving (selected from the city panel, with an order) or turning round awaits it to the end of its order: the end of the order, not "not moving", is the arrival, since a ship lying still at the anchorage while it waits or crosses the harbour has not arrived (builder's call).
- **Debug tools**: the ships table shows the new states.

### The city view

- Ships on the grid (arriving, docked, leaving) are drawn as **cylinders a little smaller than a cell**, like buildings are smaller than their footprint *(the user's decision)*. Placeholder sprite made by `CityViewSetup` like the other placeholders, following the projection; the player's ships told apart from the AI's by tint (builder's call, matching the map's tints).
- They move smoothly between cells; drawn in the right order against the port and the other ships (their sortingOrder follows `IsoProjection.DrawKeyOf` of where they are, updated as they move: the only things of the view that are re-ranked while it is open).
- The view reads the harbour's state through `SimulationServices.Current` and shows the ships already on the grid when it opens.
- **Ships are not controlled in the view** *(the user's decision)*: no orders from it.
- **Selecting a ship**: a click on a ship shows its information in a ship panel in the view, like the map's ship panel (same content, same `InfoPanel.uss` styles, same shared `PanelSettings`) *(the user's decision)*. A click elsewhere, or on the panel's close button, closes it. A ship that leaves the grid while selected closes the panel. While the player is placing a building or tracing a road, clicks place and do not select (builder's call).

## Non-goals

- No control of ships in the city view.
- No collision between ships, no avoidance, no waiting for a cell.
- No harbour fees, no limit on ship size per berth, no berth types.
- No new art: placeholders only.
- No change to the world map's navigation.

## Anti-goals

- A crossing that only runs while the view is open, or that differs with the view open or closed.
- A ship that can trade, or whose AI stopover counts, before it lies at its berth.
- A city that half-exists (on the map without a usable grid).
- Berths visible in the game.

## Constraints

- Project conventions of `CLAUDE.md` and the rules (`ships.md`, `city-view.md`, `time.md`, `ai-merchants.md`): Core vs Game split, assets for values, French player text, alphabetical lists, striped lists, UI Toolkit, scene setup creating whatever is missing (the view's ship panel and placeholder sprite included).
- Every simulated thing advances on `GameClock.Stepped`.
- No mention of the game this one draws on.

## Existing layouts

The 24 existing layouts have no berth: with this work they would all become unusable, and their cities inactive. **Every city's layout is regenerated** (`CityLayoutEditing.TryGenerate`, seed the city's index in the map, as "Regenerate layout" does) as part of this work, hand edits included; the existing layouts are not patched *(the user's decision, now a standing rule in `city-view.md`: a change to what makes a layout usable always comes with the regeneration of every city)*. After it, `CityViewContentTests` passes and every city of the map is active.

## Left to the builder

The class split and names in Core, the A\* implementation (its own, on the city grid; the map's `NavigationPathfinder` is a different grid), the storage of berths in `CityLayoutDefinition`, where the harbour state is held (per city, next to the market's grid or in a world-level object), the exact cylinder sprite, and everything marked as the builder's call above.
