using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Draws a city's grid in placeholder shapes: a diamond per cell, tinted free or forbidden, and a block per
    /// building, a house tinted by the class it shelters (<see cref="HouseColors"/>, as in the layout window), a
    /// workshop in the workshop colour. A building placed while the grid is shown gets its block then; nothing is
    /// redrawn every frame. Every sprite is on this object's layer, which only the city view's camera draws.
    /// </summary>
    public sealed class CityGridView : MonoBehaviour
    {
        // Ground cells are flat diamonds that never overlap: one order for all, below every building.
        private const int GroundSortingOrder = 0;
        private const int FirstBuildingSortingOrder = 1;

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

        [SerializeField] private Color freeCellColor = new Color32(0x3B, 0x4A, 0x3A, 0xFF);
        [SerializeField] private Color forbiddenCellColor = new Color32(0x24, 0x43, 0x5E, 0xFF);
        [Tooltip("The colour of every workshop; a house takes the colour of its class.")]
        [SerializeField] private Color workshopColor = new Color32(0xD6, 0x3A, 0xC4, 0xFF);
        [SerializeField] private Color validGhostColor = new Color32(0x6F, 0xBF, 0x73, 0x99);
        [SerializeField] private Color invalidGhostColor = new Color32(0xD0, 0x50, 0x50, 0x99);

        private readonly List<SpriteRenderer> cellRenderers = new List<SpriteRenderer>();

        // The buildings' renderers by rank: index i is drawn with sortingOrder FirstBuildingSortingOrder + i.
        private readonly List<SpriteRenderer> buildingRenderers = new List<SpriteRenderer>();
        private readonly BuildingDrawOrder drawOrder = new BuildingDrawOrder();

        private CityGrid grid;
        private Transform cellRoot;
        private Transform buildingRoot;
        private SpriteRenderer ghostRenderer;
        private Vector2Int ghostSize;

        /// <summary>The grid shown, or null.</summary>
        public CityGrid Grid => grid;

        public IReadOnlyList<SpriteRenderer> CellRenderers => cellRenderers;

        /// <summary>The buildings' renderers, in drawing order.</summary>
        public IReadOnlyList<SpriteRenderer> BuildingRenderers => buildingRenderers;

        /// <summary>The block of the building being placed, or null before one is shown; off when hidden.</summary>
        public SpriteRenderer GhostRenderer => ghostRenderer;

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
        }

        /// <summary>
        /// Shows the block of the building being placed over the footprint at the origin, above every building,
        /// green where it can stand and red where it cannot.
        /// </summary>
        public void ShowGhost(Vector2Int origin, Vector2Int size, bool canStand)
        {
            if (ghostRenderer == null)
            {
                ghostRenderer = CreateRenderer("Ghost", transform, null);
                ghostRenderer.sortingOrder = GhostSortingOrder;
            }

            // Shown every frame while placing: the sprite is looked up only when the size changes.
            if (ghostRenderer.sprite == null || size != ghostSize)
            {
                ghostRenderer.sprite = BlockSpriteOf(size);
                ghostSize = size;
            }

            ghostRenderer.transform.localPosition = IsoProjection.CenterOf(origin, size);
            ghostRenderer.color = canStand ? validGhostColor : invalidGhostColor;
            ghostRenderer.enabled = true;
        }

        public void HideGhost()
        {
            if (ghostRenderer != null)
            {
                ghostRenderer.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (grid != null)
            {
                grid.BuildingPlaced -= AddBuilding;
            }
        }

        private void Clear()
        {
            if (grid != null)
            {
                grid.BuildingPlaced -= AddBuilding;
            }

            grid = null;
            cellRenderers.Clear();
            buildingRenderers.Clear();
            drawOrder.Clear();

            if (cellRoot != null)
            {
                Destroy(cellRoot.gameObject);
            }

            if (buildingRoot != null)
            {
                Destroy(buildingRoot.gameObject);
            }

            cellRoot = null;
            buildingRoot = null;
            HideGhost();
        }

        private void AddBuilding(PlacedBuilding building)
        {
            Vector2Int origin = building.Place.Origin;
            Vector2Int size = building.Size;
            SpriteRenderer buildingRenderer = CreateRenderer(
                $"{building.Owner} {(building.Kind.IsHouse ? "House" : "Workshop")} {origin.x},{origin.y}", buildingRoot, BlockSpriteOf(size));
            buildingRenderer.transform.localPosition = IsoProjection.CenterOf(origin, size);
            buildingRenderer.color = building.Kind.IsHouse ? HouseColors.Of(building.Kind.HousedClass) : workshopColor;

            // The buildings at and above the new one's rank move up by one.
            int rank = drawOrder.Insert(origin, size);
            buildingRenderers.Insert(rank, buildingRenderer);

            for (int i = rank; i < buildingRenderers.Count; i++)
            {
                buildingRenderers[i].sortingOrder = FirstBuildingSortingOrder + i;
            }
        }

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
