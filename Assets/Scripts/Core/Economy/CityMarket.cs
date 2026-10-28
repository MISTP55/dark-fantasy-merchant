using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The stocks of goods of one city. Every day the city consumes its goods, then its
    /// workshops hire or lay off by where each stock stands against its thresholds, and
    /// produce; the price of a good follows its stock against the same thresholds. There is
    /// no limit to what a city can store. Goods are indices into the catalogue. It also
    /// remembers what the days of the last week actually produced and consumed. What it
    /// consumes follows its population. Given a <see cref="LabourRule"/>, its workshops hire
    /// from their reserves and the beggars within the free room of the houses, lay off into a
    /// reserve that leaks back to the beggars, the player's workshops fill their vacancies from
    /// the beggars, and the beggars come or go; without one, hiring is immediate, layoffs
    /// vanish and the population stays as it is. Given <see cref="CityHousingRule"/>s as well,
    /// a city builds a house of its own for each crowded class from its stock. Given a
    /// <see cref="SocialRule"/> with the labour, its classes have a satisfaction that follows
    /// the goods, the poor rise into the other classes and fall back, the beggars are hired only
    /// while the poor are satisfied, and the poor without room or unhappy leave with their jobs;
    /// without one, the classes never move. On a grid, a house gives its room and a player's
    /// workshop hires only once connected to the road network.
    /// </summary>
    public sealed class CityMarket
    {
        private const double WholeUnitMargin = 1e-9;

        private readonly MarketGood[] goods;
        private readonly double[] stocks;
        private readonly int[] workers;
        private readonly int[] reserves;
        private readonly double[] leakCounters;
        private readonly List<PlayerWorkshop> playerWorkshops = new List<PlayerWorkshop>();
        private readonly IPlacementStrategy placement;
        private readonly List<Vector2Int> roadsToLay = new List<Vector2Int>();
        private readonly List<Vector2Int> houseCells = new List<Vector2Int>();

        // The room of each house on the grid that is not connected yet, given to its class when it connects.
        private readonly Dictionary<PlacedBuilding, int> roomUntilConnected = new Dictionary<PlacedBuilding, int>();

        // The room of the house the market is placing, set only for the time of the placement.
        private int roomOfPlacedHouse;
        private readonly ThresholdRule thresholdRule;
        private readonly WorkforceRule workforce;
        private readonly MarketPricing pricing;
        private readonly int daysPerWeek;
        private readonly MarketWeekLog weekLog;
        private readonly int[] playerHouses = new int[3];
        private readonly StockThresholds[] thresholds;

        // By good, what the player's workshops of the good have made and not delivered yet, under a unit, and,
        // by input of its recipe, what they owe of it and have not taken yet, under a unit, backed by units still
        // in the trading post. Null until the good is first made.
        private readonly double[] playerPending;
        private readonly double[][] playerInputDebts;

        // By good, the workshops that take it as an input and how much for every unit they make,
        // each list in the catalogue's order of its workshops: the recipes never change once the
        // market exists.
        private readonly (int consumer, double unitsPerUnit)[][] consumersOf;

        private static readonly SocialClass[] Classes = { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor };

        /// <param name="thresholdRule">How the stock thresholds of a good, at which its prices are given, follow what the city needs (its inhabitants and its workshops' inputs) and produces of it.</param>
        /// <param name="workforce">How the workshops hire, lay off and start.</param>
        /// <param name="startingDays">Days of its demand (consumption and inputs) the city holds of every good when the game starts.</param>
        /// <param name="daysPerWeek">Days of the calendar's week, over which the weekly figures are given.</param>
        /// <param name="population">The inhabitants of the city, who set what it consumes.</param>
        /// <param name="labour">How the workshops take their workers from the population; null for immediate hiring and a population that stays.</param>
        /// <param name="housing">When the city builds a house of its own for a class, each class once at most, only with labour; null for a city that never does.</param>
        /// <param name="social">The satisfaction and the moves of the classes, only with labour; null for classes that never move.</param>
        /// <param name="grid">The city grid the houses stand on, with its placement strategy (both or neither); null for a city whose houses have no place. Every housing rule then has a footprint. The houses already connected are in the population's room; one not connected yet gives the room of its class's housing rule when it connects, and needs that rule.</param>
        /// <param name="placement">Where the city builds its own houses on the grid.</param>
        /// <param name="merchantOutput">What the player's workshops make; null for workshops that hire but make nothing.</param>
        public CityMarket(
            IReadOnlyList<MarketGood> goods,
            ThresholdRule thresholdRule,
            WorkforceRule workforce,
            double startingDays,
            MarketPricing pricing,
            int daysPerWeek,
            CityPopulation population,
            LabourRule labour = null,
            IReadOnlyList<CityHousingRule> housing = null,
            SocialRule social = null,
            CityGrid grid = null,
            IPlacementStrategy placement = null,
            MerchantOutputRule merchantOutput = null)
        {
            if (goods == null)
            {
                throw new ArgumentNullException(nameof(goods));
            }

            this.thresholdRule = thresholdRule ?? throw new ArgumentNullException(nameof(thresholdRule));
            this.workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));

            // The negated comparisons also reject NaN.
            if (!(startingDays >= 0d) || double.IsInfinity(startingDays))
            {
                throw new ArgumentOutOfRangeException(nameof(startingDays));
            }

            if (daysPerWeek < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysPerWeek));
            }

            this.pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
            Population = population ?? throw new ArgumentNullException(nameof(population));
            if (social != null && labour == null)
            {
                throw new ArgumentException("The classes move only with labour.", nameof(social));
            }

            Labour = labour;
            Housing = CopyOf(housing);
            Social = social;

            if ((grid == null) != (placement == null))
            {
                throw new ArgumentException("A grid and a placement strategy come together.", nameof(grid));
            }

            if (grid != null)
            {
                foreach (CityHousingRule rule in Housing)
                {
                    if (rule.Footprint == null)
                    {
                        throw new ArgumentException("A market with a grid needs a footprint for every housing rule.", nameof(housing));
                    }
                }

                foreach (PlacedBuilding building in grid.Buildings)
                {
                    if (building.Kind.IsHouse && !building.IsConnected)
                    {
                        CityHousingRule rule = RuleOf(Housing, building.Kind.HousedClass)
                            ?? throw new ArgumentException(
                                "A house of the grid is not connected and its class has no housing rule to give its room.", nameof(grid));
                        roomUntilConnected[building] = rule.Room;
                    }
                }
            }

            Grid = grid;
            this.placement = placement;
            MerchantOutput = merchantOutput;
            this.daysPerWeek = daysPerWeek;
            weekLog = new MarketWeekLog(goods.Count, daysPerWeek);
            LastDay = new CityDayReport(goods.Count);
            this.goods = new MarketGood[goods.Count];
            stocks = new double[goods.Count];
            workers = StartingWorkersOf(goods, workforce);
            reserves = new int[goods.Count];
            leakCounters = new double[goods.Count];
            thresholds = new StockThresholds[goods.Count];
            playerPending = new double[goods.Count];
            playerInputDebts = new double[goods.Count][];

            for (int i = 0; i < goods.Count; i++)
            {
                this.goods[i] = goods[i];

                foreach (MarketInput input in goods[i].Inputs)
                {
                    if (input.Good >= goods.Count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(goods), $"The good {i} takes the good {input.Good}, which the market does not have.");
                    }

                    if (input.Good == i)
                    {
                        throw new ArgumentException($"The good {i} is its own input.", nameof(goods));
                    }
                }
            }

            consumersOf = ConsumersOf(goods);

            // Every good first: the demand of one reads the workshops of the others.
            for (int i = 0; i < goods.Count; i++)
            {
                // The same days for every good, produced or not: producers fill up as the days pass.
                stocks[i] = startingDays * DailyDemandOf(i);
            }

            UpdateThresholds();

            foreach (CityHousingRule rule in Housing)
            {
                foreach (MaterialCost material in rule.Materials)
                {
                    RequireGood(material.Good);
                }
            }

            if (social != null)
            {
                foreach (SocialClass housedClass in Classes)
                {
                    Population.SetSatisfaction(housedClass, social.StartingSatisfaction);
                }
            }

            if (grid != null)
            {
                grid.BuildingPlaced += OnBuildingPlaced;
                grid.BuildingConnected += OnBuildingConnected;
            }
        }

        public int GoodCount => goods.Length;

        public CityPopulation Population { get; }

        /// <summary>How the workshops take their workers from the population; null when hiring is immediate.</summary>
        public LabourRule Labour { get; }

        /// <summary>The beggars the city draws towards: 0 without labour.</summary>
        public int BeggarTarget => Labour?.BeggarTargetFor(Population.Inhabitants) ?? 0;

        /// <summary>When the city builds a house of its own for each class; empty when it never does.</summary>
        public IReadOnlyList<CityHousingRule> Housing { get; }

        /// <summary>The grid the houses and the player's workshops stand on; null for a city whose buildings have no place.</summary>
        public CityGrid Grid { get; }

        /// <summary>How the classes are satisfied and move; null when they never do.</summary>
        public SocialRule Social { get; }

        /// <summary>What the player's workshops make; null when they make nothing.</summary>
        public MerchantOutputRule MerchantOutput { get; }

        /// <summary>
        /// The player's trading post in the city, which the player's workshops take their inputs from and deliver
        /// into; null until the player has one there, and their workshops make nothing until then.
        /// </summary>
        public IGoodStore PlayerTradingPost { get; private set; }

        /// <summary>Gives the market the player's trading post, once it opens in the city.</summary>
        /// <exception cref="InvalidOperationException">The market already has one.</exception>
        public void SetPlayerTradingPost(IGoodStore post)
        {
            if (post == null)
            {
                throw new ArgumentNullException(nameof(post));
            }

            if (PlayerTradingPost != null)
            {
                throw new InvalidOperationException("The market already has the player's trading post.");
            }

            PlayerTradingPost = post;
        }

        /// <summary>Houses of a class the player has built in the city.</summary>
        public int PlayerHousesOf(SocialClass housedClass)
        {
            return playerHouses[(int)housedClass];
        }

        /// <summary>Houses the city has built of its own since the game started, not the player's.</summary>
        public int CityHouses { get; private set; }

        /// <summary>What happened in the last <see cref="StartDay"/>, for the debug tools; the same object every day.</summary>
        public CityDayReport LastDay { get; }

        /// <summary>
        /// Raised once by a day that starts, once when units were taken or put, and once by
        /// <see cref="AddPlayerWorkshop"/>, by <see cref="AddPlayerHouse"/> and by <see cref="AddPlayerRoads"/>.
        /// </summary>
        public event Action Changed;

        /// <summary>Units the housed inhabitants consume in a day while the city has them, each class at its rate; the beggars consume nothing.</summary>
        public double DailyConsumptionOf(int good)
        {
            RequireGood(good);

            PerClass<double> rates = goods[good].DailyConsumptionPerThousand;

            return (rates.Rich * Population.CountOf(SocialClass.Rich)
                + rates.Wealthy * Population.CountOf(SocialClass.Wealthy)
                + rates.Poor * Population.CountOf(SocialClass.Poor)) / 1000d;
        }

        /// <summary>
        /// Units of a good the city's workshops take in a day as their input, with their workers of
        /// the moment and no shortage, whether their own stock is above T3 or not.
        /// </summary>
        public double DailyInputNeedOf(int good)
        {
            RequireGood(good);

            double need = 0d;

            foreach ((int consumer, double unitsPerUnit) in consumersOf[good])
            {
                need += DailyProductionOf(consumer) * unitsPerUnit;
            }

            return need;
        }

        /// <summary>What the city needs of a good in a day: what its inhabitants consume and its workshops take as input.</summary>
        public double DailyDemandOf(int good)
        {
            return DailyConsumptionOf(good) + DailyInputNeedOf(good);
        }

        /// <summary>Units in stock; a city consumes fractions of a unit, so it is not whole.</summary>
        public double StockOf(int good)
        {
            RequireGood(good);

            return stocks[good];
        }

        /// <summary>Whole units in stock: what is shown and what can be bought.</summary>
        public int AvailableOf(int good)
        {
            // Days of fractional consumption leave a whole stock a hair short of itself.
            return (int)Math.Min(int.MaxValue, Math.Floor(StockOf(good) + WholeUnitMargin));
        }

        /// <summary>
        /// The stocks at which the prices of a good are given and its workshop hires, as the city
        /// keeps them: worked out at the start and at the end of every day, for the population
        /// of that moment, and unchanged in between, so that every workshop of a day decides on the
        /// same thresholds.
        /// </summary>
        public StockThresholds ThresholdsOf(int good)
        {
            RequireGood(good);

            return thresholds[good];
        }

        private void UpdateThresholds()
        {
            for (int i = 0; i < goods.Length; i++)
            {
                thresholds[i] = thresholdRule.ThresholdsFor(DailyDemandOf(i), goods[i].DailyCapacity);
            }
        }

        /// <summary>Workers of the city's workshop for a good: 0 for a good it does not produce.</summary>
        public int WorkersOf(int good)
        {
            RequireGood(good);

            return workers[good];
        }

        /// <summary>Laid-off workers of the city's workshop for a good, still housed, waiting to be hired back.</summary>
        public int ReserveOf(int good)
        {
            RequireGood(good);

            return reserves[good];
        }

        /// <summary>Workshops the player has built for a good in the city.</summary>
        public int PlayerWorkshopsOf(int good)
        {
            RequireGood(good);

            return playerWorkshops.FindAll(workshop => workshop.Good == good).Count;
        }

        /// <summary>Workers of all the player's workshops for a good in the city.</summary>
        public int PlayerWorkersOf(int good)
        {
            RequireGood(good);
            int sum = 0;

            foreach (PlayerWorkshop workshop in playerWorkshops)
            {
                if (workshop.Good == good)
                {
                    sum += workshop.Workers;
                }
            }

            return sum;
        }

        /// <summary>
        /// Units the player's workshops for a good make in a day with their workers of the moment, by the market's
        /// <see cref="MerchantOutput"/>, inputs not counted; 0 without it.
        /// </summary>
        public double PlayerDailyOutputOf(int good)
        {
            RequireGood(good);

            return MerchantOutput == null ? 0d : PlayerDailyOutputOf(good, MerchantOutput);
        }

        /// <summary>
        /// Units the player's workshops for a good would make in a day with their workers of
        /// the moment, by a rule of a merchant's workshops, inputs not counted.
        /// </summary>
        public double PlayerDailyOutputOf(int good, MerchantOutputRule rule)
        {
            RequireGood(good);

            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            return rule.DailyOutputOf(PlayerWorkersOf(good), goods[good].MerchantDailyOutputPerWorker, PlayerWorkshopsOf(good));
        }

        /// <summary>
        /// The wages of the workers of all the player's workshops in the city for a week, unrounded: a share of the base
        /// value of what they make in a week with their workers of the moment (<see cref="PlayerDailyOutputOf(int)"/>),
        /// whatever the inputs they lack.
        /// </summary>
        /// <param name="wageShare">The share of that value the workers are paid, 0 or more.</param>
        public double PlayerWeeklyWagesOf(double wageShare)
        {
            // The negated comparison also rejects NaN.
            if (!(wageShare >= 0d) || double.IsInfinity(wageShare))
            {
                throw new ArgumentOutOfRangeException(nameof(wageShare));
            }

            double wages = 0d;

            for (int good = 0; good < goods.Length; good++)
            {
                wages += PlayerDailyOutputOf(good) * daysPerWeek * goods[good].BasePrice * wageShare;
            }

            return wages;
        }

        /// <summary>Jobs of all the player's workshops for a good in the city.</summary>
        public int PlayerJobsOf(int good)
        {
            RequireGood(good);
            int sum = 0;

            foreach (PlayerWorkshop workshop in playerWorkshops)
            {
                if (workshop.Good == good)
                {
                    sum += workshop.Jobs;
                }
            }

            return sum;
        }

        /// <summary>
        /// The workers the city's workshops start with, added up: the starting workforce
        /// spread over the goods that are produced, as the constructor spreads it.
        /// </summary>
        public static int StartingWorkforceOf(IReadOnlyList<MarketGood> goods, WorkforceRule workforce)
        {
            int sum = 0;

            foreach (int count in StartingWorkersOf(goods, workforce))
            {
                sum += count;
            }

            return sum;
        }

        /// <summary>
        /// Adds a workshop of the player for a good the city produces, with no worker yet: its
        /// vacancies are filled from the beggars, after the city's workshops have hired, and
        /// only with labour.
        /// </summary>
        public void AddPlayerWorkshop(int good, int jobs)
        {
            RequireNoGrid();
            RequireWorkshop(good, jobs);
            playerWorkshops.Add(new PlayerWorkshop(good, jobs));
            Changed?.Invoke();
        }

        /// <summary>
        /// Adds a workshop of the player on the grid, as <see cref="AddPlayerWorkshop(int, int)"/> does; it fills its
        /// vacancies only while it is connected to the road network.
        /// </summary>
        /// <exception cref="InvalidOperationException">The market has no grid, or the footprint cannot stand at the place; nothing has changed then.</exception>
        public void AddPlayerWorkshop(int good, int jobs, Footprint footprint, GridPlace place)
        {
            RequireStandable(footprint, place);
            RequireWorkshop(good, jobs);
            PlacedBuilding building = Grid.Place(BuildingKind.Workshop(good), BuildingOwner.Player, footprint, place);
            playerWorkshops.Add(new PlayerWorkshop(good, jobs, building));
            Changed?.Invoke();
        }

        /// <summary>Adds a house of the player for a class: its room is the city's, open to any inhabitant of the class.</summary>
        public void AddPlayerHouse(SocialClass housedClass, int room)
        {
            RequireNoGrid();
            Population.AddRoom(housedClass, room);
            playerHouses[(int)housedClass]++;
            Changed?.Invoke();
        }

        /// <summary>
        /// Adds a house of the player on the grid, as <see cref="AddPlayerHouse(SocialClass, int)"/> does, except that
        /// its room is given only when it connects to the road network: at once when it touches a network road.
        /// </summary>
        /// <exception cref="InvalidOperationException">The market has no grid, or the footprint cannot stand at the place; nothing has changed then.</exception>
        public void AddPlayerHouse(SocialClass housedClass, int room, Footprint footprint, GridPlace place)
        {
            if (housedClass < SocialClass.Rich || housedClass > SocialClass.Poor)
            {
                throw new ArgumentOutOfRangeException(nameof(housedClass));
            }

            RequireStandable(footprint, place);

            if (room < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(room));
            }

            PlaceHouse(housedClass, BuildingOwner.Player, room, footprint, place);
            playerHouses[(int)housedClass]++;
            Changed?.Invoke();
        }

        /// <summary>
        /// Lays the player's roads on every cell that is not a road already, all at once; the houses they connect
        /// give their room.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The market has no grid, a cell is off the grid, forbidden or under a building that is not a road, or no
        /// cell is new; nothing has changed then.
        /// </exception>
        public void AddPlayerRoads(IReadOnlyList<Vector2Int> cells)
        {
            if (Grid == null)
            {
                throw new InvalidOperationException("The market has no grid.");
            }

            var newCells = new List<Vector2Int>();

            if (!TryCollectNewRoads(Grid, cells, newCells))
            {
                throw new InvalidOperationException("A cell cannot hold a road.");
            }

            if (newCells.Count == 0)
            {
                throw new InvalidOperationException("Every cell is a road already.");
            }

            Grid.PlaceRoads(newCells, BuildingOwner.Player);
            Changed?.Invoke();
        }

        /// <summary>
        /// Fills newCells (cleared first) with the distinct cells that are not roads yet, in their order. False, and
        /// newCells empty, when a cell cannot hold a road: off the grid, forbidden, outside the walls of a grid kept inside
        /// them (<see cref="CityGrid.IsBuildable"/>) or under a building that is not a road.
        /// </summary>
        internal static bool TryCollectNewRoads(CityGrid grid, IReadOnlyList<Vector2Int> cells, List<Vector2Int> newCells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            newCells.Clear();

            foreach (Vector2Int cell in cells)
            {
                PlacedBuilding there = grid.BuildingAt(cell);

                if (!grid.IsBuildable(cell) || (there != null && !there.Kind.IsRoad))
                {
                    newCells.Clear();
                    return false;
                }

                if (there == null && !newCells.Contains(cell))
                {
                    newCells.Add(cell);
                }
            }

            return true;
        }

        // Places a house whose room waits for its connection, which may come at once.
        private void PlaceHouse(SocialClass housedClass, BuildingOwner owner, int room, Footprint footprint, GridPlace place)
        {
            roomOfPlacedHouse = room;

            try
            {
                Grid.Place(BuildingKind.House(housedClass), owner, footprint, place);
            }
            finally
            {
                roomOfPlacedHouse = 0;
            }
        }

        // Before the house can connect: the grid raises BuildingPlaced first.
        private void OnBuildingPlaced(PlacedBuilding building)
        {
            // Only the houses the market places: their room is known.
            if (building.Kind.IsHouse && roomOfPlacedHouse > 0)
            {
                roomUntilConnected[building] = roomOfPlacedHouse;
                roomOfPlacedHouse = 0;
            }
        }

        private void OnBuildingConnected(PlacedBuilding building)
        {
            if (roomUntilConnected.TryGetValue(building, out int room))
            {
                roomUntilConnected.Remove(building);
                Population.AddRoom(building.Kind.HousedClass, room);
            }
        }

        private static CityHousingRule RuleOf(IReadOnlyList<CityHousingRule> housing, SocialClass housedClass)
        {
            foreach (CityHousingRule rule in housing)
            {
                if (rule.HousedClass == housedClass)
                {
                    return rule;
                }
            }

            return null;
        }

        private void RequireNoGrid()
        {
            if (Grid != null)
            {
                throw new InvalidOperationException("A market with a grid builds at a place.");
            }
        }

        private void RequireStandable(Footprint footprint, GridPlace place)
        {
            if (Grid == null)
            {
                throw new InvalidOperationException("The market has no grid.");
            }

            if (!Grid.CanPlace(footprint, place))
            {
                throw new InvalidOperationException("The footprint does not fit at this place.");
            }
        }

        private void RequireWorkshop(int good, int jobs)
        {
            RequireGood(good);

            if (!goods[good].IsProduced)
            {
                throw new ArgumentOutOfRangeException(nameof(good), "The city does not produce the good.");
            }

            if (jobs < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(jobs));
            }
        }

        /// <summary>The most workers the city's workshop for a good can have: 0 for a good it does not produce.</summary>
        public int MaxWorkersOf(int good)
        {
            RequireGood(good);

            return goods[good].IsProduced ? goods[good].MaxWorkers : 0;
        }

        /// <summary>Units the workshop makes in a week with the workers of the moment, while the stock is not above T3.</summary>
        public double NominalWeeklyProductionOf(int good)
        {
            return workers[good] * goods[good].DailyOutputPerWorker * daysPerWeek;
        }

        /// <summary>Units the city consumes in a week while it has them.</summary>
        public double NominalWeeklyConsumptionOf(int good)
        {
            return DailyConsumptionOf(good) * daysPerWeek;
        }

        /// <summary>
        /// The factor at which the city sells the good to a buyer when its stock is <paramref name="stock"/>,
        /// as the AI merchants weigh it: one point of the curve, no integral.
        /// </summary>
        public double BuyFactorAt(int good, double stock)
        {
            RequireGood(good);

            return pricing.BuyCurve.FactorAt(stock, ThresholdsOf(good));
        }

        /// <summary>
        /// The factor at which the city buys the good from a seller when its stock is <paramref name="stock"/>,
        /// as the AI merchants weigh it: one point of the curve, no integral.
        /// </summary>
        public double SellFactorAt(int good, double stock)
        {
            RequireGood(good);

            return pricing.SellCurve.FactorAt(stock, ThresholdsOf(good));
        }

        /// <summary>
        /// Units the workshop makes in a day with the workers of the moment, whatever the stock;
        /// 0 for a good the city does not produce.
        /// </summary>
        public double DailyProductionOf(int good)
        {
            RequireGood(good);

            return workers[good] * goods[good].DailyOutputPerWorker;
        }

        /// <summary>
        /// Units the city actually produced over the last days, a week of them at most:
        /// less than the nominal figure on the days the stock was above T3 or an input was short.
        /// </summary>
        public double RecentProductionOf(int good)
        {
            return weekLog.ProducedOf(good);
        }

        /// <summary>Units the workshops take of a good as their input in a week, with the workers of the moment and no shortage.</summary>
        public double NominalWeeklyInputNeedOf(int good)
        {
            return DailyInputNeedOf(good) * daysPerWeek;
        }

        /// <summary>
        /// Units the workshops actually took of a good as their input over the last days, a week
        /// of them at most: less than the nominal figure when the stock was short.
        /// </summary>
        public double RecentInputsOf(int good)
        {
            return weekLog.InputsOf(good);
        }

        /// <summary>
        /// Units the city actually consumed over the last days, a week of them at most:
        /// less than the nominal figure when the stock ran out. What the player buys is not counted.
        /// </summary>
        public double RecentConsumptionOf(int good)
        {
            return weekLog.ConsumedOf(good);
        }

        /// <summary>What the player pays for the next unit, or for a later one of the same purchase.</summary>
        /// <param name="unitsAlreadyBought">Units of this purchase that come before it.</param>
        public long BuyPriceOf(int good, int unitsAlreadyBought = 0)
        {
            RequireGood(good);
            RequireNotNegative(unitsAlreadyBought, nameof(unitsAlreadyBought));

            return pricing.BuyPrice(goods[good].BasePrice, StockOf(good) - unitsAlreadyBought, ThresholdsOf(good));
        }

        /// <summary>What the player is paid for the next unit, or for a later one of the same sale.</summary>
        /// <param name="unitsAlreadySold">Units of this sale that come before it.</param>
        public long SellPriceOf(int good, int unitsAlreadySold = 0)
        {
            RequireGood(good);
            RequireNotNegative(unitsAlreadySold, nameof(unitsAlreadySold));

            return pricing.SellPrice(goods[good].BasePrice, StockOf(good) + unitsAlreadySold, ThresholdsOf(good));
        }

        /// <summary>Takes whole units out of the stock. Throws when fewer are available.</summary>
        public void Take(int good, int units)
        {
            RequirePositive(units);

            if (units > AvailableOf(good))
            {
                throw new ArgumentOutOfRangeException(nameof(units));
            }

            // Not below nothing: the last unit can be that hair short.
            stocks[good] = Math.Max(0d, stocks[good] - units);
            Changed?.Invoke();
        }

        /// <summary>Adds whole units to the stock, whatever it already holds.</summary>
        public void Put(int good, int units)
        {
            RequireGood(good);
            RequirePositive(units);

            stocks[good] += units;
            Changed?.Invoke();
        }

        /// <summary>
        /// One day of the city: the inhabitants consume every good, down to nothing; then every
        /// workshop hires or lays off by where the stock left stands against the thresholds, takes
        /// its inputs, in proportion to the scarcest, and produces as much, unless that stock is
        /// above T3, where the inputs are taken all the same. With labour,
        /// the classes' satisfactions follow the stocks (with a social rule), the player's
        /// workshops fill their vacancies from the beggars, the poor rise and fall between the
        /// classes and some leave (with a social rule), the beggars come or go, and the city
        /// builds a house for each crowded class.
        /// </summary>
        public void StartDay()
        {
            weekLog.StartDay();
            LastDay.Clear();

            // Every good first, by the inhabitants of the morning: a workshop that hires
            // changes the poor, so it must not change what the goods after it consume.
            var consumed = new double[goods.Length];

            for (int i = 0; i < goods.Length; i++)
            {
                double needed = DailyConsumptionOf(i);
                consumed[i] = Math.Min(stocks[i], needed);
                stocks[i] -= consumed[i];
                LastDay.RecordConsumption(i, needed, consumed[i]);
            }

            for (int i = 0; i < goods.Length; i++)
            {
                MarketGood good = goods[i];
                double produced = 0d;

                if (good.IsProduced)
                {
                    StockThresholds goodThresholds = thresholds[i];
                    int target = workforce.NextWorkers(workers[i], good.MaxWorkers, stocks[i], goodThresholds);

                    if (Labour == null)
                    {
                        workers[i] = target;
                    }
                    else if (target > workers[i])
                    {
                        Hire(i, target - workers[i]);
                    }
                    else if (target < workers[i])
                    {
                        LayOff(i, workers[i] - target);
                    }

                    double potential = workers[i] * good.DailyOutputPerWorker;
                    double made = potential * TakeInputs(good, potential);

                    // Above T3 the inputs are taken all the same, and lost.
                    if (WorkforceRule.ProducesAt(stocks[i], goodThresholds))
                    {
                        produced = made;
                        stocks[i] += produced;
                    }
                }

                weekLog.Record(i, produced, consumed[i]);
            }

            if (Labour != null)
            {
                if (Social != null)
                {
                    UpdateSatisfactions();
                }

                FillVacancies();
                RunPlayerWorkshops();

                if (Social != null)
                {
                    Social.RiseAndFall(Population);
                    LetThePoorLeave();
                }

                Population.SetBeggars(Labour.NextBeggars(Population.Beggars, Population.Inhabitants));
                BuildCityHouses();
            }

            // Last, so that the next day starts on the thresholds of the population this day leaves.
            UpdateThresholds();
            Changed?.Invoke();
        }

        // Takes what a workshop needs to make these units, in proportion to its scarcest input, from
        // the stocks as they stand, and returns that proportion: 1 with all its inputs or none.
        private double TakeInputs(MarketGood good, double units)
        {
            IReadOnlyList<MarketInput> inputs = good.Inputs;
            double factor = 1d;
            int scarcest = -1;

            for (int k = 0; k < inputs.Count; k++)
            {
                double needed = units * inputs[k].UnitsPerUnit;

                if (needed > 0d && stocks[inputs[k].Good] / needed < factor)
                {
                    factor = stocks[inputs[k].Good] / needed;
                    scarcest = k;
                }
            }

            for (int k = 0; k < inputs.Count; k++)
            {
                int input = inputs[k].Good;
                double needed = units * inputs[k].UnitsPerUnit;

                // The scarcest is taken whole, so that no rounding leaves a crumb of it.
                double taken = k == scarcest ? stocks[input] : Math.Min(stocks[input], needed * factor);
                stocks[input] -= taken;
                weekLog.AddInputs(input, taken);
                LastDay.AddInputs(input, needed, taken);
            }

            return factor;
        }

        private static (int consumer, double unitsPerUnit)[][] ConsumersOf(IReadOnlyList<MarketGood> goods)
        {
            var consumers = new List<(int consumer, double unitsPerUnit)>[goods.Count];

            for (int i = 0; i < goods.Count; i++)
            {
                consumers[i] = new List<(int consumer, double unitsPerUnit)>();
            }

            for (int i = 0; i < goods.Count; i++)
            {
                foreach (MarketInput input in goods[i].Inputs)
                {
                    consumers[input.Good].Add((i, input.UnitsPerUnit));
                }
            }

            return Array.ConvertAll(consumers, list => list.ToArray());
        }

        private static int[] StartingWorkersOf(IReadOnlyList<MarketGood> goods, WorkforceRule workforce)
        {
            if (goods == null)
            {
                throw new ArgumentNullException(nameof(goods));
            }

            if (workforce == null)
            {
                throw new ArgumentNullException(nameof(workforce));
            }

            int workshops = 0;

            for (int i = 0; i < goods.Count; i++)
            {
                if (goods[i].IsProduced)
                {
                    workshops++;
                }
            }

            var starting = new int[goods.Count];

            for (int i = 0; i < goods.Count; i++)
            {
                starting[i] = goods[i].IsProduced ? workforce.StartingWorkersOf(goods[i].MaxWorkers, workshops) : 0;
            }

            return starting;
        }

        // In this order: the workshop's own reserve, the beggars within the free room,
        // then the reserves of the other workshops in the order of the catalogue. What is still
        // missing is not hired today.
        private void Hire(int good, int wanted)
        {
            wanted -= TakeFromReserve(good, good, wanted);

            // The beggars are hired only while the poor are satisfied.
            bool poorSatisfied = Social == null || Population.SatisfactionOf(SocialClass.Poor) > 0;
            int fromBeggars = poorSatisfied
                ? Math.Min(wanted, Labour.WorkersHireableFrom(Population.Beggars, Population.FreeRoomOf(SocialClass.Poor)))
                : 0;
            Population.Hire(fromBeggars, Labour.InhabitantsPerWorker);
            workers[good] += fromBeggars;
            wanted -= fromBeggars;

            for (int other = 0; other < goods.Length && wanted > 0; other++)
            {
                if (other != good)
                {
                    wanted -= TakeFromReserve(other, good, wanted);
                }
            }
        }

        // Moves up to the wanted workers from a workshop's reserve to a workshop; housed inhabitants do not change.
        private int TakeFromReserve(int reserve, int good, int wanted)
        {
            int taken = Math.Min(wanted, reserves[reserve]);
            reserves[reserve] -= taken;
            workers[good] += taken;

            return taken;
        }

        // The laid-off workers stay housed in the reserve, which leaks back to the beggars.
        private void LayOff(int good, int laidOff)
        {
            workers[good] -= laidOff;
            reserves[good] += laidOff;

            ReserveLeak leak = Labour.LeakAfterLayoff(leakCounters[good], workers[good], reserves[good]);
            leakCounters[good] = leak.Counter;
            reserves[good] -= leak.Leaving;

            // A worker whose family cannot be taken stays in the reserve.
            reserves[good] += Population.SendToBeggars(leak.Leaving, Labour.InhabitantsPerWorker);
        }

        // The second hiring: the beggars fill the player's workshops in the order
        // they were built, each up to its jobs; a workshop of the grid only while it is connected.
        private void FillVacancies()
        {
            int fillable = Labour.VacanciesFillable(Population.Beggars, Population.FreeRoomOf(SocialClass.Poor));

            foreach (PlayerWorkshop workshop in playerWorkshops)
            {
                if (fillable == 0)
                {
                    break;
                }

                if (workshop.Building != null && !workshop.Building.IsConnected)
                {
                    continue;
                }

                int hired = Math.Min(fillable, workshop.Jobs - workshop.Workers);
                Population.Hire(hired, Labour.InhabitantsPerWorker);
                workshop.Workers += hired;
                fillable -= hired;
            }
        }

        // The player's workshops make their goods into the trading post, from its inputs, each good in the order its
        // first workshop was built: a good made earlier is already there for the next. Only whole units move; the
        // fractions are carried to the next day.
        private void RunPlayerWorkshops()
        {
            if (MerchantOutput == null || PlayerTradingPost == null)
            {
                return;
            }

            var done = new HashSet<int>();

            foreach (PlayerWorkshop workshop in playerWorkshops)
            {
                if (done.Add(workshop.Good))
                {
                    RunPlayerWorkshopsOf(workshop.Good);
                }
            }
        }

        private void RunPlayerWorkshopsOf(int good)
        {
            // What floating sums lose: 10 days of a tenth make a whole unit.
            const double Tolerance = 1e-9;

            double potential = PlayerDailyOutputOf(good);

            if (potential <= 0d)
            {
                return;
            }

            IGoodStore post = PlayerTradingPost;
            IReadOnlyList<MarketInput> inputs = goods[good].Inputs;
            double[] debts = playerInputDebts[good] ??= new double[inputs.Count];
            double factor = 1d;

            // A debt is backed by a unit still in the post, which it does not leave for the day's needs.
            for (int k = 0; k < inputs.Count; k++)
            {
                double available = Math.Max(0d, post.UnitsOf(inputs[k].Good) - debts[k]);
                factor = Math.Min(factor, available / (potential * inputs[k].UnitsPerUnit));
            }

            double made = potential * factor;

            if (made <= 0d)
            {
                return;
            }

            for (int k = 0; k < inputs.Count; k++)
            {
                debts[k] += made * inputs[k].UnitsPerUnit;
                int taken = Math.Min((int)Math.Floor(debts[k] + Tolerance), post.UnitsOf(inputs[k].Good));

                if (taken > 0)
                {
                    post.TryRemove(inputs[k].Good, taken);
                    debts[k] = Math.Max(0d, debts[k] - taken);
                }
            }

            playerPending[good] += made;
            int delivered = (int)Math.Floor(playerPending[good] + Tolerance);

            if (delivered > 0)
            {
                post.TryAdd(good, delivered);
                playerPending[good] = Math.Max(0d, playerPending[good] - delivered);
            }
        }

        // One house a day at most for each class, with every material or none taken. What it
        // takes is not consumption: the week's log does not count it.
        private void BuildCityHouses()
        {
            foreach (CityHousingRule rule in Housing)
            {
                if (rule.IsCrowded(Population))
                {
                    BuildCityHouse(rule);
                }
            }
        }

        private void BuildCityHouse(CityHousingRule rule)
        {
            foreach (MaterialCost material in rule.Materials)
            {
                if (AvailableOf(material.Good) < material.Units)
                {
                    return;
                }
            }

            // With a grid the house needs a place and the roads that connect it, found before anything is taken:
            // a place off the network, without the roads to join it, builds nothing.
            GridPlace place = default;

            if (Grid != null)
            {
                if (!placement.TryChoose(Grid, rule.Footprint.Value, out place, roadsToLay))
                {
                    return;
                }

                CellsOf(rule.Footprint.Value, place, houseCells);

                if (!Grid.CanPlace(rule.Footprint.Value, place) || !RoadPlanner.Connects(Grid, houseCells, roadsToLay))
                {
                    return;
                }
            }

            foreach (MaterialCost material in rule.Materials)
            {
                // Not below nothing: the last unit can be a hair short.
                stocks[material.Good] = Math.Max(0d, stocks[material.Good] - material.Units);
            }

            if (Grid != null)
            {
                // Its room comes when it connects: at once or through its roads.
                PlaceHouse(rule.HousedClass, BuildingOwner.City, rule.Room, rule.Footprint.Value, place);
                Grid.PlaceRoads(roadsToLay, BuildingOwner.City);
            }
            else
            {
                Population.AddRoom(rule.HousedClass, rule.Room);
            }

            CityHouses++;
        }

        private static void CellsOf(Footprint footprint, GridPlace place, List<Vector2Int> cells)
        {
            cells.Clear();
            Vector2Int size = footprint.SizeFor(place.Orientation);

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    cells.Add(new Vector2Int(place.Origin.x + x, place.Origin.y + y));
                }
            }
        }

        // Each class's satisfaction takes a step towards the target the stocks of the goods it
        // consumes give.
        private void UpdateSatisfactions()
        {
            foreach (SocialClass housedClass in Classes)
            {
                int goodsTerm = 0;

                for (int i = 0; i < goods.Length; i++)
                {
                    if (DailyConsumptionOf(i) > 0d && ThresholdsOf(i).T0 > 0d)
                    {
                        goodsTerm += Social.GoodTerm(goods[i].SatisfactionWeights[housedClass], stocks[i], ThresholdsOf(i).T0);
                    }
                }

                int target = Social.TargetOf(housedClass, goodsTerm);
                Population.SetSatisfaction(housedClass, Social.NextSatisfaction(Population.SatisfactionOf(housedClass), target));
            }
        }

        // The poor who leave take their jobs with them, a worker for every family.
        private void LetThePoorLeave()
        {
            int workers = Social.LeavingPoorOf(Population) / Labour.InhabitantsPerWorker;

            if (workers < 1)
            {
                return;
            }

            int removed = TakeWorkersAway(workers);
            Population.Leave(SocialClass.Poor, removed * Labour.InhabitantsPerWorker);
        }

        // The reserves first, in the catalogue's order; then the workers above the protected ones
        // of the city's workshops, spread by a step over all that can be taken; then the player's
        // workshops, in the order they were built.
        private int TakeWorkersAway(int wanted)
        {
            int removed = 0;

            for (int good = 0; good < goods.Length && wanted > 0; good++)
            {
                int taken = Math.Min(wanted, reserves[good]);
                reserves[good] -= taken;
                wanted -= taken;
                removed += taken;
            }

            if (wanted == 0)
            {
                return removed;
            }

            int protectedWorkers = Social.ProtectedWorkers;
            int takeable = 0;

            for (int good = 0; good < goods.Length; good++)
            {
                takeable += Math.Max(0, workers[good] - protectedWorkers);
            }

            foreach (PlayerWorkshop workshop in playerWorkshops)
            {
                takeable += workshop.Workers;
            }

            int step = Math.Max(2, takeable / wanted);
            int accumulated = 0;

            for (int good = 0; good < goods.Length; good++)
            {
                if (workers[good] <= protectedWorkers)
                {
                    continue;
                }

                accumulated += workers[good] - protectedWorkers;

                if (accumulated >= step)
                {
                    int taken = Math.Min(accumulated / step, wanted);

                    if (taken >= workers[good])
                    {
                        taken = workers[good] - 1;
                    }

                    workers[good] -= taken;
                    wanted -= taken;
                    accumulated -= taken * step;
                    removed += taken;
                }
            }

            foreach (PlayerWorkshop workshop in playerWorkshops)
            {
                if (wanted < 1)
                {
                    break;
                }

                if (workshop.Workers == 0)
                {
                    continue;
                }

                accumulated += workshop.Workers;

                if (accumulated >= step)
                {
                    int taken = Math.Min(Math.Min(accumulated / step, wanted), workshop.Workers);
                    workshop.Workers -= taken;
                    accumulated -= taken * step;
                    wanted -= taken;
                    removed += taken;
                }
            }

            return removed;
        }

        private static IReadOnlyList<CityHousingRule> CopyOf(IReadOnlyList<CityHousingRule> housing)
        {
            var copy = new List<CityHousingRule>();

            if (housing == null)
            {
                return copy;
            }

            foreach (CityHousingRule rule in housing)
            {
                if (rule == null)
                {
                    throw new ArgumentNullException(nameof(housing));
                }

                if (copy.Exists(other => other.HousedClass == rule.HousedClass))
                {
                    throw new ArgumentException("A class has two housing rules.", nameof(housing));
                }

                copy.Add(rule);
            }

            return copy;
        }

        private void RequireGood(int good)
        {
            if (good < 0 || good >= goods.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(good));
            }
        }

        private static void RequirePositive(int units)
        {
            if (units < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(units));
            }
        }

        private static void RequireNotNegative(int units, string name)
        {
            if (units < 0)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        /// <summary>A workshop the player has built: its good, its jobs, the workers it has and its building on the grid.</summary>
        private sealed class PlayerWorkshop
        {
            public PlayerWorkshop(int good, int jobs, PlacedBuilding building = null)
            {
                Good = good;
                Jobs = jobs;
                Building = building;
            }

            /// <summary>Null for a workshop of a market without grid.</summary>
            public PlacedBuilding Building { get; }

            public int Good { get; }

            public int Jobs { get; }

            public int Workers { get; set; }
        }
    }
}
