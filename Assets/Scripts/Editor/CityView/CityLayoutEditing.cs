using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// The editing operations of a city's layout. Each records an undo and marks the asset dirty.
    /// Where the footprint of a house matters, the catalogue gives it; without one, a house is 2 by 1.
    /// </summary>
    public static class CityLayoutEditing
    {
        private static readonly Footprint DefaultHouseFootprint = new Footprint(2, 1);

        /// <summary>The footprint of the house of the class: the catalogue's, or 2 by 1 when it has none.</summary>
        public static Footprint HouseFootprint(BuildingsDefinition buildings, SocialClass housedClass)
        {
            HouseDefinition house = buildings != null ? buildings.HouseOf(housedClass) : null;

            return house != null && house.FootprintSize.x >= 1 && house.FootprintSize.y >= 1
                ? house.Footprint
                : DefaultHouseFootprint;
        }

        /// <summary>Gives the layout the size; the forbidden cells and the houses that fall outside are dropped.</summary>
        public static void Resize(CityLayoutDefinition layout, int width, int height, BuildingsDefinition buildings = null)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            var forbidden = new List<Vector2Int>();

            foreach (Vector2Int cell in layout.ForbiddenCells)
            {
                if (cell.x < width && cell.y < height)
                {
                    forbidden.Add(cell);
                }
            }

            var houses = new List<CityLayoutDefinition.StartingHouse>();

            foreach (CityLayoutDefinition.StartingHouse house in layout.StartingHouses)
            {
                Vector2Int size = HouseFootprint(buildings, house.socialClass).SizeFor(house.orientation);

                if (house.origin.x + size.x <= width && house.origin.y + size.y <= height)
                {
                    houses.Add(house);
                }
            }

            Apply(layout, "Resize city layout", width, height, forbidden, houses);
        }

        /// <summary>Forbids the cell or frees it. Refused on a cell a house covers, and outside the grid.</summary>
        public static void ToggleForbidden(CityLayoutDefinition layout, Vector2Int cell, BuildingsDefinition buildings = null)
        {
            if (!IsInside(layout, cell) || HouseIndexAt(layout, cell, buildings) >= 0)
            {
                return;
            }

            var forbidden = new List<Vector2Int>(layout.ForbiddenCells);

            if (!forbidden.Remove(cell))
            {
                forbidden.Add(cell);
            }

            Apply(layout, "Toggle forbidden cell", layout.Width, layout.Height, forbidden, layout.StartingHouses);
        }

        /// <summary>Adds a starting house; false when it does not fit, or the layout cannot make its grid.</summary>
        public static bool TryAddHouse(CityLayoutDefinition layout, BuildingsDefinition buildings, SocialClass housedClass, GridPlace place)
        {
            if (!layout.TryCreateGrid(buildings, out CityGrid grid, out _)
                || !grid.CanPlace(HouseFootprint(buildings, housedClass), place))
            {
                return false;
            }

            var houses = new List<CityLayoutDefinition.StartingHouse>(layout.StartingHouses)
            {
                new CityLayoutDefinition.StartingHouse(housedClass, place.Origin, place.Orientation),
            };

            Apply(layout, "Add starting house", layout.Width, layout.Height, layout.ForbiddenCells, houses);
            return true;
        }

        /// <summary>Removes the starting house covering the cell, whichever of its cells it is.</summary>
        public static void RemoveHouseAt(CityLayoutDefinition layout, Vector2Int cell, BuildingsDefinition buildings = null)
        {
            int index = HouseIndexAt(layout, cell, buildings);

            if (index < 0)
            {
                return;
            }

            var houses = new List<CityLayoutDefinition.StartingHouse>(layout.StartingHouses);
            houses.RemoveAt(index);
            Apply(layout, "Remove starting house", layout.Width, layout.Height, layout.ForbiddenCells, houses);
        }

        /// <summary>The index of the starting house covering the cell; -1 when there is none.</summary>
        public static int HouseIndexAt(CityLayoutDefinition layout, Vector2Int cell, BuildingsDefinition buildings = null)
        {
            for (int i = 0; i < layout.StartingHouses.Count; i++)
            {
                CityLayoutDefinition.StartingHouse house = layout.StartingHouses[i];
                Vector2Int size = HouseFootprint(buildings, house.socialClass).SizeFor(house.orientation);

                if (cell.x >= house.origin.x && cell.x < house.origin.x + size.x
                    && cell.y >= house.origin.y && cell.y < house.origin.y + size.y)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsInside(CityLayoutDefinition layout, Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < layout.Width && cell.y < layout.Height;
        }

        private static void Apply(
            CityLayoutDefinition layout, string undoName, int width, int height,
            IEnumerable<Vector2Int> forbidden, IEnumerable<CityLayoutDefinition.StartingHouse> houses)
        {
            Undo.RecordObject(layout, undoName);
            layout.SetContent(width, height, forbidden, houses);
            EditorUtility.SetDirty(layout);
        }
    }
}
