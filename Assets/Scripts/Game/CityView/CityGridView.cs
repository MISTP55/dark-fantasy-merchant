using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Draws a city's grid in placeholder shapes: a diamond per cell, tinted free or forbidden, a flat tile per road,
    /// and a block per other building, a house tinted by the class it shelters (<see cref="HouseColors"/>, as in the
    /// layout window), a workshop in the workshop colour, the town hall in its own; a house or workshop that is not
    /// connected is darkened until it is. A building placed while the grid is shown gets its block then; nothing is
    /// redrawn every frame. Every sprite is on this object's layer, which only the city view's camera draws.
    /// </summary>
    public sealed class CityGridView : MonoBehaviour
    {
        // Ground cells are flat diamonds that never overlap: one order for all, below every road and building.
        private const int GroundSortingOrder = 0;

        // Roads are flat on the ground: above the cells, below every building.
        private const int RoadSortingOrder = 1;
        private const int FirstBuildingSortingOrder = 2;

        // Above every building: a sortingOrder is a 16-bit value.
        private const int GhostSortingOrder = short.MaxValue;

        [Tooltip("Unlit: the city view has no light.")]
        [SerializeField] private Material spriteMaterial;

        [SerializeField] private Sprite cellSprite;

        [Tooltip("A block covering 2 cells on x and 1 on y.")]
        [SerializeField] private Sprite block2x1Sprite;

        [Tooltip("A block covering 1 cell on x and 2 on y.")]
        [SerializeField] private Sprite block1x2Sprite;

        [Tooltip("A block covering 2 cells on x and 2 on y.")]
        [SerializeField] private Sprite block2x2Sprite;

        [Tooltip("A block covering 3 cells on x and 2 on y.")]
        [SerializeField] private Sprite block3x2Sprite;

        [Tooltip("A block covering 2 cells on x and 3 on y.")]
        [SerializeField] private Sprite block2x3Sprite;

        [SerializeField] private Color freeCellColor = new Color32(0x3B, 0x4A, 0x3A, 0xFF);
        [SerializeField] private Color forbiddenCellColor = new Color32(0x24, 0x43, 0x5E, 0xFF);
        [Tooltip("The colour of every workshop; a house takes the colour of its class.")]
        [SerializeField] private Color workshopColor = new Color32(0xD6, 0x3A, 0xC4, 0xFF);
        [SerializeField] private Color townHallColor = new Color32(0xC9, 0xA2, 0x27, 0xFF);
        [SerializeField] private Color roadColor = new Color32(0x8A, 0x7B, 0x62, 0xFF);
        [Tooltip("A house or workshop that is not connected is drawn with its colour times this on red, green and blue.")]
        [SerializeField, Range(0.1f, 1f)] private float inactiveShade = 0.45f;
        [Tooltip("A building's block is drawn this much smaller than its footprint, around its centre, so that a gap shows between neighbours.")]
        [SerializeField, Range(0.5f, 1f)] private float buildingScale = 0.85f;

        [SerializeField] private Color validGhostColor = new Color32(0x6F, 0xBF, 0x73, 0x99);
        [SerializeField] private Color unconnectedGhostColor = new Color32(0xE0, 0xC0, 0x40, 0x99);
        [SerializeField] private Color invalidGhostColor = new Color32(0xD0, 0x50, 0x50, 0x99);

        private readonly List<SpriteRenderer> cellRenderers = new List<SpriteRenderer>();

        // The buildings' renderers by rank: index i is drawn with sortingOrder FirstBuildingSortingOrder + i.
        private readonly List<SpriteRenderer> buildingRenderers = new List<SpriteRenderer>();
        private readonly BuildingDrawOrder drawOrder = new BuildingDrawOrder();

        // The renderers of the houses and workshops, to give them their colour when they connect.
        private readonly Dictionary<PlacedBuilding, SpriteRenderer> shadedRenderers = new Dictionary<PlacedBuilding, SpriteRenderer>();
        private readonly List<SpriteRenderer> roadRenderers = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> roadGhostRenderers = new List<SpriteRenderer>();

        private CityGrid grid;
        private Transform cellRoot;
        private Transform buildingRoot;
        private Transform roadGhostRoot;
        private SpriteRenderer ghostRenderer;
        private Vector2Int ghostSize;

        /// <summary>The grid shown, or null.</summary>
        public CityGrid Grid => grid;

        public IReadOnlyList<SpriteRenderer> CellRenderers => cellRenderers;

        /// <summary>The blocks' renderers (every building but the roads), in drawing order.</summary>
        public IReadOnlyList<SpriteRenderer> BuildingRenderers => buildingRenderers;

        /// <summary>The roads' tiles, in no particular order.</summary>
        public IReadOnlyList<SpriteRenderer> RoadRenderers => roadRenderers;

        /// <summary>The block of the building being placed, or null before one is shown; off when hidden.</summary>
        public SpriteRenderer GhostRenderer => ghostRenderer;

        /// <summary>The tiles of the road being placed, pooled: those past the road's length are off.</summary>
        public IReadOnlyList<SpriteRenderer> RoadGhostRenderers => roadGhostRenderers;

        /// <summary>The rectangle, in world units, that holds every cell's diamond.</summary>
        public static Rect WorldBoundsOf(CityGrid grid)
        {
            // The grid is a diamond: its bottom is the bottom corner of cell (0, 0), its sides and top the far corners.
            float halfCellWidth = IsoProjection.CellWidth * 0.5f;
            float halfCellHeight = IsoProjection.CellHeight * 0.5f;
            float bottom = -halfCellHeight;
            float top = bottom + (grid.Width + grid.Height) * halfCellHeight;
            float left = -grid.Height * halfCellWidth;
            float right = grid.Width * halfCellWidth;
            return Rect.MinMaxRect(left, bottom, right, top);
        }

        /// <summary>Draws this grid in place of the one shown, if any. Null shows nothing.</summary>
        public void Show(CityGrid newGrid)
        {
            Clear();
            grid = newGrid;

            if (grid == null)
            {
                return;
            }

            cellRoot = CreateChild("Cells", transform).transform;
            buildingRoot = CreateChild("Buildings", transform).transform;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    SpriteRenderer cellRenderer = CreateRenderer($"Cell {x},{y}", cellRoot, cellSprite);
                    cellRenderer.transform.localPosition = IsoProjection.CenterOf(cell);
                    cellRenderer.color = grid.IsForbidden(cell) ? forbiddenCellColor : freeCellColor;
                    cellRenderer.sortingOrder = GroundSortingOrder;
                    cellRenderers.Add(cellRenderer);
                }
            }

            foreach (PlacedBuilding building in grid.Buildings)
            {
                AddBuilding(building);
            }

            grid.BuildingPlaced += AddBuilding;
            grid.BuildingConnected += OnBuildingConnected;
        }

        /// <summary>
        /// Shows the block of the building being placed over the footprint at the origin, above every building,
        /// in the colour of its state.
        /// </summary>
        public void ShowGhost(Vector2Int origin, Vector2Int size, GhostState state)
        {
            HideRoadGhost();

            if (ghostRenderer == null)
            {
                ghostRenderer = CreateRenderer("Ghost", transform, null);
                ghostRenderer.sortingOrder = GhostSortingOrder;
                ghostRenderer.transform.localScale = BuildingScaleVector;
            }

            // Shown every frame while placing: the sprite is looked up only when the size changes.
            if (ghostRenderer.sprite == null || size != ghostSize)
            {
                ghostRenderer.sprite = BlockSpriteOf(size);
                ghostSize = size;
            }

            ghostRenderer.transform.localPosition = IsoProjection.CenterOf(origin, size);
            ghostRenderer.color = GhostColorOf(state);
            ghostRenderer.enabled = true;
        }

        /// <summary>
        /// Shows a flat tile over each cell of the road being placed, above every building, in the colour of its
        /// state; the block ghost is hidden.
        /// </summary>
        public void ShowRoadGhost(IReadOnlyList<Vector2Int> cells, GhostState state)
        {
            if (ghostRenderer != null)
            {
                ghostRenderer.enabled = false;
            }

            if (roadGhostRoot == null)
            {
                roadGhostRoot = CreateChild("Road ghosts", transform).transform;
            }

            Color color = GhostColorOf(state);

            for (int i = 0; i < cells.Count; i++)
            {
                if (i == roadGhostRenderers.Count)
                {
                    SpriteRenderer newTile = CreateRenderer("Road ghost", roadGhostRoot, cellSprite);
                    newTile.sortingOrder = GhostSortingOrder;
                    roadGhostRenderers.Add(newTile);
                }

                SpriteRenderer tile = roadGhostRenderers[i];
                tile.transform.localPosition = IsoProjection.CenterOf(cells[i]);
                tile.color = color;
                tile.enabled = true;
            }

            for (int i = cells.Count; i < roadGhostRenderers.Count; i++)
            {
                roadGhostRenderers[i].enabled = false;
            }
        }

        public void HideGhost()
        {
            if (ghostRenderer != null)
            {
                ghostRenderer.enabled = false;
            }

            HideRoadGhost();
        }

        private void HideRoadGhost()
        {
            foreach (SpriteRenderer tile in roadGhostRenderers)
            {
                tile.enabled = false;
            }
        }

        private Color GhostColorOf(GhostState state)
        {
            switch (state)
            {
                case GhostState.Connected:
                    return validGhostColor;
                case GhostState.Unconnected:
                    return unconnectedGhostColor;
                default:
                    return invalidGhostColor;
            }
        }

        private void OnDestroy()
        {
            if (grid != null)
            {
                grid.BuildingPlaced -= AddBuilding;
                grid.BuildingConnected -= OnBuildingConnected;
            }
        }

        private void Clear()
        {
            if (grid != null)
            {
                grid.BuildingPlaced -= AddBuilding;
                grid.BuildingConnected -= OnBuildingConnected;
            }

            grid = null;
            cellRenderers.Clear();
            buildingRenderers.Clear();
            shadedRenderers.Clear();
            roadRenderers.Clear();
            roadGhostRenderers.Clear();
            drawOrder.Clear();

            if (cellRoot != null)
            {
                Destroy(cellRoot.gameObject);
            }

            if (buildingRoot != null)
            {
                Destroy(buildingRoot.gameObject);
            }

            if (roadGhostRoot != null)
            {
                Destroy(roadGhostRoot.gameObject);
            }

            cellRoot = null;
            buildingRoot = null;
            roadGhostRoot = null;
            HideGhost();
        }

        private void AddBuilding(PlacedBuilding building)
        {
            Vector2Int origin = building.Place.Origin;

            if (building.Kind.IsRoad)
            {
                SpriteRenderer tile = CreateRenderer($"Road {origin.x},{origin.y}", buildingRoot, cellSprite);
                tile.transform.localPosition = IsoProjection.CenterOf(origin);
                tile.color = roadColor;
                tile.sortingOrder = RoadSortingOrder;
                roadRenderers.Add(tile);
                return;
            }

            Vector2Int size = building.Size;
            string kindName = building.Kind.IsHouse ? "House" : building.Kind.IsWorkshop ? "Workshop" : "Town hall";
            SpriteRenderer buildingRenderer = CreateRenderer(
                $"{building.Owner} {kindName} {origin.x},{origin.y}", buildingRoot, BlockSpriteOf(size));
            buildingRenderer.transform.localPosition = IsoProjection.CenterOf(origin, size);
            buildingRenderer.transform.localScale = BuildingScaleVector;
            buildingRenderer.color = ColorOf(building);

            if (building.Kind.IsHouse || building.Kind.IsWorkshop)
            {
                shadedRenderers.Add(building, buildingRenderer);
            }

            // The buildings at and above the new one's rank move up by one.
            int rank = drawOrder.Insert(origin, size);
            buildingRenderers.Insert(rank, buildingRenderer);

            for (int i = rank; i < buildingRenderers.Count; i++)
            {
                buildingRenderers[i].sortingOrder = FirstBuildingSortingOrder + i;
            }
        }

        // IsConnected is not up to date inside BuildingPlaced: a building placed connected is lit by BuildingConnected.
        private void OnBuildingConnected(PlacedBuilding building)
        {
            if (shadedRenderers.TryGetValue(building, out SpriteRenderer buildingRenderer))
            {
                buildingRenderer.color = ColorOf(building);
            }
        }

        private Color ColorOf(PlacedBuilding building)
        {
            if (building.Kind.IsTownHall)
            {
                return townHallColor;
            }

            Color color = building.Kind.IsHouse ? HouseColors.Of(building.Kind.HousedClass) : workshopColor;

            if (building.IsConnected)
            {
                return color;
            }

            return new Color(color.r * inactiveShade, color.g * inactiveShade, color.b * inactiveShade, color.a);
        }

        // The scale of a building's block, and of the ghost, which shows the building as it will stand.
        private Vector3 BuildingScaleVector => new Vector3(buildingScale, buildingScale, 1f);

        private Sprite BlockSpriteOf(Vector2Int size)
        {
            if (size == new Vector2Int(2, 1))
            {
                return block2x1Sprite;
            }

            if (size == new Vector2Int(1, 2))
            {
                return block1x2Sprite;
            }

            if (size == new Vector2Int(3, 2))
            {
                return block3x2Sprite;
            }

            if (size == new Vector2Int(2, 3))
            {
                return block2x3Sprite;
            }

            if (size != new Vector2Int(2, 2))
            {
                Debug.LogWarning($"The city view has no block for a {size.x} by {size.y} footprint; it shows a 2 by 2 one.", this);
            }

            return block2x2Sprite;
        }

        private SpriteRenderer CreateRenderer(string objectName, Transform parent, Sprite sprite)
        {
            var spriteRenderer = CreateChild(objectName, parent).AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;

            if (spriteMaterial != null)
            {
                spriteRenderer.sharedMaterial = spriteMaterial;
            }

            return spriteRenderer;
        }

        private GameObject CreateChild(string objectName, Transform parent)
        {
            var child = new GameObject(objectName) { layer = gameObject.layer };
            child.transform.SetParent(parent, false);
            return child;
        }
    }
}
