using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The cells of a city and the buildings on them. It knows footprints and places, never who builds or why.
    /// </summary>
    public sealed class CityGrid
    {
        private readonly bool[] forbidden;
        private readonly PlacedBuilding[] occupancy;
        private readonly List<PlacedBuilding> buildings = new List<PlacedBuilding>();

        public CityGrid(int width, int height, IEnumerable<Vector2Int> forbidden)
        {
            if (width < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            Width = width;
            Height = height;
            this.forbidden = new bool[width * height];
            occupancy = new PlacedBuilding[width * height];
            foreach (Vector2Int cell in forbidden)
            {
                if (!Contains(cell))
                {
                    throw new ArgumentOutOfRangeException(nameof(forbidden), "A forbidden cell is outside the grid.");
                }

                this.forbidden[Index(cell)] = true;
            }
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>Buildings in the order they were placed.</summary>
        public IReadOnlyList<PlacedBuilding> Buildings => buildings;

        public event Action<PlacedBuilding> BuildingPlaced;

        public bool Contains(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        }

        public bool IsForbidden(Vector2Int cell)
        {
            return Contains(cell) && forbidden[Index(cell)];
        }

        /// <summary>The building covering the cell; null when it is free or outside the grid.</summary>
        public PlacedBuilding BuildingAt(Vector2Int cell)
        {
            return Contains(cell) ? occupancy[Index(cell)] : null;
        }

        public bool CanPlace(Footprint footprint, GridPlace place)
        {
            Vector2Int size = footprint.SizeFor(place.Orientation);
            Vector2Int origin = place.Origin;
            if (origin.x < 0 || origin.y < 0 || origin.x + size.x > Width || origin.y + size.y > Height)
            {
                return false;
            }

            for (int y = origin.y; y < origin.y + size.y; y++)
            {
                for (int x = origin.x; x < origin.x + size.x; x++)
                {
                    int index = y * Width + x;
                    if (forbidden[index] || occupancy[index] != null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public PlacedBuilding Place(BuildingKind kind, BuildingOwner owner, Footprint footprint, GridPlace place)
        {
            if (!CanPlace(footprint, place))
            {
                throw new InvalidOperationException("The footprint does not fit at this place.");
            }

            var building = new PlacedBuilding(kind, owner, footprint, place);
            Vector2Int size = building.Size;
            for (int y = place.Origin.y; y < place.Origin.y + size.y; y++)
            {
                for (int x = place.Origin.x; x < place.Origin.x + size.x; x++)
                {
                    occupancy[y * Width + x] = building;
                }
            }

            buildings.Add(building);
            BuildingPlaced?.Invoke(building);
            return building;
        }

        /// <summary>
        /// Fills results with every place where the footprint fits, by x then y, Along before Across;
        /// a square footprint is listed Along only.
        /// </summary>
        public void GetPlaces(Footprint footprint, List<GridPlace> results)
        {
            results.Clear();
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var along = new GridPlace(new Vector2Int(x, y), BuildingOrientation.Along);
                    if (CanPlace(footprint, along))
                    {
                        results.Add(along);
                    }

                    if (footprint.IsSquare)
                    {
                        continue;
                    }

                    var across = new GridPlace(new Vector2Int(x, y), BuildingOrientation.Across);
                    if (CanPlace(footprint, across))
                    {
                        results.Add(across);
                    }
                }
            }
        }

        /// <summary>The houses of the class on the grid, the city's and the player's.</summary>
        public int HousesOf(SocialClass housedClass)
        {
            int count = 0;
            foreach (PlacedBuilding building in buildings)
            {
                if (building.Kind.IsHouse && building.Kind.HousedClass == housedClass)
                {
                    count++;
                }
            }

            return count;
        }

        private int Index(Vector2Int cell)
        {
            return cell.y * Width + cell.x;
        }
    }
}
