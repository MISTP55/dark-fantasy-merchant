namespace DarkFantasyMerchant.Core
{
    /// <summary>Chooses where a building goes on the grid.</summary>
    public interface IPlacementStrategy
    {
        /// <returns>False when the footprint fits nowhere.</returns>
        bool TryChoose(CityGrid grid, Footprint footprint, out GridPlace place);
    }
}
