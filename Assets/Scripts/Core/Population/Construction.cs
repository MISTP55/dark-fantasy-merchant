using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Units of one good a building needs.</summary>
    public readonly struct MaterialCost
    {
        public MaterialCost(int good, int units)
        {
            if (good < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(good));
            }

            if (units < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(units));
            }

            Good = good;
            Units = units;
        }

        public int Good { get; }

        public int Units { get; }
    }

    /// <summary>What a building costs: gold and units of goods, each good at most once.</summary>
    public sealed class BuildingCost
    {
        public BuildingCost(long gold, IReadOnlyList<MaterialCost> materials)
        {
            if (gold < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(gold));
            }

            if (materials == null)
            {
                throw new ArgumentNullException(nameof(materials));
            }

            var seen = new HashSet<int>();
            var copy = new List<MaterialCost>(materials.Count);

            foreach (MaterialCost material in materials)
            {
                if (!seen.Add(material.Good))
                {
                    throw new ArgumentException("A good appears twice in the cost.", nameof(materials));
                }

                copy.Add(material);
            }

            Gold = gold;
            Materials = copy;
        }

        public long Gold { get; }

        public IReadOnlyList<MaterialCost> Materials { get; }
    }

    /// <summary>
    /// What building would cost now. <see cref="Gold"/> is the building's gold plus the price
    /// of the materials that would be bought in the city; the materials come first from the hold,
    /// and what is bought does not pass through a hold. When the building is impossible, Gold is
    /// the building's gold plus the price of whatever could be priced, and means nothing more.
    /// </summary>
    public readonly struct ConstructionQuote
    {
        public ConstructionQuote(bool isPossible, long gold)
        {
            IsPossible = isPossible;
            Gold = gold;
        }

        public bool IsPossible { get; }

        public long Gold { get; }
    }

    /// <summary>
    /// Quotes and pays for a player's building (a house or a workshop): the materials come first
    /// from a ship's hold, the rest is bought from the city's market, and the gold is taken from
    /// the treasury, all or nothing. What is bought does not pass through a hold. The purchase is
    /// priced with the market as it is before anything is taken, and exactly that is paid.
    /// </summary>
    public static class Construction
    {
        /// <summary>Quotes a house; <paramref name="hold"/> may be null (no ship: everything is bought).</summary>
        public static ConstructionQuote QuoteHouse(CityMarket market, CargoHold hold, Treasury treasury, BuildingCost cost)
        {
            Require(market, treasury, cost);

            return Quote(market, hold, treasury, cost, true);
        }

        /// <summary>
        /// Quotes a workshop for a good; impossible when the city does not produce it.
        /// <paramref name="hold"/> may be null (no ship: everything is bought).
        /// </summary>
        public static ConstructionQuote QuoteWorkshop(CityMarket market, CargoHold hold, Treasury treasury, BuildingCost cost, int good)
        {
            Require(market, treasury, cost);

            return Quote(market, hold, treasury, cost, market.MaxWorkersOf(good) > 0);
        }

        /// <returns>False when the quote is impossible; nothing was taken then.</returns>
        /// <exception cref="ArgumentOutOfRangeException">A room under 1, before anything is taken.</exception>
        public static bool TryBuildHouse(CityMarket market, CargoHold hold, Treasury treasury, BuildingCost cost, int room)
        {
            RequireAtLeastOne(room, nameof(room));

            ConstructionQuote quote = QuoteHouse(market, hold, treasury, cost);

            if (!quote.IsPossible)
            {
                return false;
            }

            Pay(market, hold, treasury, cost, quote);
            market.AddPlayerHouse(room);
            return true;
        }

        /// <returns>False when the quote is impossible; nothing was taken then.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Jobs under 1, before anything is taken.</exception>
        public static bool TryBuildWorkshop(CityMarket market, CargoHold hold, Treasury treasury, BuildingCost cost, int good, int jobs)
        {
            RequireAtLeastOne(jobs, nameof(jobs));

            ConstructionQuote quote = QuoteWorkshop(market, hold, treasury, cost, good);

            if (!quote.IsPossible)
            {
                return false;
            }

            Pay(market, hold, treasury, cost, quote);
            market.AddPlayerWorkshop(good, jobs);
            return true;
        }

        private static ConstructionQuote Quote(CityMarket market, CargoHold hold, Treasury treasury, BuildingCost cost, bool buildable)
        {
            bool possible = buildable;
            long gold = cost.Gold;

            foreach (MaterialCost material in cost.Materials)
            {
                int toBuy = ToBuy(hold, material);
                int available = market.AvailableOf(material.Good);

                if (toBuy > available)
                {
                    possible = false;
                }

                for (int i = 0; i < Math.Min(toBuy, available); i++)
                {
                    gold += market.BuyPriceOf(material.Good, i);
                }
            }

            // A treasury in debt builds nothing, even for free.
            possible = possible && !treasury.IsInDebt && treasury.CanAfford(gold);
            return new ConstructionQuote(possible, gold);
        }

        private static void Pay(CityMarket market, CargoHold hold, Treasury treasury, BuildingCost cost, ConstructionQuote quote)
        {
            foreach (MaterialCost material in cost.Materials)
            {
                int toBuy = ToBuy(hold, material);
                int fromHold = material.Units - toBuy;

                if (fromHold > 0)
                {
                    hold.TryRemove(material.Good, fromHold);
                }

                if (toBuy > 0)
                {
                    market.Take(material.Good, toBuy);
                }
            }

            treasury.TryWithdraw(quote.Gold);
        }

        private static int ToBuy(CargoHold hold, MaterialCost material)
        {
            int fromHold = hold == null ? 0 : Math.Min(hold.UnitsOf(material.Good), material.Units);

            return material.Units - fromHold;
        }

        private static void RequireAtLeastOne(int value, string name)
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private static void Require(CityMarket market, Treasury treasury, BuildingCost cost)
        {
            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            if (treasury == null)
            {
                throw new ArgumentNullException(nameof(treasury));
            }

            if (cost == null)
            {
                throw new ArgumentNullException(nameof(cost));
            }
        }
    }
}
