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
