using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Tells the player when a ship reaches the end of its route, in a port or at sea.
    /// </summary>
    public sealed class ShipArrivalNotifier : MonoBehaviour
    {
        [SerializeField] private ShipsView shipsView;
        [SerializeField] private PlayerNotifications playerNotifications;

        private bool isBound;

        private void OnEnable()
        {
            if (shipsView == null || playerNotifications == null)
            {
                Debug.LogError("ShipArrivalNotifier needs a ships view and the player's notifications.", this);
                return;
            }

            if (!playerNotifications.IsReady)
            {
                // PlayerNotifications already logged why there is no feed.
                return;
            }

            shipsView.ShipArrived += OnShipArrived;
            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            shipsView.ShipArrived -= OnShipArrived;
            isBound = false;
        }

        /// <summary>
        /// The text of an arrival: "Navire marchand a jeté l'ancre à Sparia." in a port,
        /// "Navire marchand a atteint sa destination." at sea. Written without an article
        /// or a word that agrees with the ship's name or elides before the city's.
        /// </summary>
        /// <param name="cityName">Null or empty for a ship that stopped at sea.</param>
        public static string FormatArrival(string shipName, string cityName)
        {
            return string.IsNullOrEmpty(cityName)
                ? $"{shipName} a atteint sa destination."
                : $"{shipName} a jeté l'ancre à {cityName}.";
        }

        private void OnShipArrived(Ship ship, CityDefinition city)
        {
            string shipName = shipsView.DisplayNameOf(ship);

            if (string.IsNullOrEmpty(shipName))
            {
                return;
            }

            playerNotifications.Feed.Post(FormatArrival(shipName, city != null ? city.DisplayName : null));
        }
    }
}
