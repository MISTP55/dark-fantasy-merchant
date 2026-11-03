using System.IO;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>The default layout of a city without one, which the layout window's "Create layout" makes.</summary>
    public static class CityLayoutCreation
    {
        private const string LayoutsFolder = "Assets/Data/CityLayouts";

        /// <summary>
        /// Makes the default layout asset of a city and links it: a generated grid
        /// (<see cref="CityLayoutEditing.TryGenerate"/>) with water on the side of the sea, the port, the town hall,
        /// and just the houses its starting population needs, placed with the seed. A layout asset already at the
        /// city's path is linked instead, its content untouched. False (and an error logged) when the port or the
        /// houses do not fit, or something else holds the path. The caller checks that the city has no layout.
        /// </summary>
        /// <param name="mask">
        /// Where the ships sail on the world map, which puts the water on the side of the sea; null puts it to the south.
        /// </param>
        public static bool TryCreate(
            CityDefinition city, EconomyDefinition economy, BuildingsDefinition buildings, NavigationGrid mask, int seed)
        {
            string path = $"{LayoutsFolder}/{city.name}Layout.asset";
            Object existing = AssetDatabase.LoadMainAssetAtPath(path);

            // A layout made by hand whose link was lost: CreateAsset would replace it.
            if (existing is CityLayoutDefinition existingLayout)
            {
                LinkLayout(city, existingLayout);
                return true;
            }

            if (existing != null)
            {
                Debug.LogError($"CityDefinition '{city.name}': '{path}' is not a city layout; no layout was made.", existing);
                return false;
            }

            if (!CityLayoutEditing.TryGenerate(
                city, economy, buildings, mask, seed, CityLayoutDefinition.DefaultSize, CityLayoutDefinition.DefaultSize,
                out CityGrid grid, out string problem))
            {
                Debug.LogError($"CityDefinition '{city.name}': {problem}; no layout was made.", city);
                return false;
            }

            var layout = ScriptableObject.CreateInstance<CityLayoutDefinition>();
            layout.SetContent(grid);
            Directory.CreateDirectory(LayoutsFolder);
            AssetDatabase.CreateAsset(layout, path);
            LinkLayout(city, layout);
            return true;
        }

        private static void LinkLayout(CityDefinition city, CityLayoutDefinition layout)
        {
            var serialized = new SerializedObject(city);
            serialized.FindProperty("layout").objectReferenceValue = layout;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(city);
        }
    }
}
