using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Chooses, at random, among the places that need the fewest roads to touch the road network (none for a place
    /// that already touches it), and returns those roads.
    /// </summary>
    public sealed class RoadsidePlacement : IPlacementStrategy
    {
        private readonly System.Random random;
        private readonly List<GridPlace> places = new List<GridPlace>();
        private readonly List<Vector2Int> cells = new List<Vector2Int>();
        private int[] distances = new int[0];
        private int[] costs = new int[0];
        private bool[] remaining = new bool[0];
        private readonly List<int> indices = new List<int>();

        public RoadsidePlacement(System.Random random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <remarks>
        /// A place costs the least distance, in roads, over the free cells touching it, ignoring the building's own
        /// cells, so the cost is a lower bound: the drawn place is confirmed by the exact search, and dropped when
        /// its roads are longer or missing.
        /// </remarks>
        public bool TryChoose(CityGrid grid, Footprint footprint, out GridPlace place, List<Vector2Int> roads)
        {
            roads.Clear();
            place = default;
            if (grid.TownHall == null)
            {
                return false;
            }

            grid.GetPlaces(footprint, places);
            int count = grid.Width * grid.Height;
            if (distances.Length != count)
            {
                distances = new int[count];
            }

            RoadPlanner.DistancesFromNetwork(grid, distances);
            if (costs.Length < places.Count)
            {
                costs = new int[places.Count];
            }

            for (int i = 0; i < places.Count; i++)
            {
                costs[i] = CostOf(grid, footprint, places[i]);
            }

            if (remaining.Length < places.Count)
            {
                remaining = new bool[places.Count];
            }

            for (int i = 0; i < places.Count; i++)
            {
                remaining[i] = costs[i] != int.MaxValue;
            }

            while (true)
            {
                int best = int.MaxValue;
                for (int i = 0; i < places.Count; i++)
                {
                    if (remaining[i] && costs[i] < best)
                    {
                        best = costs[i];
                    }
                }

                if (best == int.MaxValue)
                {
                    roads.Clear();
                    return false;
                }

                indices.Clear();
                for (int i = 0; i < places.Count; i++)
                {
                    if (remaining[i] && costs[i] == best)
                    {
                        indices.Add(i);
                    }
                }

                while (indices.Count > 0)
                {
                    int pick = random.Next(indices.Count);
                    int chosen = indices[pick];
                    indices.RemoveAt(pick);

                    FillCells(footprint, places[chosen]);
                    if (!RoadPlanner.TryFindRoads(grid, cells, roads))
                    {
                        remaining[chosen] = false;
                    }
                    else if (roads.Count == best)
                    {
                        place = places[chosen];
                        return true;
                    }
                    else
                    {
                        // The bound was out of reach: the place stays, ranked at its exact cost.
                        costs[chosen] = roads.Count;
                    }
                }
            }
        }

        private int CostOf(CityGrid grid, Footprint footprint, GridPlace candidate)
        {
            if (grid.WouldConnect(footprint, candidate))
            {
                return 0;
            }

            Vector2Int size = footprint.SizeFor(candidate.Orientation);
            Vector2Int origin = candidate.Origin;
            int best = int.MaxValue;
            for (int y = origin.y - 1; y <= origin.y + size.y; y++)
            {
                for (int x = origin.x - 1; x <= origin.x + size.x; x++)
                {
                    bool inside = x >= origin.x && x < origin.x + size.x && y >= origin.y && y < origin.y + size.y;
                    if (inside || !grid.Contains(new Vector2Int(x, y)))
                    {
                        continue;
                    }

                    best = Math.Min(best, distances[y * grid.Width + x]);
                }
            }

            return best;
        }

        private void FillCells(Footprint footprint, GridPlace candidate)
        {
            cells.Clear();
            Vector2Int size = footprint.SizeFor(candidate.Orientation);
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    cells.Add(new Vector2Int(candidate.Origin.x + x, candidate.Origin.y + y));
                }
            }
        }
    }
}
