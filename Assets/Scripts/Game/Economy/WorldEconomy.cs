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

        private GameClock clock;

        public EconomyDefinition Economy => economy;

        public BuildingsDefinition Buildings => buildings;

        /// <summary>What a house costs, or null when it cannot be built (a negative gold): a material that is not an active good is left out.</summary>
        /// <summary>What the player's house of each class costs; null for a house the player cannot build.</summary>
        public PerClass<BuildingCost> HouseCosts { get; private set; }

        /// <summary>What a workshop costs, or null when it cannot be built (a negative gold): a material that is not an active good is left out.</summary>
        public BuildingCost WorkshopCost { get; private set; }

        /// <summary>False until the markets exist, which is in Start, and for good when they could not be made.</summary>
        public bool IsReady { get; private set; }

        /// <summary>Raised once every city's day has been played, with the date of the day that started.</summary>
        public event Action<GameDate> DayEnded;

        /// <returns>The market of a city of the map, or null for another city or before the markets exist.</returns>
        public CityMarket MarketOf(CityDefinition city)
        {
            return city != null && markets.TryGetValue(city, out CityMarket market) ? market : null;
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
            HouseCosts = new PerClass<BuildingCost>(
                CostOf(buildings.HouseOf(SocialClass.Rich), "rich house"),
                CostOf(buildings.HouseOf(SocialClass.Wealthy), "wealthy house"),
                CostOf(buildings.HouseOf(SocialClass.Poor), "poor house"));
            WorkshopCost = CostOf(buildings.Workshop, "workshop");

            if (!TryCreateMarkets())
            {
                markets.Clear();
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

        private BuildingCost CostOf(BuildingsDefinition.BuildingType type, string building)
        {
            if (buildings.TryCreateCost(type, economy, out BuildingCost cost, out string problem))
            {
                return cost;
            }

            Debug.LogWarning($"BuildingsDefinition '{buildings.name}': {problem}. The player cannot build a {building}.", buildings);
            return null;
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
                        housedClass, rooms[housedClass], buildings.MaterialsOf(buildings.HouseOf(housedClass), economy)));
                }

                foreach (CityDefinition city in mapView.Cities)
                {
                    WarnAbout(city);
                    markets[city] = economy.CreateMarket(city, pricing, daysPerWeek, labour, rooms, housing, social);
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
