using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The player's trading posts, at most one a city: the warehouse that opens in a city once the trading post
    /// stands on its grid. <see cref="WorldEconomy"/> holds the one of the game.
    /// </summary>
    public sealed class PlayerTradingPosts
    {
        private readonly Dictionary<CityDefinition, Warehouse> posts = new Dictionary<CityDefinition, Warehouse>();
        private readonly int capacity;
        private readonly GoodUnits units;

        /// <param name="capacity">The barrels each warehouse stores free of rent.</param>
        /// <param name="units">The room a unit of each good takes; without it, every unit takes one barrel.</param>
        public PlayerTradingPosts(int capacity, GoodUnits units)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            this.capacity = capacity;
            this.units = units;
        }

        /// <summary>Every warehouse opened, in no particular order.</summary>
        public IEnumerable<Warehouse> All => posts.Values;

        /// <summary>Raised with the city once its warehouse is open.</summary>
        public event Action<CityDefinition> Opened;

        /// <returns>The warehouse of the player's trading post in the city, or null when the player has none there.</returns>
        public Warehouse Of(CityDefinition city)
        {
            return city != null && posts.TryGetValue(city, out Warehouse post) ? post : null;
        }

        /// <summary>Opens the city's warehouse, empty, once the trading post stands on the city's grid.</summary>
        /// <exception cref="InvalidOperationException">The city already has one, or the grid has no trading post.</exception>
        public Warehouse Open(CityDefinition city, CityGrid grid)
        {
            if (city == null)
            {
                throw new ArgumentNullException(nameof(city));
            }

            if (posts.ContainsKey(city))
            {
                throw new InvalidOperationException($"'{city.name}' already has the player's trading post.");
            }

            if (grid == null || grid.TradingPost == null)
            {
                throw new InvalidOperationException($"The trading post of '{city.name}' does not stand on its grid.");
            }

            var post = new Warehouse(capacity, units);
            posts[city] = post;
            Opened?.Invoke(city);
            return post;
        }

        /// <summary>Places the trading post on the city's site and opens it, for nothing.</summary>
        /// <returns>False when the city has no grid, no free site or already has one; nothing was done then.</returns>
        public bool TryGive(CityDefinition city, CityGrid grid)
        {
            if (city == null || grid == null || !grid.CanPlaceTradingPost || posts.ContainsKey(city))
            {
                return false;
            }

            grid.PlaceTradingPost();
            Open(city, grid);
            return true;
        }
    }
}
