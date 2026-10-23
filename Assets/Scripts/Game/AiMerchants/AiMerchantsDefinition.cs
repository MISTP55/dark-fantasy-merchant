using System;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Static definition of the merchants that trade on their own: how many ships each city
    /// has, what they sail and how they behave. Holds no runtime state.
    /// </summary>
    [CreateAssetMenu(fileName = "AiMerchants", menuName = "Dark Fantasy Merchant/AI Merchants")]
    public sealed class AiMerchantsDefinition : ScriptableObject
    {
        private const string DefaultShipNameFormat = "Marchand indépendant {0}";

        [Tooltip("AI ships whose home is each city that reaches another by water. Each starts in the port of its home.")]
        [SerializeField, Min(0)] private int shipsPerCity = 4;

        [SerializeField] private ShipDefinition shipDefinition;

        [Tooltip("Color of their sprites, which tells them from the player's ships.")]
        [SerializeField] private Color tint = new Color(0.5f, 0.7f, 1f);

        [Tooltip("Sailors aboard each ship, cut down to its capacity. Half the capacity is full speed.")]
        [SerializeField, Min(1)] private int crew = 14;

        [Tooltip("Days a ship stays in a port before it buys and leaves.")]
        [SerializeField, Min(0f)] private float stopoverDays = 1f;

        [Tooltip("Name of a ship; {0} is its number, from 1.")]
        [SerializeField] private string shipNameFormat = DefaultShipNameFormat;

        public int ShipsPerCity => shipsPerCity;

        public ShipDefinition ShipDefinition => shipDefinition;

        public Color Tint => tint;

        public int Crew => crew;

        public float StopoverDays => stopoverDays;

        /// <param name="number">Number of the ship, from 1.</param>
        /// <returns>The name of the ship; a format without a usable number gets it at its end.</returns>
        public string ShipNameFor(int number)
        {
            string format = string.IsNullOrWhiteSpace(shipNameFormat) ? DefaultShipNameFormat : shipNameFormat;

            if (format.Contains("{0}"))
            {
                try
                {
                    return string.Format(format, number);
                }
                catch (FormatException)
                {
                    // Another placeholder that is not filled: the format is written as it is.
                }
            }

            return $"{format} {number}";
        }

        /// <returns>The crew of one ship, or null without a ship definition that has room for a sailor.</returns>
        public ShipCrew CreateCrew()
        {
            if (shipDefinition == null || shipDefinition.CrewCapacity < 1)
            {
                return null;
            }

            int capacity = shipDefinition.CrewCapacity;

            return new ShipCrew(capacity, Mathf.Clamp(crew, 0, capacity));
        }
    }
}
