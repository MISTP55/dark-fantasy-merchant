using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Static definition of the economy: the catalogue of goods, in the order they are
    /// listed everywhere, and the settings of production and prices. Holds no runtime
    /// state: the stocks are in a <see cref="CityMarket"/> per city.
    /// </summary>
    [CreateAssetMenu(fileName = "Economy", menuName = "Dark Fantasy Merchant/Economy")]
    public sealed class EconomyDefinition : ScriptableObject
    {
        [Tooltip("The goods of the game. Their order is their order on screen.")]
        [SerializeField] private List<GoodDefinition> goods = new List<GoodDefinition>();

        [Tooltip("Days of its consumption a city holds when a good is at its base price.")]
        [SerializeField, Min(0f)] private double targetStockDays = 10d;

        [Tooltip("Days of its consumption a city holds of every good when the game starts, whether it produces it or not.")]
        [SerializeField, Min(0f)] private double startingStockDays = 7d;

        [Header("Production, as a share of the city's own consumption")]
        [SerializeField, Min(0f)] private double efficientProductionRate = 2d;

        [Tooltip("Days of consumption above which an efficient production stops raising the stock.")]
        [SerializeField, Min(0f)] private double efficientCeilingDays = 60d;

        [SerializeField, Min(0f)] private double inefficientProductionRate = 1.5d;

        [Tooltip("Days of consumption above which an inefficient production stops raising the stock.")]
        [SerializeField, Min(0f)] private double inefficientCeilingDays = 36d;

        [Header("Margins, as a share of the price (its bounds are each good's own)")]
        [Tooltip("What a city adds to the price when it sells to the player.")]
        [SerializeField, Range(0f, 0.99f)] private double buyMargin = 0.05d;

        [Tooltip("What a city takes off the price when it buys from the player.")]
        [SerializeField, Range(0f, 0.99f)] private double sellMargin = 0.05d;

        [Header("Population, by the days of its consumption a city holds of food (the average of its foods)")]
        [Tooltip("Days of food at which the population does not change. Under them it declines.")]
        [SerializeField, Min(0f)] private double stableFoodDays = 7d;

        [Tooltip("Days of food at which the population grows the fastest. A food is not counted for more.")]
        [SerializeField, Min(0f)] private double abundantFoodDays = 14d;

        [Tooltip("Growth of the population over a month at the abundant days, as a share: 0.05 is +5 %.")]
        [SerializeField, Min(0f)] private double maxMonthlyGrowth = 0.05d;

        [Tooltip("Decline of the population over a month with no food at all, as a share: 0.1 is -10 %.")]
        [SerializeField, Range(0f, 1f)] private double maxMonthlyDecline = 0.1d;

        public IReadOnlyList<GoodDefinition> Goods => goods;

        public double TargetStockDays => targetStockDays;

        public double StartingStockDays => startingStockDays;

        public double EfficientProductionRate => efficientProductionRate;

        public double EfficientCeilingDays => efficientCeilingDays;

        public double InefficientProductionRate => inefficientProductionRate;

        public double InefficientCeilingDays => inefficientCeilingDays;

        public double BuyMargin => buyMargin;

        public double SellMargin => sellMargin;

        public double StableFoodDays => stableFoodDays;

        public double AbundantFoodDays => abundantFoodDays;

        public double MaxMonthlyGrowth => maxMonthlyGrowth;

        public double MaxMonthlyDecline => maxMonthlyDecline;

        /// <returns>The index of a good in the catalogue, which is what Core knows it by, or -1.</returns>
        public int IndexOf(GoodDefinition good)
        {
            return good != null ? goods.IndexOf(good) : -1;
        }

        /// <summary>Whether the catalogue can be used: at least one good, none missing, none twice.</summary>
        /// <param name="problem">What is wrong, for the console; null when nothing is.</param>
        public bool TryValidate(out string problem)
        {
            problem = null;

            if (goods.Count == 0)
            {
                problem = "it has no good";
                return false;
            }

            for (int i = 0; i < goods.Count; i++)
            {
                if (goods[i] == null)
                {
                    problem = $"its good {i} is empty";
                    return false;
                }

                if (goods.IndexOf(goods[i]) != i)
                {
                    problem = $"'{goods[i].name}' is listed twice";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The pricing of these settings. Throws when one of them is unusable: the
        /// attributes only constrain the Inspector, not the file.
        /// </summary>
        public MarketPricing CreatePricing()
        {
            return new MarketPricing(buyMargin, sellMargin);
        }

        /// <summary>
        /// How food changes the population of a city, from these settings. Throws when one
        /// of them is unusable.
        /// </summary>
        /// <param name="daysPerMonth">Days of the calendar's month: the rates are given per month.</param>
        public FoodGrowth CreateFoodGrowth(int daysPerMonth)
        {
            return new FoodGrowth(stableFoodDays, abundantFoodDays, maxMonthlyGrowth, maxMonthlyDecline, daysPerMonth);
        }

        /// <summary>
        /// The market of a city at the start of the game, with the population of its
        /// definition and one stock per good of the catalogue. Call
        /// <see cref="TryValidate"/> first. Throws when a setting or a good's figure is unusable.
        /// </summary>
        /// <param name="daysPerWeek">Days of the calendar's week: goods give their consumption per week.</param>
        /// <param name="foodGrowth">How food changes the population. Without it, the population stays what it is.</param>
        public CityMarket CreateMarket(
            CityDefinition city,
            MarketPricing pricing,
            int daysPerWeek,
            FoodGrowth foodGrowth = null)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (daysPerWeek < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysPerWeek));
            }

            var marketGoods = new MarketGood[goods.Count];

            for (int i = 0; i < goods.Count; i++)
            {
                GoodDefinition good = goods[i];
                double rate = 0d;
                double ceilingDays = 0d;

                switch (city.ProductionOf(good))
                {
                    case GoodProduction.Efficient:
                        rate = efficientProductionRate;
                        ceilingDays = efficientCeilingDays;
                        break;
                    case GoodProduction.Inefficient:
                        rate = inefficientProductionRate;
                        ceilingDays = inefficientCeilingDays;
                        break;
                }

                marketGoods[i] = new MarketGood(
                    good.MinPrice,
                    good.BasePrice,
                    good.MaxPrice,
                    good.WeeklyConsumptionPerThousand / daysPerWeek,
                    rate,
                    ceilingDays,
                    good.Category == GoodCategory.Food);
            }

            return new CityMarket(
                marketGoods,
                targetStockDays,
                startingStockDays,
                pricing,
                daysPerWeek,
                new CityPopulation(Math.Max(0, city.Population)),
                foodGrowth);
        }
    }
}
