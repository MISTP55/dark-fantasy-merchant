using System;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Owns the world's <see cref="GameClock"/> and advances it every frame, before the
    /// components that read its time.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class WorldClock : MonoBehaviour
    {
        [SerializeField] private CalendarDefinition calendar;

        /// <summary>Null when the calendar is missing or unusable.</summary>
        public GameClock Clock { get; private set; }

        public CalendarDefinition Calendar => calendar;

        public bool IsReady => Clock != null;

        // Awake, not Start: the other components look for the clock in their OnEnable or Start.
        private void Awake()
        {
            if (calendar == null)
            {
                Debug.LogError("WorldClock needs a calendar definition.", this);
                enabled = false;
                return;
            }

            try
            {
                Clock = calendar.CreateClock();
            }
            catch (ArgumentOutOfRangeException exception)
            {
                Debug.LogError(
                    $"CalendarDefinition '{calendar.name}' has an invalid value ({exception.ParamName}); time does not pass.",
                    calendar);
                enabled = false;
            }
        }

        private void Update()
        {
            // Real time: the clock is what turns it into the world's time.
            Clock.Advance(Time.deltaTime);
        }
    }
}
