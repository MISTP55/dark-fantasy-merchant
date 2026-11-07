using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// One city in play, around its <see cref="Market"/>. Every day the city consumes its goods, then its workshops
    /// hire or lay off by where each stock stands against its thresholds, and produce. It also remembers what the days
    /// of the last week actually produced and consumed. What it consumes follows its population. Given a
    /// <see cref="LabourRule"/>, its workshops hire from their reserves and the beggars within the free room of the
    /// houses, lay off into a reserve that leaks back to the beggars, the player's workshops fill their vacancies from
    /// the beggars, and the beggars come or go; without one, hiring is immediate, layoffs vanish and the population
    /// stays as it is. Given <see cref="CityHousingRule"/>s as well, a city builds a house of its own for each crowded
    /// class from its stock. Given a <see cref="LeavingRule"/> with the labour, the poor without room leave with their
    /// jobs; without one, nobody leaves. The classes never move. On a grid, a house gives its room and a player's workshop hires only once connected to the road network.
    /// </summary>
    public sealed class City
    {
        private readonly MarketGood[] goods;
        private readonly CityWorkforce cityWorkforce;
        private readonly PlayerWorkshops playerWorkshops;
        private readonly CityHousing cityHousing;
        private readonly WorkforceRule workforceRule;
        private readonly int daysPerWeek;
        private readonly MarketWeekLog weekLog;

        // By good, the workshops that take it as an input and how much for every unit they make,
        // each list in the catalogue's order of its workshops: the recipes never change once the
        // city exists.
        private readonly (int consumer, double unitsPerUnit)[][] consumersOf;

        /// <param name="thresholdRule">How the stock thresholds of a good, at which its prices are given, follow what the city needs (its inhabitants and its workshops' inputs) and produces of it.</param>
        /// <param name="workforce">How the workshops hire, lay off and start.</param>
        /// <param name="startingDays">Days of its demand (consumption and inputs) the city holds of every good when the game starts.</param>
        /// <param name="daysPerWeek">Days of the calendar's week, over which the weekly figures are given.</param>
        /// <param name="population">The inhabitants of the city, who set what it consumes.</param>
        /// <param name="labour">How the workshops take their workers from the population; null for immediate hiring and a population that stays.</param>
        /// <param name="housing">When the city builds a house of its own for a class, each class once at most, only with labour; null for a city that never does.</param>
        /// <param name="leaving">Who leaves the city for want of room, only with labour; null for a city nobody leaves.</param>
        /// <param name="grid">The city grid the houses stand on, with its placement strategy (both or neither); null for a city whose houses have no place. Every housing rule then has a footprint. The houses already connected are in the population's room; one not connected yet gives the room of its class's housing rule when it connects, and needs that rule.</param>
        /// <param name="placement">Where the city builds its own houses on the grid.</param>
        /// <param name="merchantOutput">What the player's workshops make; null for workshops that hire but make nothing.</param>
        public City(
            IReadOnlyList<MarketGood> goods,
            ThresholdRule thresholdRule,
            WorkforceRule workforce,
            double startingDays,
            MarketPricing pricing,
            int daysPerWeek,
            CityPopulation population,
            LabourRule labour = null,
            IReadOnlyList<CityHousingRule> housing = null,
            LeavingRule leaving = null,
            CityGrid grid = null,
            IPlacementStrategy placement = null,
            MerchantOutputRule merchantOutput = null)
        {
            if (goods == null)
            {
                throw new ArgumentNullException(nameof(goods));
            }

            workforceRule = workforce ?? throw new ArgumentNullException(nameof(workforce));

            if (daysPerWeek < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysPerWeek));
            }

            Population = population ?? throw new ArgumentNullException(nameof(population));

            if (leaving != null && labour == null)
            {
                throw new ArgumentException("Inhabitants leave only with labour.", nameof(leaving));
            }

            Labour = labour;
            Leaving = leaving;
            cityHousing = new CityHousing(Population, housing, grid, placement);
            Grid = grid;
            this.daysPerWeek = daysPerWeek;
            weekLog = new MarketWeekLog(goods.Count, daysPerWeek);
            LastDay = new CityDayReport(goods.Count);
            this.goods = new MarketGood[goods.Count];
            cityWorkforce = new CityWorkforce(CityWorkforce.StartingWorkersOf(goods, workforce), Population, labour);
            playerWorkshops = new PlayerWorkshops(this.goods, daysPerWeek, merchantOutput);

            for (int i = 0; i < goods.Count; i++)
            {
                this.goods[i] = goods[i];

                foreach (MarketInput input in goods[i].Inputs)
                {
                    if (input.Good >= goods.Count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(goods), $"The good {i} takes the good {input.Good}, which the city does not have.");
                    }

                    if (input.Good == i)
                    {
                        throw new ArgumentException($"The good {i} is its own input.", nameof(goods));
                    }
                }
            }

            consumersOf = ConsumersOf(goods);

            // Every good first: the demand the market reads of one looks at the workshops of the others.
            Market = new CityMarket(this.goods, thresholdRule, pricing, DailyDemandOf, startingDays);

            foreach (CityHousingRule rule in Housing)
            {
                foreach (MaterialCost material in rule.Materials)
                {
                    RequireGood(material.Good);
                }
            }

            cityHousing.FollowGrid();
            Market.Changed += RaiseChanged;
        }

        /// <summary>The stocks of the city, their thresholds and their prices.</summary>
        public CityMarket Market { get; }

        public CityPopulation Population { get; }

        /// <summary>How the workshops take their workers from the population; null when hiring is immediate.</summary>
        public LabourRule Labour { get; }

        /// <summary>The beggars the city draws towards: 0 without labour.</summary>
        public int BeggarTarget => Labour?.BeggarTargetFor(Population.Inhabitants) ?? 0;

        /// <summary>When the city builds a house of its own for each class; empty when it never does.</summary>
        public IReadOnlyList<CityHousingRule> Housing => cityHousing.Rules;

        /// <summary>The grid the houses and the player's workshops stand on; null for a city whose buildings have no place.</summary>
        public CityGrid Grid { get; }

        /// <summary>Who leaves the city for want of room; null when nobody does.</summary>
        public LeavingRule Leaving { get; }

        /// <summary>What the player's workshops make; null when they make nothing.</summary>
        public MerchantOutputRule MerchantOutput => playerWorkshops.Output;

        /// <summary>
        /// The player's trading post in the city, which the player's workshops take their inputs from and deliver
        /// into; null until the player has one there, and their workshops make nothing until then.
        /// </summary>
        public IGoodStore PlayerTradingPost => playerWorkshops.TradingPost;

        /// <summary>Gives the city the player's trading post, once it opens there.</summary>
        /// <exception cref="InvalidOperationException">The city already has one.</exception>
        public void SetPlayerTradingPost(IGoodStore post)
        {
            playerWorkshops.SetTradingPost(post);
        }

        /// <summary>Houses of a class the player has built in the city.</summary>
        public int PlayerHousesOf(SocialClass housedClass)
        {
            return cityHousing.PlayerHousesOf(housedClass);
        }

        /// <summary>Houses the city has built of its own since the game started, not the player's.</summary>
        public int CityHouses => cityHousing.CityHouses;

        /// <summary>What happened in the last <see cref="StartDay"/>, for the debug tools; the same object every day.</summary>
        public CityDayReport LastDay { get; }

        /// <summary>
        /// Raised once by a day that starts, once when units were taken from the market or put in it, and once by
        /// <see cref="AddPlayerWorkshop"/>, by <see cref="AddPlayerHouse"/> and by <see cref="AddPlayerRoads"/>.
        /// </summary>
        public event Action Changed;

        /// <summary>Units the housed inhabitants consume in a day while the city has them, at the good's rate; the beggars consume nothing.</summary>
        public double DailyConsumptionOf(int good)
        {
            RequireGood(good);

            return goods[good].DailyConsumptionPerThousand * Population.Housed / 1000d;
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

        /// <summary>Workers of the city's workshop for a good: 0 for a good it does not produce.</summary>
        public int WorkersOf(int good)
        {
            RequireGood(good);

            return cityWorkforce.WorkersOf(good);
        }

        /// <summary>Laid-off workers of the city's workshop for a good, still housed, waiting to be hired back.</summary>
        public int ReserveOf(int good)
        {
            RequireGood(good);

            return cityWorkforce.ReserveOf(good);
        }

        /// <summary>Workshops the player has built for a good in the city.</summary>
        public int PlayerWorkshopsOf(int good)
        {
            RequireGood(good);

            return playerWorkshops.CountOf(good);
        }

        /// <summary>Workers of all the player's workshops for a good in the city.</summary>
        public int PlayerWorkersOf(int good)
        {
            RequireGood(good);

            return playerWorkshops.WorkersOf(good);
        }

        /// <summary>
        /// Units the player's workshops for a good make in a day with their workers of the moment, by the city's
        /// <see cref="MerchantOutput"/>, inputs not counted; 0 without it.
        /// </summary>
        public double PlayerDailyOutputOf(int good)
        {
            RequireGood(good);

            return playerWorkshops.DailyOutputOf(good);
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

            return playerWorkshops.DailyOutputOf(good, rule);
        }

        /// <summary>
        /// The wages of the workers of all the player's workshops in the city for a week, unrounded: a share of the base
        /// value of what they make in a week with their workers of the moment (<see cref="PlayerDailyOutputOf(int)"/>),
        /// whatever the inputs they lack.
        /// </summary>
        /// <param name="wageShare">The share of that value the workers are paid, 0 or more.</param>
        public double PlayerWeeklyWagesOf(double wageShare)
        {
            return playerWorkshops.WeeklyWagesOf(wageShare);
        }

        /// <summary>Jobs of all the player's workshops for a good in the city.</summary>
        public int PlayerJobsOf(int good)
        {
            RequireGood(good);

            return playerWorkshops.JobsOf(good);
        }

        /// <summary>
        /// The workers the city's workshops start with, added up: the starting workforce
        /// spread over the goods that are produced, as the constructor spreads it.
        /// </summary>
        public static int StartingWorkforceOf(IReadOnlyList<MarketGood> goods, WorkforceRule workforce)
        {
            int sum = 0;

            foreach (int count in CityWorkforce.StartingWorkersOf(goods, workforce))
            {
                sum += count;
            }

            return sum;
        }

        /// <summary>
        /// Adds a workshop of the player on the grid for a good the city produces, with no worker yet: its vacancies
        /// are filled from the beggars, after the city's workshops have hired, only with labour and only while it is
        /// connected to the road network.
        /// </summary>
        /// <exception cref="InvalidOperationException">The city has no grid, or the footprint cannot stand at the place; nothing has changed then.</exception>
        public void AddPlayerWorkshop(int good, int jobs, Footprint footprint, GridPlace place)
        {
            RequireStandable(footprint, place);
            RequireWorkshop(good, jobs);
            PlacedBuilding building = Grid.Place(BuildingKind.Workshop(good), BuildingOwner.Player, footprint, place);
            playerWorkshops.Add(good, jobs, building);
            Changed?.Invoke();
        }

        /// <summary>
        /// Adds a house of the player on the grid for a class: its room is the city's, open to any inhabitant of the
        /// class, given only when it connects to the road network: at once when it touches a network road.
        /// </summary>
        /// <exception cref="InvalidOperationException">The city has no grid, or the footprint cannot stand at the place; nothing has changed then.</exception>
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

            cityHousing.AddPlayerHouse(housedClass, room, footprint, place);
            Changed?.Invoke();
        }

        /// <summary>
        /// Lays the player's roads on every cell that is not a road already, all at once; the houses they connect
        /// give their room.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The city has no grid, a cell is off the grid, forbidden or under a building that is not a road, or no
        /// cell is new; nothing has changed then.
        /// </exception>
        public void AddPlayerRoads(IReadOnlyList<Vector2Int> cells)
        {
            if (Grid == null)
            {
                throw new InvalidOperationException("The city has no grid.");
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

        private void RequireStandable(Footprint footprint, GridPlace place)
        {
            if (Grid == null)
            {
                throw new InvalidOperationException("The city has no grid.");
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
            return cityWorkforce.WorkersOf(good) * goods[good].DailyOutputPerWorker * daysPerWeek;
        }

        /// <summary>Units the city consumes in a week while it has them.</summary>
        public double NominalWeeklyConsumptionOf(int good)
        {
            return DailyConsumptionOf(good) * daysPerWeek;
        }

        /// <summary>
        /// Units the workshop makes in a day with the workers of the moment, whatever the stock;
        /// 0 for a good the city does not produce.
        /// </summary>
        public double DailyProductionOf(int good)
        {
            RequireGood(good);

            return cityWorkforce.WorkersOf(good) * goods[good].DailyOutputPerWorker;
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
                consumed[i] = Market.Withdraw(i, needed);
                LastDay.RecordConsumption(i, needed, consumed[i]);
            }

            for (int i = 0; i < goods.Length; i++)
            {
                MarketGood good = goods[i];
                double produced = 0d;

                if (good.IsProduced)
                {
                    StockThresholds goodThresholds = Market.ThresholdsOf(i);
                    cityWorkforce.MoveTo(i, workforceRule.NextWorkers(cityWorkforce.WorkersOf(i), good.MaxWorkers, Market.StockOf(i), goodThresholds));

                    double potential = cityWorkforce.WorkersOf(i) * good.DailyOutputPerWorker;
                    double made = potential * TakeInputs(good, potential);

                    // Above T3 the inputs are taken all the same, and lost.
                    if (WorkforceRule.ProducesAt(Market.StockOf(i), goodThresholds))
                    {
                        produced = made;
                        Market.Deposit(i, produced);
                    }
                }

                weekLog.Record(i, produced, consumed[i]);
            }

            if (Labour != null)
            {
                playerWorkshops.FillVacancies(Population, Labour);
                playerWorkshops.Run();

                if (Leaving != null)
                {
                    LetThePoorLeave();
                }

                Population.SetBeggars(Labour.NextBeggars(Population.Beggars, Population.Inhabitants));
                cityHousing.BuildCityHouses(Market.AvailableOf, TakeMaterial);
            }

            // Last, so that the next day starts on the thresholds of the population this day leaves.
            Market.EndDay();
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

                if (needed > 0d && Market.StockOf(inputs[k].Good) / needed < factor)
                {
                    factor = Market.StockOf(inputs[k].Good) / needed;
                    scarcest = k;
                }
            }

            for (int k = 0; k < inputs.Count; k++)
            {
                int input = inputs[k].Good;
                double needed = units * inputs[k].UnitsPerUnit;

                // The scarcest is taken whole, so that no rounding leaves a crumb of it.
                double taken = Market.Withdraw(input, k == scarcest ? Market.StockOf(input) : needed * factor);
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

        // The poor who leave take their jobs with them, a worker for every family.
        private void LetThePoorLeave()
        {
            int workers = Leaving.LeavingOf(Population) / Labour.InhabitantsPerWorker;

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
            int removed = cityWorkforce.TakeReserves(ref wanted);

            if (wanted == 0)
            {
                return removed;
            }

            int protectedWorkers = Leaving.ProtectedWorkers;
            int takeable = cityWorkforce.TakeableAbove(protectedWorkers) + playerWorkshops.Workers;
            int step = Math.Max(2, takeable / wanted);
            int accumulated = 0;

            removed += cityWorkforce.TakeAbove(protectedWorkers, step, ref wanted, ref accumulated);
            removed += playerWorkshops.TakeAway(step, ref wanted, ref accumulated);

            return removed;
        }

        // Takes the materials of a house the city builds of its own; not below nothing: the last unit can be a hair short.
        private void TakeMaterial(int good, int units)
        {
            Market.Withdraw(good, units);
        }

        private void RaiseChanged()
        {
            Changed?.Invoke();
        }

        private void RequireGood(int good)
        {
            if (good < 0 || good >= goods.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(good));
            }
        }
    }
}
