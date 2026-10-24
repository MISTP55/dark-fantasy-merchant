using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The city view scene's entry point: <see cref="CityViewLoader"/> hands it the city to show. It writes the
    /// city's name, draws its grid, frames the camera over it, and its back button asks the loader to close the
    /// view. It reaches the world only through <see cref="SimulationServices.Current"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CityViewController : MonoBehaviour
    {
        [SerializeField] private CityGridView gridView;
        [SerializeField] private CityViewCamera viewCamera;

        private Label nameLabel;
        private Button backButton;
        private CityViewLoader loader;
        private bool isBound;

        /// <summary>The city shown, or null before the loader shows one.</summary>
        public CityDefinition City { get; private set; }

        private void OnEnable()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            if (root == null || gridView == null || viewCamera == null)
            {
                Debug.LogError("CityViewController needs a UIDocument with a source asset, a grid view and a camera.", this);
                return;
            }

            nameLabel = root.Q<Label>("city-name");
            backButton = root.Q<Button>("back-button");

            if (nameLabel == null || backButton == null)
            {
                Debug.LogError("CityView.uxml is missing an expected element.", this);
                return;
            }

            // Only the view's own elements block the pointer, not the full-screen root.
            root.pickingMode = PickingMode.Ignore;

            // Clicked, not navigated.
            backButton.focusable = false;
            backButton.clicked += OnBackClicked;
            isBound = true;

            if (City != null)
            {
                nameLabel.text = City.DisplayName;
            }
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            backButton.clicked -= OnBackClicked;
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
