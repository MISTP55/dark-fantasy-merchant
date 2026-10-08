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
    }
}
