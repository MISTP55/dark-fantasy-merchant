using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Tells the player when one of their ships reaches the end of its route, at its berth or at sea,
    /// and when it waits for a berth in a city's queue.
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
            shipsView.ShipQueued += OnShipQueued;
            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            shipsView.ShipArrived -= OnShipArrived;
            shipsView.ShipQueued -= OnShipQueued;
            isBound = false;
        }

        /// <summary>
        /// The text of an arrival: "Navire marchand a jeté l'ancre à Bruges." in a port,
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

        /// <summary>
        /// The text of a ship that entered a city's queue: "Navire marchand attend une place au port de Bruges.",
        /// written as <see cref="FormatArrival"/> is.
        /// </summary>
        public static string FormatQueued(string shipName, string cityName)
        {
            return $"{shipName} attend une place au port de {cityName}.";
        }

        private void OnShipQueued(Ship ship, CityDefinition city)
        {
            if (city == null || !shipsView.IsPlayerShip(ship))
            {
                return;
            }

            string shipName = shipsView.DisplayNameOf(ship);

            if (!string.IsNullOrEmpty(shipName))
            {
                playerNotifications.Feed.Post(FormatQueued(shipName, city.DisplayName));
            }
        }

        private void OnShipArrived(Ship ship, CityDefinition city)
        {
            // The other merchants' ships come and go without a word.
            if (!shipsView.IsPlayerShip(ship))
            {
                return;
            }

            string shipName = shipsView.DisplayNameOf(ship);

            if (string.IsNullOrEmpty(shipName))
            {
                return;
            }

            playerNotifications.Feed.Post(FormatArrival(shipName, city != null ? city.DisplayName : null));
        }
    }
}
