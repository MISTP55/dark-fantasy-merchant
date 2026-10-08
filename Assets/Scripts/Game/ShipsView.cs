using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Owns the ships of the map, their views and the ship selection state, and sails
    /// the ships every frame.
    /// </summary>
    public sealed class ShipsView : MonoBehaviour
    {
        private static readonly Vector2 MapCenter = new Vector2(0.5f, 0.5f);

        [SerializeField] private WorldMapView mapView;
        [SerializeField] private ShipView shipPrefab;
        [SerializeField] private ShipDefinition playerShipDefinition;

        [Tooltip("City the player ship starts on. Empty uses the first city of the map.")]
        [SerializeField] private CityDefinition startCity;

        [Tooltip("On-screen size, in pixels, of one world unit of ship sprite.")]
        [SerializeField] private float shipScreenPixelsPerUnit = 32f;

        private readonly List<Ship> ships = new List<Ship>();
        private readonly List<Vector2> shipWorldPositions = new List<Vector2>();
        private readonly List<ShipView> views = new List<ShipView>();

        // Null on a map without a navigation mask: ships then sail in a straight line.
        private NavigationPathfinder navigation;

        private Ship hoveredShip;
        private Ship selectedShip;

        public MapSelectionState<Ship> Selection { get; } = new MapSelectionState<Ship>();

        public IReadOnlyList<Ship> Ships => ships;

        /// <summary>World position of each entry of <see cref="Ships"/>, in the same order.</summary>
        public IReadOnlyList<Vector2> ShipWorldPositions => shipWorldPositions;

        public bool IsReady => ships.Count > 0;

        // Start, not Awake: WorldMapView lays the map out in its Awake.
        private void Start()
        {
            if (mapView == null || shipPrefab == null || playerShipDefinition == null)
            {
                Debug.LogError("ShipsView needs a map view, a ship prefab and a player ship definition.", this);
                enabled = false;
                return;
            }

            if (!mapView.IsReady)
            {
                // WorldMapView already logged why the map could not be laid out.
                enabled = false;
                return;
            }

            // The Min attribute only constrains the Inspector, not the asset file.
            float speed = playerShipDefinition.Speed;

            if (!(speed > 0f) || float.IsInfinity(speed))
            {
                Debug.LogError(
                    $"ShipDefinition '{playerShipDefinition.name}' has an invalid speed ({speed}).",
                    playerShipDefinition);
                enabled = false;
                return;
            }

            navigation = CreateNavigation();
            Spawn(playerShipDefinition, StartPosition());
            Selection.HoveredChanged += OnHoveredChanged;
            Selection.SelectedChanged += OnSelectedChanged;
        }

        private void OnDestroy()
        {
            Selection.HoveredChanged -= OnHoveredChanged;
            Selection.SelectedChanged -= OnSelectedChanged;
        }

        private void Update()
        {
            for (int i = 0; i < ships.Count; i++)
            {
                ships[i].Advance(Time.deltaTime);
                shipWorldPositions[i] = ships[i].WorldPosition;
                views[i].Refresh();
            }
        }

        public void SetShipScale(float worldUnitsPerPixel)
        {
            float scale = worldUnitsPerPixel * shipScreenPixelsPerUnit;

            foreach (ShipView view in views)
            {
                view.SetBaseScale(scale);
            }
        }

        private Vector2 StartPosition()
        {
            if (startCity != null)
            {
                return startCity.MapPosition;
            }

            if (mapView.Cities.Count > 0)
            {
                return mapView.Cities[0].MapPosition;
            }

            Debug.LogWarning("ShipsView found no city to start on; the player ship starts at the map center.", this);
            return MapCenter;
        }

        private NavigationPathfinder CreateNavigation()
        {
            // The definition is set: the map view is ready.
            NavigationMaskDefinition mask = mapView.Definition.NavigationMask;

            if (mask == null)
            {
                return null;
            }

            var pathfinder = new NavigationPathfinder(mask.CreateGrid());

            if (!pathfinder.HasNavigableCells)
            {
                Debug.LogWarning(
                    $"NavigationMaskDefinition '{mask.name}' has no navigable cell; ships cannot move.",
                    mask);
            }

            return pathfinder;
        }

        private void Spawn(ShipDefinition definition, Vector2 position)
        {
            // A NaN city position would be rejected by the ship.
            if (float.IsNaN(position.x) || float.IsNaN(position.y))
            {
                position = MapCenter;
            }

            // With a pathfinder, a ship that starts on a city starts on the water beside it.
            var ship = new Ship(mapView.Projection, position, definition.Speed, navigation);
            ShipView view = Instantiate(shipPrefab, transform);
            view.Initialize(ship, definition);

            ships.Add(ship);
            shipWorldPositions.Add(ship.WorldPosition);
            views.Add(view);
        }

        private void OnHoveredChanged(Ship ship)
        {
            ViewOf(hoveredShip)?.SetHovered(false);
            hoveredShip = ship;
            ViewOf(hoveredShip)?.SetHovered(true);
        }

        private void OnSelectedChanged(Ship ship)
        {
            ViewOf(selectedShip)?.SetSelected(false);
            selectedShip = ship;
            ViewOf(selectedShip)?.SetSelected(true);
        }

        private ShipView ViewOf(Ship ship)
        {
            int index = ship != null ? ships.IndexOf(ship) : -1;

            return index >= 0 ? views[index] : null;
        }
    }
}
