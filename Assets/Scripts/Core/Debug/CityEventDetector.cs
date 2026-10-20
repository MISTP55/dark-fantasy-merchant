using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Turns a city's day, its report and its figures against those of the day before, into events.</summary>
    public static class CityEventDetector
    {
        // A shortage a hair over nothing is a rounding of the consumption, not one.
        private const double ShortageMargin = 1e-6;

        /// <param name="previous">The city's row of the day before; null on the first day, which has no crossing.</param>
        /// <param name="current">The city's row of the day.</param>
        /// <param name="wasHalted">Whether each workshop stood still the day before; updated.</param>
        public static void Detect(
            int day,
            int city,
            CityMarket market,
            CityDayReport report,
            float[] previous,
            float[] current,
            bool[] wasHalted,
            List<SimulationEvent> into)
        {
            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (wasHalted == null)
            {
                throw new ArgumentNullException(nameof(wasHalted));
            }

            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            int goods = Math.Min(market.GoodCount, wasHalted.Length);

            for (int good = 0; good < goods; good++)
            {
                double missing = report.MissingOf(good);

                if (missing > ShortageMargin)
                {
                    into.Add(new SimulationEvent(day, city, good, SimulationEventType.Shortage, missing));
                }

                if (previous != null && TryBandOf(previous, good, out StockBand before) && TryBandOf(current, good, out StockBand after)
                    && before != after)
                {
                    into.Add(new SimulationEvent(day, city, good, SimulationEventType.ThresholdCrossing, 0d, (int)before, (int)after));
                }

                AddCount(into, day, city, good, SimulationEventType.Hired, report.HiredOf(good));
                AddCount(into, day, city, good, SimulationEventType.LaidOff, report.LaidOffOf(good));

                bool halted = report.IsHaltedOf(good);

                if (halted && !wasHalted[good])
                {
                    into.Add(new SimulationEvent(day, city, good, SimulationEventType.ProductionHalted, 0d));
                }
                else if (!halted && wasHalted[good] && market.MaxWorkersOf(good) > 0)
                {
                    into.Add(new SimulationEvent(day, city, good, SimulationEventType.ProductionResumed, 0d));
                }

                wasHalted[good] = halted;
            }

            if (report.PoorLeft > 0)
            {
                into.Add(new SimulationEvent(day, city, -1, SimulationEventType.PoorLeft, report.PoorLeft, 0, report.WorkersLeft));
            }

            foreach (SocialClass housedClass in new[] { SocialClass.Rich, SocialClass.Wealthy })
            {
                int change = report.ClassChangeOf(housedClass);

                if (change != 0)
                {
                    into.Add(new SimulationEvent(day, city, -1, SimulationEventType.ClassChange, change, 0, (int)housedClass));
                }
            }

            AddCount(into, day, city, -1, SimulationEventType.BeggarsArrived, report.BeggarChange);
            AddCount(into, day, city, -1, SimulationEventType.BeggarsLeft, -report.BeggarChange);

            foreach (SocialClass housedClass in new[] { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor })
            {
                int houses = report.HousesBuiltOf(housedClass);

                if (houses > 0)
                {
                    into.Add(new SimulationEvent(day, city, -1, SimulationEventType.CityHouseBuilt, houses, 0, (int)housedClass));
                }
            }
        }

        private static void AddCount(List<SimulationEvent> into, int day, int city, int good, SimulationEventType type, int count)
        {
            if (count > 0)
            {
                into.Add(new SimulationEvent(day, city, good, type, count));
            }
        }

        private static bool TryBandOf(float[] row, int good, out StockBand band)
        {
            float stock = row[CitySnapshot.IndexOf(GoodIndicator.Stock, good)];
            band = StockBands.Of(
                stock,
                row[CitySnapshot.IndexOf(GoodIndicator.T0, good)],
                row[CitySnapshot.IndexOf(GoodIndicator.T1, good)],
                row[CitySnapshot.IndexOf(GoodIndicator.T2, good)],
                row[CitySnapshot.IndexOf(GoodIndicator.T3, good)]);

            return !float.IsNaN(stock);
        }
    }
}
