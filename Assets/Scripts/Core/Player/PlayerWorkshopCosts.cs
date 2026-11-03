using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>What the player's workshops of every city cost a week: their running cost and their workers' wages.</summary>
    public static class PlayerWorkshopCosts
    {
        /// <summary>The running cost of every workshop of the player, whether it has workers or not.</summary>
        /// <param name="runningCostOfGood">The weekly running cost of a workshop of a good, by the good's index.</param>
        public static long WeeklyRunningCostOf(IEnumerable<City> cities, Func<int, long> runningCostOfGood)
        {
            if (cities == null)
            {
                throw new ArgumentNullException(nameof(cities));
            }

            if (runningCostOfGood == null)
            {
                throw new ArgumentNullException(nameof(runningCostOfGood));
            }

            long cost = 0;

            foreach (City city in cities)
            {
                for (int good = 0; good < city.Market.GoodCount; good++)
                {
                    int workshops = city.PlayerWorkshopsOf(good);

                    if (workshops > 0)
                    {
                        cost += workshops * runningCostOfGood(good);
                    }
                }
            }

            return cost;
        }

        /// <summary>
        /// The wages of the workers of every workshop of the player (<see cref="City.PlayerWeeklyWagesOf"/>),
        /// summed over the cities and only then rounded to the nearest gold.
        /// </summary>
        /// <param name="wageShare">The share of the base value of what the workers make in a week that they are paid.</param>
        public static long WeeklyWagesOf(IEnumerable<City> cities, double wageShare)
        {
            if (cities == null)
            {
                throw new ArgumentNullException(nameof(cities));
            }

            double wages = 0d;

            foreach (City city in cities)
            {
                wages += city.PlayerWeeklyWagesOf(wageShare);
            }

            return (long)Math.Round(wages, MidpointRounding.AwayFromZero);
        }
    }
}
