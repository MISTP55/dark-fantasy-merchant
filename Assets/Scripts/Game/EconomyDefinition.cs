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
        [SerializeField, Min(0f)] private double targetStockDays = 30d;

        [Header("Production, as a share of the city's own consumption")]
        [SerializeField, Min(0f)] private double efficientProductionRate = 1.5d;

        [Tooltip("Days of consumption above which an efficient production stops raising the stock.")]
        [SerializeField, Min(0f)] private double efficientCeilingDays = 60d;

        [SerializeField, Min(0f)] private double inefficientProductionRate = 1.1d;

        [Tooltip("Days of consumption above which an inefficient production stops raising the stock.")]
        [SerializeField, Min(0f)] private double inefficientCeilingDays = 36d;

        [Header("Prices, as a share of the base price")]
        [Tooltip("At an empty stock.")]
        [SerializeField, Min(1f)] private double emptyStockPriceMultiplier = 2.5d;

        [Tooltip("At twice the target stock and above.")]
        [SerializeField, Range(0.01f, 1f)] private double surplusPriceMultiplier = 0.5d;

        [Tooltip("What a city adds to the price when it sells to the player.")]
        [SerializeField, Range(0f, 0.99f)] private double buyMargin = 0.05d;

        [Tooltip("What a city takes off the price when it buys from the player.")]
        [SerializeField, Range(0f, 0.99f)] private double sellMargin = 0.05d;

        public IReadOnlyList<GoodDefinition> Goods => goods;

        public double TargetStockDays => targetStockDays;

        public double EfficientProductionRate => efficientProductionRate;

        public double EfficientCeilingDays => efficientCeilingDays;

        public double InefficientProductionRate => inefficientProductionRate;

        public double InefficientCeilingDays => inefficientCeilingDays;

        public double EmptyStockPriceMultiplier => emptyStockPriceMultiplier;

        public double SurplusPriceMultiplier => surplusPriceMultiplier;

        public double BuyMargin => buyMargin;

        public double SellMargin => sellMargin;

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
            return new MarketPricing(emptyStockPriceMultiplier, surplusPriceMultiplier, buyMargin, sellMargin);
        }

        /// <summary>
        /// The market of a city at the start of the game, with one stock per good of the
        /// catalogue. Call <see cref="TryValidate"/> first. Throws when a setting or a
        /// good's figure is unusable.
        /// </summary>
        public CityMarket CreateMarket(CityDefinition city, MarketPricing pricing)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            double thousands = Math.Max(0, city.Population) / 1000d;
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
                    good.BasePrice, thousands * good.ConsumptionPerThousand, rate, ceilingDays);
            }

            return new CityMarket(marketGoods, targetStockDays, pricing);
        }
    }
}
