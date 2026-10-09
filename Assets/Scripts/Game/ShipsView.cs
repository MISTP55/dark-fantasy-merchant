using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Owns the ships of the map, their views, the ship selection state and which ships
    /// lie in which city's port, sails the ships every frame and shows the route of the
    /// selected one.
    /// </summary>
    public sealed class ShipsView : MonoBehaviour
    {
        private static readonly Vector2 MapCenter = new Vector2(0.5f, 0.5f);

        // CityPicker skips NaN positions.
        private static readonly Vector2 NotOnMap = new Vector2(float.NaN, float.NaN);

        [SerializeField] private WorldMapView mapView;
        [SerializeField] private ShipView shipPrefab;
        [SerializeField] private ShipDefinition playerShipDefinition;

        [Tooltip("City the player ship starts on. Empty uses the first city of the map.")]
        [SerializeField] private CityDefinition startCity;

        [Tooltip("Optional. Without it, the player ship starts with a full crew.")]
        [SerializeField] private PlayerStartDefinition playerStart;

        [Tooltip("On-screen size, in pixels, of one world unit of ship sprite.")]
        [SerializeField] private float shipScreenPixelsPerUnit = 32f;

        [Tooltip("Optional. Shows the route of the selected ship.")]
        [SerializeField] private ShipRouteView routeView;

        [Tooltip("Optional. Without it, ships sail in real time.")]
        [SerializeField] private WorldClock worldClock;

        private readonly List<Ship> ships = new List<Ship>();
        private readonly List<Vector2> shipWorldPositions = new List<Vector2>();
        private readonly List<ShipView> views = new List<ShipView>();
        private readonly List<ShipDefinition> definitions = new List<ShipDefinition>();

        private ShipArrivals<CityDefinition> arrivals;

        // Null on a map without a navigation mask: ships then sail in a straight line.
        private NavigationPathfinder navigation;

        private Ship hoveredShip;
        private Ship selectedShip;

        public MapSelectionState<Ship> Selection { get; } = new MapSelectionState<Ship>();

        /// <summary>
        /// Ships in port. Orders go through it, so that a ship enters the city it was
        /// sent to and leaves the one it is in.
        /// </summary>
        public ShipDocking<CityDefinition> Docking { get; } = new ShipDocking<CityDefinition>();

        /// <summary>
        /// Raised when a ship arrives (see <see cref="ShipArrivals{TPort}"/>), with the city
        /// whose port it has entered, or null when it stopped at sea.
        /// </summary>
        public event Action<Ship, CityDefinition> ShipArrived;

        public IReadOnlyList<Ship> Ships => ships;

        /// <summary>
        /// World position of each entry of <see cref="Ships"/>, in the same order. NaN for
        /// a ship in port: it is not on the map and cannot be picked there.
        /// </summary>
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

            int crewCapacity = playerShipDefinition.CrewCapacity;

            if (crewCapacity < 1)
            {
                Debug.LogError(
                    $"ShipDefinition '{playerShipDefinition.name}' has an invalid crew capacity ({crewCapacity}).",
                    playerShipDefinition);
                enabled = false;
                return;
            }

            navigation = CreateNavigation();
            arrivals = new ShipArrivals<CityDefinition>(Docking);
            arrivals.Arrived += OnShipArrived;
            Spawn(playerShipDefinition, StartPosition(), CreatePlayerCrew(crewCapacity));
            Selection.HoveredChanged += OnHoveredChanged;
            Selection.SelectedChanged += OnSelectedChanged;
            Docking.Docked += OnDocked;
            Docking.Undocked += OnUndocked;
        }

        private void OnDestroy()
        {
            Selection.HoveredChanged -= OnHoveredChanged;
            Selection.SelectedChanged -= OnSelectedChanged;
            Docking.Docked -= OnDocked;
            Docking.Undocked -= OnUndocked;
        }

        private void Update()
        {
            // The world's time: ships sail faster in fast forward.
            float deltaTime = worldClock != null && worldClock.IsReady ? worldClock.Clock.DeltaTime : Time.deltaTime;

            foreach (Ship ship in ships)
            {
                ship.Advance(deltaTime);
            }

            Docking.Update();

            // After the docking's update, so that a ship that arrived at its port is in it.
            arrivals.Report();

            for (int i = 0; i < ships.Count; i++)
            {
                shipWorldPositions[i] = Docking.IsDocked(ships[i]) ? NotOnMap : ships[i].WorldPosition;
                views[i].Refresh();
            }
        }

        // LateUpdate, not Update: the selection and the orders of this frame are in.
        private void LateUpdate()
        {
            if (routeView == null)
            {
                return;
            }

            Ship ship = Selection.Selected;

            // A city marks the end of a route that enters its port.
            routeView.Show(ship, mapView.Projection, Docking.DestinationPortOf(ship) == null);
        }

        /// <returns>The name of the ship's definition, or null for a ship that is not on this map.</returns>
        public string DisplayNameOf(Ship ship)
        {
            int index = ship != null ? ships.IndexOf(ship) : -1;

            return index >= 0 ? definitions[index].DisplayName : null;
        }

        public void SetShipScale(float worldUnitsPerPixel)
        {
            float scale = worldUnitsPerPixel * shipScreenPixelsPerUnit;

            foreach (ShipView view in views)
            {
                view.SetBaseScale(scale);
            }

            if (routeView != null)
            {
                routeView.SetScale(worldUnitsPerPixel);
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

        private ShipCrew CreatePlayerCrew(int capacity)
        {
            if (playerStart == null)
            {
                return new ShipCrew(capacity, capacity);
            }

            if (playerStart.StartingCrew > capacity)
            {
                Debug.LogWarning(
                    $"PlayerStartDefinition '{playerStart.name}' starts with {playerStart.StartingCrew} sailors; "
                    + $"the player ship only has room for {capacity}.",
                    playerStart);
            }

            // Nothing hires sailors yet: a ship that starts without one never sails.
            if (playerStart.StartingCrew < 1)
            {
                Debug.LogWarning(
                    $"PlayerStartDefinition '{playerStart.name}' starts with no sailor; the player ship cannot sail.",
                    playerStart);
            }

            return playerStart.CreateShipCrew(capacity);
        }

        private void Spawn(ShipDefinition definition, Vector2 position, ShipCrew crew)
        {
            // A NaN city position would be rejected by the ship.
            if (float.IsNaN(position.x) || float.IsNaN(position.y))
            {
                position = MapCenter;
            }

            // With a pathfinder, a ship that starts on a city starts on the water beside it.
            // The Min attribute only constrains the Inspector, not the asset file.
            var cargo = new CargoHold(Mathf.Max(0, definition.CargoCapacity));
            var ship = new Ship(mapView.Projection, position, definition.Speed, crew, navigation, cargo);
            ShipView view = Instantiate(shipPrefab, transform);
            view.Initialize(ship, definition);
            arrivals.Track(ship);

            ships.Add(ship);
            shipWorldPositions.Add(ship.WorldPosition);
            views.Add(view);
            definitions.Add(definition);
        }

        // A ship in port is not on the map: it is neither shown, hovered nor selected
        // there. It can be selected again from its city's panel.
        private void OnDocked(Ship ship, CityDefinition city)
        {
            if (ReferenceEquals(Selection.Hovered, ship))
            {
                Selection.SetHovered(null);
            }

            if (ReferenceEquals(Selection.Selected, ship))
            {
                Selection.ClearSelection();
            }

            int index = ships.IndexOf(ship);

            if (index >= 0)
            {
                shipWorldPositions[index] = NotOnMap;
                views[index].gameObject.SetActive(false);
            }
        }

        private void OnUndocked(Ship ship, CityDefinition city)
        {
            int index = ships.IndexOf(ship);

            if (index >= 0)
            {
                shipWorldPositions[index] = ship.WorldPosition;
                views[index].gameObject.SetActive(true);
                views[index].Refresh();
            }
        }

        private void OnShipArrived(Ship ship, CityDefinition city)
        {
            ShipArrived?.Invoke(ship, city);
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
