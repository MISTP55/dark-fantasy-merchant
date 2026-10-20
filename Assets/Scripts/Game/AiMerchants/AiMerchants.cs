using System;
using System.Collections.Generic;
using System.Diagnostics;
using DarkFantasyMerchant.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The merchants that trade on their own: homes ships in every city that reaches
    /// another by water, and lets them buy and sell between the cities of the map as the
    /// world's time passes. The player cannot order them.
    /// </summary>
    public sealed class AiMerchants : MonoBehaviour
    {
        // Real milliseconds a frame spends measuring the routes between the cities.
        private const long MeasureBudgetMilliseconds = 4;

        [SerializeField] private AiMerchantsDefinition definition;
        [SerializeField] private ShipsView shipsView;
        [SerializeField] private WorldEconomy worldEconomy;
        [SerializeField] private WorldMapView mapView;
        [SerializeField] private WorldClock worldClock;

        // The cities that have a market, their positions and their markets, in the same order.
        private readonly List<CityDefinition> ports = new List<CityDefinition>();
        private readonly List<Vector2> portPositions = new List<Vector2>();
        private readonly List<CityMarket> markets = new List<CityMarket>();

        private readonly Stopwatch stopwatch = new Stopwatch();

        // Made once the ships view and the economy have started, then filled over a few frames.
        private PortTravelTable travelTable;

        // Made once the travel table is complete.
        private AiMerchantFleet<CityDefinition> fleet;

        // The ports a ship can start in: those that reach at least one other.
        private readonly List<int> candidates = new List<int>();

        // Ships spawned since the game started: a number is not given twice, even after a ship was removed.
        private int spawnedShips;

        /// <summary>False until the merchants have started, and on a map with no city to trade between.</summary>
        public bool CanAddShip => fleet != null && candidates.Count > 0;

        /// <summary>
        /// Adds a ship homed in the city with the fewest AI ships homed there, at random among ties.
        /// For the debug tools.
        /// </summary>
        /// <returns>The ship, or null when none can be added.</returns>
        public Ship AddShip()
        {
            if (!CanAddShip)
            {
                return null;
            }

            return SpawnShip(HomePortChoice.Pick(candidates, fleet.ShipsHomedAt, bound => UnityEngine.Random.Range(0, bound)));
        }

        /// <summary>Takes one of these merchants' ships off the map, with what it carries. For the debug tools.</summary>
        /// <returns>False for a ship that is not one of these merchants'.</returns>
        public bool RemoveShip(Ship ship)
        {
            if (fleet == null || !fleet.Remove(ship))
            {
                return false;
            }

            shipsView.RemoveShip(ship);
            return true;
        }

        /// <returns>False for a ship that is not one of these merchants'.</returns>
        public bool TryGetProfit(Ship ship, out long profit)
        {
            if (fleet == null || !fleet.Contains(ship))
            {
                profit = 0;
                return false;
            }

            profit = fleet.ProfitOf(ship);
            return true;
        }

        /// <summary>Every purchase and sale of these merchants' ships, for the debug tools; none before they have started.</summary>
        public event Action<FleetTrade> Traded;

        /// <summary>Units of a good the ships carry to a city, for the debug tools: 0 for a city they do not trade with, or before they have started.</summary>
        public int UnderWayTo(CityDefinition city, int good)
        {
            int port = fleet != null ? ports.IndexOf(city) : -1;

            return port >= 0 && good >= 0 && good < markets[port].GoodCount ? fleet.UnderWayTo(port, good) : 0;
        }

        /// <summary>For the debug tools.</summary>
        /// <param name="finalDestination">Null when the ship has none.</param>
        /// <returns>False for a ship that is not one of these merchants'.</returns>
        public bool TryGetTour(Ship ship, out CityDefinition home, out CityDefinition finalDestination)
        {
            home = null;
            finalDestination = null;

            if (fleet == null || !fleet.Contains(ship))
            {
                return false;
            }

            int homePort = fleet.HomeOf(ship);
            int destinationPort = fleet.FinalDestinationOf(ship);

            home = homePort >= 0 ? ports[homePort] : null;
            finalDestination = destinationPort >= 0 ? ports[destinationPort] : null;
            return true;
        }

        private void Start()
        {
            if (definition == null || shipsView == null || worldEconomy == null || mapView == null || worldClock == null)
            {
                Debug.LogError(
                    "AiMerchants needs a definition, a ships view, a world economy, a map view and a world clock.", this);
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (fleet != null)
            {
                shipsView.StepStarted -= OnStepStarted;
                fleet.Traded -= OnTraded;
            }
        }

        private void OnTraded(FleetTrade trade)
        {
            Traded?.Invoke(trade);
        }

        // Only to start: once started, the fleet advances with the steps of the world's time.
        private void Update()
        {
            if (fleet != null)
            {
                return;
            }

            if (travelTable == null)
            {
                // Both start in their Start, and log why when they cannot.
                if (!shipsView.IsReady || !worldEconomy.IsReady || !worldClock.IsReady)
                {
                    return;
                }

                if (!TryStartMeasuring())
                {
                    enabled = false;
                }

                return;
            }

            stopwatch.Restart();

            while (!travelTable.IsComplete && stopwatch.ElapsedMilliseconds < MeasureBudgetMilliseconds)
            {
                travelTable.MeasureNext();
            }

            if (travelTable.IsComplete)
            {
                StartFleet();
            }
        }

        private bool TryStartMeasuring()
        {
            ShipCrew crew = definition.CreateCrew();

            if (crew == null)
            {
                Debug.LogError(
                    $"AiMerchantsDefinition '{definition.name}' has no ship definition with room for a crew.", definition);
                return false;
            }

            // What a ship sails in a day of the world.
            double worldUnitsPerDay =
                (double)definition.ShipDefinition.Speed * crew.SpeedFactor * worldClock.Calendar.SecondsPerDay;

            // The negated comparison also rejects NaN.
            if (!(worldUnitsPerDay > 0d) || double.IsInfinity(worldUnitsPerDay))
            {
                Debug.LogError(
                    $"AiMerchantsDefinition '{definition.name}' gives ships that do not sail: check the ship's speed and the crew.",
                    definition);
                return false;
            }

            NavigationPathfinder navigation = shipsView.Navigation;
            var anchorages = new List<Vector2>();

            foreach (CityDefinition city in mapView.Cities)
            {
                CityMarket market = worldEconomy.MarketOf(city);

                // A city without a market is not traded with.
                if (market == null)
                {
                    continue;
                }

                ports.Add(city);
                portPositions.Add(city.MapPosition);
                markets.Add(market);
                anchorages.Add(AnchorageOf(city, navigation));
            }

            travelTable = new PortTravelTable(anchorages, navigation, mapView.Projection, worldUnitsPerDay);
            return true;
        }

        // Where a ship lies when it is at the city: what Ship.AnchorageAt gives.
        private static Vector2 AnchorageOf(CityDefinition city, NavigationPathfinder navigation)
        {
            Vector2 position = city.MapPosition;
            var clamped = new Vector2(Mathf.Clamp01(position.x), Mathf.Clamp01(position.y));

            return navigation != null && navigation.TryGetNearestNavigable(clamped, out Vector2 onWater)
                ? onWater
                : clamped;
        }

        private void StartFleet()
        {
            double stopoverDays = Math.Max(0d, definition.StopoverDays);

            try
            {
                var planner = new SupplyPlanner(
                    markets, travelTable.Days, worldEconomy.Economy.CreateGoodUnits(), worldEconomy.Economy.CreateAiWeights());
                fleet = new AiMerchantFleet<CityDefinition>(
                    shipsView.Docking,
                    ports,
                    portPositions,
                    markets,
                    planner,
                    stopoverDays * worldClock.Calendar.SecondsPerDay,
                    travelTable);
                fleet.Traded += OnTraded;
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"AiMerchants could not start: {exception.Message}", this);
                enabled = false;
                return;
            }

            // The world's time: stopovers are shorter in fast forward, like the trips. Before
            // the ships sail the step, so that a ship that leaves sails it.
            shipsView.StepStarted += OnStepStarted;
            candidates.Clear();

            for (int i = 0; i < ports.Count; i++)
            {
                if (travelTable.ReachesAnother(i))
                {
                    candidates.Add(i);
                }
                else
                {
                    // Most likely a mistake of the navigation mask.
                    Debug.LogWarning(
                        $"CityDefinition '{ports[i].name}' reaches no other city by water; AI merchants ignore it.", ports[i]);
                }
            }

            if (candidates.Count == 0)
            {
                Debug.LogWarning("AiMerchants found no city to trade between; no AI ship sails.", this);
                return;
            }

            foreach (int candidate in candidates)
            {
                for (int i = 0; i < definition.ShipsPerCity; i++)
                {
                    // ShipsView already logged why. The port becomes the ship's home.
                    if (SpawnShip(candidate) == null)
                    {
                        return;
                    }
                }
            }
        }

        private void OnStepStarted(float deltaTime)
        {
            fleet.Advance(deltaTime);
        }

        private Ship SpawnShip(int port)
        {
            Ship ship = shipsView.SpawnAiShip(
                definition.ShipDefinition,
                portPositions[port],
                definition.CreateCrew(),
                definition.ShipNameFor(spawnedShips + 1),
                definition.Tint);

            if (ship != null)
            {
                spawnedShips++;
                fleet.Add(ship, port);
            }

            return ship;
        }
    }
}
