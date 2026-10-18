using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>A good of the economy's catalogue on one line: the box that makes it active, then the good.</summary>
    [CustomPropertyDrawer(typeof(CatalogueGood))]
    public sealed class CatalogueGoodDrawer : PropertyDrawer
    {
        private const float ToggleWidth = 18f;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            var active = new PropertyField(property.FindPropertyRelative("active"), string.Empty)
            {
                tooltip = "Active: the good exists in the game. Inactive: no city stocks, consumes, produces or trades it.",
            };
            active.style.width = ToggleWidth;
            row.Add(active);

            var good = new PropertyField(property.FindPropertyRelative("good"), string.Empty);
            good.style.flexGrow = 1f;
            row.Add(good);

            return row;
        }

        // The Inspector draws with UI Toolkit; this is for the IMGUI ones that still exist.
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var toggle = new Rect(position.x, position.y, ToggleWidth, position.height);
            var field = new Rect(position.x + ToggleWidth, position.y, position.width - ToggleWidth, position.height);

            EditorGUI.PropertyField(toggle, property.FindPropertyRelative("active"), GUIContent.none);
            EditorGUI.PropertyField(field, property.FindPropertyRelative("good"), GUIContent.none);

            EditorGUI.EndProperty();
        }
    }
}
