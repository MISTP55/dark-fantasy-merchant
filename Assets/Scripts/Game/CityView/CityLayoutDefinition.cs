using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The grid of one city: its size, the cells nothing can cover and the houses it starts with.
    /// Holds no runtime state: <see cref="TryCreateGrid"/> makes the grid the market works on.
    /// </summary>
    [CreateAssetMenu(fileName = "CityLayout", menuName = "Dark Fantasy Merchant/City layout")]
    public sealed class CityLayoutDefinition : ScriptableObject
    {
        /// <summary>A house the city starts with, where it stands.</summary>
        [Serializable]
        public sealed class StartingHouse
        {
            [SerializeField] public SocialClass socialClass;

            [Tooltip("The covered cell of smallest x and y.")]
            [SerializeField] public Vector2Int origin;

            [SerializeField] public BuildingOrientation orientation;

            public StartingHouse()
            {
            }

            public StartingHouse(SocialClass socialClass, Vector2Int origin, BuildingOrientation orientation)
            {
                this.socialClass = socialClass;
                this.origin = origin;
                this.orientation = orientation;
            }
        }

        /// <summary>Where the city's town hall stands.</summary>
        [Serializable]
        public struct TownHallSite
        {
            [Tooltip("The covered cell of smallest x and y.")]
            public Vector2Int origin;

            public BuildingOrientation orientation;
        }

        /// <summary>Cells on each side of a new layout, and of the default grids the scene setup makes.</summary>
        public const int DefaultSize = 40;

        [Tooltip("Cells on x, at least 1.")]
        [SerializeField, Min(1)] private int width = DefaultSize;

        [Tooltip("Cells on y, at least 1.")]
        [SerializeField, Min(1)] private int height = DefaultSize;

        [Tooltip("Cells nothing can cover (water, rock, fixed scenery), inside the grid.")]
        [SerializeField] private List<Vector2Int> forbiddenCells = new List<Vector2Int>();

        [SerializeField] private bool hasTownHall;

        [Tooltip("Where the town hall stands, with the footprint of the catalogue's. Used only when the layout has one.")]
        [SerializeField] private TownHallSite townHall;

        [Tooltip("The roads the city starts with, one cell each, inside the grid.")]
        [SerializeField] private List<Vector2Int> roads = new List<Vector2Int>();

        [Tooltip("The houses the city starts with, each with the footprint of its class's house.")]
        [SerializeField] private List<StartingHouse> startingHouses = new List<StartingHouse>();

        public int Width => width;

        public int Height => height;

        public IReadOnlyList<Vector2Int> ForbiddenCells => forbiddenCells;

        public IReadOnlyList<StartingHouse> StartingHouses => startingHouses;

        public bool HasTownHall => hasTownHall;

        /// <summary>Where the town hall stands; meaningful only when <see cref="HasTownHall"/>.</summary>
        public GridPlace TownHallPlace => new GridPlace(townHall.origin, townHall.orientation);

        public IReadOnlyList<Vector2Int> Roads => roads;

        /// <summary>
        /// The grid with the town hall, then the roads, then the starting houses placed as the city's, with
        /// the footprints of the catalogue's buildings, which <see cref="BuildingsDefinition.TryValidate"/> accepts.
        /// </summary>
        /// <param name="problem">What is wrong, for the logs: a size under 1, a forbidden cell outside, no town hall, a town hall, a road or a house that cannot stand; null when nothing is.</param>
        public bool TryCreateGrid(BuildingsDefinition buildings, out CityGrid grid, out string problem)
        {
            grid = null;
            problem = null;

            if (buildings == null)
            {
                problem = "there are no buildings to give the houses their footprint";
                return false;
            }

            if (width < 1 || height < 1)
            {
                problem = $"its size is {width} by {height}, under 1";
                return false;
            }

            foreach (Vector2Int cell in forbiddenCells)
            {
                if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height)
                {
                    problem = $"the forbidden cell ({cell.x}, {cell.y}) is outside the grid";
                    return false;
                }
            }

            var created = new CityGrid(width, height, forbiddenCells);

            if (!hasTownHall)
            {
                problem = "it has no town hall";
                return false;
            }

            TownHallDefinition hall = buildings.TownHall;

            if (hall == null || hall.FootprintSize.x < 1 || hall.FootprintSize.y < 1)
            {
                problem = "the buildings have no town hall with a footprint";
                return false;
            }

            if (!created.CanPlace(hall.Footprint, TownHallPlace))
            {
                problem = $"the town hall cannot stand at ({townHall.origin.x}, {townHall.origin.y}): it is outside the grid or on a forbidden cell";
                return false;
            }

            created.Place(BuildingKind.TownHall(), BuildingOwner.City, hall.Footprint, TownHallPlace);

            foreach (Vector2Int cell in roads)
            {
                var roadPlace = new GridPlace(cell, BuildingOrientation.Along);

                if (!created.CanPlace(new Footprint(1, 1), roadPlace))
                {
                    problem = $"the road at ({cell.x}, {cell.y}) cannot stand: it is outside the grid or on a forbidden cell or another building";
                    return false;
                }

                created.Place(BuildingKind.Road(), BuildingOwner.City, new Footprint(1, 1), roadPlace);
            }

            foreach (StartingHouse house in startingHouses)
            {
                HouseDefinition definition = buildings.HouseOf(house.socialClass);

                if (definition == null || definition.FootprintSize.x < 1 || definition.FootprintSize.y < 1)
                {
                    problem = $"the {house.socialClass} house at ({house.origin.x}, {house.origin.y}) has no house with a footprint in the buildings";
                    return false;
                }

                var place = new GridPlace(house.origin, house.orientation);

                if (!created.CanPlace(definition.Footprint, place))
                {
                    problem = $"the {house.socialClass.ToString().ToLowerInvariant()} house at ({house.origin.x}, {house.origin.y}) cannot stand: it is outside the grid or on a forbidden cell or another house";
                    return false;
                }

                created.Place(BuildingKind.House(house.socialClass), BuildingOwner.City, definition.Footprint, place);
            }

            grid = created;
            return true;
        }

        /// <summary>Editor and setup only: replaces the whole content.</summary>
        /// <param name="townHall">Where the town hall stands; null for a layout without one.</param>
        public void SetContent(
            int width, int height, IEnumerable<Vector2Int> forbidden, IEnumerable<StartingHouse> houses,
            GridPlace? townHall, IEnumerable<Vector2Int> roads)
        {
            this.width = width;
            this.height = height;
            forbiddenCells = new List<Vector2Int>(forbidden);
            startingHouses = new List<StartingHouse>(houses);
            hasTownHall = townHall.HasValue;
            this.townHall = townHall.HasValue
                ? new TownHallSite { origin = townHall.Value.Origin, orientation = townHall.Value.Orientation }
                : default;
            this.roads = new List<Vector2Int>(roads);
        }
    }
}
