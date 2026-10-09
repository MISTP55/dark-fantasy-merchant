using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Presents the selected ship's panel, at the bottom left of the screen: its name
    /// and its crew. Reads the ship selection state; knows nothing about ship views.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ShipInfoPanelController : MonoBehaviour
    {
        [SerializeField] private ShipsView shipsView;

        private VisualElement shipPanel;
        private Label nameLabel;
        private Label crewLabel;
        private Button closeButton;
        private bool isBound;

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            if (root == null)
            {
                Debug.LogError("ShipInfoPanelController needs a UIDocument with a source asset.", this);
                return;
            }

            // Only the panel itself should block the pointer, not the full-screen root.
            root.pickingMode = PickingMode.Ignore;

            // Covers the screen whatever else shares the panel. Set before anything can
            // fail: a root left in the flow would push the city panel off the center.
            root.style.position = Position.Absolute;
            root.style.left = 0f;
            root.style.top = 0f;
            root.style.right = 0f;
            root.style.bottom = 0f;

            shipPanel = root.Q<VisualElement>("ship-panel");
            nameLabel = root.Q<Label>("ship-name");
            crewLabel = root.Q<Label>("ship-crew");
            closeButton = root.Q<Button>("close-button");

            if (shipsView == null)
            {
                Debug.LogError("ShipInfoPanelController needs a ships view.", this);
                root.style.display = DisplayStyle.None;
                return;
            }

            if (shipPanel == null || nameLabel == null || crewLabel == null || closeButton == null)
            {
                Debug.LogError("ShipInfoPanel.uxml is missing an expected element.", this);
                root.style.display = DisplayStyle.None;
                return;
            }

            // The button is clicked, not navigated: the keyboard pans the map.
            closeButton.focusable = false;

            closeButton.clicked += OnCloseClicked;
            shipsView.Selection.SelectedChanged += ShowSelected;
            isBound = true;

            ShowSelected(shipsView.Selection.Selected);
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            closeButton.clicked -= OnCloseClicked;
            shipsView.Selection.SelectedChanged -= ShowSelected;
            isBound = false;
        }

        /// <summary>The crew as the panel writes it: "Équipage : 12 / 28", the sailors aboard out of the capacity.</summary>
        public static string FormatCrew(ShipCrew crew)
        {
            return $"Équipage : {crew.Count} / {crew.Capacity}";
        }

        /// <summary>The hold as the panels write it: "Cale : 60 / 200 tonneaux", the barrels aboard out of the capacity.</summary>
        public static string FormatCargo(CargoHold hold)
        {
            return $"Cale : {TreasuryHudController.FormatNumber(hold.Used)} / "
                + $"{TreasuryHudController.FormatNumber(hold.Capacity)} tonneaux";
        }

        private void ShowSelected(Ship ship)
        {
            if (ship == null)
            {
                shipPanel.style.display = DisplayStyle.None;
                return;
            }

            // Written on selection: nothing changes a crew yet.
            nameLabel.text = shipsView.DisplayNameOf(ship);
            crewLabel.text = FormatCrew(ship.Crew);
            shipPanel.style.display = DisplayStyle.Flex;
        }

        private void OnCloseClicked()
        {
            shipsView.Selection.ClearSelection();
        }
    }
}
