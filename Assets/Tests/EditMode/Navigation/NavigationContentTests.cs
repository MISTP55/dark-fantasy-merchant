using System.Collections.Generic;
using System.Diagnostics;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>Checks routes between the cities of the real map, on its real mask.</summary>
    public class NavigationContentTests
    {
        private const string MapPath = "Assets/Data/WorldMap/WorldMap.asset";

        [Test]
        public void RoutesBetweenCities_StayOnWater()
        {
            var map = AssetDatabase.LoadAssetAtPath<WorldMapDefinition>(MapPath);
            Assert.IsNotNull(map, MapPath);
            Assert.IsNotNull(map.NavigationMask, "the map has no navigation mask");

            NavigationGrid grid = map.NavigationMask.CreateGrid();
            var pathfinder = new NavigationPathfinder(grid);
            Assert.IsTrue(pathfinder.HasNavigableCells, "the navigation mask is empty");

            // Where a ship that starts on each city is put.
            var harbours = new List<Vector2>();
            var names = new List<string>();

            foreach (CityDefinition city in map.Cities)
            {
                Assert.IsTrue(pathfinder.TryGetNearestNavigable(city.MapPosition, out Vector2 harbour), city.name);
                harbours.Add(harbour);
                names.Add(city.name);
            }

            var waypoints = new List<Vector2>();

            // The first search allocates the pathfinder's buffers; keep it out of the timings.
            pathfinder.TryFindPath(harbours[0], harbours[harbours.Count - 1], waypoints);

            double longest = 0.0;
            string longestRoute = "";

            for (int from = 0; from < harbours.Count; from++)
            {
                for (int to = 0; to < harbours.Count; to++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    bool found = pathfinder.TryFindPath(harbours[from], harbours[to], waypoints);
                    stopwatch.Stop();

                    string route = $"{names[from]} to {names[to]}";
                    Assert.IsTrue(found, route);

                    // A route always ends somewhere: a city cut off from the others
                    // would give one that stops on the nearest shore of the start's sea.
                    // Entering a port relies on this too: a ship docks only where
                    // its route ends on the city's harbour.
                    Vector2 end = waypoints.Count > 0 ? waypoints[waypoints.Count - 1] : harbours[from];
                    Assert.AreEqual(harbours[to], end, $"{route}: the ship cannot sail there");

                    Vector2 previous = harbours[from];

                    for (int i = 0; i < waypoints.Count; i++)
                    {
                        Assert.IsTrue(
                            NavigationLineOfSight.IsClear(grid, previous, waypoints[i]),
                            $"{route}: leg {i} touches land");
                        previous = waypoints[i];
                    }

                    if (stopwatch.Elapsed.TotalMilliseconds > longest)
                    {
                        longest = stopwatch.Elapsed.TotalMilliseconds;
                        longestRoute = $"{route} ({waypoints.Count} waypoints)";
                    }
                }
            }

            Debug.Log($"Slowest route between cities: {longest:F1} ms, {longestRoute}.");
        }

        /// <summary>
        /// The player can click anywhere: times orders between points spread over the
        /// whole map, on land and on water, in every sea.
        /// </summary>
        [Test]
        public void OrdersAcrossTheWholeMap_StayOnWater()
        {
            const int Columns = 6;
            const int Rows = 5;

            var map = AssetDatabase.LoadAssetAtPath<WorldMapDefinition>(MapPath);
            Assert.IsNotNull(map, MapPath);
            Assert.IsNotNull(map.NavigationMask, "the map has no navigation mask");

            NavigationGrid grid = map.NavigationMask.CreateGrid();
            var pathfinder = new NavigationPathfinder(grid);
            var points = new List<Vector2>();

            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    points.Add(new Vector2((column + 0.5f) / Columns, (row + 0.5f) / Rows));
                }
            }

            var waypoints = new List<Vector2>();

            // The first search allocates the pathfinder's buffers; keep it out of the timings.
            pathfinder.TryFindPath(points[0], points[points.Count - 1], waypoints);

            double longest = 0.0;
            double total = 0.0;
            int slowOrders = 0;
            string longestOrder = "";

            for (int from = 0; from < points.Count; from++)
            {
                // A ship is always on water.
                Assert.IsTrue(pathfinder.TryGetNearestNavigable(points[from], out Vector2 start));

                for (int to = 0; to < points.Count; to++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    bool found = pathfinder.TryFindPath(start, points[to], waypoints);
                    stopwatch.Stop();

                    string order = $"{start} to {points[to]}";
                    Assert.IsTrue(found, order);

                    Vector2 previous = start;

                    for (int i = 0; i < waypoints.Count; i++)
                    {
                        Assert.IsTrue(
                            NavigationLineOfSight.IsClear(grid, previous, waypoints[i]),
                            $"{order}: leg {i} touches land");
                        previous = waypoints[i];
                    }

                    double elapsed = stopwatch.Elapsed.TotalMilliseconds;
                    total += elapsed;

                    if (elapsed > 50.0)
                    {
                        slowOrders++;
                    }

                    if (elapsed > longest)
                    {
                        longest = elapsed;
                        longestOrder = $"{order} ({waypoints.Count} waypoints)";
                    }
                }
            }

            int count = points.Count * points.Count;
            Debug.Log(
                $"Slowest order across the map: {longest:F1} ms, {longestOrder}. "
                + $"Mean {total / count:F1} ms over {count} orders, {slowOrders} over 50 ms.");
        }
    }
}
