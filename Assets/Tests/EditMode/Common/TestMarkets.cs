using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>The thresholds and prices of the markets the tests build.</summary>
    public static class TestMarkets
    {
        /// <summary>
        /// For a good the city does not produce: T0 at 10 days of consumption, T1 at 30, T2 at
        /// T1 and T3 at 40. A market that starts with 30 days of a good is at its threshold T1.
        /// </summary>
        public static ThresholdRule Thresholds => new ThresholdRule(10, 20, 10, 5, 10, 15, 20);

        /// <summary>
        /// The economy's default hiring, and a starting workforce large enough that every workshop
        /// of a test market starts with its maximum of workers.
        /// </summary>
        public static WorkforceRule Workforce => new WorkforceRule(16, 8, 4, 1, 1, 1000);

        /// <summary>The labour rule with the economy's default settings.</summary>
        public static LabourRule Labour => new LabourRule(4, 50, 3, 8, 5, 10, 5, 3, 0.02, 30);

        /// <summary>The social rule with the economy's default settings.</summary>
        public static SocialRule Social => new SocialRule(50, new PerClass<int>(-13, -18, -27), 2, 1, 9, -40, 80, 40, 320, 160, 10, 100, 30);

        /// <summary>The factors at which the player buys, the economy's defaults.</summary>
        public static PriceCurve BuyCurve => new PriceCurve(4, 1.5, 1, 0.8, 0.6);

        /// <summary>The factors at which the player sells, the economy's defaults.</summary>
        public static PriceCurve SellCurve => new PriceCurve(2, 1.4, 1, 0.7, 0.5);

        public static MarketPricing Pricing => new MarketPricing(BuyCurve, SellCurve);

        /// <summary>
        /// A grid with no forbidden cell and a 3 by 2 town hall at (0, 0), covering x 0 to 2 and y 0 and 1, and no
        /// road yet: a road at (3, 0) joins the network.
        /// </summary>
        public static CityGrid GridWithTownHall(int width, int height)
        {
            var grid = new CityGrid(width, height, new Vector2Int[0]);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, new Footprint(3, 2),
                new GridPlace(Vector2Int.zero, BuildingOrientation.Along));
            return grid;
        }

        /// <summary>
        /// A 40 by 4 grid for a market the player builds in: the town hall of <see cref="GridWithTownHall"/> and a city
        /// road along y = 0 from x = 3 to the edge, which joins it, so that a 1 by 1 building on y = 1 is connected. Give
        /// it, with <see cref="Placement"/>, to a market without housing rules: it then changes nothing but where the
        /// player's buildings stand.
        /// </summary>
        public static CityGrid GridWithRoad()
        {
            CityGrid grid = GridWithTownHall(40, 4);
            var road = new List<Vector2Int>();

            for (int x = 3; x < grid.Width; x++)
            {
                road.Add(new Vector2Int(x, 0));
            }

            grid.PlaceRoads(road, BuildingOwner.City);
            return grid;
        }

        /// <summary>The placement strategy of a market on <see cref="GridWithRoad"/>, which builds no house of its own.</summary>
        public static IPlacementStrategy Placement => new RandomPlacement(new System.Random(1));

        /// <summary>Builds a player's house of 1 by 1 on the first free cell along the road of <see cref="GridWithRoad"/>: it is connected at once.</summary>
        public static void AddPlayerHouse(CityMarket market, SocialClass housedClass, int room)
        {
            market.AddPlayerHouse(housedClass, room, new Footprint(1, 1), PlaceOnTheRoad(market.Grid));
        }

        /// <summary>Builds a player's workshop of 1 by 1 on the first free cell along the road of <see cref="GridWithRoad"/>: it is connected at once.</summary>
        public static void AddPlayerWorkshop(CityMarket market, int good, int jobs)
        {
            market.AddPlayerWorkshop(good, jobs, new Footprint(1, 1), PlaceOnTheRoad(market.Grid));
        }

        /// <summary>The first free cell along the road of <see cref="GridWithRoad"/>, where a building of one cell is connected.</summary>
        public static GridPlace PlaceOnTheRoad(CityGrid grid)
        {
            for (int x = 3; x < grid.Width; x++)
            {
                var place = new GridPlace(new Vector2Int(x, 1), BuildingOrientation.Along);

                if (grid.CanPlace(new Footprint(1, 1), place))
                {
                    return place;
                }
            }

            throw new InvalidOperationException("The road has no free cell left.");
        }
    }
}
