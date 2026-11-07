using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What a city's labour follows: how its workshops take their workers from the population, and who leaves it for
    /// want of room, which only a city with labour has.
    /// </summary>
    public sealed class CityLabourRules
    {
        /// <param name="leaving">Null for a city nobody leaves.</param>
        public CityLabourRules(LabourRule labour, LeavingRule leaving = null)
        {
            Labour = labour ?? throw new ArgumentNullException(nameof(labour));
            Leaving = leaving;
        }

        /// <summary>How the workshops take their workers from the population.</summary>
        public LabourRule Labour { get; }

        /// <summary>Who leaves the city for want of room; null when nobody does.</summary>
        public LeavingRule Leaving { get; }
    }
}
