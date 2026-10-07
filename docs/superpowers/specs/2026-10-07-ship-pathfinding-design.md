# Ship Pathfinding — Design

Date: 2026-10-07
Status: awaiting review

## Purpose

Make ships sail only on the navigable areas of the map. A right click still
sends the selected ship to the clicked point, but the ship now follows the
shortest route over water, around land and along rivers, instead of a straight
line across everything.

This is the first reader of the navigation mask in the running game.

## Scope

In scope:

- Finding a route between two map positions over the navigable cells of a
  `NavigationGrid`, from `Core`.
- A ship that follows a route of several legs at constant speed, turning its
  sprite at each leg.
- Orders to a point that is on land or cannot be reached.
- A ship that starts on water, although cities are on land.

Out of scope:

- Showing the route or the destination on the map.
- Keeping a distance from the coast: a ship may sail along the shoreline, and
  its sprite may overlap land.
- Docking in a city, and any link between a city and its harbour cell.
- Kinds of water, sailing costs that vary by area, currents, hazards.
- Searching over several frames or on another thread.
- A mask that changes while the game runs.

## Decisions

Agreed with the user before writing this spec:

| Topic | Decision |
|---|---|
| Order on land or on unreachable water | The ship sails to the nearest navigable cell it can reach. |
| Route shape | Straight legs at any angle: grid search, then removal of the waypoints a straight line can skip. |
| Route display | Not in this delivery. `Ship` exposes its remaining waypoints so it can be added later. |
| Algorithm | A\* on the mask's cells, smoothed by line of sight. |

## Facts about the current content

Measured on `Assets/Data/WorldMap/NavigationMask.asset` and the seven cities:

- The grid is 1024 × 879 cells, 900 096 in all, of which 403 901 (45 %) are
  navigable.
- The navigable cells form five separate regions of 341 056, 57 991, 2 800,
  1 794 and 260 cells. There are no stray cells.
- Every city is on land, 2 to 31 cells from the nearest navigable cell, which
  is in the largest region each time.

So the player ship currently starts on land, and an order can target water the
ship has no way to reach.

## Constraints

- Conventions from `CLAUDE.md` apply: English code, simulation logic in plain
  C# in `Core` covered by EditMode tests, MonoBehaviours as thin adapters,
  mutable state never written back into assets.
- Map positions stay **normalized** and go to world space only through
  `MapProjection`.
- A map without a navigation mask stays valid: its ships sail in a straight
  line, as they do today.
- Ship speed stays in world units per second, the same in every direction.

## Approach

A new `Core` class, `NavigationPathfinder`, answers route queries over a
`NavigationGrid`. `Ship` optionally holds one: with it, an order becomes a list
of waypoints that the ship follows; without it, an order is a single straight
leg. `ShipsView` builds the pathfinder from the map's mask.

Rejected alternatives:

- **Theta\*** (any-angle search). Slightly shorter routes, but one line-of-sight
  test per expanded cell, so a much slower and more complex search for a gain
  that is hard to see.
- **A hand-drawn graph of sea lanes.** Very fast and fully controlled, but it
  needs an authoring tool and authoring work, and ships could only follow the
  lanes that were drawn.
- **Routing in `ShipsView` or `WorldMapInteraction`**, with `Ship` only
  following waypoints. It would put the rules for unreachable orders and for
  the starting position in a MonoBehaviour, out of reach of EditMode tests.

## Movement rules

These rules define what "sailing only on navigable areas" means. The search
and the smoothing both apply them, so a smoothed route is never less strict
than the grid route it comes from.

- A ship may move from a cell to one of its four side neighbours when that
  neighbour is navigable.
- It may move diagonally only when the diagonal cell **and both side cells
  next to the move** are navigable. A diagonal coastline one cell thick cannot
  be crossed, which matches the fill bucket of the mask tool
  (4-connectivity). It follows that two cells are reachable from one another
  exactly when they are in the same 4-connected region.
- A straight leg between two points is allowed when **every cell it touches is
  navigable**, including the cells it only touches by an edge or a corner.
  Every single move allowed above satisfies this rule.

