using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The player's workshops in a city, held by its <see cref="CityMarket"/>: their jobs, their workers, hired from the
    /// beggars after the city's workshops and never laid off, and what they make by the market's
    /// <see cref="MerchantOutputRule"/> into the player's trading post of the city, from its inputs, never touching the
    /// market. Without the rule or the post they make nothing.
    /// </summary>
    internal sealed class PlayerWorkshops
    {
        // What floating sums lose: 10 days of a tenth make a whole unit.
        private const double Tolerance = 1e-9;

        private readonly IReadOnlyList<MarketGood> goods;
        private readonly int daysPerWeek;
        private readonly List<Workshop> workshops = new List<Workshop>();

        // By good, what the workshops of the good have made and not delivered yet, under a unit, and, by input of
        // its recipe, what they owe of it and have not taken yet, under a unit, backed by units still in the trading
        // post. Null until the good is first made.
        private readonly double[] pending;
        private readonly double[][] inputDebts;

        /// <param name="goods">The market's goods, by index.</param>
        /// <param name="output">Null for workshops that hire but make nothing.</param>
        public PlayerWorkshops(IReadOnlyList<MarketGood> goods, int daysPerWeek, MerchantOutputRule output)
        {
            this.goods = goods;
            this.daysPerWeek = daysPerWeek;
            Output = output;
            pending = new double[goods.Count];
            inputDebts = new double[goods.Count][];
        }

        /// <summary>What the workshops make; null when they make nothing.</summary>
        public MerchantOutputRule Output { get; }

        /// <summary>The player's trading post in the city; null until the player has one there.</summary>
        public IGoodStore TradingPost { get; private set; }

        /// <summary>Workers of all the workshops, every good together.</summary>
        public int Workers
        {
            get
            {
                int sum = 0;

                foreach (Workshop workshop in workshops)
                {
                    sum += workshop.Workers;
                }

                return sum;
            }
        }

        /// <exception cref="InvalidOperationException">The workshops already have one.</exception>
        public void SetTradingPost(IGoodStore post)
        {
            if (post == null)
            {
                throw new ArgumentNullException(nameof(post));
            }

            if (TradingPost != null)
            {
                throw new InvalidOperationException("The market already has the player's trading post.");
            }

            TradingPost = post;
        }

        /// <param name="building">Its building on the grid.</param>
        public void Add(int good, int jobs, PlacedBuilding building)
        {
            workshops.Add(new Workshop(good, jobs, building));
        }

        public int CountOf(int good)
        {
            return workshops.FindAll(workshop => workshop.Good == good).Count;
        }

        public int WorkersOf(int good)
        {
            int sum = 0;

            foreach (Workshop workshop in workshops)
            {
                if (workshop.Good == good)
                {
                    sum += workshop.Workers;
                }
            }

            return sum;
        }

        public int JobsOf(int good)
        {
            int sum = 0;

            foreach (Workshop workshop in workshops)
            {
                if (workshop.Good == good)
                {
                    sum += workshop.Jobs;
                }
            }

            return sum;
        }

        /// <summary>Units the workshops for a good make in a day with their workers of the moment, inputs not counted; 0 without <see cref="Output"/>.</summary>
        public double DailyOutputOf(int good)
        {
            return Output == null ? 0d : DailyOutputOf(good, Output);
        }

        /// <summary>Units the workshops for a good would make in a day by a rule of a merchant's workshops, inputs not counted.</summary>
        public double DailyOutputOf(int good, MerchantOutputRule rule)
        {
            return rule.DailyOutputOf(WorkersOf(good), goods[good].MerchantDailyOutputPerWorker, CountOf(good));
        }

        /// <summary>The wages of all the workers for a week, unrounded: a share of the base value of what they make in a week.</summary>
        /// <param name="wageShare">The share of that value the workers are paid, 0 or more.</param>
        public double WeeklyWagesOf(double wageShare)
        {
            // The negated comparison also rejects NaN.
            if (!(wageShare >= 0d) || double.IsInfinity(wageShare))
            {
                throw new ArgumentOutOfRangeException(nameof(wageShare));
            }

            double wages = 0d;

            for (int good = 0; good < goods.Count; good++)
            {
                wages += DailyOutputOf(good) * daysPerWeek * goods[good].BasePrice * wageShare;
            }

            return wages;
        }

        // The second hiring: the beggars fill the workshops in the order they were built, each up to its jobs, only
        // while it is connected.
        public void FillVacancies(CityPopulation population, LabourRule labour)
        {
            int fillable = labour.VacanciesFillable(population.Beggars, population.FreeRoomOf(SocialClass.Poor));

            foreach (Workshop workshop in workshops)
            {
                if (fillable == 0)
                {
                    break;
                }

                if (!workshop.Building.IsConnected)
                {
                    continue;
                }

                int hired = Math.Min(fillable, workshop.Jobs - workshop.Workers);
                population.Hire(hired, labour.InhabitantsPerWorker);
                workshop.Workers += hired;
                fillable -= hired;
            }
        }

        // The workshops make their goods into the trading post, from its inputs, each good in the order its first
        // workshop was built: a good made earlier is already there for the next. Only whole units move; the
        // fractions are carried to the next day.
        public void Run()
        {
            if (Output == null || TradingPost == null)
            {
                return;
            }

            var done = new HashSet<int>();

            foreach (Workshop workshop in workshops)
            {
                if (done.Add(workshop.Good))
                {
                    RunOf(workshop.Good);
                }
            }
        }

        /// <summary>
        /// Takes away workers, spread over the workshops in the order they were built: a worker for every step of
        /// their workers accumulated after what accumulated holds already. Lowers wanted by as many.
        /// </summary>
        /// <returns>The workers taken.</returns>
        public int TakeAway(int step, ref int wanted, ref int accumulated)
        {
            int removed = 0;

            foreach (Workshop workshop in workshops)
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

        private void RunOf(int good)
        {
            double potential = DailyOutputOf(good);

            if (potential <= 0d)
            {
                return;
            }

            IGoodStore post = TradingPost;
            IReadOnlyList<MarketInput> inputs = goods[good].Inputs;
            double[] debts = inputDebts[good] ??= new double[inputs.Count];
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

            pending[good] += made;
            int delivered = (int)Math.Floor(pending[good] + Tolerance);

            if (delivered > 0)
            {
                post.TryAdd(good, delivered);
                pending[good] = Math.Max(0d, pending[good] - delivered);
            }
        }

        /// <summary>A workshop the player has built: its good, its jobs, the workers it has and its building on the grid.</summary>
        private sealed class Workshop
        {
            public Workshop(int good, int jobs, PlacedBuilding building)
            {
                Good = good;
                Jobs = jobs;
                Building = building;
            }

            public PlacedBuilding Building { get; }

            public int Good { get; }

            public int Jobs { get; }

            public int Workers { get; set; }
        }
    }
}
