using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Presents the selected ship's panel, at the bottom left of the screen: its name,
    /// its crew and its cargo. Reads the ship selection state; knows nothing about ship views.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ShipInfoPanelController : MonoBehaviour
    {
        private const string CargoLineClass = "ship-panel__cargo-line";

        [SerializeField] private ShipsView shipsView;

        [Tooltip("Optional. Without it, the panel shows how full the hold is, not what it holds.")]
        [SerializeField] private EconomyDefinition economy;

        private VisualElement shipPanel;
        private Label nameLabel;
        private Label crewLabel;
        private Label cargoLabel;
        private VisualElement cargoList;

        // The hold the panel listens to: the shown ship's.
        private CargoHold shownCargo;
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
            cargoLabel = root.Q<Label>("ship-cargo");
            cargoList = root.Q<VisualElement>("ship-cargo-list");
            closeButton = root.Q<Button>("close-button");

            if (shipsView == null)
            {
                Debug.LogError("ShipInfoPanelController needs a ships view.", this);
                root.style.display = DisplayStyle.None;
                return;
            }

            if (shipPanel == null || nameLabel == null || crewLabel == null || closeButton == null
                || cargoLabel == null || cargoList == null)
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
            ListenTo(null);
            isBound = false;
        }

        /// <summary>The crew as the panel writes it: "Équipage : 12 / 28", the sailors aboard out of the capacity.</summary>
        public static string FormatCrew(ShipCrew crew)
        {
            return $"Équipage : {crew.Count} / {crew.Capacity}";
        }

        /// <summary>A line per good aboard (<see cref="FormatCargoLine"/>), in the alphabetical order of the goods.</summary>
        /// <param name="goods">The goods of the economy, by the hold's indices.</param>
        public static List<string> CargoLines(IReadOnlyList<GoodDefinition> goods, CargoHold hold)
        {
            var aboard = new List<int>();

            for (int i = 0; i < goods.Count; i++)
            {
                if (goods[i] != null && hold.UnitsOf(i) >= 1)
                {
                    aboard.Add(i);
                }
            }

            NameOrder.Sort(aboard, good => goods[good].DisplayName);

            var lines = new List<string>();

            foreach (int good in aboard)
            {
                lines.Add(FormatCargoLine(goods[good].DisplayName, hold.UnitsOf(good), goods[good].Unit));
            }

            return lines;
        }

        /// <summary>One good of the hold as the panel writes it: "Vin : 20 tonneaux", "Céréales : 3 boisseaux".</summary>
        public static string FormatCargoLine(string goodName, int quantity, GoodUnit unit)
        {
            return $"{goodName} : {GoodUnitText.FormatQuantity(quantity, unit)}";
        }

        private void ShowSelected(Ship ship)
        {
            ListenTo(ship?.Cargo);

            if (ship == null)
            {
                shipPanel.style.display = DisplayStyle.None;
                return;
            }

            // Written on selection: nothing changes a crew yet. A crew that changes will
            // need an event to refresh this label.
            nameLabel.text = shipsView.DisplayNameOf(ship);
            crewLabel.text = FormatCrew(ship.Crew);
            ShowCargo();
            shipPanel.style.display = DisplayStyle.Flex;
        }

        private void ListenTo(CargoHold cargo)
        {
            if (shownCargo != null)
            {
                shownCargo.Changed -= ShowCargo;
            }

            shownCargo = cargo;

            if (shownCargo != null)
            {
                shownCargo.Changed += ShowCargo;
            }
        }

        // Rebuilt on every change: a hold has a line per good aboard, a handful at most.
        private void ShowCargo()
        {
            cargoLabel.text = GoodUnitText.FormatCargo(shownCargo);
            cargoList.Clear();

            if (economy == null)
            {
                return;
            }

            foreach (string text in CargoLines(economy.Goods, shownCargo))
            {
                var line = new Label(text);
                line.AddToClassList(CargoLineClass);
                cargoList.Add(line);
            }

            ListStripes.Apply(cargoList);
        }

        private void OnCloseClicked()
        {
            shipsView.Selection.ClearSelection();
        }
    }
}
