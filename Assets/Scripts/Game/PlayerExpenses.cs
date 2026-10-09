using System;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Makes the player pay the recurring expenses every week of the world clock, from
    /// the player's treasury, and tells the player what was paid.
    /// </summary>
    public sealed class PlayerExpenses : MonoBehaviour
    {
        [SerializeField] private ExpensesDefinition expenses;
        [SerializeField] private WorldClock worldClock;
        [SerializeField] private PlayerTreasury playerTreasury;
        [SerializeField] private ShipsView shipsView;

        [Tooltip("Optional. Without it, the expenses are paid without a word.")]
        [SerializeField] private PlayerNotifications playerNotifications;

        private WeeklyBilling billing;
        private GameClock clock;
        private bool isBound;

        private void OnEnable()
        {
            if (expenses == null || worldClock == null || playerTreasury == null || shipsView == null)
            {
                Debug.LogError(
                    "PlayerExpenses needs an expenses definition, a world clock, the player's treasury and a ships view.",
                    this);
                return;
            }

            if (!worldClock.IsReady || !playerTreasury.IsReady)
            {
                // WorldClock or PlayerTreasury already logged why it has nothing to give.
                return;
            }

            if (billing == null && !TryCreateBilling())
            {
                return;
            }

            clock = worldClock.Clock;
            clock.DayStarted += OnDayStarted;
            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            clock.DayStarted -= OnDayStarted;
            isBound = false;
        }

        /// <summary>
        /// The text of a week's payment: "Dépenses de la semaine : 1 200 or.", the gold
        /// written as the treasury HUD writes it.
        /// </summary>
        public static string FormatPayment(long total)
        {
            return $"Dépenses de la semaine : {TreasuryHudController.FormatGold(total)}.";
        }

        private bool TryCreateBilling()
        {
            try
            {
                billing = expenses.CreateBilling(
                    playerTreasury.Treasury, worldClock.Calendar.DaysPerWeek, CountSailors);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                Debug.LogError(
                    $"ExpensesDefinition '{expenses.name}' or CalendarDefinition '{worldClock.Calendar.name}' has an "
                    + $"invalid value ({exception.ParamName}); the expenses are not paid.",
                    this);
                return false;
            }

            billing.Paid += OnPaid;
            return true;
        }

        private int CountSailors()
        {
            int sailors = 0;

            foreach (Ship ship in shipsView.Ships)
            {
                sailors += ship.Crew.Count;
            }

            return sailors;
        }

        private void OnDayStarted(GameDate date)
        {
            billing.StartDay(clock.ElapsedDays);
        }

        private void OnPaid(WeeklyBill bill)
        {
            if (playerNotifications != null && playerNotifications.IsReady)
            {
                playerNotifications.Feed.Post(FormatPayment(bill.Total));
            }
        }
    }
}
