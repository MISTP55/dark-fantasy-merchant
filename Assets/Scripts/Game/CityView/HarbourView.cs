using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Draws the ships on the shown city's grid (arriving, docked, leaving) as cylinders, tinted the player's or the
    /// AI's, where its <see cref="Harbour"/> puts them. They are re-placed and re-ranked every frame, the only things of
    /// the view that are: their sortingOrder is <see cref="CityGridView.ShipSortingOrderAt"/> of their rounded cell.
    /// Waiting ships are not on the grid and are not drawn; berths are never drawn. It reads the harbour through
    /// <see cref="SimulationServices.Current"/> and gives no orders.
    /// </summary>
    public sealed class HarbourView : MonoBehaviour
    {
        /// <summary>A click picks the nearest ship whose grid point is at most this far, in cells.</summary>
        public const float PickRadius = 0.5f;

        [SerializeField] private CityGridView gridView;

        [Tooltip("Unlit: the city view has no light.")]
        [SerializeField] private Material spriteMaterial;

        [Tooltip("A cylinder standing on one cell, its pivot at the centre of the cell on the ground.")]
        [SerializeField] private Sprite shipSprite;

        [Tooltip("A ship is drawn this much smaller than its cell, around the centre of the cell on the ground.")]
        [SerializeField, Range(0.3f, 1f)] private float shipScale = 0.8f;

        [SerializeField] private Color playerShipColor = Color.white;
        [SerializeField] private Color aiShipColor = new Color(0.5f, 0.7f, 1f, 1f);

        // Ships of equal sortingOrder (between the same two buildings) are told apart by their distance to the
        // camera: the farther back on the grid, the farther away, so drawn first.
        private const float DepthPerCell = 0.01f;

        private readonly List<Ship> harbourShips = new List<Ship>();

        // The ships drawn this frame and their grid points, by index: what a click picks among.
        private readonly List<Ship> drawnShips = new List<Ship>();
        private readonly List<Vector2> drawnPoints = new List<Vector2>();

        // Pooled: those past the ships drawn are off.
        private readonly List<SpriteRenderer> shipRenderers = new List<SpriteRenderer>();

        private Harbour harbour;
        private Transform shipRoot;

        /// <summary>The harbour shown, or null.</summary>
        public Harbour Harbour => harbour;

        /// <summary>The ships drawn at the last frame, in the harbour's entry order.</summary>
        public IReadOnlyList<Ship> DrawnShips => drawnShips;

        /// <summary>The renderers, pooled: the first <see cref="DrawnShips"/> count are on, by the same index.</summary>
        public IReadOnlyList<SpriteRenderer> ShipRenderers => shipRenderers;

        /// <summary>Shows the ships of this city's harbour from now on; none for a city without one, or null.</summary>
        public void Show(CityDefinition city)
        {
            SimulationServices services = SimulationServices.Current;
            harbour = city != null && services != null && services.WorldEconomy != null
                ? services.WorldEconomy.HarbourOf(city)
                : null;
            Redraw();
        }

        /// <summary>
        /// The nearest ship within <see cref="PickRadius"/> of the clicked grid point, the first listed on a tie.
        /// </summary>
        /// <param name="gridPoints">The grid point of each ship, by index.</param>
        public static bool TryPick(IReadOnlyList<Ship> ships, IReadOnlyList<Vector2> gridPoints, Vector2 clickedGridPoint, out Ship ship)
        {
            ship = null;
            float best = PickRadius * PickRadius;

            for (int i = 0; i < ships.Count && i < gridPoints.Count; i++)
            {
                float distance = (gridPoints[i] - clickedGridPoint).sqrMagnitude;

                if (distance <= best && (ship == null || distance < best))
                {
                    ship = ships[i];
                    best = distance;
                }
            }

            return ship != null;
        }

        /// <summary>
        /// The ship drawn under this world position, if any. The click is taken at the middle height of a cylinder,
        /// not on the ground, so that a click on its body picks it.
        /// </summary>
        public bool TryPickAt(Vector3 worldPosition, out Ship ship)
        {
            Vector2 local = transform.InverseTransformPoint(worldPosition);
            local.y -= ShipBodyHeight * 0.5f;
            return TryPick(drawnShips, drawnPoints, IsoProjection.GridPointAt(local), out ship);
        }

        private void LateUpdate()
        {
            Redraw();
        }

        // The ships move every step of the world's time: re-placed every frame, after the simulation's Update.
        private void Redraw()
        {
            drawnShips.Clear();
            drawnPoints.Clear();

            if (harbour != null)
            {
                harbour.GetShips(harbourShips);

                foreach (Ship ship in harbourShips)
                {
                    if (harbour.TryGetPosition(ship, out Vector2 gridPoint))
                    {
                        drawnShips.Add(ship);
                        drawnPoints.Add(gridPoint);
                    }
                }
            }

            ShipsView shipsView = SimulationServices.Current != null ? SimulationServices.Current.ShipsView : null;

            for (int i = 0; i < drawnShips.Count; i++)
            {
                if (i == shipRenderers.Count)
                {
                    shipRenderers.Add(CreateRenderer());
                }

                SpriteRenderer shipRenderer = shipRenderers[i];
                Vector2 gridPoint = drawnPoints[i];
                Vector2 position = IsoProjection.PointOf(gridPoint);
                shipRenderer.transform.localPosition = new Vector3(position.x, position.y, (gridPoint.x + gridPoint.y) * DepthPerCell);
                shipRenderer.sortingOrder = gridView != null ? gridView.ShipSortingOrderAt(Vector2Int.RoundToInt(gridPoint)) : 0;
                shipRenderer.color = shipsView != null && shipsView.IsPlayerShip(drawnShips[i]) ? playerShipColor : aiShipColor;
                shipRenderer.enabled = true;
            }

            for (int i = drawnShips.Count; i < shipRenderers.Count; i++)
            {
                shipRenderers[i].enabled = false;
            }
        }

        // The cylinder's height above its ground, in world units, as drawn: the sprite's height less its cell's diamond.
        private float ShipBodyHeight => shipSprite != null
            ? Mathf.Max(0f, shipSprite.bounds.size.y - IsoProjection.CellHeight) * shipScale
            : 0f;

        private SpriteRenderer CreateRenderer()
        {
            if (shipRoot == null)
            {
                shipRoot = new GameObject("Ships") { layer = gameObject.layer }.transform;
                shipRoot.SetParent(transform, false);
            }

            var child = new GameObject("Ship") { layer = gameObject.layer };
            child.transform.SetParent(shipRoot, false);
            child.transform.localScale = new Vector3(shipScale, shipScale, 1f);

            var spriteRenderer = child.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = shipSprite;

            if (spriteMaterial != null)
            {
                spriteRenderer.sharedMaterial = spriteMaterial;
            }

            return spriteRenderer;
        }
    }
}