Distances are measured in cells: 1 for a side move, √2 for a diagonal one.
Cells are square because the grid's height follows the map's aspect ratio, so
the shortest route in cells is the shortest in world units. When the grid no
longer matches the map's shape (the tool warns about it), routes stay valid
but may not be the shortest.

## Core

### `NavigationPathfinder` (new)

```csharp
public sealed class NavigationPathfinder
{
    public NavigationPathfinder(NavigationGrid grid);

    public bool HasNavigableCells { get; }

    public bool TryGetNearestNavigable(Vector2 point, out Vector2 nearest);
    public bool TryFindPath(Vector2 from, Vector2 to, List<Vector2> waypoints);
}
```

All points are normalized map positions. No Unity object, no scene dependency.

**Construction.** Throws `ArgumentNullException` for a null grid. It labels
the 4-connected regions of navigable cells once. The grid must not be modified
afterwards; nothing modifies a grid while the game runs.

**`TryGetNearestNavigable`.** A point outside the map is first clamped to it.
When the point is on a navigable cell, `nearest` is the point itself.
Otherwise it is the center of the navigable cell whose center is nearest to
the point. Returns false, with `nearest` set to the clamped point, for a NaN
point or a grid with no navigable cell.

**`TryFindPath`.** Clears `waypoints`, then fills it with the points to sail
through in order, **not including `from`**, ending with the point where the
route stops.

1. `from` and `to` are clamped to the map.
2. The **start cell** is the cell under `from` when it is navigable, otherwise
   the nearest navigable cell.
3. The **goal cell** is the cell under `to` when it is navigable and in the
   start cell's region, otherwise the cell of that region whose center is
   nearest to `to`.
4. The **end point** is `to` itself when `to` lies in the goal cell, otherwise
   the goal cell's center. A click on open water is therefore reached exactly,
   not rounded to a cell center.
5. A\* finds the shortest sequence of cells from the start cell to the goal
   cell under the movement rules, with the octile distance as heuristic.
6. The unsmoothed route is `from`, the centers of the cells after the start
   cell up to the goal cell, then the end point. When `from` is not in the
   start cell, the start cell's center is inserted after `from`.
7. Smoothing walks this route from `from` and drops every point that a
   straight leg satisfying the movement rules can skip. Two consecutive points
   of the unsmoothed route are always accepted as a leg, so smoothing cannot
   fail.
8. Points equal to the one before them are dropped.

Returns true when a route was produced. It may be empty: `from` is already the
end point. Returns false, leaving `waypoints` empty, for a NaN `from` or `to`
and for a grid with no navigable cell. Because the goal is always taken in the
start cell's region, no other case fails.

Every leg of a returned route satisfies the movement rules, except a first leg
that leaves a `from` which was not on water.

**Cost.** Region labels and search buffers take about 16 bytes per cell, about
14 MB for the current grid. Search buffers are allocated on the first search
and reused by the following ones, so an order allocates nothing. A search runs
synchronously inside the call.

### `Ship` (modified)

```csharp
public Ship(MapProjection projection, Vector2 position, float speed,
    NavigationPathfinder navigation = null);

public Vector2? Destination { get; }
public IReadOnlyList<Vector2> RemainingWaypoints { get; }
```

- **Without `navigation`** the ship behaves exactly as it does today: an order
  is one straight leg to the clicked point. The existing `ShipTests` stay
  valid unchanged.
- **Starting position.** With `navigation`, the position is clamped to the map
  and then replaced by `TryGetNearestNavigable`. When the grid has no
  navigable cell the clamped position is kept.
- **`SetDestination`.** A NaN point is ignored, as today. With `navigation`,
  the route comes from `TryFindPath(Position, destination, …)`. When no route
  exists the order is ignored and the ship keeps its current route. An empty
  route leaves the ship idle with its heading unchanged, as an order to its
  own position does today. Otherwise the route replaces the current one and
  the ship turns towards its first waypoint at once.
