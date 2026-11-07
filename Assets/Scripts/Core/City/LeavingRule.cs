using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Who leaves a city each day: the housed beyond the room of their houses, a quarter of the excess plus one,
    /// never the last one; they take their jobs with them, sparing the protected workers of the city's workshops
    /// while other jobs can be taken. Stateless: the population is passed in. Integer arithmetic, truncated.
    /// </summary>
    public sealed class LeavingRule
    {
        // A quarter, as a shift of two bits: a constant of its own, not the inhabitants per worker, though both are 4.
        private const int ExcessDivisor = 4;

        /// <param name="protectedWorkers">Workers of a city's workshop the leaving housed do not take while others have more (30).</param>
        public LeavingRule(int protectedWorkers)
        {
            if (protectedWorkers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(protectedWorkers), "The value is at least 0.");
            }

            ProtectedWorkers = protectedWorkers;
        }

        public int ProtectedWorkers { get; }

        /// <summary>The housed who want to leave the city today: a quarter of those beyond the room, plus one; never the last one.</summary>
        public int LeavingOf(CityPopulation population)
        {
            if (population == null)
            {
                throw new ArgumentNullException(nameof(population));
            }

            int housed = population.Housed;
            int excess = housed - population.Room;
            int leaving = excess > 0 ? excess / ExcessDivisor + 1 : 0;

            return Math.Max(0, Math.Min(leaving, housed - 1));
        }
    }
}
