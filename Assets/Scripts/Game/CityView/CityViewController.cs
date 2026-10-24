using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The city view scene's entry point: <see cref="CityViewLoader"/> hands it the city to show. It writes the
    /// city's name, draws its grid, frames the camera over it, shows the build bar and hands what the player
    /// chooses in it to the placement, which stops when the bar finds it can no longer be paid for; its back button asks the loader to close the view. It reaches the world only through <see cref="SimulationServices.Current"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CityViewController : MonoBehaviour
    {
        [SerializeField] private CityGridView gridView;
        [SerializeField] private CityViewCamera viewCamera;
        [SerializeField] private CityPlacementController placement;

        private Label nameLabel;
        private Button backButton;
        private CityViewLoader loader;
        private BuildBarController buildBar;
        private bool isBound;

        /// <summary>The city shown, or null before the loader shows one.</summary>
        public CityDefinition City { get; private set; }

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            if (root == null || gridView == null || viewCamera == null || placement == null)
            {
                Debug.LogError("CityViewController needs a UIDocument with a source asset, a grid view, a camera and a placement.", this);
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
            buildBar.Refreshed += placement.Refresh;
            isBound = true;

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
            buildBar.Refreshed -= placement.Refresh;
            buildBar.Hide();
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

            gridView.Show(grid);

            if (placement != null)
            {
                placement.Show(city);
            }

            if (buildBar != null)
            {
                buildBar.Show(city, services);
            }

            if (grid != null)
            {
                viewCamera.Frame(CityGridView.WorldBoundsOf(grid));
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