- **`Advance`.** The ship moves `speed × deltaTime` world units along its
  route. What is left of a step after reaching a waypoint is spent on the next
  leg, so the ship does not slow down at a turn. The heading is set from each
  leg when the ship starts it, measured in world space, and is kept on
  arrival. The ship stops exactly on the last waypoint.
- **`Destination`** is the last waypoint of the route, or null when idle.
  With `navigation` it can differ from the point that was ordered.
- **`RemainingWaypoints`** lists the waypoints not reached yet, the next one
  first; empty when idle.

A ship that receives a new order while under way searches from its current
position. If rounding has put that position just outside the water, the route
starts by returning to the nearest navigable cell.

## Game

### `ShipsView`

In `Start`, when `mapView.Definition.NavigationMask` is not null, it builds one
`NavigationPathfinder` from `NavigationMask.CreateGrid()` and passes it to every
ship it spawns. When that mask has no navigable cell it logs a warning: the
ships cannot move. Without a mask nothing changes.

The player ship therefore starts on the water nearest to its start city.

### `WorldMapInteraction`

Unchanged. It keeps passing the clicked point to `Ship.SetDestination`.

### `WorldMapSetup`

Unchanged: no new asset, prefab or scene object.

## Error handling

| Situation | Behaviour |
|---|---|
| Map without a mask | Straight-line movement, as today. |
| Mask with no navigable cell | Warning at startup; the ship stays where it is and ignores orders. |
| Order on land | The ship sails to the nearest navigable cell of its region. |
| Order on water of another region | Same. |
| Order outside the map | Clamped to the map, then as above. |
| NaN order | Ignored. |
| Ship in a small closed region | It can only move inside that region. |

## Performance

A route from one end of the largest region to the other can expand several
hundred thousand cells within the frame of the click. The implementation
measures the longest routes on the real mask in the Editor. If one takes more
than 50 ms, that is reported to the user with a costed remedy (searching a
reduced grid) instead of being added unasked.

## Testing

EditMode tests in `Assets/Tests/EditMode`, on small hand-built grids.

`NavigationPathfinderTests`:

- a null grid throws;
- open water gives a single waypoint, the destination itself;
- a route goes around an obstacle, and every leg satisfies the movement rules;
- the route found is the shortest one on a grid where it is known;
- a diagonal gap between two land cells is not crossed, by the search or by
  smoothing;
- a one-cell-wide channel with a bend is followed;
- a destination on land ends at the nearest navigable cell;
- a destination in another region ends at the nearest cell of the start's
  region;
- a destination outside the map is clamped;
- a start on land first goes to the nearest navigable cell;
- a route to the start's own position is empty and reported as found;
- NaN points and a grid with no navigable cell report no route and leave the
  list empty;
- the list is cleared before being filled;
- several searches in a row on one pathfinder give the same results as fresh
  pathfinders;
- `TryGetNearestNavigable` returns a navigable point unchanged, the nearest
  cell center for a point on land, and false on an empty grid;
- a grid that is not square.

`ShipTests`, added cases with a pathfinder:

- a ship created on land starts on the nearest water;
- a ship follows a route of several legs and stops on its last waypoint;
- the distance covered in a step that passes a waypoint is `speed × deltaTime`;
- the heading changes at each leg and is kept on arrival;
- `Destination` and `RemainingWaypoints` follow the route as it is sailed;
- an order on land sends the ship to the nearest reachable water;
- an order under way replaces the route;
- on a grid with no navigable cell, an order is ignored;
- over a whole trip around an obstacle, sampled in small steps, the ship is
  never on a non-navigable cell.

Checked in the Editor (through `unity-mcp` when the Editor is open): the ship
starts on the water by its start city; orders across the sea, around a cape,
up a river, on land and on the second sea; no compile or console error; the
timing of the longest routes.

## Documentation

`CLAUDE.md`: the project state and the Ships and Navigation mask sections stop
saying that ships ignore land, and describe `NavigationPathfinder`, the
movement rules and the optional pathfinder of `Ship`.
