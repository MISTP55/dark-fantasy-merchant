using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>Finds the world map the Scene view tools should work on.</summary>
    public static class WorldMapEditorContext
    {
        public static WorldMapView FindView()
        {
            return Object.FindAnyObjectByType<WorldMapView>();
        }

        /// <summary>
        /// The selected map definition if there is one, otherwise the one shown by the
        /// scene's view. Null when there is neither.
        /// </summary>
        public static WorldMapDefinition FindDefinition(WorldMapView view)
        {
            if (Selection.activeObject is WorldMapDefinition selected)
            {
                return selected;
            }

            return view != null ? view.Definition : null;
        }
    }
}
