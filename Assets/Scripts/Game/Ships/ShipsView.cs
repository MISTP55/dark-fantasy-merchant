using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Makes the ships of the map, the player's and the others, and holds them in a <see cref="ShipFleet{TPort}"/>,
    /// which it steps at every step of the world's time; shows them, keeps the ship selection state and shows the
    /// route of the selected one.
    /// </summary>
    public sealed class ShipsView : MonoBehaviour
    {
        private static readonly Vector2 MapCenter = new Vector2(0.5f, 0.5f);

        [SerializeField] private WorldMapView mapView;
        [SerializeField] private ShipView shipPrefab;
        [SerializeField] private ShipDefinition playerShipDefinition;

        [Tooltip("Optional. Without it, the player ship starts with a full crew, on the first active city of the map.")]
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

        // The view of every ship of the fleet.
        private readonly Dictionary<Ship, ShipView> views = new Dictionary<Ship, ShipView>();

        private ShipFleet<CityDefinition> fleet;

        // Null on a map without a navigation mask: ships then sail in a straight line.
        private NavigationPathfinder navigation;

        private Ship hoveredShip;
        private Ship selectedShip;

        // Null without a usable world clock: ships then sail in real time, once per frame.
        private GameClock clock;

        public MapSelectionState<Ship> Selection { get; } = new MapSelectionState<Ship>();

        /// <summary>
        /// Ships in the cities, in their harbours and in port. Orders go through it, so that a ship
        /// enters the city it was sent to and leaves the one it is in. Made on first use: other
        /// components listen to it in their OnEnable, which can come before this one's Awake.
        /// </summary>
        public ShipDocking<CityDefinition> Docking => Fleet.Docking;

        /// <summary>
        /// Raised when a ship arrives (see <see cref="ShipArrivals{TPort}"/>), with the city
        /// whose port it has entered, or null when it stopped at sea.
        /// </summary>
        public event Action<Ship, CityDefinition> ShipArrived
        {
            add => Fleet.Arrived += value;
            remove => Fleet.Arrived -= value;
        }

        /// <summary>Raised when a ship enters a city's queue, every berth being taken, with the city.</summary>
        public event Action<Ship, CityDefinition> ShipQueued
        {
            add => Fleet.Queued += value;
            remove => Fleet.Queued -= value;
        }

        /// <summary>
        /// Raised at every step of the world's time, with its simulated seconds, before the
        /// ships sail it: what gives ships their orders for the step listens to it.
        /// </summary>
        public event Action<float> StepStarted
        {
            add => Fleet.StepStarted += value;
            remove => Fleet.StepStarted -= value;
        }

        public IReadOnlyList<Ship> Ships => Fleet.Ships;

        /// <summary>The ships of <see cref="Ships"/> that the player owns and orders about.</summary>
        public IReadOnlyList<Ship> PlayerShips => Fleet.PlayerShips;

        /// <summary>Where ships can sail. Null before Start, and on a map without a navigation mask.</summary>
        public NavigationPathfinder Navigation => navigation;

        /// <summary>
        /// World position of each entry of <see cref="Ships"/>, in the same order. NaN for
        /// a ship in a city: it is not on the map and cannot be picked there.
        /// </summary>
        public IReadOnlyList<Vector2> ShipWorldPositions => Fleet.WorldPositions;

        public bool IsReady => Fleet.Ships.Count > 0;

        // Made on first use, as the docking is: listeners of the docking and of the arrivals can come before Start.
        private ShipFleet<CityDefinition> Fleet
        {
            get
            {
                if (fleet == null)
                {
                    // The economy makes the harbours with the grids, in its Start, before any ship enters a city.
                    fleet = new ShipFleet<CityDefinition>(new ShipDocking<CityDefinition>(
                        city => worldEconomy != null ? worldEconomy.HarbourOf(city) : null));
                }

                return fleet;
            }
        }

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
            Spawn(
                playerShipDefinition,
                StartPosition(),
                CreatePlayerCrew(playerShipDefinition.CrewCapacity),
                playerShipDefinition.DisplayName,
                null,
                true);
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
            Fleet.UpdatePositions();

            foreach (ShipView view in views.Values)
            {
                view.Refresh();
            }
        }

        private void Step(float deltaTime)
        {
            Fleet.Step(deltaTime);
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
            return Fleet.NameOf(ship);
        }

        public bool IsPlayerShip(Ship ship)
        {
            return Fleet.IsPlayerShip(ship);
        }

        /// <returns>Whether the ship is one of this map's: false once it was removed.</returns>
        public bool Contains(Ship ship)
        {
            return Fleet.Contains(ship);
        }

        /// <summary>
        /// Takes a ship off the map for good, with what it carries: it leaves its port, is
        /// neither hovered nor selected any more, and its view is destroyed. Any ship is
        /// taken, the player's too: whoever calls decides which ships can go.
        /// </summary>
        /// <returns>False for a ship that is not on this map.</returns>
        public bool RemoveShip(Ship ship)
        {
            if (!Fleet.Contains(ship))
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

            // The fleet takes it out of its port while it is still listed, which tells the panels that show it there.
            Fleet.Remove(ship);

            if (views.Remove(ship, out ShipView view))
            {
                Destroy(view.gameObject);
            }

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

            return IsUsable(definition) ? Spawn(definition, position, crew, displayName, tint, false) : null;
        }

        public void SetShipScale(float worldUnitsPerPixel)
        {
            float scale = worldUnitsPerPixel * shipScreenPixelsPerUnit;

            foreach (ShipView view in views.Values)
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
            CityDefinition homeCity = PlayerStartDefinition.HomeCityOn(playerStart, mapView);

            if (homeCity != null)
            {
                return homeCity.MapPosition;
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

        private Ship Spawn(ShipDefinition definition, Vector2 position, ShipCrew crew, string displayName, Color? tint, bool isPlayerShip)
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
            Fleet.Add(ship, displayName, isPlayerShip);
            views.Add(ship, view);
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
            if (!views.TryGetValue(ship, out ShipView view))
            {
                return;
            }

            bool isInCity = Docking.IsInCity(ship);
            bool wasOnMap = view.gameObject.activeSelf;

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

                Fleet.UpdatePositionOf(ship);
                view.gameObject.SetActive(false);
                return;
            }

            Fleet.UpdatePositionOf(ship);
            view.gameObject.SetActive(true);
            view.Refresh();
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
            return ship != null && views.TryGetValue(ship, out ShipView view) ? view : null;
        }
    }
}
