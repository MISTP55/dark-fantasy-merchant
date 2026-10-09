using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Buying and selling between a city's market, a ship's hold and the player's
    /// treasury. Barrels are priced one by one, each at the stock it leaves or joins,
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

        /// <summary>Sells up to <paramref name="barrels"/> barrels: as many as are aboard.</summary>
        public static TradeResult Sell(CityMarket market, CargoHold hold, Treasury treasury, int good, int barrels)
        {
            Require(market, hold, treasury, barrels);

            int sold = Math.Min(barrels, hold.BarrelsOf(good));

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
            treasury.Deposit(gold);
            return new TradeResult(sold, gold);
        }

        private static void Require(CityMarket market, CargoHold hold, Treasury treasury, int barrels)
        {
            if (market == null)
            {
                throw new ArgumentNullException(nameof(market));
            }

            if (hold == null)
            {
                throw new ArgumentNullException(nameof(hold));
            }

            if (treasury == null)
            {
                throw new ArgumentNullException(nameof(treasury));
            }

            if (barrels < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(barrels));
            }
        }
    }
}
