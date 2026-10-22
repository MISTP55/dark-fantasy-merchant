using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The figures of a city at the end of a day, written in a row of floats: every
    /// <see cref="CityIndicator"/> first, then every good's block of <see cref="GoodIndicator"/>s.
    /// A good beyond the market's goods is NaN.
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
        public static void Capture(CityMarket market, Func<int, int> underWay, float[] into)
        {
            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            if (underWay == null)
            {
                throw new ArgumentNullException(nameof(underWay));
            }

            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            CityPopulation population = market.Population;
            Set(into, CityIndicator.Inhabitants, population.Inhabitants);
            Set(into, CityIndicator.Rich, population.CountOf(SocialClass.Rich));
            Set(into, CityIndicator.Wealthy, population.CountOf(SocialClass.Wealthy));
            Set(into, CityIndicator.Poor, population.CountOf(SocialClass.Poor));
            Set(into, CityIndicator.RichRoom, population.RoomOf(SocialClass.Rich));
            Set(into, CityIndicator.WealthyRoom, population.RoomOf(SocialClass.Wealthy));
            Set(into, CityIndicator.PoorRoom, population.RoomOf(SocialClass.Poor));
            Set(into, CityIndicator.RichSatisfaction, population.SatisfactionOf(SocialClass.Rich));
            Set(into, CityIndicator.WealthySatisfaction, population.SatisfactionOf(SocialClass.Wealthy));
            Set(into, CityIndicator.PoorSatisfaction, population.SatisfactionOf(SocialClass.Poor));
            Set(into, CityIndicator.Beggars, population.Beggars);
            Set(into, CityIndicator.BeggarTarget, market.BeggarTarget);
            Set(into, CityIndicator.CityHouses, market.CityHouses);

            int goodCount = (into.Length - CityIndicatorCount) / GoodIndicatorCount;

            for (int good = 0; good < goodCount; good++)
            {
                if (good >= market.GoodCount)
                {
                    for (int indicator = 0; indicator < GoodIndicatorCount; indicator++)
                    {
                        into[IndexOf((GoodIndicator)indicator, good)] = float.NaN;
                    }

                    continue;
                }

                StockThresholds thresholds = market.ThresholdsOf(good);
                Set(into, GoodIndicator.Stock, good, market.StockOf(good));
                Set(into, GoodIndicator.T0, good, thresholds.T0);
                Set(into, GoodIndicator.T1, good, thresholds.T1);
                Set(into, GoodIndicator.T2, good, thresholds.T2);
                Set(into, GoodIndicator.T3, good, thresholds.T3);
                Set(into, GoodIndicator.BuyPrice, good, market.BuyPriceOf(good));
                Set(into, GoodIndicator.SellPrice, good, market.SellPriceOf(good));
                Set(into, GoodIndicator.NominalWeeklyProduction, good, market.NominalWeeklyProductionOf(good));
                Set(into, GoodIndicator.RecentProduction, good, market.RecentProductionOf(good));
                Set(into, GoodIndicator.NominalWeeklyConsumption, good, market.NominalWeeklyConsumptionOf(good));
                Set(into, GoodIndicator.RecentConsumption, good, market.RecentConsumptionOf(good));
                Set(into, GoodIndicator.Missing, good, market.LastDay.MissingOf(good));
                Set(into, GoodIndicator.Workers, good, market.WorkersOf(good));
                Set(into, GoodIndicator.Reserve, good, market.ReserveOf(good));
                Set(into, GoodIndicator.UnderWay, good, underWay(good));
                Set(into, GoodIndicator.NominalWeeklyInputs, good, market.NominalWeeklyInputNeedOf(good));
                Set(into, GoodIndicator.RecentInputs, good, market.RecentInputsOf(good));
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
