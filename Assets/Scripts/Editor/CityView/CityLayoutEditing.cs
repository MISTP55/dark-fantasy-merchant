using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// The editing operations of a city's layout. Each records an undo and marks the asset dirty.
    /// The footprint of a house is the catalogue's, so the operations that need it take the catalogue.
    /// </summary>
    public static class CityLayoutEditing
    {
        /// <summary>The footprint of the house of the class in the catalogue.</summary>
        public static Footprint HouseFootprint(BuildingsDefinition buildings, SocialClass housedClass)
        {
            return buildings.HouseOf(housedClass).Footprint;
        }

        /// <summary>Gives the layout the size; the forbidden cells and the houses that fall outside are dropped.</summary>
        public static void Resize(CityLayoutDefinition layout, BuildingsDefinition buildings, int width, int height)
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

        /// <summary>
        /// Forbids the cells or frees them. The cells outside the grid, and when forbidding those a house covers, are
        /// skipped. Records nothing when no cell changes.
        /// </summary>
        public static void SetForbidden(CityLayoutDefinition layout, BuildingsDefinition buildings, IEnumerable<Vector2Int> cells, bool forbid)
        {
            var forbidden = new List<Vector2Int>(layout.ForbiddenCells);
            bool changed = false;

            foreach (Vector2Int cell in cells)
            {
                if (!IsInside(layout, cell))
                {
                    continue;
                }

                if (!forbid)
                {
                    changed |= forbidden.Remove(cell);
                }
                else if (!forbidden.Contains(cell) && HouseIndexAt(layout, buildings, cell) < 0)
                {
                    forbidden.Add(cell);
                    changed = true;
                }
            }

            if (changed)
            {
                Apply(layout, forbid ? "Forbid cells" : "Free cells", layout.Width, layout.Height, forbidden, layout.StartingHouses);
            }
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

        /// <summary>
        /// Removes the starting houses covering any of the cells, whichever of their cells it is; only those of
        /// <paramref name="onlyClass"/> when it is given. Records nothing when no house is removed.
        /// </summary>
        public static void RemoveHouses(
            CityLayoutDefinition layout, BuildingsDefinition buildings, IEnumerable<Vector2Int> cells, SocialClass? onlyClass = null)
        {
            var removed = new HashSet<int>();

            foreach (Vector2Int cell in cells)
            {
                int index = HouseIndexAt(layout, buildings, cell);

                if (index >= 0 && (onlyClass == null || layout.StartingHouses[index].socialClass == onlyClass.Value))
                {
                    removed.Add(index);
                }
            }

            if (removed.Count == 0)
            {
                return;
            }

            var houses = new List<CityLayoutDefinition.StartingHouse>();

            for (int i = 0; i < layout.StartingHouses.Count; i++)
            {
                if (!removed.Contains(i))
                {
                    houses.Add(layout.StartingHouses[i]);
                }
            }

            Apply(layout, "Remove starting houses", layout.Width, layout.Height, layout.ForbiddenCells, houses);
        }

        /// <summary>The index of the starting house covering the cell; -1 when there is none.</summary>
        public static int HouseIndexAt(CityLayoutDefinition layout, BuildingsDefinition buildings, Vector2Int cell)
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

        /// <summary>
        /// The cells of a brush of <paramref name="size"/> cells across around the cell (one cell more on the low side when
        /// the size is even), cut to a grid of the size given. A round brush keeps the cells whose centre is close enough
        /// to the brush's: 3 across is a cross, 2 across a full square.
        /// </summary>
        public static List<Vector2Int> BrushCells(Vector2Int centre, int size, bool round, int width, int height)
        {
            size = Mathf.Max(1, size);
            int below = size / 2;

            // The brush's centre, in cells from its low corner; a cell is in when its centre is within the reach of it.
            float radius = (size - 1) / 2f;
            float reach = radius * radius + radius / 2f;
            var cells = new List<Vector2Int>();

            for (int dy = 0; dy < size; dy++)
            {
                for (int dx = 0; dx < size; dx++)
                {
                    var cell = new Vector2Int(centre.x - below + dx, centre.y - below + dy);
                    float offX = dx - radius;
                    float offY = dy - radius;

                    if (cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height
                        && (!round || offX * offX + offY * offY <= reach))
                    {
                        cells.Add(cell);
                    }
                }
            }

            return cells;
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
