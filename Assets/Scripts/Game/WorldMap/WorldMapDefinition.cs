using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>Static definition of the world map. Holds no runtime state.</summary>
    [CreateAssetMenu(fileName = "WorldMap", menuName = "Dark Fantasy Merchant/World Map")]
    public sealed class WorldMapDefinition : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField] private Sprite mapSprite;

        [Tooltip("Width of the map in world units. Height follows the sprite's aspect ratio.")]
        [SerializeField, Min(0.01f)] private float worldWidth = 40f;

        [Tooltip("The cities of the map. Without a home city, the player starts in the first active one. A city that is not active does not exist in the game.")]
        [SerializeField] private List<MapCity> cities = new List<MapCity>();

        // The active cities, worked out on first use and again after every change of the file.
        private List<CityDefinition> activeCities;

        [Tooltip("Smallest orthographic size the camera may reach when zooming in.")]
        [SerializeField, Min(0.01f)] private float maxZoomInOrthographicSize = 3f;

        [Tooltip("Where ships can sail. Painted with the Navigation Mask tool of the Scene view.")]
        [SerializeField] private NavigationMaskDefinition navigationMask;

        public Sprite MapSprite => mapSprite;

        public float WorldWidth => worldWidth;

        /// <summary>The active cities of the map, in its order: the only cities of the game.</summary>
        public IReadOnlyList<CityDefinition> Cities => activeCities ??= ActiveCitiesOfTheMap();

        /// <summary>Every city of the map, active or not, for the Editor and the content checks.</summary>
        public IReadOnlyList<MapCity> MapCities => cities;

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

        private void OnValidate()
        {
            activeCities = null;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        // A change made through a SerializedObject (the Inspector, an undo, the tests) comes in here.
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            activeCities = null;
        }

        private List<CityDefinition> ActiveCitiesOfTheMap()
        {
            var active = new List<CityDefinition>(cities.Count);

            foreach (MapCity entry in cities)
            {
                // An empty entry stays in, active: WorldMapView reports it rather than hide it.
                if (entry.Active)
                {
                    active.Add(entry.City);
                }
            }

            return active;
        }
    }
}
