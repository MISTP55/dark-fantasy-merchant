using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Owns the market of every city of the map and makes the cities produce and consume
    /// every day of the world clock.
    /// </summary>
    public sealed class WorldEconomy : MonoBehaviour
    {
        [SerializeField] private EconomyDefinition economy;
        [SerializeField] private WorldMapView mapView;
        [SerializeField] private WorldClock worldClock;

        private readonly Dictionary<CityDefinition, CityMarket> markets = new Dictionary<CityDefinition, CityMarket>();

        private GameClock clock;

        public EconomyDefinition Economy => economy;

        /// <summary>False until the markets exist, which is in Start, and for good when they could not be made.</summary>
        public bool IsReady { get; private set; }

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

        private bool TryCreateMarkets()
        {
            try
            {
                MarketPricing pricing = economy.CreatePricing();

                foreach (CityDefinition city in mapView.Cities)
                {
                    WarnAbout(city);
                    markets[city] = economy.CreateMarket(city, pricing);
                }
            }
            catch (ArgumentOutOfRangeException exception)
            {
                Debug.LogError(
                    $"EconomyDefinition '{economy.name}' or one of its goods has an invalid value "
                    + $"({exception.ParamName}); cities have no market.",
                    economy);
                return false;
            }

            return true;
        }

        // A doubtful city still gets a market: these are mistakes of content, not reasons to stop.
        private void WarnAbout(CityDefinition city)
        {
            if (city.Population < 1)
            {
                Debug.LogWarning($"CityDefinition '{city.name}' has no inhabitant; its market is empty.", city);
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

        private void WarnAboutGoods(CityDefinition city, IReadOnlyList<GoodDefinition> produced)
        {
            foreach (GoodDefinition good in produced)
            {
                if (good == null)
                {
                    Debug.LogWarning($"CityDefinition '{city.name}' has an empty entry among its productions.", city);
                }
                else if (economy.IndexOf(good) < 0)
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
        }
    }
}
