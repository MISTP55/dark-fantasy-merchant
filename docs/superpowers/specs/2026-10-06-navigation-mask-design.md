# Navigation Mask — Design

Date: 2026-10-06
Status: awaiting review

## Purpose

Record which parts of the world map ships can sail on, and give the designer a
tool to paint that information in the Scene view. Seas and rivers are painted
as navigable; everything else is land.

This delivers the data and its authoring tool only. Ships keep sailing in a
straight line and ignoring land; making them respect the mask, and finding
paths around land, are later pieces of work that will read the data produced
here.

## Scope

In scope:

- A grid of navigable / non-navigable cells covering the whole map, queryable
  from `Core` by cell or by normalized map position.
- A ScriptableObject asset that stores the grid, referenced by the map
  definition.
- A Scene view tool with a brush, an eraser and a fill bucket, an overlay that
  shows the mask over the map, and undo.
- A colour-based detection that pre-fills the mask from the map image.
- Resizing the grid, which resamples what is already painted.

Out of scope:

- Any use of the mask by ships: refusing orders, land avoidance, pathfinding.
- Kinds of water (sea, river, deep water, hazards).
- Showing the mask in the running game.
- Editing the mask outside Unity.

## Decisions

Agreed with the user before writing this spec:

| Topic | Decision |
|---|---|
| Deliverable | Tool and data only. Ship behaviour does not change. |
| Cell values | Binary: navigable or not. Rivers are narrow navigable bands. |
| Grid size | 1024 cells wide by default, adjustable; height follows the map's aspect ratio. |
| Authoring | Brush, eraser, fill bucket, plus colour detection from the map image. |
| Storage | In a ScriptableObject, not in an image file. |
| Tool activation | An explicit Scene view tool, not an always-on handler. |

## Constraints

- Conventions from `CLAUDE.md` apply: English code, static definitions in
  ScriptableObjects, logic in plain C# in `Core` covered by EditMode tests,
  UI Toolkit for UI.
- Map positions stay **normalized** (`(0,0)` bottom-left, `(1,1)` top-right)
  and go to world space only through `MapProjection`. The grid is defined over
  normalized space, so the map image can be replaced by one of another
  resolution without repainting.
- The current map image is 2048 × 1758 pixels and its rivers are 3 to 6 pixels
  wide. At 1024 cells wide a cell covers 2 × 2 map pixels, so a river is 2 to 3
  cells wide.
- The import settings of `WorldMap.jpg` are not changed (it is not readable).

## Approach

The mask's behaviour is a plain C# class in `Core`. A ScriptableObject in
`Game` serializes its bits. An `EditorTool` in `Editor` edits a working copy of
the grid and writes it back to the asset through Unity's undo system.

Rejected alternatives:

- **Mask stored as a PNG read at startup.** Editable in an image editor and
  diffable as an image, but undo must be written by hand, the texture must be
  imported readable and uncompressed, and the game would depend on the import
  settings of an image.
- **Always-on tool, like `CityPlacementTool`.** Simpler, but a brush that
  captures every click in the Scene view would get in the way of placing
  cities.

## Core

### `NavigationGrid`

A grid of cells, one bit each. Cell `(0, 0)` is the bottom-left one, matching
normalized coordinates. No Unity object, no scene dependency.

```csharp
public sealed class NavigationGrid
{
    public NavigationGrid(int width, int height);
    public NavigationGrid(int width, int height, byte[] bits);

    public int Width { get; }
    public int Height { get; }

    public bool IsNavigable(int x, int y);
    public bool IsNavigable(Vector2 normalized);

    public bool PaintDisc(Vector2 center, float radiusInCells, bool navigable);
    public bool PaintStroke(Vector2 from, Vector2 to, float radiusInCells, bool navigable);
    public bool Fill(Vector2 point, bool navigable);
    public void Clear(bool navigable);

    public NavigationGrid Resampled(int width, int height);
    public byte[] ToBytes();
}
```

- Both constructors throw `ArgumentOutOfRangeException` for a width or height
  below 1. A new grid is entirely non-navigable.
- Bits are packed row by row from the bottom row, eight cells per byte, least
  significant bit first. The array holds `ceil(width × height / 8)` bytes. The
  second constructor copies the array, throws `ArgumentNullException` for a
  null one and `ArgumentException` for one of the wrong length. `ToBytes`
  returns a copy.
- A normalized point maps to cell `(floor(x × width), floor(y × height))`;
  a coordinate of exactly `1` belongs to the last cell.
- `IsNavigable(int, int)` returns false outside the grid.
  `IsNavigable(Vector2)` returns false outside the map and for a NaN point.
- Points are normalized; the radius is in cells. Cells are square, because the
  grid's height follows the map's aspect ratio, so a disc measured in cells is
  a circle on screen.
- `PaintDisc` sets every cell whose center is within `radiusInCells` of the
  point, in cell space. The cell containing the point is always set, so a very
  small radius still paints one cell. Cells outside the grid are skipped; a
  center outside the map still paints the part of the disc that overlaps it.
- `PaintStroke` paints discs along the segment, no more than half a cell
  apart, so a fast pointer movement leaves no gap.
- `Fill` sets the contiguous region of cells that share the value of the cell
  under the point, using **4-connectivity**, so it does not leak diagonally
  through a thin coastline. It is iterative, not recursive. A point outside
  the map does nothing.
- `PaintDisc`, `PaintStroke` and `Fill` return true when at least one cell
  changed. A NaN point or radius changes nothing and returns false.
- `Resampled` returns a new grid in which each cell takes the value of the
  source cell under its center (nearest neighbour).

### `WaterColorClassifier`

