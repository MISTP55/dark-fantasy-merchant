using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>Whether the pointer is over the game's UI, which the map and the city view then leave alone.</summary>
    public static class UiPointer
    {
        /// <summary>
        /// Whether a screen position is over an element of the document's panel: over the document itself or any
        /// other document on the same panel settings, which every game document shares. False while the document
        /// has no panel.
        /// </summary>
        /// <param name="screenPosition">Screen position with the origin at the bottom-left.</param>
        public static bool IsOver(UIDocument document, Vector2 screenPosition)
        {
            IPanel panel = document != null && document.rootVisualElement != null
                ? document.rootVisualElement.panel
                : null;

            if (panel == null)
            {
                return false;
            }

            // Panel coordinates have their origin at the top-left.
            Vector2 flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(panel, flipped);
            return panel.Pick(panelPosition) != null;
        }
    }
}
