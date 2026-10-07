using DarkFantasyMerchant.Game;
using UnityEditor;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// The mask's fields are hidden, so the Inspector says what the asset holds and where
    /// to edit it.
    /// </summary>
    [CustomEditor(typeof(NavigationMaskDefinition))]
    public sealed class NavigationMaskDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var mask = (NavigationMaskDefinition)target;

            EditorGUILayout.HelpBox(
                $"{mask.Width} x {mask.Height} cells.\n"
                    + "Paint it in the Scene view with the Navigation Mask tool.",
                MessageType.Info);
        }
    }
}
