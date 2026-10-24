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

        /// <param name="materialBought">Told, for the debug tools, of each material bought from the market: its good, units and gold.</param>
        /// <returns>False when the quote is impossible; nothing was taken then.</returns>
        /// <exception cref="ArgumentOutOfRangeException">A room under 1, before anything is taken.</exception>
        public static bool TryBuildHouse(
            CityMarket market,
            CargoHold hold,
            Treasury treasury,
            BuildingCost cost,
            SocialClass housedClass,
            int room,
            Action<int, int, long> materialBought = null)
        {
            RequireAtLeastOne(room, nameof(room));
            RequireNoGrid(market);

            ConstructionQuote quote = QuoteHouse(market, hold, treasury, cost);

            if (!quote.IsPossible)
            {
                return false;
            }

            Pay(market, hold, treasury, cost, quote, materialBought);
            market.AddPlayerHouse(housedClass, room);
            return true;
        }

        /// <param name="materialBought">Told, for the debug tools, of each material bought from the market: its good, units and gold.</param>
        /// <returns>False when the quote is impossible; nothing was taken then.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Jobs under 1, before anything is taken.</exception>
        public static bool TryBuildWorkshop(
            CityMarket market,
            CargoHold hold,
            Treasury treasury,
            BuildingCost cost,
            int good,
            int jobs,
            Action<int, int, long> materialBought = null)
        {
            RequireAtLeastOne(jobs, nameof(jobs));
            RequireNoGrid(market);

            ConstructionQuote quote = QuoteWorkshop(market, hold, treasury, cost, good);

            if (!quote.IsPossible)
            {
                return false;
            }

            Pay(market, hold, treasury, cost, quote, materialBought);
            market.AddPlayerWorkshop(good, jobs);
            return true;
        }

        /// <summary>Whether the market has a grid where the footprint fits at the place.</summary>
        public static bool CanStand(CityMarket market, Footprint footprint, GridPlace place)
        {
            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            return market.Grid != null && market.Grid.CanPlace(footprint, place);
        }

        /// <summary>Builds a house at a place of the market's grid; the place is checked before anything is paid.</summary>
        /// <param name="materialBought">Told, for the debug tools, of each material bought from the market: its good, units and gold.</param>
        /// <returns>False when the quote is impossible or the house cannot stand there; nothing was taken then.</returns>
        /// <exception cref="ArgumentOutOfRangeException">A room under 1, before anything is taken.</exception>
        public static bool TryBuildHouse(
            CityMarket market,
            CargoHold hold,
            Treasury treasury,
            BuildingCost cost,
            SocialClass housedClass,
            int room,
            Footprint footprint,
            GridPlace place,
            Action<int, int, long> materialBought = null)
        {
            RequireAtLeastOne(room, nameof(room));
            RequireClass(housedClass);

            if (!CanStand(market, footprint, place))
            {
                return false;
            }

            ConstructionQuote quote = QuoteHouse(market, hold, treasury, cost);

            if (!quote.IsPossible)
            {
                return false;
            }

            Pay(market, hold, treasury, cost, quote, materialBought);
            market.AddPlayerHouse(housedClass, room, footprint, place);
            return true;
        }

        /// <summary>Builds a workshop at a place of the market's grid; the place is checked before anything is paid.</summary>
        /// <param name="materialBought">Told, for the debug tools, of each material bought from the market: its good, units and gold.</param>
        /// <returns>False when the quote is impossible or the workshop cannot stand there; nothing was taken then.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Jobs under 1, before anything is taken.</exception>
        public static bool TryBuildWorkshop(
            CityMarket market,
            CargoHold hold,
            Treasury treasury,
            BuildingCost cost,
            int good,
            int jobs,
            Footprint footprint,
            GridPlace place,
            Action<int, int, long> materialBought = null)
        {
            RequireAtLeastOne(jobs, nameof(jobs));

            if (!CanStand(market, footprint, place))
            {
                return false;
            }

            ConstructionQuote quote = QuoteWorkshop(market, hold, treasury, cost, good);

            if (!quote.IsPossible)
            {
                return false;
            }

            Pay(market, hold, treasury, cost, quote, materialBought);
            market.AddPlayerWorkshop(good, jobs, footprint, place);
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

        private static void Pay(
            CityMarket market,
            CargoHold hold,
            Treasury treasury,
            BuildingCost cost,
            ConstructionQuote quote,
            Action<int, int, long> materialBought)
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
                    // At the prices of the quote, read before the stock goes down.
                    long gold = 0;

                    for (int i = 0; materialBought != null && i < toBuy; i++)
                    {
                        gold += market.BuyPriceOf(material.Good, i);
                    }

                    market.Take(material.Good, toBuy);
                    materialBought?.Invoke(material.Good, toBuy, gold);
                }
            }

            treasury.TryWithdraw(quote.Gold);
        }

        private static int ToBuy(CargoHold hold, MaterialCost material)
        {
            int fromHold = hold == null ? 0 : Math.Min(hold.UnitsOf(material.Good), material.Units);

            return material.Units - fromHold;
        }

        // A placeless building on a market with a grid has no place: refused before anything is taken.
        private static void RequireNoGrid(CityMarket market)
        {
            if (market != null && market.Grid != null)
            {
                throw new InvalidOperationException("A market with a grid builds at a place.");
            }
        }

        private static void RequireClass(SocialClass housedClass)
        {
            if (housedClass < SocialClass.Rich || housedClass > SocialClass.Poor)
            {
                throw new ArgumentOutOfRangeException(nameof(housedClass));
            }
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
