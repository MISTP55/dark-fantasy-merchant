using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Draws the ports a fleet starts in.</summary>
    public static class StartPortDraw
    {
        /// <summary>
        /// Draws a port for each ship among the candidates, in a random order, without
        /// repeating one before all were drawn.
        /// </summary>
        /// <param name="nextBelow">Gives a random number from 0 to its argument, excluded.</param>
        /// <param name="results">Cleared, then filled with a candidate per ship; empty without candidates.</param>
        public static void Draw(int count, IReadOnlyList<int> candidates, Func<int, int> nextBelow, List<int> results)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (nextBelow == null)
            {
                throw new ArgumentNullException(nameof(nextBelow));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            if (count < 1 || candidates.Count == 0)
            {
                return;
            }

            var shuffled = new int[candidates.Count];

            for (int i = 0; i < shuffled.Length; i++)
            {
                shuffled[i] = candidates[i];
            }

            for (int i = shuffled.Length - 1; i > 0; i--)
            {
                // Clamped: a generator that breaks its promise must not break the draw.
                int other = Math.Min(i, Math.Max(0, nextBelow(i + 1)));
                int swapped = shuffled[i];
                shuffled[i] = shuffled[other];
                shuffled[other] = swapped;
            }

            for (int i = 0; i < count; i++)
            {
                results.Add(shuffled[i % shuffled.Length]);
            }
        }
    }
}
