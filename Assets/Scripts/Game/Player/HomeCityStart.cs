using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Starts the game in the player's home city: the player's ships lie docked at its berths, the map camera shows
    /// it close up, for the map the player finds on leaving the view, and the city view opens on it.
    /// Runs after the other components' <c>Start</c>: the harbours, the ships and the camera must be ready, and
    /// the player's ships must take their berths before the AI ships, ordered into their ports, enter them.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class HomeCityStart : MonoBehaviour
    {
        [Tooltip("Optional. Without it, the game starts in the first active city of the map.")]
        [SerializeField] private PlayerStartDefinition playerStart;

        [SerializeField] private WorldMapView mapView;
        [SerializeField] private ShipsView shipsView;
        [SerializeField] private WorldMapCameraController cameraController;
        [SerializeField] private CityViewLoader cityViewLoader;

        private void Start()
        {
            if (mapView == null || shipsView == null || cameraController == null || cityViewLoader == null)
            {
                Debug.LogError(
                    "HomeCityStart needs the map view, the ships view, the map camera and the city view loader.", this);
                return;
            }

            if (!mapView.IsReady)
            {
                // WorldMapView already logged why the map could not be laid out.
                return;
            }

            CityDefinition homeCity = PlayerStartDefinition.HomeCityOn(playerStart, mapView);

            if (homeCity == null)
            {
                Debug.LogWarning("HomeCityStart found no city to start in; the game starts on the map.", this);
                return;
            }

            // ShipsView has started them at the home city's anchorage.
            foreach (Ship ship in shipsView.PlayerShips)
            {
                if (!shipsView.Docking.PlaceInPort(ship, homeCity, homeCity.MapPosition))
                {
                    Debug.LogWarning(
                        $"HomeCityStart could not dock a player ship in '{homeCity.name}' (no free berth, or the ship "
                        + "is not at its anchorage); it starts at sea.", this);
                }
            }

            // The loader has logged why it cannot open.
            if (cityViewLoader.Open(homeCity))
            {
                cameraController.ShowCloseUp(mapView.GetWorldPosition(homeCity));
            }
        }
    }
}
