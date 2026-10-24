namespace DarkFantasyMerchant.Core
{
    /// <summary>Puts a city's starting houses on its grid.</summary>
    public static class StartingHouses
    {
        private static readonly SocialClass[] Classes = { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor };

        /// <summary>
        /// Places, as the city's, houses[c] houses of each class (Rich, then Wealthy, then Poor)
        /// where the strategy chooses.
        /// </summary>
        /// <returns>False as soon as one does not fit; those placed stay.</returns>
        public static bool TryPlace(
            CityGrid grid, PerClass<int> houses, PerClass<Footprint> footprints, IPlacementStrategy placement)
        {
            foreach (SocialClass housedClass in Classes)
            {
                for (int i = 0; i < houses[housedClass]; i++)
                {
                    if (!placement.TryChoose(grid, footprints[housedClass], out GridPlace place))
                    {
                        return false;
                    }

                    grid.Place(BuildingKind.House(housedClass), BuildingOwner.City, footprints[housedClass], place);
                }
            }

            return true;
        }
    }
}
