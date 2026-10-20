using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Records the simulation for the debug tools from the start of the game, whether their panel
    /// is open or not: every city at the end of each day, with the events of the day, and every
    /// trade of the AI merchants and of the player. Exists only in the Editor and in development
    /// builds. Reads the simulation and changes nothing.
    /// </summary>
    public sealed class SimulationRecorder : MonoBehaviour
    {
        [SerializeField] private WorldEconomy worldEconomy;
        [SerializeField] private WorldMapView mapView;
        [SerializeField] private WorldClock worldClock;

        [Tooltip("Optional. Without it, no AI trade is recorded and nothing is under way.")]
        [SerializeField] private AiMerchants aiMerchants;

        [Tooltip("Optional. Without it, no trade of the player is recorded.")]
        [SerializeField] private CityInfoPanelController cityPanel;

        private readonly List<CityDefinition> cities = new List<CityDefinition>();
        private readonly List<string> goodNames = new List<string>();

        private GameClock clock;

        /// <summary>False until the economy has started, and for good outside the Editor and development builds.</summary>
        public bool IsReady => Record != null;

        /// <summary>Null until <see cref="IsReady"/>.</summary>
        public SimulationRecord Record { get; private set; }

        /// <summary>The cities recorded, in the order of their indices in the record.</summary>
        public IReadOnlyList<CityDefinition> Cities => cities;

        /// <summary>The names of the goods, in the catalogue's order, as the record's indices.</summary>
        public IReadOnlyList<string> GoodNames => goodNames;

        public CalendarDefinition Calendar => worldClock != null ? worldClock.Calendar : null;

        /// <summary>The choice of <see cref="HorizonChoices"/>; twelve months at every start.</summary>
        public int HorizonIndex { get; private set; } = HorizonChoices.DefaultIndex;

        /// <summary>Raised once, when the recording starts.</summary>
        public event Action Started;

        public void SetHorizon(int index)
        {
            int? days = HorizonChoices.DaysOf(index, Calendar.DaysPerMonth);
            HorizonIndex = index;
            Record?.SetHorizon(days);
        }

        /// <summary>A day counted from the start of the game, as the calendar writes it.</summary>
        public string FormatDay(int day)
        {
            CalendarDefinition calendar = Calendar;

            return calendar.Format(GameDate.FromElapsedDays(day, calendar.StartYear, calendar.MonthsPerYear, calendar.DaysPerMonth));
        }

        private void Start()
        {
            // A debug tool is not a part of the game the player gets.
            if (!Debug.isDebugBuild)
            {
                enabled = false;
                return;
            }

            if (worldEconomy == null || mapView == null || worldClock == null)
            {
                Debug.LogError("SimulationRecorder needs a world economy, a map view and a world clock.", this);
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (Record == null)
            {
                return;
            }

            worldEconomy.DayEnded -= OnDayEnded;

            if (aiMerchants != null)
            {
                aiMerchants.Traded -= OnAiTraded;
            }

            if (cityPanel != null)
            {
                cityPanel.PlayerTraded -= OnPlayerTraded;
            }
        }

        // Only to start: once started, the record follows the days of the economy.
        private void Update()
        {
            // The economy and the clock log why when they cannot start.
            if (worldEconomy.IsReady && worldClock.IsReady)
            {
                Begin();
                enabled = false;
            }
        }

        private void Begin()
        {
            var markets = new List<CityMarket>();

            foreach (CityDefinition city in mapView.Cities)
            {
                CityMarket market = worldEconomy.MarketOf(city);

                if (market != null)
                {
                    cities.Add(city);
                    markets.Add(market);
                }
            }

            foreach (GoodDefinition good in worldEconomy.Economy.Goods)
            {
                goodNames.Add(good.DisplayName);
            }

            clock = worldClock.Clock;
            Record = new SimulationRecord(markets, goodNames.Count, HorizonChoices.DaysOf(HorizonIndex, Calendar.DaysPerMonth));

            // The cities as the game starts, then at the end of every day.
            Record.RecordDay(clock.ElapsedDays, UnderWay);
            worldEconomy.DayEnded += OnDayEnded;

            if (aiMerchants != null)
            {
                aiMerchants.Traded += OnAiTraded;
            }

            if (cityPanel != null)
            {
                cityPanel.PlayerTraded += OnPlayerTraded;
            }

            Started?.Invoke();
        }

        private void OnDayEnded(GameDate date)
        {
            Record.RecordDay(clock.ElapsedDays, UnderWay);
        }

        private int UnderWay(int city, int good)
        {
            return aiMerchants != null ? aiMerchants.UnderWayTo(cities[city], good) : 0;
        }

        // A trade belongs to the day under way, the last one recorded.
        private void OnAiTraded(FleetTrade trade)
        {
            Record.AddTrade(Record.History.LastDay, trade.Market, trade);
        }

        private void OnPlayerTraded(CityDefinition city, int good, int units, long gold, PlayerTradeKind kind)
        {
            Record.AddPlayerTrade(Record.History.LastDay, worldEconomy.MarketOf(city), good, units, gold, kind);
        }
    }
}
