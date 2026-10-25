using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Owns the market of every city of the map and makes the cities produce and consume,
    /// and their populations grow or decline, every day of the world clock.
    /// </summary>
    public sealed class WorldEconomy : MonoBehaviour
    {
        [SerializeField] private EconomyDefinition economy;
        [SerializeField] private BuildingsDefinition buildings;
        [SerializeField] private WorldMapView mapView;
        [SerializeField] private WorldClock worldClock;

        private readonly Dictionary<CityDefinition, CityMarket> markets = new Dictionary<CityDefinition, CityMarket>();
        private readonly Dictionary<CityDefinition, CityGrid> grids = new Dictionary<CityDefinition, CityGrid>();
        private readonly Dictionary<CityDefinition, Harbour> harbours = new Dictionary<CityDefinition, Harbour>();

        private GameClock clock;

        public EconomyDefinition Economy => economy;

        public BuildingsDefinition Buildings => buildings;

        /// <summary>
        /// The player's house of each class. A house that cannot be built (a negative gold) has a
        /// null cost; a material that is not an active good is left out.
        /// </summary>
        public IReadOnlyList<HouseOffer> HouseOffers { get; private set; }

        /// <summary>The road the player can build, with the cost of a cell; a null cost when it cannot be built.</summary>
        public RoadOffer RoadOffer { get; private set; }

        /// <summary>
        /// The player's workshops, but for those of a good that is not active. A workshop that
        /// cannot be built (a negative gold) has a null cost; a material that is not an active good
        /// is left out.
        /// </summary>
        public IReadOnlyList<WorkshopOffer> WorkshopOffers { get; private set; }

        /// <summary>False until the markets exist, which is in Start, and for good when they could not be made.</summary>
        public bool IsReady { get; private set; }

        /// <summary>Raised once every city's day has been played, with the date of the day that started.</summary>
        public event Action<GameDate> DayEnded;

        /// <returns>The market of a city of the map, or null for another city or before the markets exist.</returns>
        public CityMarket MarketOf(CityDefinition city)
        {
            return city != null && markets.TryGetValue(city, out CityMarket market) ? market : null;
        }

        /// <returns>The grid of a city's market, or null for a city without one (no usable layout) and before the markets exist.</returns>
        public CityGrid GridOf(CityDefinition city)
        {
            return city != null && grids.TryGetValue(city, out CityGrid grid) ? grid : null;
        }

        /// <returns>
        /// The ships' traffic on a city's grid, made with the grid: the same for a city all game long. Null for a
        /// city without a grid, whose port ships enter at once, and before the markets exist.
        /// </returns>
        public Harbour HarbourOf(CityDefinition city)
        {
            return city != null && harbours.TryGetValue(city, out Harbour harbour) ? harbour : null;
        }

        // Start, not Awake: WorldMapView lists its cities in its Awake.
        private void Start()
        {
            if (economy == null || mapView == null || worldClock == null)
            {
                Debug.LogError("WorldEconomy needs an economy definition, a map view and a world clock.", this);
                return;
            }

            if (!mapView.IsReady || !worldClock.IsReady)
            {
                // WorldMapView or WorldClock already logged why it has nothing to give.
                return;
            }

            if (!economy.TryValidate(out string problem))
            {
                Debug.LogError($"EconomyDefinition '{economy.name}' is unusable: {problem}. Cities have no market.", economy);
                return;
            }

            if (buildings == null)
            {
                Debug.LogError("WorldEconomy needs a buildings definition: the houses of a city set its room. Cities have no market.", this);
                return;
            }

            if (!buildings.TryValidate(out problem))
            {
                Debug.LogError($"BuildingsDefinition '{buildings.name}' is unusable: {problem}. Cities have no market.", buildings);
                return;
            }

            // A cost that cannot be worked out only takes that building away from the player.
            HouseOffers = buildings.CreateHouseOffers(economy, WarnUnavailable);
            WorkshopOffers = buildings.CreateWorkshopOffers(economy, WarnUnavailable);
            RoadOffer = buildings.CreateRoadOffer(economy, WarnUnavailable);

            if (!TryCreateMarkets())
            {
                markets.Clear();
                grids.Clear();
                harbours.Clear();
                return;
            }

            clock = worldClock.Clock;
            clock.DayStarted += OnDayStarted;
            IsReady = true;
        }

        private void OnDestroy()
        {
            if (clock != null)
            {
                clock.DayStarted -= OnDayStarted;
            }
        }

        private void WarnUnavailable(BuildingDefinition building, string problem)
        {
            Debug.LogWarning($"BuildingsDefinition '{buildings.name}': {problem}. The player cannot build '{building.name}'.", building);
        }

        private bool TryCreateMarkets()
        {
            try
            {
                MarketPricing pricing = economy.CreatePricing();

                // Not used here, but holds and the AI merchants' planner need the units: an
                // unusable setting leaves the cities without a market, as the others do.
                economy.CreateGoodUnits();
                int daysPerWeek = worldClock.Calendar.DaysPerWeek;
                LabourRule labour = economy.CreateLabourRule();

                SocialRule social = economy.CreateSocialRule();
                PerClass<int> rooms = buildings.HouseRooms;

                // A city's house is the player's of its class: the materials switched off are left out.
                var housing = new List<CityHousingRule>();

                foreach (SocialClass housedClass in new[] { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor })
                {
                    housing.Add(economy.CreateHousingRule(
                        housedClass, rooms[housedClass], buildings.HouseOf(housedClass).MaterialsOf(economy),
                        buildings.HouseOf(housedClass).Footprint));
                }

                // One chance and one strategy for the whole world.
                IPlacementStrategy placement = buildings.PlacementStrategy.CreateStrategy(new System.Random());

                foreach (CityDefinition city in mapView.Cities)
                {
                    WarnAbout(city);
                    CityGrid grid = CreateGridOf(city);

                    if (grid != null)
                    {
                        grids[city] = grid;
                        WarnAboutRoom(city, grid, rooms);
                        CreateHarbourOf(city, grid);
                    }

                    markets[city] = grid == null
                        ? economy.CreateMarket(city, pricing, daysPerWeek, labour, rooms, housing, social)
                        : economy.CreateMarket(city, pricing, daysPerWeek, labour, rooms, housing, social, grid, placement);
                }
            }
            catch (ArgumentOutOfRangeException exception)
            {
                string problem = economy.TryValidateSettings(out string settingsProblem)
                    ? $"one of its goods or the calendar's week or month has an invalid value ({exception.ParamName})"
                    : settingsProblem;

                Debug.LogError($"EconomyDefinition '{economy.name}', {problem}\nCities have no market.", economy);
                return false;
            }

            return true;
        }

        // A city with no usable layout is inactive: the map skips it (WorldMapView.IsUsable). One reaches here only
        // when the map view has no buildings catalogue to check it with, which it has logged as an error; the city
        // then still gets a market, without grid.
        private CityGrid CreateGridOf(CityDefinition city)
        {
            if (city.Layout == null)
            {
                Debug.LogWarning($"CityDefinition '{city.name}' has no layout (the map view, without buildings, could not make it inactive); its market has no grid.", city);
                return null;
            }

            if (!city.Layout.TryCreateGrid(buildings, out CityGrid grid, out string problem))
            {
                Debug.LogWarning($"CityLayoutDefinition '{city.Layout.name}' of '{city.name}' is unusable: {problem} (the map view, without buildings, could not make the city inactive). Its market has no grid.", city.Layout);
                return null;
            }

            return grid;
        }

        // A grid whose berths have no way out is refused by the map before it gets here; should one
        // come, its port is entered at once rather than the game stopping.
        private void CreateHarbourOf(CityDefinition city, CityGrid grid)
        {
            try
            {
                harbours[city] = new Harbour(grid, buildings.Port.ShipCellsPerSecond);
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning($"CityDefinition '{city.name}' has no usable harbour ({exception.Message}); ships enter its port at once.", city);
            }
        }

        // The grid's room is kept, but a class short of the room its inhabitants need starts crowded.
        private void WarnAboutRoom(CityDefinition city, CityGrid grid, PerClass<int> roomPerHouse)
        {
            PerClass<int> needed = economy.StartingRoomOf(city, roomPerHouse);

            foreach (SocialClass housedClass in new[] { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor })
            {
                int room = grid.ConnectedHousesOf(housedClass) * roomPerHouse[housedClass];

                if (room < needed[housedClass])
                {
                    Debug.LogWarning(
                        $"CityLayoutDefinition '{city.Layout.name}' of '{city.name}' gives the {housedClass} room for {room}, under the {needed[housedClass]} they need at the start.",
                        city.Layout);
                }
            }
        }

        // A doubtful city still gets a market: these are mistakes of content, not reasons to stop.
        private void WarnAbout(CityDefinition city)
        {
            if (!ProducesAnyGood(city))
            {
                Debug.LogWarning(
                    $"CityDefinition '{city.name}' produces no active good of the economy; it starts with no inhabitant.",
                    city);
            }

            WarnAboutGoods(city, city.EfficientGoods);
            WarnAboutGoods(city, city.InefficientGoods);

            foreach (GoodDefinition good in city.InefficientGoods)
            {
                if (good != null && city.ProductionOf(good) == GoodProduction.Efficient)
                {
                    Debug.LogWarning(
                        $"CityDefinition '{city.name}' lists '{good.name}' as both efficient and inefficient; it is efficient.",
                        city);
                }
            }
        }

        private bool ProducesAnyGood(CityDefinition city)
        {
            foreach (GoodDefinition good in economy.Goods)
            {
                if (city.ProductionOf(good) != GoodProduction.None)
                {
                    return true;
                }
            }

            return false;
        }

        private void WarnAboutGoods(CityDefinition city, IReadOnlyList<GoodDefinition> produced)
        {
            foreach (GoodDefinition good in produced)
            {
                if (good == null)
                {
                    Debug.LogWarning($"CityDefinition '{city.name}' has an empty entry among its productions.", city);
                }
                else if (!economy.IsInCatalogue(good))
                {
                    Debug.LogWarning(
                        $"CityDefinition '{city.name}' produces '{good.name}', which is not in the economy's goods.",
                        city);
                }
            }
        }

        private void OnDayStarted(GameDate date)
        {
            foreach (CityMarket market in markets.Values)
            {
                market.StartDay();
            }

            DayEnded?.Invoke(date);
        }
    }
}
