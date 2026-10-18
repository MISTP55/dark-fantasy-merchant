using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The player ships of a port that can trade from the city panel, and the one that
    /// trades: the one already chosen while it is in port, otherwise the first.
    /// </summary>
    public static class TradingShipChoice
    {
        /// <summary>Clears the results, then adds the player's ships of the port, in their order.</summary>
        public static void PlayerShipsAmong<TShip>(
            IReadOnlyList<TShip> shipsInPort, Predicate<TShip> isPlayerShip, List<TShip> results)
            where TShip : class
        {
            if (shipsInPort == null)
            {
                throw new ArgumentNullException(nameof(shipsInPort));
            }

            if (isPlayerShip == null)
            {
                throw new ArgumentNullException(nameof(isPlayerShip));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            foreach (TShip ship in shipsInPort)
            {
                if (isPlayerShip(ship))
                {
                    results.Add(ship);
                }
            }
        }

        /// <returns>
        /// The current ship while it is in port and the player's, otherwise the first player
        /// ship of the port, or null when it has none.
        /// </returns>
        public static TShip Choose<TShip>(IReadOnlyList<TShip> shipsInPort, Predicate<TShip> isPlayerShip, TShip current)
            where TShip : class
        {
            if (shipsInPort == null)
            {
                throw new ArgumentNullException(nameof(shipsInPort));
            }

            if (isPlayerShip == null)
            {
                throw new ArgumentNullException(nameof(isPlayerShip));
            }

            TShip first = null;

            foreach (TShip ship in shipsInPort)
            {
                if (!isPlayerShip(ship))
                {
                    continue;
                }

                if (ReferenceEquals(ship, current))
                {
                    return ship;
                }

                first ??= ship;
            }

            return first;
        }
    }
}
