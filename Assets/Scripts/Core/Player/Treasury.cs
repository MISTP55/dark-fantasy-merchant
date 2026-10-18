using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The player's gold. It can go below zero: the player is then in debt. What must be
    /// paid whatever the gold left goes through <see cref="Withdraw"/>, what the player
    /// has to be able to afford through <see cref="TryWithdraw"/>.
    /// </summary>
    public sealed class Treasury
    {
        /// <param name="startingGold">Negative to start in debt.</param>
        public Treasury(long startingGold)
        {
            Gold = startingGold;
        }

        /// <summary>Gold coins owned, or owed when negative.</summary>
        public long Gold { get; private set; }

        public bool IsInDebt => Gold < 0;

        /// <summary>Raised with the new gold, only on an actual change.</summary>
        public event Action<long> Changed;

        /// <summary>
        /// Whether the gold covers the amount. A treasury in debt affords only what is free.
        /// </summary>
        public bool CanAfford(long amount)
        {
            RequireNotNegative(amount);
            return amount == 0 || Gold >= amount;
        }

        public void Deposit(long amount)
        {
            RequireNotNegative(amount);
            SetGold(Gold + amount);
        }

        /// <summary>Takes the amount even when the gold does not cover it, leaving a debt.</summary>
        public void Withdraw(long amount)
        {
            RequireNotNegative(amount);
            SetGold(Gold - amount);
        }

        /// <summary>Takes the amount only when the gold covers it.</summary>
        /// <returns>False when it was refused; nothing changed then.</returns>
        public bool TryWithdraw(long amount)
        {
            if (!CanAfford(amount))
            {
                return false;
            }

            SetGold(Gold - amount);
            return true;
        }

        private void SetGold(long gold)
        {
            if (Gold == gold)
            {
                return;
            }

            Gold = gold;
            Changed?.Invoke(gold);
        }

        private static void RequireNotNegative(long amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }
        }
    }
}
