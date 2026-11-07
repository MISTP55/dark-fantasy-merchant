using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>The grid a city's buildings stand on, with where the city builds its own houses there: one never comes without the other.</summary>
    public sealed class CityGridSetup
    {
        public CityGridSetup(CityGrid grid, IPlacementStrategy placement)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Placement = placement ?? throw new ArgumentNullException(nameof(placement));
        }

        public CityGrid Grid { get; }

        /// <summary>Where the city builds its own houses on the grid.</summary>
        public IPlacementStrategy Placement { get; }
    }
}
