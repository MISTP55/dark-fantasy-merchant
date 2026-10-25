using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The city view scene's entry point: <see cref="CityViewLoader"/> hands it the city to show. It writes the
    /// city's name, draws its grid, frames the camera over it, shows the build bar and hands what the player
    /// chooses in it to the placement, which stops when the bar finds it can no longer be paid for; its back button asks the loader to close the view. It reaches the world only through <see cref="SimulationServices.Current"/>.
    /// It shows the harbour's ships, and a click that places nothing selects the ship under it through the map's ship
    /// selection, whose panel shows it over the view, or clears that selection; a selected ship that leaves the grid
    /// for the sea is deselected.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CityViewController : MonoBehaviour
    {
        [SerializeField] private CityGridView gridView;
        [SerializeField] private CityViewCamera viewCamera;
        [SerializeField] private CityPlacementController placement;
        [SerializeField] private HarbourView harbourView;

        private Label nameLabel;
        private Button backButton;
        private CityViewLoader loader;
        private BuildBarController buildBar;
        private bool isBound;

        // The docking whose CityStateChanged is listened to, from Show: the services may not exist at OnEnable.
        private ShipDocking<CityDefinition> docking;

        /// <summary>The city shown, or null before the loader shows one.</summary>
        public CityDefinition City { get; private set; }

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            if (root == null || gridView == null || viewCamera == null || placement == null || harbourView == null)
            {
                Debug.LogError("CityViewController needs a UIDocument with a source asset, a grid view, a camera, a placement and a harbour view.", this);
                return;
            }

            nameLabel = root.Q<Label>("city-name");
            backButton = root.Q<Button>("back-button");

            if (nameLabel == null || backButton == null)
            {
                Debug.LogError("CityView.uxml is missing an expected element.", this);
                return;
            }

            try
            {
                buildBar = new BuildBarController(root);
            }
            catch (System.ArgumentException exception)
            {
                Debug.LogError(exception.Message, this);
                return;
            }

            // Only the view's own elements block the pointer, not the full-screen root.
            root.pickingMode = PickingMode.Ignore;

            // Clicked, not navigated.
            backButton.focusable = false;
            backButton.clicked += OnBackClicked;
            buildBar.HouseChosen += placement.StartPlacing;
            buildBar.WorkshopChosen += placement.StartPlacing;
            buildBar.RoadChosen += placement.StartPlacing;
            buildBar.Refreshed += placement.Refresh;
            placement.ClickedWithoutPlacing += SelectShipAt;
            isBound = true;
            ListenTo(docking);

            if (City != null)
            {
                nameLabel.text = City.DisplayName;
                buildBar.Show(City, SimulationServices.Current);
            }
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            backButton.clicked -= OnBackClicked;
            buildBar.HouseChosen -= placement.StartPlacing;
            buildBar.WorkshopChosen -= placement.StartPlacing;
            buildBar.RoadChosen -= placement.StartPlacing;
            buildBar.Refreshed -= placement.Refresh;
            placement.ClickedWithoutPlacing -= SelectShipAt;
            buildBar.Hide();
            StopListening();
            isBound = false;
        }

        /// <summary>Shows the city, in place of the one shown if any; the back button closes the view through the loader.</summary>
        public void Show(CityDefinition city, CityViewLoader cityLoader)
        {
            City = city;
            loader = cityLoader;

            if (nameLabel != null)
            {
                nameLabel.text = city != null ? city.DisplayName : string.Empty;
            }

            SimulationServices services = SimulationServices.Current;
            CityGrid grid = services != null && services.WorldEconomy != null
                ? services.WorldEconomy.GridOf(city)
                : null;

            if (grid == null && city != null)
            {
                Debug.LogWarning($"CityDefinition '{city.name}' has no grid; the city view shows nothing.", city);
            }

            WorldEconomy worldEconomy = services != null ? services.WorldEconomy : null;
            gridView.Show(grid, worldEconomy != null ? worldEconomy.Buildings : null, worldEconomy != null ? worldEconomy.Economy : null);

            if (placement != null)
            {
                placement.Show(city);
            }

            if (buildBar != null)
            {
                buildBar.Show(city, services);
            }

            if (harbourView != null)
            {
                harbourView.Show(city);
            }
            StopListening();
            docking = services != null && services.ShipsView != null ? services.ShipsView.Docking : null;

            if (isBound)
            {
                ListenTo(docking);
            }

            if (grid != null)
            {
                viewCamera.Frame(CityGridView.WorldBoundsOf(grid));
            }
        }

        /// <summary>
        /// Selects the ship drawn under the screen position (origin at the bottom-left) through the map's ship
        /// selection, or clears that selection when there is none: what a click that places nothing does.
        /// </summary>
        public void SelectShipAt(Vector2 screenPosition)
        {
            ShipsView shipsView = SimulationServices.Current != null ? SimulationServices.Current.ShipsView : null;

            if (shipsView == null)
            {
                return;
            }

            if (harbourView.TryPickAt(viewCamera.ScreenToWorld(screenPosition), out Ship ship))
            {
                shipsView.Selection.Select(ship);
            }
            else
            {
                shipsView.Selection.ClearSelection();
            }
        }

        private void ListenTo(ShipDocking<CityDefinition> shipDocking)
        {
            StopListening();
            docking = shipDocking;

            if (docking != null)
            {
                docking.CityStateChanged += OnCityStateChanged;
            }
        }

        private void StopListening()
        {
            if (docking != null)
            {
                docking.CityStateChanged -= OnCityStateChanged;
            }
        }

        // A selected ship of this city that leaves the grid for the sea closes the ship panel.
        private void OnCityStateChanged(Ship ship, CityDefinition city)
        {
            ShipsView shipsView = SimulationServices.Current != null ? SimulationServices.Current.ShipsView : null;

            if (shipsView != null && ReferenceEquals(city, City) && ReferenceEquals(shipsView.Selection.Selected, ship)
                && docking.StateOf(ship) == HarbourState.AtSea)
            {
                shipsView.Selection.ClearSelection();
            }
        }

        private void OnBackClicked()
        {
            if (loader != null)
            {
                loader.Close();
            }
        }
    }
}
