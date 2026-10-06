using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Shows the map sprite and one marker per city, and owns the map's selection state.
    /// </summary>
    public sealed class WorldMapView : MonoBehaviour
    {
        [SerializeField] private WorldMapDefinition definition;
        [SerializeField] private SpriteRenderer mapRenderer;
        [SerializeField] private CityMarkerView markerPrefab;
        [SerializeField] private Transform markerRoot;

        [Tooltip("On-screen size, in pixels, of one world unit of marker sprite.")]
        [SerializeField] private float markerScreenPixelsPerUnit = 32f;

        private readonly List<CityDefinition> cities = new List<CityDefinition>();
        private readonly List<Vector2> cityWorldPositions = new List<Vector2>();
        private readonly Dictionary<CityDefinition, CityMarkerView> markers =
            new Dictionary<CityDefinition, CityMarkerView>();

        private CityDefinition hoveredCity;
        private CityDefinition selectedCity;

        public WorldMapDefinition Definition => definition;

        public MapProjection Projection { get; private set; }

        public MapSelectionState<CityDefinition> Selection { get; } = new MapSelectionState<CityDefinition>();

        /// <summary>Valid cities shown on the map.</summary>
        public IReadOnlyList<CityDefinition> Cities => cities;

        /// <summary>World position of each entry of <see cref="Cities"/>, in the same order.</summary>
        public IReadOnlyList<Vector2> CityWorldPositions => cityWorldPositions;

        public bool IsReady => Projection != null;

        private void Awake()
        {
            if (!LayOutMap())
            {
                return;
            }

            SpawnMarkers();
            Selection.HoveredChanged += OnHoveredChanged;
            Selection.SelectedChanged += OnSelectedChanged;
        }

        private void OnDestroy()
        {
            Selection.HoveredChanged -= OnHoveredChanged;
            Selection.SelectedChanged -= OnSelectedChanged;
        }

        /// <summary>
        /// Builds the projection and sizes the map sprite to it. Also called by editor
        /// tooling so the scene shows the map where it will be at runtime.
        /// </summary>
        public bool LayOutMap()
        {
            Projection = null;

            if (definition == null)
            {
                Debug.LogError("WorldMapView has no WorldMapDefinition assigned.", this);
                return false;
            }

            if (mapRenderer == null)
            {
                Debug.LogError("WorldMapView has no map SpriteRenderer assigned.", this);
                return false;
            }

            if (!definition.TryCreateProjection(out MapProjection projection))
            {
                // Do not keep showing a sprite left over from an earlier layout.
                mapRenderer.sprite = null;
                Debug.LogError(
                    $"WorldMapDefinition '{definition.name}' has no map sprite or an invalid world width.",
                    definition);
                return false;
            }

            Projection = projection;

            // Size and center the sprite from its bounds so its resolution, pixels-per-unit
            // and pivot do not matter.
            Sprite sprite = definition.MapSprite;
            float scale = projection.WorldRect.width / sprite.bounds.size.x;
            Transform mapTransform = mapRenderer.transform;

            mapRenderer.sprite = sprite;
            mapTransform.localScale = new Vector3(scale, scale, 1f);
            mapTransform.position = -(sprite.bounds.center * scale);
            return true;
        }

        /// <summary>
        /// Whether the map sprite in the scene already matches what <see cref="LayOutMap"/>
        /// would produce. False after the map image or its world width has changed.
        /// </summary>
        public bool IsMapLaidOut()
        {
            if (definition == null || mapRenderer == null)
            {
                return true;
            }

            if (!definition.TryCreateProjection(out MapProjection projection))
            {
                return mapRenderer.sprite == null;
            }

            Sprite sprite = definition.MapSprite;
            float scale = projection.WorldRect.width / sprite.bounds.size.x;
            Transform mapTransform = mapRenderer.transform;
            Vector3 expectedPosition = -(sprite.bounds.center * scale);

            return mapRenderer.sprite == sprite
                && Mathf.Approximately(mapTransform.localScale.x, scale)
                && Mathf.Approximately(mapTransform.localScale.y, scale)
                && (mapTransform.position - expectedPosition).sqrMagnitude < 1e-6f;
        }

        public Vector2 GetWorldPosition(CityDefinition city)
        {
            return Projection.NormalizedToWorld(city.MapPosition);
        }

        public void SetMarkerScale(float worldUnitsPerPixel)
        {
            float scale = worldUnitsPerPixel * markerScreenPixelsPerUnit;

            foreach (CityMarkerView marker in markers.Values)
            {
                marker.SetBaseScale(scale);
            }
        }

        private void SpawnMarkers()
        {
            if (markerPrefab == null)
            {
                Debug.LogError("WorldMapView has no city marker prefab assigned.", this);
                return;
            }

            IReadOnlyList<CityDefinition> definedCities = definition.Cities;

            for (int i = 0; i < definedCities.Count; i++)
            {
                CityDefinition city = definedCities[i];

                if (city == null)
                {
                    Debug.LogError(
                        $"WorldMapDefinition '{definition.name}' has a null city at index {i}; skipped.",
                        definition);
                    continue;
                }

                if (markers.ContainsKey(city))
                {
                    Debug.LogError(
                        $"WorldMapDefinition '{definition.name}' lists city '{city.name}' twice (index {i}); skipped.",
                        definition);
                    continue;
                }

                Vector2 worldPosition = GetWorldPosition(city);
                CityMarkerView marker = Instantiate(markerPrefab, markerRoot != null ? markerRoot : transform);
                marker.transform.position = worldPosition;
                marker.Initialize(city);

                cities.Add(city);
                cityWorldPositions.Add(worldPosition);
                markers.Add(city, marker);
            }
        }

        private void OnHoveredChanged(CityDefinition city)
        {
            SetMarkerHovered(hoveredCity, false);
            hoveredCity = city;
            SetMarkerHovered(hoveredCity, true);
        }

        private void OnSelectedChanged(CityDefinition city)
        {
            SetMarkerSelected(selectedCity, false);
            selectedCity = city;
            SetMarkerSelected(selectedCity, true);
        }

        private void SetMarkerHovered(CityDefinition city, bool hovered)
        {
            if (city != null && markers.TryGetValue(city, out CityMarkerView marker))
            {
                marker.SetHovered(hovered);
            }
        }

        private void SetMarkerSelected(CityDefinition city, bool selected)
        {
            if (city != null && markers.TryGetValue(city, out CityMarkerView marker))
            {
                marker.SetSelected(selected);
            }
        }
    }
}
