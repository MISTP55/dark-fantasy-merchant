---
paths:
  - "Assets/Scripts/*/Navigation/**"
  - "Assets/Tests/EditMode/Navigation/**"
---

# Navigation mask and pathfinding

## Mask

- `NavigationGrid` (Core) is one bit per cell, cell `(0,0)` bottom-left, over normalized space, with square cells (its height follows the map's aspect ratio). It also holds the painting logic. Replacing the map image by one of another shape needs a **Resize** in the tool, not a repaint. The mask is not expected to change while the game runs.
- `NavigationMaskDefinition` stores the grid; its fields are hidden in the Inspector on purpose: size and bits only make sense together. A map without a mask is valid.
- The mask is painted in the Scene view with the **Navigation Mask** tool (`NavigationMaskTool`, `NavigationMaskOverlay`); while it is active, `CityPlacementTool` draws no handles. Its working copy, `NavigationMaskSession`, is written to the asset once per stroke through `NavigationMaskAuthoring.Apply` (one stroke, one undo step) and rebuilt from the asset after undo/redo or an outside change (`SyncWithAsset`). Its preview texture is rewritten only where a stroke passed; do not go back to rebuilding it whole on every drag event.

## Pathfinding

- Movement rules, shared by the search, route smoothing and the fill bucket: a move to a side neighbour, or to a diagonal one only when both cells beside it are navigable; a straight leg only when every cell it touches, even by a corner, is navigable (`NavigationLineOfSight`, with a margin of a thousandth of a cell because positions are floats). A diagonal coastline one cell thick therefore holds, and two cells are reachable exactly when they are in the same 4-connected region.
- `NavigationCellSearch` is an A* whose buffers are allocated once and reused: an order allocates nothing. Keep it that way. It runs synchronously on the click, with about 16 bytes per cell held.
- An order ends on the clicked point when the ship can reach it, otherwise on the nearest cell of the ship's own region.
