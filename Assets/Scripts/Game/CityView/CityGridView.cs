using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Draws a city's grid: its ground in one mesh (<see cref="GroundMesh"/>: a diamond per cell, tinted free,
    /// forbidden or road), and a placeholder block per other building, a house tinted by the class it shelters
    /// (<see cref="HouseColors"/>, as in the layout window), a workshop in the workshop colour, the town hall and the walls in their own, a wall
    /// by the walls beside it (<see cref="WallShapes"/>). A building whose definition has a view
    /// sprite is drawn with it instead, untinted, mirrored in the orientation the sprite does not show. A house or
    /// workshop that is not connected is darkened until it is. A building placed while the grid is shown is drawn
    /// then; nothing is redrawn every frame, a road's cell being tinted on the ground at the end of the frame it is
    /// placed in. Everything is on this object's layer, which only the city view's camera draws.
    /// </summary>
    public sealed class CityGridView : MonoBehaviour
    {
        // The ground and its roads, flat diamonds that never overlap: below every building and ship.
        private const int GroundSortingOrder = 0;

        // Buildings go every other order from here, a ship in the gap before the building it is behind: the lowest
        // ship order, FirstBuildingSortingOrder - 1, stays above the ground.
        private const int FirstBuildingSortingOrder = 3;
        private const int BuildingSortingStride = 2;

        // Above every building: a sortingOrder is a 16-bit value.
        private const int GhostSortingOrder = short.MaxValue;

        /// <summary>The default of buildingScale, which the walls' placeholders are drawn for.</summary>
        public const float DefaultBuildingScale = 0.85f;

        [Tooltip("Unlit: the city view has no light.")]
        [SerializeField] private Material spriteMaterial;

        [Tooltip("Drawn on every cell of the ground, its pivot at the cell's centre, and as the road ghost's tiles.")]
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

        [Tooltip("The port facing north: 4 cells on x and 3 on y, its water rows at the back. Mirrored, it faces east.")]
        [SerializeField] private Sprite portBackSprite;

        [Tooltip("The port facing south: 4 cells on x and 3 on y, its water rows in front. Mirrored, it faces west.")]
        [SerializeField] private Sprite portFrontSprite;

        [Tooltip("A wall by the sides on which another wall stands, indexed by its WallSides (east 1, north 2, west 4, south 8): a straight section for east and west or north and south, a tower joined to its neighbours otherwise. Their gap with the cells beside them is drawn in, so they are not scaled by buildingScale.")]
        [SerializeField] private Sprite[] wallSprites = new Sprite[WallShapes.Count];

        [SerializeField] private Color freeCellColor = new Color32(0x3B, 0x4A, 0x3A, 0xFF);
        [SerializeField] private Color forbiddenCellColor = new Color32(0x24, 0x43, 0x5E, 0xFF);
        [Tooltip("The colour of every workshop; a house takes the colour of its class.")]
        [SerializeField] private Color workshopColor = new Color32(0xD6, 0x3A, 0xC4, 0xFF);
        [SerializeField] private Color townHallColor = new Color32(0xC9, 0xA2, 0x27, 0xFF);
        [SerializeField] private Color portColor = new Color32(0xB0, 0x7A, 0x48, 0xFF);
        [SerializeField] private Color wallColor = new Color32(0x3A, 0x3A, 0x3A, 0xFF);
        [SerializeField] private Color roadColor = new Color32(0x8A, 0x7B, 0x62, 0xFF);
        [Tooltip("A house or workshop that is not connected is drawn with its colour times this on red, green and blue.")]
        [SerializeField, Range(0.1f, 1f)] private float inactiveShade = 0.45f;
        [Tooltip("A building's block is drawn this much smaller than its footprint, around its centre, so that a gap shows between neighbours.")]
        [SerializeField, Range(0.5f, 1f)] private float buildingScale = DefaultBuildingScale;

        [SerializeField] private Color validGhostColor = new Color32(0x6F, 0xBF, 0x73, 0x99);
        [SerializeField] private Color unconnectedGhostColor = new Color32(0xE0, 0xC0, 0x40, 0x99);
        [SerializeField] private Color invalidGhostColor = new Color32(0xD0, 0x50, 0x50, 0x99);

        // The buildings' renderers by rank: index i is drawn with sortingOrder FirstBuildingSortingOrder + 2 i.
        private readonly List<SpriteRenderer> buildingRenderers = new List<SpriteRenderer>();
        private readonly BuildingDrawOrder drawOrder = new BuildingDrawOrder();

        // The renderers of the houses and workshops, to give them their colour when they connect.
        private readonly Dictionary<PlacedBuilding, SpriteRenderer> shadedRenderers = new Dictionary<PlacedBuilding, SpriteRenderer>();

        // The buildings drawn with their definition's view sprite rather than a block: they are not tinted.
        private readonly HashSet<SpriteRenderer> viewSpriteRenderers = new HashSet<SpriteRenderer>();
        private readonly List<SpriteRenderer> roadGhostRenderers = new List<SpriteRenderer>();

        private CityGrid grid;
        private BuildingsDefinition buildings;
        private EconomyDefinition economy;
        private GroundMesh ground;
        private GameObject groundObject;
        private MeshRenderer groundRenderer;
        private Transform buildingRoot;
        private Transform roadGhostRoot;
        private SpriteRenderer ghostRenderer;
        private Vector2Int ghostSize;

        /// <summary>The grid shown, or null.</summary>
        public CityGrid Grid => grid;

        /// <summary>The ground of the grid shown, its roads included, or null.</summary>
        public GroundMesh Ground => ground;

        /// <summary>The renderer of <see cref="Ground"/>, or null.</summary>
        public MeshRenderer GroundRenderer => groundRenderer;

        /// <summary>The blocks' renderers (every building but the roads), in drawing order.</summary>
        public IReadOnlyList<SpriteRenderer> BuildingRenderers => buildingRenderers;

        /// <summary>The block of the building being placed, or null before one is shown; off when hidden.</summary>
        public SpriteRenderer GhostRenderer => ghostRenderer;

        /// <summary>The tiles of the road being placed, pooled: those past the road's length are off.</summary>
        public IReadOnlyList<SpriteRenderer> RoadGhostRenderers => roadGhostRenderers;

        /// <summary>
        /// The sortingOrder of a ship on this cell: in the gap below the first building whose draw key is not smaller
        /// than a 1 by 1 block's here, so that it is drawn after every building behind it and before every building
        /// in front of it. Above the ground and the roads, below the ghost.
        /// </summary>
        public int ShipSortingOrderAt(Vector2Int cell)
        {
            int rank = drawOrder.RankOf(IsoProjection.DrawKeyOf(cell, Vector2Int.one));
            return FirstBuildingSortingOrder + BuildingSortingStride * rank - 1;
        }

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

        /// <summary>
        /// Draws this grid in place of the one shown, if any. Null shows nothing. The catalogue gives the buildings
        /// their view sprites, the economy the good of a workshop; without them every building is a block.
        /// </summary>
        public void Show(CityGrid newGrid, BuildingsDefinition buildingsDefinition, EconomyDefinition economyDefinition)
        {
            Clear();
            grid = newGrid;
            buildings = buildingsDefinition;
            economy = economyDefinition;

            if (grid == null)
            {
                return;
            }

            CreateGround();
            buildingRoot = CreateChild("Buildings", transform).transform;

            foreach (PlacedBuilding building in grid.Buildings)
            {
                AddBuilding(building);
            }

            // Its water and roads from the first frame, whenever in the frame it is shown.
            ground?.Apply();
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

        // Roads are placed by the trace or by the simulation, often many in one frame: uploaded once.
        private void LateUpdate()
        {
            ground?.Apply();
        }

        private void OnDestroy()
        {
            if (grid != null)
            {
                grid.BuildingPlaced -= AddBuilding;
                grid.BuildingConnected -= OnBuildingConnected;
            }

            DestroyGround();
        }

        private void CreateGround()
        {
            if (cellSprite == null)
            {
                Debug.LogError("CityGridView needs a cell sprite to draw the ground.", this);
                return;
            }

            ground = new GroundMesh(grid.Width, grid.Height, cellSprite, freeCellColor);

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);

                    if (grid.IsForbidden(cell))
                    {
                        ground.SetColor(cell, forbiddenCellColor);
                    }
                }
            }

            groundObject = CreateChild("Ground", transform);
            groundObject.AddComponent<MeshFilter>().sharedMesh = ground.Mesh;
            groundRenderer = groundObject.AddComponent<MeshRenderer>();
            groundRenderer.sortingOrder = GroundSortingOrder;

            if (spriteMaterial != null)
            {
                groundRenderer.sharedMaterial = spriteMaterial;
            }

            // The material is shared with the sprites, which give it their texture themselves: the ground's is its own.
            var properties = new MaterialPropertyBlock();
            properties.SetTexture("_MainTex", cellSprite.texture);
            groundRenderer.SetPropertyBlock(properties);
        }

        private void DestroyGround()
        {
            if (groundObject != null)
            {
                Destroy(groundObject);
            }

            ground?.Dispose();
            ground = null;
            groundObject = null;
            groundRenderer = null;
        }

        private void Clear()
        {
            if (grid != null)
            {
                grid.BuildingPlaced -= AddBuilding;
                grid.BuildingConnected -= OnBuildingConnected;
            }

            grid = null;
            buildings = null;
            economy = null;
            buildingRenderers.Clear();
            shadedRenderers.Clear();
            viewSpriteRenderers.Clear();
            roadGhostRenderers.Clear();
            drawOrder.Clear();
            DestroyGround();

            if (buildingRoot != null)
            {
                Destroy(buildingRoot.gameObject);
            }

            if (roadGhostRoot != null)
            {
                Destroy(roadGhostRoot.gameObject);
            }

            buildingRoot = null;
            roadGhostRoot = null;
            HideGhost();
        }

        private void AddBuilding(PlacedBuilding building)
        {
            Vector2Int origin = building.Place.Origin;

            // A road is flat on its cell: the ground's cell takes its colour.
            if (building.Kind.IsRoad)
            {
                ground?.SetColor(origin, roadColor);
                return;
            }

            Vector2Int size = building.Size;
            string kindName = building.Kind.IsHouse ? "House" : building.Kind.IsWorkshop ? "Workshop"
                : building.Kind.IsPort ? "Port" : building.Kind.IsWall ? "Wall" : "Town hall";

            // The port's placeholder shows its facing; a view sprite, which has only two orientations, is not used for it.
            BuildingDefinition definition = buildings != null && !building.Kind.IsPort ? buildings.DefinitionOf(building.Kind, economy) : null;
            Sprite viewSprite = definition != null ? definition.ViewSprite : null;
            (bool waterAtBack, bool portFlipped) = building.Kind.IsPort ? PortSpriteOf(building.PortFacing) : (false, false);
            Sprite sprite = viewSprite != null ? viewSprite
                : building.Kind.IsPort ? (waterAtBack ? portBackSprite : portFrontSprite)
                : building.Kind.IsWall ? WallSpriteOf(WallShapes.SidesOf(grid, origin))
                : BlockSpriteOf(size);
            SpriteRenderer buildingRenderer = CreateRenderer(
                $"{building.Owner} {kindName} {origin.x},{origin.y}", buildingRoot, sprite);
            buildingRenderer.transform.localPosition = IsoProjection.CenterOf(origin, size);
            buildingRenderer.flipX = portFlipped;

            // A view sprite has its own size; mirrored around its pivot, it shows the other orientation.
            if (viewSprite != null)
            {
                buildingRenderer.flipX = building.Place.Orientation != definition.ViewSpriteOrientation;
                viewSpriteRenderers.Add(buildingRenderer);
            }
            else if (!building.Kind.IsWall)
            {
                buildingRenderer.transform.localScale = BuildingScaleVector;
            }

            buildingRenderer.color = ColorOf(building, buildingRenderer);

            if (building.Kind.IsHouse || building.Kind.IsWorkshop)
            {
                shadedRenderers.Add(building, buildingRenderer);
            }

            // The buildings at and above the new one's rank move up by one.
            int rank = drawOrder.Insert(origin, size);
            buildingRenderers.Insert(rank, buildingRenderer);

            for (int i = rank; i < buildingRenderers.Count; i++)
            {
                buildingRenderers[i].sortingOrder = FirstBuildingSortingOrder + BuildingSortingStride * i;
            }
        }

        // IsConnected is not up to date inside BuildingPlaced: a building placed connected is lit by BuildingConnected.
        private void OnBuildingConnected(PlacedBuilding building)
        {
            if (shadedRenderers.TryGetValue(building, out SpriteRenderer buildingRenderer))
            {
                buildingRenderer.color = ColorOf(building, buildingRenderer);
            }
        }

        private Color ColorOf(PlacedBuilding building, SpriteRenderer buildingRenderer)
        {
            bool isViewSprite = viewSpriteRenderers.Contains(buildingRenderer);

            if (building.Kind.IsTownHall)
            {
                return isViewSprite ? Color.white : townHallColor;
            }

            if (building.Kind.IsPort)
            {
                return portColor;
            }

            if (building.Kind.IsWall)
            {
                return isViewSprite ? Color.white : wallColor;
            }

            Color color = isViewSprite ? Color.white
                : building.Kind.IsHouse ? HouseColors.Of(building.Kind.HousedClass)
                : workshopColor;

            if (building.IsConnected)
            {
                return color;
            }

            return new Color(color.r * inactiveShade, color.g * inactiveShade, color.b * inactiveShade, color.a);
        }

        // The scale of a building's block, and of the ghost, which shows the building as it will stand.
        private Vector3 BuildingScaleVector => new Vector3(buildingScale, buildingScale, 1f);

        /// <summary>
        /// The port's placeholder for a facing: the one with its water rows at the back (facing north) or in front
        /// (facing south), and whether it is mirrored. Mirroring swaps the screen directions of x and y, so the north
        /// one, its water at the upper left, shows the east facing, its water at the upper right.
        /// </summary>
        internal static (bool waterAtBack, bool flipX) PortSpriteOf(PortFacing facing)
        {
            switch (facing)
            {
                case PortFacing.North:
                    return (true, false);
                case PortFacing.East:
                    return (true, true);
                case PortFacing.South:
                    return (false, false);
                default:
                    return (false, true);
            }
        }

        // The walls are placed only by the layout, before the grid is shown: their neighbours never change under the view.
        private Sprite WallSpriteOf(WallSides sides)
        {
            int index = (int)sides;
            return wallSprites != null && index < wallSprites.Length ? wallSprites[index] : null;
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
