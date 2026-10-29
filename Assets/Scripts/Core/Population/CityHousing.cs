using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The houses of a city, held by its <see cref="CityMarket"/>: the player's, and those the city builds of its own
    /// for each crowded class by its <see cref="CityHousingRule"/>s. On a grid, a house gives its room to its class only
    /// once connected to the road network, and the city builds one only with the roads that connect it.
    /// </summary>
    internal sealed class CityHousing
    {
        private readonly CityPopulation population;
        private readonly IPlacementStrategy placement;
        private readonly List<Vector2Int> roadsToLay = new List<Vector2Int>();
        private readonly List<Vector2Int> houseCells = new List<Vector2Int>();
        private readonly int[] playerHouses = new int[3];

        // The room of each house on the grid that is not connected yet, given to its class when it connects.
        private readonly Dictionary<PlacedBuilding, int> roomUntilConnected = new Dictionary<PlacedBuilding, int>();

        // The room of the house being placed, set only for the time of the placement.
        private int roomOfPlacedHouse;

        /// <param name="housing">One rule per class at most; null for a city that never builds.</param>
        /// <param name="grid">With its placement strategy (both or neither); null for a city whose houses have no place. Every rule then has a footprint, and a house of the grid not connected yet needs the rule of its class, which gives its room.</param>
        public CityHousing(CityPopulation population, IReadOnlyList<CityHousingRule> housing, CityGrid grid, IPlacementStrategy placement)
        {
            this.population = population;
            Rules = CopyOf(housing);

            if ((grid == null) != (placement == null))
            {
                throw new ArgumentException("A grid and a placement strategy come together.", nameof(grid));
            }

            if (grid != null)
            {
                foreach (CityHousingRule rule in Rules)
                {
                    if (rule.Footprint == null)
                    {
                        throw new ArgumentException("A market with a grid needs a footprint for every housing rule.", nameof(housing));
                    }
                }

                foreach (PlacedBuilding building in grid.Buildings)
                {
                    if (building.Kind.IsHouse && !building.IsConnected)
                    {
                        CityHousingRule rule = RuleOf(Rules, building.Kind.HousedClass)
                            ?? throw new ArgumentException(
                                "A house of the grid is not connected and its class has no housing rule to give its room.", nameof(grid));
                        roomUntilConnected[building] = rule.Room;
                    }
                }
            }

            Grid = grid;
            this.placement = placement;
        }

        /// <summary>When the city builds a house of its own for each class; empty when it never does.</summary>
        public IReadOnlyList<CityHousingRule> Rules { get; }

        /// <summary>Null for a city whose houses have no place.</summary>
        public CityGrid Grid { get; }

        /// <summary>Houses the city has built of its own since the game started, not the player's.</summary>
        public int CityHouses { get; private set; }

        /// <summary>Houses of a class the player has built in the city.</summary>
        public int PlayerHousesOf(SocialClass housedClass)
        {
            return playerHouses[(int)housedClass];
        }

        /// <summary>
        /// From now on, a house the grid places takes its room when it connects. Apart from the constructor, so that a
        /// market that fails to be made leaves nothing listening to its grid.
        /// </summary>
        public void FollowGrid()
        {
            if (Grid != null)
            {
                Grid.BuildingPlaced += OnBuildingPlaced;
                Grid.BuildingConnected += OnBuildingConnected;
            }
        }

        /// <summary>A house of the player on the grid, at a place it can stand: its room is given when it connects.</summary>
        public void AddPlayerHouse(SocialClass housedClass, int room, Footprint footprint, GridPlace place)
        {
            PlaceHouse(housedClass, BuildingOwner.Player, room, footprint, place);
            playerHouses[(int)housedClass]++;
        }

        /// <summary>
        /// One house a day at most for each crowded class, with every material or none taken. What it takes is not
        /// consumption: the week's log does not count it.
        /// </summary>
        /// <param name="availableOf">The whole units of a good in the city's stock.</param>
        /// <param name="take">Takes units of a good out of the city's stock, not below nothing.</param>
        public void BuildCityHouses(Func<int, int> availableOf, Action<int, int> take)
        {
            foreach (CityHousingRule rule in Rules)
            {
                if (rule.IsCrowded(population))
                {
                    BuildCityHouse(rule, availableOf, take);
                }
            }
        }

        private void BuildCityHouse(CityHousingRule rule, Func<int, int> availableOf, Action<int, int> take)
        {
            foreach (MaterialCost material in rule.Materials)
            {
                if (availableOf(material.Good) < material.Units)
                {
                    return;
                }
            }

            // With a grid the house needs a place and the roads that connect it, found before anything is taken:
            // a place off the network, without the roads to join it, builds nothing.
            GridPlace place = default;

            if (Grid != null)
            {
                if (!placement.TryChoose(Grid, rule.Footprint.Value, out place, roadsToLay))
                {
                    return;
                }

                CellsOf(rule.Footprint.Value, place, houseCells);

                if (!Grid.CanPlace(rule.Footprint.Value, place) || !RoadPlanner.Connects(Grid, houseCells, roadsToLay))
                {
                    return;
                }
            }

            foreach (MaterialCost material in rule.Materials)
            {
                take(material.Good, material.Units);
            }

            if (Grid != null)
            {
                // Its room comes when it connects: at once or through its roads.
                PlaceHouse(rule.HousedClass, BuildingOwner.City, rule.Room, rule.Footprint.Value, place);
                Grid.PlaceRoads(roadsToLay, BuildingOwner.City);
            }
            else
            {
                population.AddRoom(rule.HousedClass, rule.Room);
            }

            CityHouses++;
        }

        // Places a house whose room waits for its connection, which may come at once.
        private void PlaceHouse(SocialClass housedClass, BuildingOwner owner, int room, Footprint footprint, GridPlace place)
        {
            roomOfPlacedHouse = room;

            try
            {
                Grid.Place(BuildingKind.House(housedClass), owner, footprint, place);
            }
            finally
            {
                roomOfPlacedHouse = 0;
            }
        }

        // Before the house can connect: the grid raises BuildingPlaced first.
        private void OnBuildingPlaced(PlacedBuilding building)
        {
            // Only the houses placed here: their room is known.
            if (building.Kind.IsHouse && roomOfPlacedHouse > 0)
            {
                roomUntilConnected[building] = roomOfPlacedHouse;
                roomOfPlacedHouse = 0;
            }
        }

        private void OnBuildingConnected(PlacedBuilding building)
        {
            if (roomUntilConnected.TryGetValue(building, out int room))
            {
                roomUntilConnected.Remove(building);
                population.AddRoom(building.Kind.HousedClass, room);
            }
        }

        private static void CellsOf(Footprint footprint, GridPlace place, List<Vector2Int> cells)
        {
            cells.Clear();
            Vector2Int size = footprint.SizeFor(place.Orientation);

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    cells.Add(new Vector2Int(place.Origin.x + x, place.Origin.y + y));
                }
            }
        }

        private static CityHousingRule RuleOf(IReadOnlyList<CityHousingRule> housing, SocialClass housedClass)
        {
            foreach (CityHousingRule rule in housing)
            {
                if (rule.HousedClass == housedClass)
                {
                    return rule;
                }
            }

            return null;
        }

        private static IReadOnlyList<CityHousingRule> CopyOf(IReadOnlyList<CityHousingRule> housing)
        {
            var copy = new List<CityHousingRule>();

            if (housing == null)
            {
                return copy;
            }

            foreach (CityHousingRule rule in housing)
            {
                if (rule == null)
                {
                    throw new ArgumentNullException(nameof(housing));
                }

                if (copy.Exists(other => other.HousedClass == rule.HousedClass))
                {
                    throw new ArgumentException("A class has two housing rules.", nameof(housing));
                }

                copy.Add(rule);
            }

            return copy;
        }
    }
}
