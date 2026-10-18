using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>Static definition of the world map. Holds no runtime state.</summary>
    [CreateAssetMenu(fileName = "WorldMap", menuName = "Dark Fantasy Merchant/World Map")]
    public sealed class WorldMapDefinition : ScriptableObject
    {
        [SerializeField] private Sprite mapSprite;

        [Tooltip("Width of the map in world units. Height follows the sprite's aspect ratio.")]
        [SerializeField, Min(0.01f)] private float worldWidth = 40f;

        [SerializeField] private List<CityDefinition> cities = new List<CityDefinition>();

        [Tooltip("Smallest orthographic size the camera may reach when zooming in.")]
        [SerializeField, Min(0.01f)] private float maxZoomInOrthographicSize = 3f;

        [Tooltip("Where ships can sail. Painted with the Navigation Mask tool of the Scene view.")]
        [SerializeField] private NavigationMaskDefinition navigationMask;

        public Sprite MapSprite => mapSprite;

        public float WorldWidth => worldWidth;

        public IReadOnlyList<CityDefinition> Cities => cities;

        public float MaxZoomInOrthographicSize => maxZoomInOrthographicSize;

        /// <summary>Navigable areas of the map, or null when none has been painted.</summary>
        public NavigationMaskDefinition NavigationMask => navigationMask;

        /// <summary>
        /// Builds the projection for this map. Fails when the sprite is missing or
        /// the world width is not a positive number.
        /// </summary>
        public bool TryCreateProjection(out MapProjection projection)
        {
            projection = null;

            if (mapSprite == null || mapSprite.rect.width <= 0f || mapSprite.rect.height <= 0f)
            {
                return false;
            }

            if (!(worldWidth > 0f) || float.IsInfinity(worldWidth))
            {
                return false;
            }

            projection = new MapProjection(worldWidth, mapSprite.rect.width / mapSprite.rect.height);
            return true;
        }
    }
}
