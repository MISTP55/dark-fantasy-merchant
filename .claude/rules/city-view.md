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
- `CanPlace` refuses a footprint with a side under 1 (`default(Footprint)` is 0 by 0) and `Place` throws for it: it would give room with no place.
- The grid only knows footprints, kinds and owners; it never knows who builds, what it costs or why. Rules about that (money, materials, class housing) stay in the callers.
- `BuildingKind.HousedClass` throws for a workshop and `Good` for a house: test `IsHouse` first.
- `GetPlaces` lists by x then y, `Along` before `Across`; callers that pick "the first place" rely on that order.

## Projection

- `IsoProjection` (Core): cell (0, 0) is at the bottom of the screen, x goes to the upper right and y to the upper left, so that the grid as the layout window shows it (x east, y north) appears turned, not mirrored: its north at the upper left, its east at the upper right. The nearer of two blocks is the one beyond the other towards smaller x or y. `CellAt` is the exact inverse of `CenterOf` on every point of a cell's diamond. `DrawKeyOf` orders buildings for drawing: a larger key is drawn later. It is a `long` and not a sortingOrder: the city view ranks the placed buildings by it and gives each sprite its rank; ground cells sit below all buildings. The key is lexicographic, the centre (`2 * (origin.x + origin.y) + size.x + size.y`, smaller drawn later, not the far cell, which ties for neighbours of different sizes) then an unclamped tie-break on the far corner's side of the diagonal (same-shape neighbours tie on the centre); it must not be clamped or squeezed into a short (a staircase of same-shape blocks needs ~400 tie-break values). Good up to 1000 by 1000. `DrawKeyOf_OrdersEveryFrontBlockAfterTheOneBehindIt_*` brute-force 1×1, 2×1, 1×2 and 2×2 blocks (origins 0..19 and the far corners of a 200×200 grid); a new footprint size needs them extended.
- `PlacementCursor` (Core): the footprint being placed; `Start` always begins `Along`, `Rotate` does nothing when not placing or for a square footprint.

## The market on its grid

- A class's room is its connected houses on the grid × the room of a house of the class; never `AddRoom` outside a placed house when the market has a grid. `CityMarket` listens to `CityGrid.BuildingConnected`: a house gives its room when it connects (at once when it touches a network road), and keeps it waiting until then (the player's: the room it was built with; the city's and the starting ones: its class's housing rule, which a grid holding an unconnected house must have). Only the houses the market places, or that are on the grid at its creation, give room: a house placed on the grid directly afterwards gives none. A player's workshop on the grid hires only while connected. The city's houses (`CityMarket.BuildCityHouse`, materials checked, then a place found, then taken) and the player's (`Construction.TryBuildHouse`/`TryBuildWorkshop` with a place, checked before anything is paid) are the only ways to build.
- A market with a grid needs a placement strategy and a footprint on every housing rule (`CityHousingRule.Footprint` is nullable, required only then); without a grid the placeless `AddPlayerHouse`/`AddPlayerWorkshop` apply, and with one they throw.

## Grid data (Game)

- A building asset has a `footprint` (Along: x = width, y = depth), each side at least 1 (`TryValidate`); the scene setup fills an unset (0, 0) one: 2 by 1 for houses, 2 by 2 for workshops. Never build a `Footprint` from it without validating (`default(Footprint)` is 0 by 0 and skips validation).
- `BuildingsDefinition.PlacementStrategy` (a `PlacementStrategyDefinition`, `Assets/Data/Buildings/Placement/`) is required: `WorldEconomy` makes one strategy for the whole world from it.
- `CityLayoutDefinition` (`CityDefinition.Layout`) is a city's grid: size, forbidden cells, starting houses with their place. `TryCreateGrid` gives the grid; a city without a usable layout gets a market without grid and one warning at the start. `WorldEconomy.GridOf` returns the grid.
- With a grid, the room of a class at the start is its connected houses on the grid × the room of a house (`EconomyDefinition.CreateMarket`); `StartingRoomOf` is the room a grid should give at least.
- The scene setup gives a city without a layout (`Assets/Data/CityLayouts/<city>Layout.asset`) a 40 by 40 grid (`CityLayoutDefinition.DefaultSize`) with just the starting houses `StartingRoomOf` asks for, placed at random with the city's index in the map as seed; a city with a layout is never touched. A layout asset already at that path (a link lost) is linked to the city, never rewritten; the layout window's "Create layout" does the same.

## The city view scene

