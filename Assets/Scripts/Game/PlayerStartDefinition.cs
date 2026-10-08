using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Static definition of what the player starts the game with. Holds no runtime state:
    /// the current gold is in a <see cref="Treasury"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStart", menuName = "Dark Fantasy Merchant/Player Start")]
    public sealed class PlayerStartDefinition : ScriptableObject
    {
        [Tooltip("Gold coins in the treasury on the first day. Negative to start in debt.")]
        [SerializeField] private long startingGold = 10000;

        public long StartingGold => startingGold;

        public Treasury CreateTreasury()
        {
            return new Treasury(startingGold);
        }
    }
}
