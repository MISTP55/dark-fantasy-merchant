using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>Puts a city's starting houses on its grid.</summary>
    public static class StartingHouses
    {
        private static readonly SocialClass[] Classes = { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor };

        /// <summary>
        /// Places, as the city's, houses[c] houses of each class (Rich, then Wealthy, then Poor) in three steps: every
        /// house at random on the grid; then roads, the house needing the fewest first, so that later houses reuse
        /// them; then every house no road can reach is taken off, and they are placed again along the roads
        /// (<see cref="RoadsidePlacement"/>) with the roads they need.
        /// </summary>
        /// <returns>
        /// False as soon as a house fits nowhere, or one taken off cannot be connected (always without a town hall);
        /// the grid is then left as it stands, short of the houses taken off and not placed again.
        /// </returns>
        public static bool TryPlace(CityGrid grid, PerClass<int> houses, PerClass<Footprint> footprints, System.Random random)
        {
            var placed = new List<PlacedBuilding>();
            var roads = new List<Vector2Int>();
            var drawn = new RandomPlacement(random);
            foreach (SocialClass housedClass in Classes)
            {
                for (int i = 0; i < houses[housedClass]; i++)
                {
                    if (!drawn.TryChoose(grid, footprints[housedClass], out GridPlace place, roads))
                    {
                        return false;
                    }

                    placed.Add(grid.Place(BuildingKind.House(housedClass), BuildingOwner.City, footprints[housedClass], place));
                }
            }

            ConnectByRoads(grid, placed);

            // All taken off before any is placed again: their cells may be the room the others need.
            var unconnected = placed.FindAll(house => !house.IsConnected);
            foreach (PlacedBuilding house in unconnected)
            {
                grid.RemoveHouse(house);
            }

            var roadside = new RoadsidePlacement(random);
            foreach (PlacedBuilding house in unconnected)
            {
                if (!roadside.TryChoose(grid, house.Footprint, out GridPlace place, roads))
                {
                    return false;
                }

                grid.Place(house.Kind, BuildingOwner.City, house.Footprint, place);
                grid.PlaceRoads(roads, BuildingOwner.City);
            }

            return true;
        }

        // Lays, one house at a time, the roads of the unconnected house that needs the fewest. A house no road reaches
        // never becomes reachable: roads only take free cells, and every cell they cut off touches them.
        private static void ConnectByRoads(CityGrid grid, List<PlacedBuilding> houses)
        {
            var unreachable = new HashSet<PlacedBuilding>();
            var roads = new List<Vector2Int>();
            var best = new List<Vector2Int>();
            var cells = new List<Vector2Int>();

            while (true)
            {
                best.Clear();
                bool found = false;
                foreach (PlacedBuilding house in houses)
                {
                    if (house.IsConnected || unreachable.Contains(house))
                    {
                        continue;
                    }

                    FillCells(house, cells);
                    if (!RoadPlanner.TryFindRoads(grid, cells, roads))
                    {
                        unreachable.Add(house);
                    }
                    else if (!found || roads.Count < best.Count)
                    {
                        found = true;
                        best.Clear();
                        best.AddRange(roads);
                    }
                }

                if (!found || best.Count == 0)
                {
                    return;
                }

                grid.PlaceRoads(best, BuildingOwner.City);
            }
        }

        private static void FillCells(PlacedBuilding building, List<Vector2Int> cells)
        {
            cells.Clear();
            Vector2Int size = building.Size;
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    cells.Add(new Vector2Int(building.Place.Origin.x + x, building.Place.Origin.y + y));
                }
            }
        }
    }
}
