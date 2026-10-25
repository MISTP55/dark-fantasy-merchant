using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// Regenerates the layout of every city of the map at once (<see cref="CityLayoutEditing.Regenerate"/>), for when
    /// what makes a layout usable changes: hand edits are replaced too.
    /// </summary>
    public static class CityLayoutRegeneration
    {
        [MenuItem("Tools/Dark Fantasy Merchant/Regenerate All City Layouts")]
        public static void RegenerateAll()
        {
            if (!EditorUtility.DisplayDialog(
                "Regenerate all city layouts",
                "The layout of every city of the map is replaced by a generated one, hand edits included. Undo brings it back one city at a time.",
                "Regenerate", "Cancel"))
            {
                return;
            }

            Run();
        }

        /// <summary>The regeneration without the confirmation; returns the number of layouts regenerated.</summary>
        public static int Run()
        {
            var map = CityLayoutWindow.FindAsset<WorldMapDefinition>();
            var economy = CityLayoutWindow.FindAsset<EconomyDefinition>();
            var buildings = CityLayoutWindow.FindAsset<BuildingsDefinition>();

            if (map == null || economy == null || buildings == null)
            {
                Debug.LogError("The map, the economy or the buildings asset is missing: build the scene first.");
                return 0;
            }

            if (!buildings.TryValidate(out string buildingsProblem))
            {
                Debug.LogError($"The buildings asset cannot be used: {buildingsProblem}.");
                return 0;
            }

            NavigationGrid mask = map.NavigationMask != null ? map.NavigationMask.CreateGrid() : null;
            IReadOnlyList<MapCity> cities = map.MapCities;
            int regenerated = 0;

            for (int i = 0; i < cities.Count; i++)
            {
                CityDefinition city = cities[i].City;

                if (city == null || city.Layout == null)
                {
                    continue;
                }

                if (CityLayoutEditing.Regenerate(city.Layout, city, economy, buildings, mask, i, out string problem))
                {
                    regenerated++;
                }
                else
                {
                    Debug.LogError($"The layout of '{city.name}' was not regenerated: {problem}.", city.Layout);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"{regenerated} city layouts regenerated.");
            return regenerated;
        }
    }
}