- `CityView.unity` is loaded additively over the map, while the simulation keeps running in the map's scene. `CityViewLoader` (map scene, `World`) is the only way in and out: the city panel's "Voir la ville" calls its `Open`, the view's "Retour à la carte" its `Close`. Never load or unload the scene another way, or the map stays switched off or reacts under the view.
- Opening clears the map's hover and selections (its panels close) and switches off the map camera's object, `WorldMapInput` and `WorldMapInteraction`: the map would otherwise pick, pan and give orders under the view, since the city view reads the same mouse. Closing switches them back on and selects the city again, whose panel reopens, except when fast forward closed the view or is on when the unload ends: the map is then only watched and nothing is selected, the decision being taken when the unload ends. A request that arrives while the scene loads or unloads is kept, the latest winning, and carried out when it is done.
- Opening also remembers the player's ship that was selected on the map and under way (null otherwise), since the view clears that selection: fast forward started from the view awaits it, through `CityViewLoader.TakeShipUnderWay` (see `time.md`). A normal close forgets it, and so does the end of any unload.
- The view's objects reach the world only through `SimulationServices.Current`; the loader hands itself to `CityViewController.Show(city, loader)`.
- The map's `AudioListener` is on its camera's object, which is off while the view is open; the view's camera carries the only other one.
- The scene has no `Light2D`: every sprite of the view uses the unlit sprite material (`Assets/Art/CityView/CityViewSprite.mat`). Its sprites are on the `CityView` layer (made by the setup), the only one its camera draws: the map's objects stay at the same world positions under it.
- `CityViewInput` is the only reader of the `CityView` action map. Click and PanDrag are both the left button: a press released where it started is a click, one moved past the threshold pans. It ignores pointer input over any document of the shared panel, the HUDs included.
- `CityGridView` draws one sprite per cell (all at one sortingOrder, below every building) and one per building, ranked by `BuildingDrawOrder` (the order of `IsoProjection.DrawKeyOf`) and given its rank as sortingOrder; a building placed while the view is open is inserted in its rank, nothing is redrawn every frame. The placeholder sprites (`Assets/Art/CityView/`) follow the projection (the 2×1 block's long side goes to the upper right) and have their pivot at the centre of the footprint on the ground, where `IsoProjection.CenterOf(origin, size)` puts them; a footprint size other than 2×1, 1×2 and 2×2 has no block yet. A house's block takes its class's colour from `HouseColors` (Game), which the layout window also uses, so that both show the same; a workshop's takes the workshop colour of `CityGridView`, whoever owns it. A building's block is scaled down around that pivot (`buildingScale`), which leaves a gap between neighbours and never changes the drawing order; the ghost is scaled the same, so that it shows the building as it will stand.

## Building in the view

- `BuildBarController` (plain class over the view's document) shows the offers of `WorldEconomy.HouseOffers`/`WorkshopOffers`, the only offers that carry their footprint (one made elsewhere is 0 by 0, which `CityPlacementController` refuses). It quotes through `PlayerBuilder`, and refreshes on the market's, the treasury's and the hold's `Changed` and on `PlayerBuilder.FreeChanged` (the debug tools' free building, during which the cost tip says so), re-choosing the hold (`PlayerBuilder.HoldIn`) when a ship docks in or leaves the city; it is unbound when the view's controller is disabled.
- The workshops category lists only the workshops of the goods the city produces (`MaxWorkersOf` above 0): one of another good could never be built there, so it is left out rather than disabled.
- An offer's button is wrapped in an area that takes the pointer for it: a disabled button gets no pointer event, and its cost tip must still show.
- `CityPlacementController` is the only listener of `CityViewInput.Clicked`, `Rotated` and `Cancelled`. Its `Hover`, `Click`, `Rotate` and `Cancel` are public so that a play-mode check can drive it without Input System events; `Update` hovers the real pointer every frame while placing, so a driven hover lasts until the next frame.
- The ghost (`CityGridView.ShowGhost`) is drawn at the highest sortingOrder, above every building; it is hidden over the UI and off the grid, and red where the footprint does not fit, partly off the grid included, or wherever the offer's quote (`PlayerBuilder.QuoteHouse`/`QuoteWorkshop`) is impossible. The bar raises `Refreshed` after each re-quote; `CityViewController` hands it to `CityPlacementController.Refresh`, which stops placing when the offer can no longer be paid for (gold or materials): the player sees its button disabled and the ghost gone, rather than clicks that silently do nothing.
