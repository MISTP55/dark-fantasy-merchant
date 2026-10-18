using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Owns the player's <see cref="Core.Treasury"/>, created before the components that
    /// read it.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerTreasury : MonoBehaviour
    {
        [SerializeField] private PlayerStartDefinition playerStart;

        /// <summary>Null when the player start definition is missing.</summary>
        public Treasury Treasury { get; private set; }

        public bool IsReady => Treasury != null;

        // Awake, not Start: the other components look for the treasury in their OnEnable or Start.
        private void Awake()
        {
            if (playerStart == null)
            {
                Debug.LogError("PlayerTreasury needs a player start definition.", this);
                return;
            }

            Treasury = playerStart.CreateTreasury();
        }
    }
}
