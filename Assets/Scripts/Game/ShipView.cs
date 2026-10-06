using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>One ship on the world map.</summary>
    public sealed class ShipView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoveredColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.55f, 0.2f);
        [SerializeField] private float selectedScale = 1.25f;

        private Ship ship;
        private ShipDefinition definition;
        private bool isHovered;
        private bool isSelected;
        private float baseScale = 1f;

        public void Initialize(Ship ship, ShipDefinition definition)
        {
            this.ship = ship;
            this.definition = definition;
            name = $"Ship ({definition.DisplayName})";
            Refresh();
        }

        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
            Refresh();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            Refresh();
        }

        /// <summary>Scale that keeps the ship at a constant on-screen size.</summary>
        public void SetBaseScale(float scale)
        {
            baseScale = scale;
            Refresh();
        }

        /// <summary>Shows the ship where it is now, facing its heading.</summary>
        public void Refresh()
        {
            if (ship == null)
            {
                return;
            }

            Vector2 worldPosition = ship.WorldPosition;
            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);

            // A definition with a missing sprite keeps showing the previous heading.
            Sprite sprite = definition.SpriteFor(ship.Heading);

            if (sprite != null)
            {
                spriteRenderer.sprite = sprite;
            }

            spriteRenderer.color = isSelected ? selectedColor : isHovered ? hoveredColor : normalColor;

            float scale = baseScale * (isSelected ? selectedScale : 1f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
