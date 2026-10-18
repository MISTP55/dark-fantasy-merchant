---
paths:
  - "Assets/Scripts/*/Navigation/**"
  - "Assets/Tests/EditMode/Navigation/**"
---

# Navigation mask and pathfinding

## Mask

- `NavigationGrid` (Core) is one bit per cell, cell `(0,0)` bottom-left, over normalized space, with square cells (its height follows the map's aspect ratio). Replacing the map image by one of another shape needs a **Resize** in the tool, not a repaint. The mask is not expected to change while the game runs; a map without a mask is valid.
- The mask is painted in the Scene view with the **Navigation Mask** tool (`NavigationMaskTool`, `NavigationMaskOverlay`); while it is active, `CityPlacementTool` draws no handles. Its working copy is `NavigationMaskSession`, written to the asset through `NavigationMaskAuthoring.Apply`, one stroke per undo step.

## Pathfinding

- Movement rules, shared by the search (`NavigationCellSearch`), route smoothing (`NavigationLineOfSight`) and the fill bucket: a move to a side neighbour, or to a diagonal one only when both cells beside it are navigable; a straight leg only when every cell it touches, even by a corner, is navigable. A diagonal coastline one cell thick therefore holds, and two cells are reachable exactly when they are in the same 4-connected region. Change them together.
- A search runs synchronously on the click and allocates nothing (about 16 bytes per cell are held).
- An order ends on the clicked point when the ship can reach it, otherwise on the nearest cell of the ship's own region.
