using System.Collections;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The only way into and out of the city view. Opening it clears the map's hover and selections (which closes
    /// its panels), switches off the map's camera (with its audio listener), its input and its interaction, and
    /// loads the city view scene over the map; the simulation keeps running. Closing it unloads the scene, switches
    /// the map back on and selects the city again, whose panel reopens.
    /// Loading and unloading take frames: a request that arrives meanwhile is kept, the latest one winning, and
    /// carried out when the scene is loaded or unloaded.
    /// </summary>
    public sealed class CityViewLoader : MonoBehaviour
    {
        /// <summary>The city view scene, which must be in the build list.</summary>
        public const string SceneName = "CityView";

        [SerializeField] private GameObject mapCamera;
        [SerializeField] private WorldMapInput mapInput;
        [SerializeField] private WorldMapInteraction mapInteraction;
        [SerializeField] private WorldMapView mapView;

        [Tooltip("Optional. Without it, no ship is deselected.")]
        [SerializeField] private ShipsView shipsView;

        [SerializeField] private CityInfoPanelController cityPanel;

        [Tooltip("Optional. With it, starting fast forward closes the view.")]
        [SerializeField] private WorldClock worldClock;

        // The city the view should show, null when it should be closed: what the last request asked for.
        private CityDefinition requestedCity;
        private CityViewController controller;
        private Scene loadedScene;
        private bool isBusy;
        private bool isBound;
        private GameClock clock;

        // Whether the closing view selects its city again on the map: not when fast forward closed it.
        private bool selectOnClose = true;

        /// <summary>True while the city view shows a city.</summary>
        public bool IsOpen => OpenCity != null;

        /// <summary>The city the view shows, or null while it is closed or loading.</summary>
        public CityDefinition OpenCity { get; private set; }

        private void OnEnable()
        {
            if (mapCamera == null || mapInput == null || mapInteraction == null || mapView == null || cityPanel == null)
            {
                Debug.LogError(
                    "CityViewLoader needs the map camera, its input and interaction, the map view and the city panel.",
                    this);
                return;
            }

            cityPanel.ViewCityRequested += Open;

            if (worldClock != null && worldClock.IsReady)
            {
                clock = worldClock.Clock;
                clock.FastForwardChanged += OnFastForwardChanged;
            }

            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            cityPanel.ViewCityRequested -= Open;

            if (clock != null)
            {
                clock.FastForwardChanged -= OnFastForwardChanged;
                clock = null;
            }

            isBound = false;
        }

        /// <summary>Opens the view on the city; while it is open, shows this city instead.</summary>
        public void Open(CityDefinition city)
        {
            if (city == null || !isBound)
            {
                return;
            }

            requestedCity = city;

            if (!isBusy)
            {
                Apply();
            }
        }

        /// <summary>Closes the view, if open, and selects its city on the map again.</summary>
        public void Close()
        {
            Close(true);
        }

        private void Close(bool selectCity)
        {
            selectOnClose = selectCity;
            requestedCity = null;

            if (!isBusy)
            {
                Apply();
            }
        }

        // Fast forward is a mode in which the map is only watched: the view closes and nothing is selected on the
        // map, which the fast forward has just cleared. Raised even while the interaction is switched off.
        private void OnFastForwardChanged(bool isFastForward)
        {
            if (!isFastForward || (!IsOpen && requestedCity == null))
            {
                return;
            }

            Close(false);
        }

        // Carries out the last request, from whatever state the view is in.
        private void Apply()
        {
            if (requestedCity != null)
            {
                if (controller == null)
                {
                    StartCoroutine(Load());
                }
                else if (requestedCity != OpenCity)
                {
                    Show(requestedCity);
                }
            }
            else if (controller != null)
            {
                StartCoroutine(Unload());
            }
        }

        private IEnumerator Load()
        {
            isBusy = true;
            SwitchMap(false);

            AsyncOperation loading = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);

            if (loading == null)
            {
                // Unity has logged why: the scene is not in the build list.
                CityDefinition city = requestedCity;
                isBusy = false;
                requestedCity = null;
                SwitchMap(true);
                mapView.Selection.Select(city);
                yield break;
            }

            yield return loading;

            loadedScene = SceneManager.GetSceneByName(SceneName);
            controller = FindController(loadedScene);
            isBusy = false;

            if (controller == null)
            {
                Debug.LogError($"The scene '{SceneName}' has no CityViewController.", this);
                requestedCity = null;
                StartCoroutine(Unload());
                yield break;
            }

            // Closed while it loaded: unloaded at once.
            if (requestedCity == null)
            {
                Apply();
                yield break;
            }

            Show(requestedCity);
        }

        private IEnumerator Unload()
        {
            isBusy = true;
            CityDefinition closedCity = selectOnClose ? OpenCity : null;
            OpenCity = null;
            controller = null;

            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(loadedScene);
            }

            loadedScene = default;
            isBusy = false;

            // Opened again while it unloaded: the map stays off.
            if (requestedCity != null)
            {
                Apply();
                yield break;
            }

            SwitchMap(true);

            if (closedCity != null)
            {
                mapView.Selection.Select(closedCity);
            }
        }

        private void Show(CityDefinition city)
        {
            OpenCity = city;
            controller.Show(city, this);
        }

        private void SwitchMap(bool on)
        {
            if (!on)
            {
                // Nothing of the map stays hovered or selected under the view: its panels close.
                mapView.Selection.SetHovered(null);
                mapView.Selection.ClearSelection();

                if (shipsView != null)
                {
                    shipsView.Selection.SetHovered(null);
                    shipsView.Selection.ClearSelection();
                }
            }

            // The map's camera carries its audio listener: the view's camera has the only other one.
            mapCamera.SetActive(on);
            mapInput.enabled = on;
            mapInteraction.enabled = on;
        }

        private static CityViewController FindController(Scene scene)
        {
            if (!scene.IsValid())
            {
                return null;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<CityViewController>(true);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
