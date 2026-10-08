using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Presents the hovered city's name and the selected city's information panel, with
    /// the ships in its port: clicking one selects it, so that it can be ordered out.
    /// Reads the map's selection state; knows nothing about markers.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CityInfoPanelController : MonoBehaviour
    {
        private const string ShipRowClass = "city-panel__ship";
        private const string SelectedShipRowClass = "city-panel__ship--selected";

        [SerializeField] private WorldMapView mapView;
        [SerializeField] private Camera mapCamera;

        [Tooltip("Optional. Without it, the panel lists no ship in port.")]
        [SerializeField] private ShipsView shipsView;

        [Tooltip("Offset of the hover label from the city, in panel units.")]
        [SerializeField] private Vector2 hoverLabelOffset = new Vector2(14f, -30f);

        private VisualElement root;
        private VisualElement cityPanel;
        private Label hoverLabel;
        private Label nameLabel;
        private Label sizeLabel;
        private Label accessLabel;
        private Label descriptionLabel;
        private Label noShipLabel;
        private VisualElement shipList;
        private Button closeButton;
        private bool isBound;

        // Ship of each row of the ship list, in the same order.
        private readonly List<Ship> listedShips = new List<Ship>();

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;

            if (root == null || mapView == null || mapCamera == null)
            {
                Debug.LogError("CityInfoPanelController needs a UIDocument with a source asset, a map view and a camera.", this);
                return;
            }

            cityPanel = root.Q<VisualElement>("city-panel");
            hoverLabel = root.Q<Label>("hover-label");
            nameLabel = root.Q<Label>("city-name");
            sizeLabel = root.Q<Label>("city-size");
            accessLabel = root.Q<Label>("city-access");
            descriptionLabel = root.Q<Label>("city-description");
            noShipLabel = root.Q<Label>("no-ship-label");
            shipList = root.Q<VisualElement>("ship-list");
            closeButton = root.Q<Button>("close-button");

            if (cityPanel == null || hoverLabel == null || nameLabel == null || sizeLabel == null
                || accessLabel == null || descriptionLabel == null || noShipLabel == null
                || shipList == null || closeButton == null)
            {
                Debug.LogError("CityInfoPanel.uxml is missing an expected element.", this);
                return;
            }

            // Only the panel itself should block the pointer, not the full-screen root.
            root.pickingMode = PickingMode.Ignore;

            closeButton.clicked += OnCloseClicked;
            mapView.Selection.HoveredChanged += ShowHovered;
            mapView.Selection.SelectedChanged += ShowSelected;

            if (shipsView != null)
            {
                shipsView.Docking.Docked += OnDockingChanged;
                shipsView.Docking.Undocked += OnDockingChanged;
                shipsView.Selection.SelectedChanged += OnShipSelected;
            }

            isBound = true;

            ShowHovered(mapView.Selection.Hovered);
            ShowSelected(mapView.Selection.Selected);
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            closeButton.clicked -= OnCloseClicked;
            mapView.Selection.HoveredChanged -= ShowHovered;
            mapView.Selection.SelectedChanged -= ShowSelected;

            if (shipsView != null)
            {
                shipsView.Docking.Docked -= OnDockingChanged;
                shipsView.Docking.Undocked -= OnDockingChanged;
                shipsView.Selection.SelectedChanged -= OnShipSelected;
            }

            isBound = false;
        }

        private void LateUpdate()
        {
            if (!isBound || !mapView.IsReady)
            {
                return;
            }

            CityDefinition hovered = mapView.Selection.Hovered;

            if (hovered == null || root.panel == null)
            {
                return;
            }

            Vector2 panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(
                root.panel, mapView.GetWorldPosition(hovered), mapCamera);

            hoverLabel.style.left = panelPosition.x + hoverLabelOffset.x;
            hoverLabel.style.top = panelPosition.y + hoverLabelOffset.y;
        }

        /// <param name="screenPosition">Screen position with the origin at the bottom-left.</param>
        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            if (!isBound || root.panel == null)
            {
                return false;
            }

            // Panel coordinates have their origin at the top-left.
            Vector2 flipped = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, flipped);
            return root.panel.Pick(panelPosition) != null;
        }

        private void ShowHovered(CityDefinition city)
        {
            if (city == null)
            {
                hoverLabel.style.display = DisplayStyle.None;
                return;
            }

            hoverLabel.text = city.DisplayName;
            hoverLabel.style.display = DisplayStyle.Flex;
        }

        private void ShowSelected(CityDefinition city)
        {
            if (city == null)
            {
                cityPanel.style.display = DisplayStyle.None;
                return;
            }

            nameLabel.text = city.DisplayName;
            sizeLabel.text = SizeText(city.Size);
            accessLabel.text = AccessText(city.Access);
            descriptionLabel.text = city.Description;
            ShowShipsIn(city);
            cityPanel.style.display = DisplayStyle.Flex;
        }

        private void ShowShipsIn(CityDefinition city)
        {
            shipList.Clear();
            listedShips.Clear();

            if (shipsView != null)
            {
                shipsView.Docking.GetShipsIn(city, listedShips);
            }

            foreach (Ship ship in listedShips)
            {
                // Clicking the selected ship again deselects it.
                var row = new Button(() => shipsView.Selection.Select(
                    ReferenceEquals(shipsView.Selection.Selected, ship) ? null : ship));
                row.text = shipsView.DisplayNameOf(ship);
                row.AddToClassList(ShipRowClass);
                shipList.Add(row);
            }

            noShipLabel.style.display = listedShips.Count > 0 ? DisplayStyle.None : DisplayStyle.Flex;
            HighlightSelectedShip();
        }

        // Rows are restyled, not rebuilt: a selection comes from a click on one of them.
        private void HighlightSelectedShip()
        {
            Ship selected = shipsView != null ? shipsView.Selection.Selected : null;

            for (int i = 0; i < listedShips.Count; i++)
            {
                shipList[i].EnableInClassList(SelectedShipRowClass, ReferenceEquals(listedShips[i], selected));
            }
        }

        private void OnDockingChanged(Ship ship, CityDefinition city)
        {
            if (ReferenceEquals(city, mapView.Selection.Selected))
            {
                ShowShipsIn(city);
            }
        }

        private void OnShipSelected(Ship ship)
        {
            HighlightSelectedShip();
        }

        private void OnCloseClicked()
        {
            mapView.Selection.ClearSelection();
        }

        private static string SizeText(CitySize size)
        {
            switch (size)
            {
                case CitySize.Capital:
                    return "Capital";
                case CitySize.Town:
                    return "Town";
                default:
                    return "Village";
            }
        }

        private static string AccessText(CityAccess access)
        {
            return access == CityAccess.River ? "River port" : "Coastal port";
        }
    }
}
