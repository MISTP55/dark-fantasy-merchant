using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The figures of a city at the end of a day, written in a row of floats: every
    /// <see cref="CityIndicator"/> first, then every good's block of <see cref="GoodIndicator"/>s.
    /// A good beyond the city's goods is NaN.
    /// </summary>
    public static class CitySnapshot
    {
        private static readonly int CityIndicatorCount = Enum.GetValues(typeof(CityIndicator)).Length;
        private static readonly int GoodIndicatorCount = Enum.GetValues(typeof(GoodIndicator)).Length;

        public static int SizeFor(int goodCount)
        {
            return CityIndicatorCount + goodCount * GoodIndicatorCount;
        }

        public static int IndexOf(CityIndicator indicator)
        {
            return (int)indicator;
        }

        public static int IndexOf(GoodIndicator indicator, int good)
        {
            return CityIndicatorCount + good * GoodIndicatorCount + (int)indicator;
        }

        /// <param name="underWay">Units of a good the AI merchants carry to the city.</param>
        /// <param name="into">A row of <see cref="SizeFor"/> the goods of the record.</param>
        public static void Capture(City city, Func<int, int> underWay, float[] into)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (underWay == null)
            {
                throw new ArgumentNullException(nameof(underWay));
            }

            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            CityPopulation population = city.Population;
            Set(into, CityIndicator.Inhabitants, population.Inhabitants);
            Set(into, CityIndicator.Housed, population.Housed);
            Set(into, CityIndicator.HousedRoom, population.Room);
            Set(into, CityIndicator.Beggars, population.Beggars);
            Set(into, CityIndicator.BeggarTarget, city.BeggarTarget);
            Set(into, CityIndicator.CityHouses, city.CityHouses);

            int goodCount = (into.Length - CityIndicatorCount) / GoodIndicatorCount;

            for (int good = 0; good < goodCount; good++)
            {
                if (good >= city.Market.GoodCount)
                {
                    for (int indicator = 0; indicator < GoodIndicatorCount; indicator++)
                    {
                        into[IndexOf((GoodIndicator)indicator, good)] = float.NaN;
                    }

                    continue;
                }

                StockThresholds thresholds = city.Market.ThresholdsOf(good);
                Set(into, GoodIndicator.Stock, good, city.Market.AmountOf(good));
                Set(into, GoodIndicator.T0, good, thresholds.T0);
                Set(into, GoodIndicator.T1, good, thresholds.T1);
                Set(into, GoodIndicator.T2, good, thresholds.T2);
                Set(into, GoodIndicator.T3, good, thresholds.T3);
                Set(into, GoodIndicator.BuyPrice, good, city.Market.BuyPriceOf(good));
                Set(into, GoodIndicator.SellPrice, good, city.Market.SellPriceOf(good));
                Set(into, GoodIndicator.NominalWeeklyProduction, good, city.NominalWeeklyProductionOf(good));
                Set(into, GoodIndicator.RecentProduction, good, city.RecentProductionOf(good));
                Set(into, GoodIndicator.NominalWeeklyConsumption, good, city.NominalWeeklyConsumptionOf(good));
                Set(into, GoodIndicator.RecentConsumption, good, city.RecentConsumptionOf(good));
                Set(into, GoodIndicator.Missing, good, city.LastDay.MissingOf(good));
                Set(into, GoodIndicator.Workers, good, city.WorkersOf(good));
                Set(into, GoodIndicator.Reserve, good, city.ReserveOf(good));
                Set(into, GoodIndicator.UnderWay, good, underWay(good));
                Set(into, GoodIndicator.NominalWeeklyInputs, good, city.NominalWeeklyInputNeedOf(good));
                Set(into, GoodIndicator.RecentInputs, good, city.RecentInputsOf(good));
            }
        }

        private static void Set(float[] row, CityIndicator indicator, double value)
        {
            row[IndexOf(indicator)] = (float)value;
        }

        private static void Set(float[] row, GoodIndicator indicator, int good, double value)
        {
            row[IndexOf(indicator, good)] = (float)value;
        }
    }
}