```csharp
public static class WaterColorClassifier
{
    public static bool IsWater(Color32 color, float maxSaturation);
}
```

True when the HSV saturation of the colour, `(max − min) / max` over its RGB
components, is at or below `maxSaturation`. Black has a saturation of zero.
Alpha is ignored. This is the whole detection rule: on the current map the
seas are grey and the land is brown or orange.

## Game

### `NavigationMaskDefinition` (ScriptableObject)

Static definition of the navigable areas of a map. Menu:
`Dark Fantasy Merchant/Navigation Mask`.

- Serialized: `width`, `height`, and `bits`, a byte array hidden in the
  Inspector.
- `Width`, `Height`.
- `CreateGrid()` returns a new `NavigationGrid` built from the asset. When the
  dimensions are invalid or the array does not match them, it returns an empty
  grid (of the stored dimensions when valid, otherwise 1 × 1) instead of
  throwing.
- `SetGrid(NavigationGrid)` copies a grid into the asset. Only the editing
  tool calls it; the asset is never written while the game runs.

Asset: `Assets/Data/WorldMap/NavigationMask.asset`.

### `WorldMapDefinition`

Gains a serialized `navigationMask` field and a `NavigationMask` property. It
may be null: a map without a mask is valid.

Nothing in the running game reads the mask yet.

## Editor

### `WorldMapEditorContext`

Small static class holding the lookup `CityPlacementTool` already does: the
selected `WorldMapDefinition` if there is one, otherwise the definition of the
scene's `WorldMapView`. Both tools use it; `CityPlacementTool` loses its
private copy.

### `NavigationMaskTool` (`EditorTool`)

A global Scene view tool. It appears in the Scene view toolbar and takes
pointer input only while it is the active tool. It edits the mask of the
active map and does nothing in Play mode or when no map definition with a
valid projection is found.

Pointer and keyboard:

| Gesture | Effect |
|---|---|
| Left click or drag | Applies the current mode |
| Shift held | Swaps brush and eraser; the fill bucket fills with non-navigable |
| `[` and `]` | Shrink and grow the brush |
| Alt, wheel, middle or right button | Scene view navigation, unchanged |

A circle follows the pointer at the brush size.

Modes:

- **Brush**: paints navigable cells with `PaintDisc` on press and
  `PaintStroke` while dragging.
- **Eraser**: the same with non-navigable.
- **Fill**: one `Fill` per click.

Display: navigable cells are tinted semi-transparent blue over the map, from a
texture of the grid's size with point filtering, drawn in the Scene view only
while the tool is active. Nothing is added to the scene.

Undo and saving:

- The tool paints on a working copy of the grid. At the end of each stroke it
  records the asset for undo and writes the grid into it, when the stroke
  changed anything. A stroke, a fill, a detection, a resize and a clear are one
  undo step each.
- After an undo or redo, the working copy is rebuilt from the asset and the
  overlay refreshed.
- The asset is marked dirty and saved with the project.

### Tool panel (Scene view `Overlay`, UI Toolkit)

Visible only while the tool is active.

- Mode: Brush, Eraser, Fill.
- Brush size in cells, 1 to 64.
- Overlay opacity.
- Detection: saturation threshold and a **Detect from map** button.
- Grid: width, 64 to 4096, and a **Resize** button that resamples the mask.
  The height is `max(1, round(width / aspect ratio))`.
- **Clear**.
- When the map has no mask, the panel shows only a **Create mask** button. It
  creates `Assets/Data/WorldMap/NavigationMask.asset` at the default width and
  assigns it to the map definition, with undo.
- A warning when the grid no longer has the aspect ratio of the map, which
  happens when the map image is replaced; resizing fixes it.

Brush size, mode, opacity and threshold are editor preferences of the tool,
not part of the asset.

### Detection

**Detect from map** copies the map sprite's area of its texture into a
temporary render texture of the grid's size, reads it back, and sets each cell
from `WaterColorClassifier.IsWater` with the panel's threshold. The copy keeps
the image's own colours (no linear conversion), so the threshold means the
same thing as in an image editor. The result replaces the whole mask, after a
confirmation when the mask is not empty.

The result is expected to be noisy on the current map: sea names, the frame,
the compass rose and snowy mountains are grey, and rivers are cut by bridges
and labels. It is a starting point to correct by hand.

## Testing

EditMode tests in `Assets/Tests/EditMode`:

- `NavigationGridTests`:
  - invalid dimensions throw; a null or wrong-length bit array throws;
  - a new grid is entirely non-navigable;
  - normalized points map to the expected cells, including `0` and `1` on both
    axes, on a grid that is not square;
  - cells outside the grid, points outside the map and NaN points are not
    navigable;
  - a disc sets the expected cells and is clipped at the edges;
  - a very small radius paints the cell under the point;
  - a stroke between two distant points leaves no gap;
  - a fill stops at a coastline, does not leak through a diagonal, and reports
    no change on a region already at the target value;
  - painting reports whether anything changed;
  - erasing clears painted cells;
  - `Clear` sets every cell;
  - resampling to a larger and a smaller grid keeps the painted shape;
  - `ToBytes` round-trips through the constructor and returns a copy.
- `WaterColorClassifierTests`: grey is water; brown and orange are land; the
  threshold is inclusive; black and white.

Checked in the Editor (through `unity-mcp` when the Editor is open): creating
the mask; painting, erasing and filling a closed sea; undo and redo; detection
on the current map; resizing; the mask surviving a domain reload and an Editor
restart; city handles still usable when the tool is not active.

## Documentation

`CLAUDE.md` gets a "Navigation mask" subsection under Architecture.
