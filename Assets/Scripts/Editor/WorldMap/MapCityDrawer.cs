using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>A city of the world map on one line: the box that makes it active, then the city.</summary>
    [CustomPropertyDrawer(typeof(MapCity))]
    public sealed class MapCityDrawer : PropertyDrawer
    {
        private const float ToggleWidth = 18f;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            var active = new PropertyField(property.FindPropertyRelative("active"), string.Empty)
            {
                tooltip = "Active: the city exists in the game. Inactive: it has no marker, no market and no AI ships.",
            };
            active.style.width = ToggleWidth;
            row.Add(active);

            var city = new PropertyField(property.FindPropertyRelative("city"), string.Empty);
            city.style.flexGrow = 1f;
            row.Add(city);

            return row;
        }

        // The Inspector draws with UI Toolkit; this is for the IMGUI ones that still exist.
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var toggle = new Rect(position.x, position.y, ToggleWidth, position.height);
            var field = new Rect(position.x + ToggleWidth, position.y, position.width - ToggleWidth, position.height);

            EditorGUI.PropertyField(toggle, property.FindPropertyRelative("active"), GUIContent.none);
            EditorGUI.PropertyField(field, property.FindPropertyRelative("city"), GUIContent.none);

            EditorGUI.EndProperty();
        }
    }
}
