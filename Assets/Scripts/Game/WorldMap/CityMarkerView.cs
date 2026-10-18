using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>One city marker on the world map.</summary>
    public sealed class CityMarkerView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite villageSprite;
        [SerializeField] private Sprite townSprite;
        [SerializeField] private Sprite capitalSprite;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoveredColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.55f, 0.2f);
        [SerializeField] private float selectedScale = 1.25f;

        private bool isHovered;
        private bool isSelected;
        private float baseScale = 1f;

        public void Initialize(CityDefinition city)
        {
            name = $"CityMarker ({city.DisplayName})";
            spriteRenderer.sprite = SpriteFor(city.Size);
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

        /// <summary>Scale that keeps the marker at a constant on-screen size.</summary>
        public void SetBaseScale(float scale)
        {
            baseScale = scale;
            Refresh();
        }

        private Sprite SpriteFor(CitySize size)
        {
            switch (size)
            {
                case CitySize.Capital:
                    return capitalSprite;
                case CitySize.Town:
                    return townSprite;
                default:
                    return villageSprite;
            }
        }

        private void Refresh()
        {
            spriteRenderer.color = isSelected ? selectedColor : isHovered ? hoveredColor : normalColor;

            float scale = baseScale * (isSelected ? selectedScale : 1f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
