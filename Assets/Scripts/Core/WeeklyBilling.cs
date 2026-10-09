using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Makes the player pay the recurring expenses of every week, once its days have
    /// elapsed: the <see cref="WeeklyBill"/> is taken from the treasury when the first day
    /// of the next week starts, whatever the gold left. A player who cannot pay goes into
    /// debt.
    /// </summary>
    public sealed class WeeklyBilling
    {
        private readonly Treasury treasury;
        private readonly int daysPerWeek;
        private readonly long sailorWeeklyWage;
        private readonly Func<int> sailorCount;

        /// <param name="sailorWeeklyWage">Gold coins one sailor is paid for a week.</param>
        /// <param name="sailorCount">
        /// The sailors aboard all the player's ships. Asked on every payment: the bill is
        /// for the sailors aboard when the week ends.
        /// </param>
        public WeeklyBilling(Treasury treasury, int daysPerWeek, long sailorWeeklyWage, Func<int> sailorCount)
        {
            if (daysPerWeek < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(daysPerWeek));
            }

            if (sailorWeeklyWage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sailorWeeklyWage));
            }

            this.treasury = treasury ?? throw new ArgumentNullException(nameof(treasury));
            this.daysPerWeek = daysPerWeek;
            this.sailorWeeklyWage = sailorWeeklyWage;
            this.sailorCount = sailorCount ?? throw new ArgumentNullException(nameof(sailorCount));
        }

        /// <summary>
        /// Raised with the bill of a week, once it has been taken from the treasury. Not
        /// raised for a week in which nothing is owed.
        /// </summary>
        public event Action<WeeklyBill> Paid;

        /// <summary>
        /// To call for every day that starts. Weeks are counted from the first day of the
        /// game, so a day that is not reported shifts none of them: the week it ended is
        /// only left unpaid.
        /// </summary>
        /// <param name="elapsedDays">Whole days since the start of the game, at least one.</param>
        public void StartDay(int elapsedDays)
        {
            if (elapsedDays < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedDays));
            }

            if (elapsedDays % daysPerWeek != 0)
            {
                return;
            }

            var bill = new WeeklyBill(sailorCount(), sailorWeeklyWage);

            if (bill.Total == 0)
            {
                return;
            }

            treasury.Withdraw(bill.Total);
            Paid?.Invoke(bill);
        }
    }
}
