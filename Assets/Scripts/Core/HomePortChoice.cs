using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Chooses the home city of a ship that is added to a fleet.</summary>
    public static class HomePortChoice
    {
        /// <summary>
        /// Picks the candidate with the fewest ships homed at it; among ties, the one that
        /// <paramref name="nextBelow"/> draws, in candidate order. It is asked only when
        /// several candidates tie.
        /// </summary>
        /// <param name="shipsHomedAt">Gives the ships homed at a candidate.</param>
        /// <param name="nextBelow">Gives a random number from 0 to its argument, excluded.</param>
        /// <returns>The candidate, or -1 without any.</returns>
        public static int Pick(IReadOnlyList<int> candidates, Func<int, int> shipsHomedAt, Func<int, int> nextBelow)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (shipsHomedAt == null)
            {
                throw new ArgumentNullException(nameof(shipsHomedAt));
            }

            if (nextBelow == null)
            {
                throw new ArgumentNullException(nameof(nextBelow));
            }

            var tied = new List<int>();
            int fewest = int.MaxValue;

            foreach (int candidate in candidates)
            {
                int homed = shipsHomedAt(candidate);

                if (homed < fewest)
                {
                    fewest = homed;
                    tied.Clear();
                }

                if (homed == fewest)
                {
                    tied.Add(candidate);
                }
            }

            if (tied.Count == 0)
            {
                return -1;
            }

            return tied.Count == 1 ? tied[0] : tied[nextBelow(tied.Count)];
        }
    }
}
