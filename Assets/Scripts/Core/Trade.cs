using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Buying and selling between a city's market, a ship's hold and, for the player,
    /// a treasury. Barrels are priced one by one, each at the stock it leaves or joins,
    /// and a trade stops at the first barrel that cannot be traded.
    /// </summary>
    public static class Trade
    {
        /// <summary>
        /// Buys up to <paramref name="barrels"/> barrels: as many as the city has, the
        /// hold has room for and the gold covers. A treasury in debt buys nothing.
        /// </summary>
        public static TradeResult Buy(CityMarket market, CargoHold hold, Treasury treasury, int good, int barrels)
        {
            Require(market, hold, treasury, barrels);

            int limit = Math.Min(barrels, Math.Min(market.AvailableOf(good), hold.Free));
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
        /// Buys up to <paramref name="barrels"/> barrels for a merchant whose gold is not
        /// counted: as many as the city has and the hold has room for.
        /// </summary>
        /// <returns>The barrels bought and what they cost.</returns>
        public static TradeResult Buy(CityMarket market, CargoHold hold, int good, int barrels)
        {
            Require(market, hold, barrels);

            int bought = Math.Min(barrels, Math.Min(market.AvailableOf(good), hold.Free));

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

        /// <summary>Sells up to <paramref name="barrels"/> barrels: as many as are aboard.</summary>
        public static TradeResult Sell(CityMarket market, CargoHold hold, Treasury treasury, int good, int barrels)
        {
            Require(market, hold, treasury, barrels);

            TradeResult result = Sell(market, hold, good, barrels);

            if (result.Barrels > 0)
            {
                treasury.Deposit(result.Gold);
            }

            return result;
        }

        /// <summary>
        /// Sells up to <paramref name="barrels"/> barrels for a merchant whose gold is not
        /// counted: as many as are aboard.
        /// </summary>
        /// <returns>The barrels sold and what they were paid.</returns>
        public static TradeResult Sell(CityMarket market, CargoHold hold, int good, int barrels)
        {
            Require(market, hold, barrels);

            int sold = Math.Min(barrels, hold.UnitsOf(good));

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

        private static void Require(CityMarket market, CargoHold hold, Treasury treasury, int barrels)
        {
            Require(market, hold, barrels);

            if (treasury == null)
            {
                throw new ArgumentNullException(nameof(treasury));
            }
        }

        private static void Require(CityMarket market, CargoHold hold, int barrels)
        {
            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            if (hold == null)
            {
                throw new ArgumentNullException(nameof(hold));
            }

            if (barrels < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(barrels));
            }
        }
    }
}
