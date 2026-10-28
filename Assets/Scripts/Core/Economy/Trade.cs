using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>What the player did with a market, for the debug tools.</summary>
    public enum PlayerTradeKind
    {
        Buy,
        Sell,

        /// <summary>Materials of a building bought from the market.</summary>
        Construction,
    }

    /// <summary>
    /// Buying and selling between a city's market, a ship's hold and, for the player,
    /// a treasury, in units of the good, barrels or bushels. Units are priced one by one,
    /// each at the stock it leaves or joins, and a trade stops at the first unit that
    /// cannot be traded; a unit is never split. The units of a trade are moved at once, so
    /// the market and the hold raise Changed once per trade.
    /// </summary>
    public static class Trade
    {
        /// <summary>
        /// Buys up to <paramref name="units"/> units: as many as the city has, the
        /// hold has room for and the gold covers. A treasury in debt buys nothing.
        /// </summary>
        public static TradeResult Buy(CityMarket market, IGoodStore hold, Treasury treasury, int good, int units)
        {
            Require(market, hold, treasury, units);

            int limit = Math.Min(units, Math.Min(market.AvailableOf(good), hold.RoomFor(good)));
            int bought = 0;
            long gold = 0;

            while (bought < limit)
            {
                long price = market.BuyPriceOf(good, bought);

                if (!treasury.CanAfford(gold + price))
                {
                    break;
                }

                gold += price;
                bought++;
            }

            if (bought == 0)
            {
                return default;
            }

            treasury.TryWithdraw(gold);
            market.Take(good, bought);
            hold.TryAdd(good, bought);
            return new TradeResult(bought, gold);
        }

        /// <summary>
        /// Buys up to <paramref name="units"/> units for a merchant whose gold is not
        /// counted: as many as the city has and the hold has room for.
        /// </summary>
        /// <returns>The units bought and what they cost.</returns>
        public static TradeResult Buy(CityMarket market, IGoodStore hold, int good, int units)
        {
            Require(market, hold, units);

            int bought = Math.Min(units, Math.Min(market.AvailableOf(good), hold.RoomFor(good)));

            if (bought == 0)
            {
                return default;
            }

            long gold = 0;

            for (int i = 0; i < bought; i++)
            {
                gold += market.BuyPriceOf(good, i);
            }

            market.Take(good, bought);
            hold.TryAdd(good, bought);
            return new TradeResult(bought, gold);
        }

        /// <summary>Sells up to <paramref name="units"/> units: as many as are aboard.</summary>
        public static TradeResult Sell(CityMarket market, IGoodStore hold, Treasury treasury, int good, int units)
        {
            Require(market, hold, treasury, units);

            TradeResult result = Sell(market, hold, good, units);

            if (result.Units > 0)
            {
                treasury.Deposit(result.Gold);
            }

            return result;
        }

        /// <summary>
        /// Sells up to <paramref name="units"/> units for a merchant whose gold is not
        /// counted: as many as are aboard.
        /// </summary>
        /// <returns>The units sold and what they were paid.</returns>
        public static TradeResult Sell(CityMarket market, IGoodStore hold, int good, int units)
        {
            Require(market, hold, units);

            int sold = Math.Min(units, hold.UnitsOf(good));

            if (sold == 0)
            {
                return default;
            }

            long gold = 0;

            for (int i = 0; i < sold; i++)
            {
                gold += market.SellPriceOf(good, i);
            }

            hold.TryRemove(good, sold);
            market.Put(good, sold);
            return new TradeResult(sold, gold);
        }

        private static void Require(CityMarket market, IGoodStore hold, Treasury treasury, int units)
        {
            Require(market, hold, units);

            if (treasury == null)
            {
                throw new ArgumentNullException(nameof(treasury));
            }
        }

        private static void Require(CityMarket market, IGoodStore hold, int units)
        {
            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            if (hold == null)
            {
                throw new ArgumentNullException(nameof(hold));
            }

            if (units < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(units));
            }
        }
    }
}
