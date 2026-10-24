---
paths:
  - "Assets/Scripts/*/CityView/**"
  - "Assets/Tests/EditMode/CityView/**"
  - "Assets/Data/CityLayouts/**"
  - "Assets/UI/CityView/**"
---

# City view

## The grid

- `CityGrid` (Core) is a rectangle of cells; a cell is a `Vector2Int` with x to the right and y up, (0, 0) at the smallest corner. Some cells are forbidden (water, rock, fixed scenery): nothing can ever cover them.
- A `Footprint` is given once, in its `Along` orientation (`Width` on x, `Depth` on y); `Across` swaps the two. `GridPlace.Origin` is the covered cell of smallest x and y, whatever the orientation. A square footprint has one orientation: `GetPlaces` lists it `Along` only.
- The grid only knows footprints, kinds and owners; it never knows who builds, what it costs or why. Rules about that (money, materials, class housing) stay in the callers.
- `BuildingKind.HousedClass` throws for a workshop and `Good` for a house: test `IsHouse` first.
- `GetPlaces` lists by x then y, `Along` before `Across`; callers that pick "the first place" rely on that order.

## Projection

- `IsoProjection` (Core): cell (0, 0) is at the top of the screen, x goes to the lower right and y to the lower left. `CellAt` is the exact inverse of `CenterOf` on every point of a cell's diamond. `DrawKeyOf` orders buildings for drawing: a larger key is drawn later. It is a `long` and not a sortingOrder: the city view ranks the placed buildings by it and gives each sprite its rank; ground cells sit below all buildings. The key is lexicographic, the centre (`2 * (origin.x + origin.y) + size.x + size.y`, not the far cell, which ties for neighbours of different sizes) then an unclamped tie-break on the origin's side of the diagonal (same-shape neighbours tie on the centre); it must not be clamped or squeezed into a short (a staircase of same-shape blocks needs ~400 tie-break values). Good up to 1000 by 1000. `DrawKeyOf_OrdersEveryFrontBlockAfterTheOneBehindIt_*` brute-force 1×1, 2×1, 1×2 and 2×2 blocks (origins 0..19 and the far corners of a 200×200 grid); a new footprint size needs them extended.
- `PlacementCursor` (Core): the footprint being placed; `Start` always begins `Along`, `Rotate` does nothing when not placing or for a square footprint.

## The market on its grid

- A class's room is its houses on the grid × the room of a house of the class; never `AddRoom` outside a placed house when the market has a grid. The city's houses (`CityMarket.BuildCityHouse`, materials checked, then a place found, then taken) and the player's (`Construction.TryBuildHouse`/`TryBuildWorkshop` with a place, checked before anything is paid) are the only ways to build.
- A market with a grid needs a placement strategy and a footprint on every housing rule (`CityHousingRule.Footprint` is nullable, required only then); without a grid the placeless `AddPlayerHouse`/`AddPlayerWorkshop` apply, and with one they throw.

## Grid data (Game)

- A building asset has a `footprint` (Along: x = width, y = depth), each side at least 1 (`TryValidate`); the scene setup fills an unset (0, 0) one: 2 by 1 for houses, 2 by 2 for workshops. Never build a `Footprint` from it without validating (`default(Footprint)` is 0 by 0 and skips validation).
- `BuildingsDefinition.PlacementStrategy` (a `PlacementStrategyDefinition`, `Assets/Data/Buildings/Placement/`) is required: `WorldEconomy` makes one strategy for the whole world from it.
- `CityLayoutDefinition` (`CityDefinition.Layout`) is a city's grid: size, forbidden cells, starting houses with their place. `TryCreateGrid` gives the grid; a city without a usable layout gets a market without grid and one warning at the start. `WorldEconomy.GridOf` returns the grid.
- With a grid, the room of a class at the start is its houses on the grid × the room of a house (`EconomyDefinition.CreateMarket`); `StartingRoomOf` is the room a grid should give at least.
- The scene setup gives a city without a layout (`Assets/Data/CityLayouts/<city>Layout.asset`) a 40 by 40 grid (`CityLayoutDefinition.DefaultSize`) with just the starting houses `StartingRoomOf` asks for, placed at random with the city's index in the map as seed; a city with a layout is never touched.
