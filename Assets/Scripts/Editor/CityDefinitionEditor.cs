using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;

namespace DarkFantasyMerchant.Editor
{
    [CustomEditor(typeof(CityDefinition))]
    [CanEditMultipleObjects]
    public sealed class CityDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            foreach (UnityEngine.Object inspected in targets)
            {
                var city = (CityDefinition)inspected;

                if (!MapProjection.IsInsideMap(city.MapPosition))
                {
                    EditorGUILayout.HelpBox(
                        $"'{city.name}': map position is outside the map (expected 0 to 1 on both axes).",
                        MessageType.Warning);
                }
            }
        }
    }
}
