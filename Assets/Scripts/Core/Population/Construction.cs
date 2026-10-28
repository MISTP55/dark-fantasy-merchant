using System;
using System.Collections.Generic;
using UnityEngine;

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

        /// <summary>The cost of count buildings at once: the gold and every material's units times count.</summary>
        /// <exception cref="ArgumentOutOfRangeException">count under 1.</exception>
        /// <exception cref="OverflowException">A figure too large to be held.</exception>
        public BuildingCost Times(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var materials = new List<MaterialCost>(Materials.Count);

            foreach (MaterialCost material in Materials)
            {
                materials.Add(new MaterialCost(material.Good, checked(material.Units * count)));
            }

            return new BuildingCost(checked(Gold * count), materials);
        }
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
    /// Quotes and pays for a player's building (a house, a workshop or a trace of roads): the materials come first
    /// from a ship's hold, the rest is bought from the city's market, and the gold is taken from
    /// the treasury, all or nothing. What is bought does not pass through a hold. The purchase is
    /// priced with the market as it is before anything is taken, and exactly that is paid. A free
    /// building (the debug tools') takes nothing and asks nothing of the treasury, not even to be
    /// out of debt; a workshop still needs a city that produces its good, and a building its place.
    /// </summary>
    public static class Construction
    {
        /// <summary>Quotes a house; <paramref name="materials"/> may be null (no ship: everything is bought).</summary>
        /// <param name="isFree">For the debug tools: the quote is possible at 0 gold.</param>
        public static ConstructionQuote QuoteHouse(CityMarket market, IGoodSource materials, Treasury treasury, BuildingCost cost, bool isFree = false)
        {
            Require(market, treasury, cost);

            return Quote(market, materials, treasury, cost, true, isFree);
        }

        /// <summary>
        /// Quotes a workshop for a good; impossible when the city does not produce it.
        /// <paramref name="materials"/> may be null (no ship: everything is bought).
        /// </summary>
        /// <param name="isFree">For the debug tools: the quote is possible at 0 gold when the city produces the good.</param>
        public static ConstructionQuote QuoteWorkshop(
            CityMarket market, IGoodSource materials, Treasury treasury, BuildingCost cost, int good, bool isFree = false)
        {
            Require(market, treasury, cost);

            return Quote(market, materials, treasury, cost, market.MaxWorkersOf(good) > 0, isFree);
        }

        /// <param name="materialBought">Told, for the debug tools, of each material bought from the market: its good, units and gold.</param>
        /// <returns>False when the quote is impossible; nothing was taken then.</returns>
        /// <exception cref="ArgumentOutOfRangeException">A room under 1, before anything is taken.</exception>
        public static bool TryBuildHouse(
            CityMarket market,
            IGoodSource materials,
            Treasury treasury,
            BuildingCost cost,
            SocialClass housedClass,
            int room,
            Action<int, int, long> materialBought = null)
        {
            RequireAtLeastOne(room, nameof(room));
            RequireNoGrid(market);

            ConstructionQuote quote = QuoteHouse(market, materials, treasury, cost);

            if (!quote.IsPossible)
            {
                return false;
            }

            Pay(market, materials, treasury, cost, quote, materialBought, false);
            market.AddPlayerHouse(housedClass, room);
            return true;
        }

        /// <param name="materialBought">Told, for the debug tools, of each material bought from the market: its good, units and gold.</param>
        /// <returns>False when the quote is impossible; nothing was taken then.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Jobs under 1, before anything is taken.</exception>
        public static bool TryBuildWorkshop(
            CityMarket market,
            IGoodSource materials,
            Treasury treasury,
            BuildingCost cost,
            int good,
            int jobs,
            Action<int, int, long> materialBought = null)
        {
            RequireAtLeastOne(jobs, nameof(jobs));
            RequireNoGrid(market);

            ConstructionQuote quote = QuoteWorkshop(market, materials, treasury, cost, good);

            if (!quote.IsPossible)
            {
                return false;
            }

            Pay(market, materials, treasury, cost, quote, materialBought, false);
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
        /// <param name="isFree">For the debug tools: nothing is taken, nothing is bought.</param>
        /// <returns>False when the quote is impossible or the house cannot stand there; nothing was taken then.</returns>
        /// <exception cref="ArgumentOutOfRangeException">A room under 1, before anything is taken.</exception>
        public static bool TryBuildHouse(
            CityMarket market,
            IGoodSource materials,
            Treasury treasury,
            BuildingCost cost,
            SocialClass housedClass,
            int room,
            Footprint footprint,
            GridPlace place,
            Action<int, int, long> materialBought = null,
            bool isFree = false)
        {
            RequireAtLeastOne(room, nameof(room));
            RequireClass(housedClass);

            if (!CanStand(market, footprint, place))
            {
                return false;
            }

            ConstructionQuote quote = QuoteHouse(market, materials, treasury, cost, isFree);

            if (!quote.IsPossible)
            {
                return false;
            }

            Pay(market, materials, treasury, cost, quote, materialBought, isFree);
            market.AddPlayerHouse(housedClass, room, footprint, place);
            return true;
        }

        /// <summary>Builds a workshop at a place of the market's grid; the place is checked before anything is paid.</summary>
        /// <param name="materialBought">Told, for the debug tools, of each material bought from the market: its good, units and gold.</param>
        /// <param name="isFree">For the debug tools: nothing is taken, nothing is bought.</param>
        /// <returns>False when the quote is impossible or the workshop cannot stand there; nothing was taken then.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Jobs under 1, before anything is taken.</exception>
        public static bool TryBuildWorkshop(
            CityMarket market,
            IGoodSource materials,
            Treasury treasury,
            BuildingCost cost,
            int good,
            int jobs,
            Footprint footprint,
            GridPlace place,
            Action<int, int, long> materialBought = null,
            bool isFree = false)
        {
            RequireAtLeastOne(jobs, nameof(jobs));

            if (!CanStand(market, footprint, place))
            {
                return false;
            }

            ConstructionQuote quote = QuoteWorkshop(market, materials, treasury, cost, good, isFree);

            if (!quote.IsPossible)
            {
                return false;
            }

            Pay(market, materials, treasury, cost, quote, materialBought, isFree);
            market.AddPlayerWorkshop(good, jobs, footprint, place);
            return true;
        }

        /// <summary>
        /// The cells of a trace that are not roads yet, each counted once; -1 when one cannot hold a road (off the
        /// grid, forbidden, under a building that is not a road) or the market has no grid.
        /// </summary>
        public static int NewRoadCellsOf(CityMarket market, IReadOnlyList<Vector2Int> cells)
        {
            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            if (market.Grid == null)
            {
                return -1;
            }

            var newCells = new List<Vector2Int>();
            return CityMarket.TryCollectNewRoads(market.Grid, cells, newCells) ? newCells.Count : -1;
        }

        /// <summary>
        /// Quotes the roads of a trace: each new cell at <paramref name="costPerCell"/>, the cells already roads free;
        /// impossible when a cell cannot hold a road or none is new. <paramref name="materials"/> may be null.
        /// </summary>
        /// <param name="isFree">For the debug tools: the quote is possible at 0 gold when the trace can be laid.</param>
        public static ConstructionQuote QuoteRoads(
            CityMarket market, IGoodSource materials, Treasury treasury, BuildingCost costPerCell, IReadOnlyList<Vector2Int> cells, bool isFree = false)
        {
            Require(market, treasury, costPerCell);
            int count = NewRoadCellsOf(market, cells);

            return count < 1
                ? Quote(market, materials, treasury, costPerCell, false, isFree)
                : Quote(market, materials, treasury, costPerCell.Times(count), true, isFree);
        }

        /// <summary>
        /// Quotes one new cell of road, wherever it would be laid: what the player is offered before tracing.
        /// <paramref name="materials"/> may be null.
        /// </summary>
        /// <param name="isFree">For the debug tools: the quote is possible at 0 gold.</param>
        public static ConstructionQuote QuoteRoadCell(
            CityMarket market, IGoodSource materials, Treasury treasury, BuildingCost costPerCell, bool isFree = false)
        {
            Require(market, treasury, costPerCell);

            return Quote(market, materials, treasury, costPerCell, true, isFree);
        }

        /// <summary>Pays for and lays the new roads of a trace, all or nothing.</summary>
        /// <param name="materialBought">Told, for the debug tools, of each material bought from the market: its good, units and gold.</param>
        /// <param name="isFree">For the debug tools: nothing is taken, nothing is bought.</param>
        /// <returns>False when the quote is impossible; nothing was taken then.</returns>
        public static bool TryBuildRoads(
            CityMarket market,
            IGoodSource materials,
            Treasury treasury,
            BuildingCost costPerCell,
            IReadOnlyList<Vector2Int> cells,
            Action<int, int, long> materialBought = null,
            bool isFree = false)
        {
            ConstructionQuote quote = QuoteRoads(market, materials, treasury, costPerCell, cells, isFree);

            if (!quote.IsPossible)
            {
                return false;
            }

            Pay(market, materials, treasury, costPerCell.Times(NewRoadCellsOf(market, cells)), quote, materialBought, isFree);
            market.AddPlayerRoads(cells);
            return true;
        }

        /// <summary>
        /// Quotes the trading post of the city; impossible when the market has no grid, no reserved site, or the post
        /// is already built. <paramref name="materials"/> may be null.
        /// </summary>
        /// <param name="isFree">For the debug tools: the quote is possible at 0 gold when the post can be built.</param>
        public static ConstructionQuote QuoteTradingPost(
            CityMarket market, IGoodSource materials, Treasury treasury, BuildingCost cost, bool isFree = false)
        {
            Require(market, treasury, cost);

            return Quote(market, materials, treasury, cost, market.Grid != null && market.Grid.CanPlaceTradingPost, isFree);
        }

        /// <summary>Pays for and places the trading post on its reserved site, all or nothing.</summary>
        /// <param name="materialBought">Told, for the debug tools, of each material bought from the market: its good, units and gold.</param>
        /// <param name="isFree">For the debug tools: nothing is taken, nothing is bought.</param>
        /// <returns>False when the quote is impossible; nothing was taken then.</returns>
        public static bool TryBuildTradingPost(
            CityMarket market,
            IGoodSource materials,
            Treasury treasury,
            BuildingCost cost,
            Action<int, int, long> materialBought = null,
            bool isFree = false)
        {
            ConstructionQuote quote = QuoteTradingPost(market, materials, treasury, cost, isFree);

            if (!quote.IsPossible)
            {
                return false;
            }

            Pay(market, materials, treasury, cost, quote, materialBought, isFree);
            market.Grid.PlaceTradingPost();
            return true;
        }

        private static ConstructionQuote Quote(
            CityMarket market, IGoodSource materials, Treasury treasury, BuildingCost cost, bool buildable, bool isFree)
        {
            if (isFree)
            {
                return new ConstructionQuote(buildable, 0);
            }

            bool possible = buildable;
            long gold = cost.Gold;

            foreach (MaterialCost material in cost.Materials)
            {
                int toBuy = ToBuy(materials, material);
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
            IGoodSource materials,
            Treasury treasury,
            BuildingCost cost,
            ConstructionQuote quote,
            Action<int, int, long> materialBought,
            bool isFree)
        {
            if (isFree)
            {
                return;
            }

            foreach (MaterialCost material in cost.Materials)
            {
                int toBuy = ToBuy(materials, material);
                int fromHold = material.Units - toBuy;

                if (fromHold > 0)
                {
                    materials.TryRemove(material.Good, fromHold);
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

        private static int ToBuy(IGoodSource materials, MaterialCost material)
        {
            int fromHold = materials == null ? 0 : Math.Min(materials.UnitsOf(material.Good), material.Units);

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
