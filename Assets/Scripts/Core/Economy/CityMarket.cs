using System;
using System.Collections.Generic;

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
    /// without one, the classes never move.
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
        private readonly ThresholdRule thresholdRule;
        private readonly WorkforceRule workforce;
        private readonly MarketPricing pricing;
        private readonly int daysPerWeek;
        private readonly MarketWeekLog weekLog;
        private readonly int[] playerHouses = new int[3];

        private static readonly SocialClass[] Classes = { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor };

        /// <param name="thresholdRule">How the stock thresholds of a good, at which its prices are given, follow what the city consumes and produces of it.</param>
        /// <param name="workforce">How the workshops hire, lay off and start.</param>
        /// <param name="startingDays">Days of consumption the city holds of every good when the game starts.</param>
        /// <param name="daysPerWeek">Days of the calendar's week, over which the weekly figures are given.</param>
        /// <param name="population">The inhabitants of the city, who set what it consumes.</param>
        /// <param name="labour">How the workshops take their workers from the population; null for immediate hiring and a population that stays.</param>
        /// <param name="housing">When the city builds a house of its own for a class, each class once at most, only with labour; null for a city that never does.</param>
        /// <param name="social">The satisfaction and the moves of the classes, only with labour; null for classes that never move.</param>
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
            SocialRule social = null)
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
            this.daysPerWeek = daysPerWeek;
            weekLog = new MarketWeekLog(goods.Count, daysPerWeek);
            this.goods = new MarketGood[goods.Count];
            stocks = new double[goods.Count];
            workers = StartingWorkersOf(goods, workforce);
            reserves = new int[goods.Count];
            leakCounters = new double[goods.Count];

            for (int i = 0; i < goods.Count; i++)
            {
                this.goods[i] = goods[i];

                // The same for every good, produced or not: producers fill up as the days pass.
                stocks[i] = startingDays * DailyConsumptionOf(i);
            }

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
        }

        public int GoodCount => goods.Length;

        public CityPopulation Population { get; }

        /// <summary>How the workshops take their workers from the population; null when hiring is immediate.</summary>
        public LabourRule Labour { get; }

        /// <summary>The beggars the city draws towards: 0 without labour.</summary>
        public int BeggarTarget => Labour?.BeggarTargetFor(Population.Inhabitants) ?? 0;

        /// <summary>When the city builds a house of its own for each class; empty when it never does.</summary>
        public IReadOnlyList<CityHousingRule> Housing { get; }

        /// <summary>How the classes are satisfied and move; null when they never do.</summary>
        public SocialRule Social { get; }

        /// <summary>Houses of a class the player has built in the city.</summary>
        public int PlayerHousesOf(SocialClass housedClass)
        {
            return playerHouses[(int)housedClass];
        }

        /// <summary>Houses the city has built of its own since the game started, not the player's.</summary>
        public int CityHouses { get; private set; }

        /// <summary>
        /// Raised once by a day that starts, once when units were taken or put, and once by
        /// <see cref="AddPlayerWorkshop"/> and by <see cref="AddPlayerHouse"/>.
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

        /// <summary>The stocks at which the prices of a good are given, for the population of the moment.</summary>
        public StockThresholds ThresholdsOf(int good)
        {
            RequireGood(good);

            return thresholdRule.ThresholdsFor(DailyConsumptionOf(good), goods[good].DailyCapacity);
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
        /// Units the player's workshops for a good would make in a day with their workers of
        /// the moment, by the rule of a merchant's workshops. They produce nothing yet: this is
        /// only what they are worth.
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
            RequireGood(good);

            if (!goods[good].IsProduced)
            {
                throw new ArgumentOutOfRangeException(nameof(good), "The city does not produce the good.");
            }

            if (jobs < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(jobs));
            }

            playerWorkshops.Add(new PlayerWorkshop(good, jobs));
            Changed?.Invoke();
        }

        /// <summary>Adds a house of the player for a class: its room is the city's, open to any inhabitant of the class.</summary>
        public void AddPlayerHouse(SocialClass housedClass, int room)
        {
            Population.AddRoom(housedClass, room);
            playerHouses[(int)housedClass]++;
            Changed?.Invoke();
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
        /// less than the nominal figure on the days the stock was above T3.
        /// </summary>
        public double RecentProductionOf(int good)
        {
            return weekLog.ProducedOf(good);
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
        /// One day of the city, as in Patrician III: the inhabitants consume every good, down
        /// to nothing; then every workshop hires or lays off by where the stock left stands
        /// against the thresholds, and produces, unless that stock is above T3. With labour,
        /// the classes' satisfactions follow the stocks (with a social rule), the player's
        /// workshops fill their vacancies from the beggars, the poor rise and fall between the
        /// classes and some leave (with a social rule), the beggars come or go, and the city
        /// builds a house for each crowded class.
        /// </summary>
        public void StartDay()
        {
            weekLog.StartDay();

            // Every good first, by the inhabitants of the morning: a workshop that hires
            // changes the poor, so it must not change what the goods after it consume.
            var consumed = new double[goods.Length];

            for (int i = 0; i < goods.Length; i++)
            {
                consumed[i] = Math.Min(stocks[i], DailyConsumptionOf(i));
                stocks[i] -= consumed[i];
            }

            for (int i = 0; i < goods.Length; i++)
            {
                MarketGood good = goods[i];
                double produced = 0d;

                if (good.IsProduced)
                {
                    StockThresholds thresholds = ThresholdsOf(i);
                    int target = workforce.NextWorkers(workers[i], good.MaxWorkers, stocks[i], thresholds);

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

                    if (WorkforceRule.ProducesAt(stocks[i], thresholds))
                    {
                        produced = workers[i] * good.DailyOutputPerWorker;
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

                if (Social != null)
                {
                    Social.RiseAndFall(Population);
                    LetThePoorLeave();
                }

                Population.SetBeggars(Labour.NextBeggars(Population.Beggars, Population.Inhabitants));
                BuildCityHouses();
            }

            Changed?.Invoke();
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

        // Patrician III's order: the workshop's own reserve, the beggars within the free room,
        // then the reserves of the other workshops in the order of the catalogue. What is still
        // missing is not hired today.
        private void Hire(int good, int wanted)
        {
            wanted -= TakeFromReserve(good, good, wanted);

            // Patrician III hires the beggars only while the poor are satisfied (0x510C30).
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

        // Patrician III's second hiring: the beggars fill the player's workshops in the order
        // they were built, each up to its jobs.
        private void FillVacancies()
        {
            int fillable = Labour.VacanciesFillable(Population.Beggars, Population.FreeRoomOf(SocialClass.Poor));

            foreach (PlayerWorkshop workshop in playerWorkshops)
            {
                if (fillable == 0)
                {
                    break;
                }

                int hired = Math.Min(fillable, workshop.Jobs - workshop.Workers);
                Population.Hire(hired, Labour.InhabitantsPerWorker);
                workshop.Workers += hired;
                fillable -= hired;
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

            foreach (MaterialCost material in rule.Materials)
            {
                // Not below nothing: the last unit can be a hair short.
                stocks[material.Good] = Math.Max(0d, stocks[material.Good] - material.Units);
            }

            Population.AddRoom(rule.HousedClass, rule.Room);
            CityHouses++;
        }

        // Each class's satisfaction takes a step towards the target the stocks of the goods it
        // consumes give, as Patrician III's 0x51CB80.
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

        // Patrician III's 0x51C6B0: the reserves first, in the catalogue's order; then the
        // workers above the protected ones of the city's workshops, spread by a step over all
        // that can be taken; then the player's workshops, in the order they were built.
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

        /// <summary>A workshop the player has built: its good, its jobs and the workers it has.</summary>
        private sealed class PlayerWorkshop
        {
            public PlayerWorkshop(int good, int jobs)
            {
                Good = good;
                Jobs = jobs;
            }

            public int Good { get; }

            public int Jobs { get; }

            public int Workers { get; set; }
        }
    }
}
