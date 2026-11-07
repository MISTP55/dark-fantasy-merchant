using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The production of a city's own workshops, held by its <see cref="City"/>, over its <see cref="CityWorkforce"/>:
    /// every day, in the catalogue's order, a workshop hires or lays off by where its stock stands against the
    /// thresholds, takes its inputs from the stocks as they stand, in proportion to the scarcest, and produces as
    /// much, unless that stock is above T3, where the inputs are taken all the same and lost.
    /// </summary>
    internal sealed class CityProduction
    {
        private readonly MarketGood[] goods;
        private readonly WorkforceRule rule;
        private readonly CityWorkforce workforce;

        // By good, the workshops that take it as an input and how much for every unit they make,
        // each list in the catalogue's order of its workshops: the recipes never change once the
        // city exists.
        private readonly (int consumer, double unitsPerUnit)[][] consumersOf;

        /// <param name="goods">The city's goods, whose inputs are checked here; kept, not copied.</param>
        public CityProduction(MarketGood[] goods, WorkforceRule rule, CityWorkforce workforce)
        {
            for (int i = 0; i < goods.Length; i++)
            {
                foreach (MarketInput input in goods[i].Inputs)
                {
                    if (input.Good >= goods.Length)
                    {
                        throw new ArgumentOutOfRangeException(nameof(goods), $"The good {i} takes the good {input.Good}, which the city does not have.");
                    }

                    if (input.Good == i)
                    {
                        throw new ArgumentException($"The good {i} is its own input.", nameof(goods));
                    }
                }
            }

            this.goods = goods;
            this.rule = rule;
            this.workforce = workforce;
            consumersOf = ConsumersOf(goods);
        }

        /// <summary>Units the workshop for a good makes in a day with the workers of the moment, whatever the stock.</summary>
        public double DailyProductionOf(int good)
        {
            return workforce.WorkersOf(good) * goods[good].DailyOutputPerWorker;
        }

        /// <summary>Units of a good the workshops take in a day as their input, with their workers of the moment and no shortage.</summary>
        public double DailyInputNeedOf(int good)
        {
            double need = 0d;

            foreach ((int consumer, double unitsPerUnit) in consumersOf[good])
            {
                need += DailyProductionOf(consumer) * unitsPerUnit;
            }

            return need;
        }

        /// <summary>
        /// One day of the workshops, in the catalogue's order, on the stocks the inhabitants left: what a workshop
        /// makes is there for the inputs of the workshops after it.
        /// </summary>
        /// <param name="produced">Filled, by good, with what entered the stock: 0 for a good the city does not produce.</param>
        public void Run(CityMarket market, MarketWeekLog weekLog, CityDayReport report, double[] produced)
        {
            for (int i = 0; i < goods.Length; i++)
            {
                MarketGood good = goods[i];
                produced[i] = 0d;

                if (!good.IsProduced)
                {
                    continue;
                }

                StockThresholds thresholds = market.ThresholdsOf(i);
                workforce.MoveTo(i, rule.NextWorkers(workforce.WorkersOf(i), good.MaxWorkers, market.AmountOf(i), thresholds));

                double potential = workforce.WorkersOf(i) * good.DailyOutputPerWorker;
                double made = potential * TakeInputs(market, weekLog, report, good, potential);

                // Above T3 the inputs are taken all the same, and lost.
                if (WorkforceRule.ProducesAt(market.AmountOf(i), thresholds))
                {
                    produced[i] = made;
                    market.Deposit(i, made);
                }
            }
        }

        // Takes what a workshop needs to make these units, in proportion to its scarcest input, from
        // the stocks as they stand, and returns that proportion: 1 with all its inputs or none.
        private static double TakeInputs(CityMarket market, MarketWeekLog weekLog, CityDayReport report, MarketGood good, double units)
        {
            IReadOnlyList<MarketInput> inputs = good.Inputs;
            double factor = 1d;
            int scarcest = -1;

            for (int k = 0; k < inputs.Count; k++)
            {
                double needed = units * inputs[k].UnitsPerUnit;

                if (needed > 0d && market.AmountOf(inputs[k].Good) / needed < factor)
                {
                    factor = market.AmountOf(inputs[k].Good) / needed;
                    scarcest = k;
                }
            }

            for (int k = 0; k < inputs.Count; k++)
            {
                int input = inputs[k].Good;
                double needed = units * inputs[k].UnitsPerUnit;

                // The scarcest is taken whole, so that no rounding leaves a crumb of it.
                double taken = market.Withdraw(input, k == scarcest ? market.AmountOf(input) : needed * factor);
                weekLog.AddInputs(input, taken);
                report.AddInputs(input, needed, taken);
            }

            return factor;
        }

        private static (int consumer, double unitsPerUnit)[][] ConsumersOf(MarketGood[] goods)
        {
            var consumers = new List<(int consumer, double unitsPerUnit)>[goods.Length];

            for (int i = 0; i < goods.Length; i++)
            {
                consumers[i] = new List<(int consumer, double unitsPerUnit)>();
            }

            for (int i = 0; i < goods.Length; i++)
            {
                foreach (MarketInput input in goods[i].Inputs)
                {
                    consumers[input.Good].Add((i, input.UnitsPerUnit));
                }
            }

            return Array.ConvertAll(consumers, list => list.ToArray());
        }
    }
}
