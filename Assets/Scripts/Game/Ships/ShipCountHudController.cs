using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Shows, above each city where the player has ships, how many there are, in any state of the city (queued,
    /// crossing its harbour, in port or leaving). The counts are rewritten when a ship of the player changes state
    /// in a city; the indicators follow the map's camera every frame, and are hidden while it is off (a city view
    /// is open).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ShipCountHudController : MonoBehaviour
    {
        private const string ItemClass = "ship-count-hud__item";
        private const string CountClass = "ship-count-hud__count";

        [SerializeField] private WorldMapView mapView;
        [SerializeField] private ShipsView shipsView;

        [Tooltip("The world map's camera: the indicators stand above the cities it shows, and are hidden while it is off.")]
        [SerializeField] private Camera mapCamera;

        [Tooltip("Gap, in screen pixels, between a city's position and the bottom of its indicator.")]
        [SerializeField, Min(0f)] private float gapPixels = 14f;

        private readonly Dictionary<CityDefinition, VisualElement> indicators =
            new Dictionary<CityDefinition, VisualElement>();

        private VisualElement root;
        private VisualElement container;
        private bool isBound;

        // Start, not OnEnable: the map view lists its cities in its Awake.
        private void Start()
        {
            root = GetComponent<UIDocument>().rootVisualElement;

            if (root == null || mapView == null || shipsView == null || mapCamera == null)
            {
                Debug.LogError("ShipCountHudController needs a UIDocument with a source asset, a map view, a ships view and the map camera.", this);
                enabled = false;
                return;
            }

            container = root.Q<VisualElement>("ship-count-list");

            if (container == null)
            {
                Debug.LogError("ShipCountHud.uxml is missing an expected element.", this);
                enabled = false;
                return;
            }

            // Nothing here is clicked: the map under the indicators stays reachable.
            root.pickingMode = PickingMode.Ignore;

            // Covers the screen whatever else shares the panel.
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;

            shipsView.Docking.CityStateChanged += OnCityStateChanged;
            isBound = true;

            foreach (CityDefinition city in mapView.Cities)
            {
                ShowCount(city);
            }
        }

        private void OnDestroy()
        {
            if (isBound)
            {
                shipsView.Docking.CityStateChanged -= OnCityStateChanged;
                isBound = false;
            }
        }

        // LateUpdate: the camera has moved this frame.
        private void LateUpdate()
        {
            bool isShown = mapCamera.isActiveAndEnabled && indicators.Count > 0;
            root.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;

            if (!isShown || root.panel == null)
            {
                return;
            }

            foreach (KeyValuePair<CityDefinition, VisualElement> entry in indicators)
            {
                Vector3 screen = mapCamera.WorldToScreenPoint(mapView.GetWorldPosition(entry.Key));

                // The screen's y goes up, the panel's down.
                Vector2 panel = RuntimePanelUtils.ScreenToPanel(
                    root.panel,
                    new Vector2(screen.x, Screen.height - screen.y - gapPixels));

                // The indicator's bottom centre is at that point (ShipCountHud.uss).
                entry.Value.style.left = panel.x;
                entry.Value.style.top = panel.y;
            }
        }

        private void OnCityStateChanged(Ship ship, CityDefinition city)
        {
            if (shipsView.IsPlayerShip(ship))
            {
                ShowCount(city);
            }
        }

        private void ShowCount(CityDefinition city)
        {
            if (!mapView.HasCity(city))
            {
                return;
            }

            int count = shipsView.CountPlayerShipsInCity(city);
            indicators.TryGetValue(city, out VisualElement indicator);

            if (count == 0)
            {
                if (indicator != null)
                {
                    indicator.RemoveFromHierarchy();
                    indicators.Remove(city);
                }

                return;
            }

            if (indicator == null)
            {
                indicator = new VisualElement { pickingMode = PickingMode.Ignore };
                indicator.AddToClassList(ItemClass);
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList(CountClass);
                indicator.Add(label);
                container.Add(indicator);
                indicators.Add(city, indicator);
            }

            indicator.Q<Label>().text = NumberText.Format(count);
        }
    }
}
