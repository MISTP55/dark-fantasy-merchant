using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Owns the ships of the map, the player's and the others, their views, the ship
    /// selection state and which ships lie in which city's port, sails the ships at every
    /// step of the world's time and shows the route of the selected one.
    /// </summary>
    public sealed class ShipsView : MonoBehaviour
    {
        private static readonly Vector2 MapCenter = new Vector2(0.5f, 0.5f);

        // CityPicker skips NaN positions.
        private static readonly Vector2 NotOnMap = new Vector2(float.NaN, float.NaN);

        [SerializeField] private WorldMapView mapView;
        [SerializeField] private ShipView shipPrefab;
        [SerializeField] private ShipDefinition playerShipDefinition;

        [Tooltip("City the player ship starts on. Empty, or an inactive city, uses the first active city of the map.")]
        [SerializeField] private CityDefinition startCity;

        [Tooltip("Optional. Without it, the player ship starts with a full crew.")]
        [SerializeField] private PlayerStartDefinition playerStart;

        [Tooltip("On-screen size, in pixels, of one world unit of ship sprite.")]
        [SerializeField] private float shipScreenPixelsPerUnit = 32f;

        [Tooltip("Optional. Shows the route of the selected ship.")]
        [SerializeField] private ShipRouteView routeView;

        [Tooltip("Optional. Without it, ships sail in real time.")]
        [SerializeField] private WorldClock worldClock;

        [Tooltip("Optional. Without it, ships enter ports at once.")]
        [SerializeField] private WorldEconomy worldEconomy;

        [Tooltip("The economy whose goods ships carry: it sets the room a unit of each good takes in a hold. Without it, every unit takes one barrel.")]
        [SerializeField] private EconomyDefinition economy;

        private GoodUnits goodUnits;

        // Parallel lists, by index: a ship is added to and removed from all of them at once (RemoveShip).
        private readonly List<Ship> ships = new List<Ship>();
        private readonly List<Vector2> shipWorldPositions = new List<Vector2>();
        private readonly List<ShipView> views = new List<ShipView>();
        // Name of each entry of ships, in the same order: several ships can share a definition.
        private readonly List<string> displayNames = new List<string>();

        private readonly List<Ship> playerShips = new List<Ship>();

        private ShipArrivals<CityDefinition> arrivals;

        // Null on a map without a navigation mask: ships then sail in a straight line.
        private NavigationPathfinder navigation;

        private Ship hoveredShip;
        private Ship selectedShip;

        // Null without a usable world clock: ships then sail in real time, once per frame.
        private GameClock clock;

        private ShipDocking<CityDefinition> docking;

        public MapSelectionState<Ship> Selection { get; } = new MapSelectionState<Ship>();

        /// <summary>
        /// Ships in the cities, in their harbours and in port. Orders go through it, so that a ship
        /// enters the city it was sent to and leaves the one it is in. Made on first use: other
        /// components listen to it in their OnEnable, which can come before this one's Awake.
        /// </summary>
        public ShipDocking<CityDefinition> Docking
        {
            get
            {
                if (docking == null)
                {
                    // The economy makes the harbours with the grids, in its Start, before any ship enters a city.
                    docking = new ShipDocking<CityDefinition>(city => worldEconomy != null ? worldEconomy.HarbourOf(city) : null);
                }

                return docking;
            }
        }

        /// <summary>
        /// Raised when a ship arrives (see <see cref="ShipArrivals{TPort}"/>), with the city
        /// whose port it has entered, or null when it stopped at sea.
        /// </summary>
        public event Action<Ship, CityDefinition> ShipArrived;

        /// <summary>Raised when a ship enters a city's queue, every berth being taken, with the city.</summary>
        public event Action<Ship, CityDefinition> ShipQueued;

        /// <summary>
        /// Raised at every step of the world's time, with its simulated seconds, before the
        /// ships sail it: what gives ships their orders for the step listens to it.
        /// </summary>
        public event Action<float> StepStarted;

        public IReadOnlyList<Ship> Ships => ships;

        /// <summary>The ships of <see cref="Ships"/> that the player owns and orders about.</summary>
        public IReadOnlyList<Ship> PlayerShips => playerShips;

        /// <summary>Where ships can sail. Null before Start, and on a map without a navigation mask.</summary>
        public NavigationPathfinder Navigation => navigation;

        /// <summary>
        /// World position of each entry of <see cref="Ships"/>, in the same order. NaN for
        /// a ship in a city: it is not on the map and cannot be picked there.
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

            if (!IsUsable(playerShipDefinition))
            {
                enabled = false;
                return;
            }

            navigation = CreateNavigation();
            arrivals = new ShipArrivals<CityDefinition>(Docking);
            arrivals.Arrived += OnShipArrived;
            arrivals.Queued += OnShipQueued;
            playerShips.Add(Spawn(
                playerShipDefinition,
                StartPosition(),
                CreatePlayerCrew(playerShipDefinition.CrewCapacity),
                playerShipDefinition.DisplayName,
                null));
            Selection.HoveredChanged += OnHoveredChanged;
            Selection.SelectedChanged += OnSelectedChanged;
            Docking.CityStateChanged += OnCityStateChanged;

            // The world's time: ships sail faster in fast forward, in steps short enough that
            // none sails past its port's arrival, however fast.
            if (worldClock != null && worldClock.IsReady)
            {
                clock = worldClock.Clock;
                clock.Stepped += Step;
            }
        }

        private void OnDestroy()
        {
            Selection.HoveredChanged -= OnHoveredChanged;
            Selection.SelectedChanged -= OnSelectedChanged;
            Docking.CityStateChanged -= OnCityStateChanged;

            if (clock != null)
            {
                clock.Stepped -= Step;
            }
        }

        private void Update()
        {
            if (clock == null)
            {
                Step(Time.deltaTime);
            }

            // Once per frame, after all the steps of the frame.
            for (int i = 0; i < ships.Count; i++)
            {
                shipWorldPositions[i] = Docking.IsInCity(ships[i]) ? NotOnMap : ships[i].WorldPosition;
                views[i].Refresh();
            }
        }

        private void Step(float deltaTime)
        {
            StepStarted?.Invoke(deltaTime);

            // A ship in a city keeps its route for when it is back at sea: it does not sail it there.
            foreach (Ship ship in ships)
            {
                if (!Docking.IsInCity(ship))
                {
                    ship.Advance(deltaTime);
                }
            }

            // After the ships: it lets into the cities the ships that have reached their anchorage.
            Docking.Update(deltaTime);

            // After the docking's update, so that a ship that arrived at its port is in it.
            arrivals.Report();
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

        /// <returns>The name of the ship, or null for a ship that is not on this map.</returns>
        public string DisplayNameOf(Ship ship)
        {
            int index = ship != null ? ships.IndexOf(ship) : -1;

            return index >= 0 ? displayNames[index] : null;
        }

        public bool IsPlayerShip(Ship ship)
        {
            return ship != null && playerShips.Contains(ship);
        }

        /// <returns>Whether the ship is one of this map's: false once it was removed.</returns>
        public bool Contains(Ship ship)
        {
            return ship != null && ships.Contains(ship);
        }

        /// <summary>
        /// Takes a ship off the map for good, with what it carries: it leaves its port, is
        /// neither hovered nor selected any more, and its view is destroyed. Any ship is
        /// taken, the player's too: whoever calls decides which ships can go.
        /// </summary>
        /// <returns>False for a ship that is not on this map.</returns>
        public bool RemoveShip(Ship ship)
        {
            int index = ship != null ? ships.IndexOf(ship) : -1;

            if (index < 0)
            {
                return false;
            }

            if (ReferenceEquals(Selection.Hovered, ship))
            {
                Selection.SetHovered(null);
            }

            if (ReferenceEquals(Selection.Selected, ship))
            {
                Selection.ClearSelection();
            }

            // While the ship is still listed: leaving its port tells the panels that show it there.
            Docking.Remove(ship);
            arrivals.Forget(ship);

            Destroy(views[index].gameObject);
            ships.RemoveAt(index);
            shipWorldPositions.RemoveAt(index);
            views.RemoveAt(index);
            displayNames.RemoveAt(index);
            playerShips.Remove(ship);
            return true;
        }

        /// <summary>
        /// Adds a ship that the player does not own: it is shown, hovered and selected
        /// like the player's, and takes its orders from whoever spawned it.
        /// </summary>
        /// <param name="position">Normalized map position; the ship starts on the water nearest to it.</param>
        /// <param name="tint">The color of its sprite, which tells it from the player's.</param>
        /// <returns>The ship, or null when this view is not ready or the definition is unusable.</returns>
        public Ship SpawnAiShip(ShipDefinition definition, Vector2 position, ShipCrew crew, string displayName, Color tint)
        {
            if (!IsReady)
            {
                Debug.LogError("ShipsView cannot spawn a ship before it has started.", this);
                return null;
            }

            if (definition == null || crew == null)
            {
                Debug.LogError("ShipsView needs a ship definition and a crew to spawn a ship.", this);
                return null;
            }

            return IsUsable(definition) ? Spawn(definition, position, crew, displayName, tint) : null;
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
            // An inactive city does not exist in the game: the player cannot start there.
            if (mapView.HasCity(startCity))
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

        // The Min attributes only constrain the Inspector, not the asset file.
        private static bool IsUsable(ShipDefinition definition)
        {
            float speed = definition.Speed;

            if (!(speed > 0f) || float.IsInfinity(speed))
            {
                Debug.LogError($"ShipDefinition '{definition.name}' has an invalid speed ({speed}).", definition);
                return false;
            }

            if (definition.CrewCapacity < 1)
            {
                Debug.LogError(
                    $"ShipDefinition '{definition.name}' has an invalid crew capacity ({definition.CrewCapacity}).",
                    definition);
                return false;
            }

            return true;
        }

        private Ship Spawn(ShipDefinition definition, Vector2 position, ShipCrew crew, string displayName, Color? tint)
        {
            // A NaN city position would be rejected by the ship.
            if (float.IsNaN(position.x) || float.IsNaN(position.y))
            {
                position = MapCenter;
            }

            // With a pathfinder, a ship that starts on a city starts on the water beside it.
            // The Min attribute only constrains the Inspector, not the asset file.
            var cargo = new CargoHold(Mathf.Max(0, definition.CargoCapacity), GoodUnitsOfTheEconomy());
            var ship = new Ship(mapView.Projection, position, definition.Speed, crew, navigation, cargo);
            ShipView view = Instantiate(shipPrefab, transform);
            view.Initialize(ship, definition, displayName, tint);
            arrivals.Track(ship);

            ships.Add(ship);
            shipWorldPositions.Add(ship.WorldPosition);
            views.Add(view);
            displayNames.Add(displayName);
            return ship;
        }

        // Made once, on the first ship: the economy is an asset and does not change in a game.
        private GoodUnits GoodUnitsOfTheEconomy()
        {
            if (goodUnits != null)
            {
                return goodUnits;
            }

            goodUnits = GoodUnits.AllBarrels;

            if (economy == null)
            {
                Debug.LogWarning("ShipsView has no economy: every good takes one barrel of room in a hold.", this);
            }
            else
            {
                try
                {
                    goodUnits = economy.CreateGoodUnits();
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    Debug.LogWarning(
                        $"ShipsView: the economy cannot give the units of its goods ({exception.Message}); every good takes one barrel of room.",
                        this);
                }
            }

            return goodUnits;
        }

        // A ship in a city, waiting, crossing its harbour or in port, is not on the map: it is
        // neither shown, hovered nor selected there. It can be selected from its city's panel,
        // and stays selected through the city's states; it is back on the map at sea.
        private void OnCityStateChanged(Ship ship, CityDefinition city)
        {
            int index = ships.IndexOf(ship);

            if (index < 0)
            {
                return;
            }

            bool isInCity = Docking.IsInCity(ship);
            bool wasOnMap = views[index].gameObject.activeSelf;

            if (isInCity == !wasOnMap)
            {
                return;
            }

            if (isInCity)
            {
                if (ReferenceEquals(Selection.Hovered, ship))
                {
                    Selection.SetHovered(null);
                }

                if (ReferenceEquals(Selection.Selected, ship))
                {
                    Selection.ClearSelection();
                }

                shipWorldPositions[index] = NotOnMap;
                views[index].gameObject.SetActive(false);
                return;
            }

            shipWorldPositions[index] = ship.WorldPosition;
            views[index].gameObject.SetActive(true);
            views[index].Refresh();
        }

        private void OnShipArrived(Ship ship, CityDefinition city)
        {
            ShipArrived?.Invoke(ship, city);
        }

        private void OnShipQueued(Ship ship, CityDefinition city)
        {
            ShipQueued?.Invoke(ship, city);
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
