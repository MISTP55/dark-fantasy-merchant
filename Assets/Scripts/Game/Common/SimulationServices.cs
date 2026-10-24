using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The one place to reach the components that run the world, for code that has no
    /// serialized reference to them, such as the objects of another scene.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SimulationServices : MonoBehaviour
    {
        [SerializeField] private WorldClock worldClock;
        [SerializeField] private PlayerTreasury playerTreasury;
        [SerializeField] private PlayerNotifications playerNotifications;
        [SerializeField] private WorldEconomy worldEconomy;
        [SerializeField] private ShipsView shipsView;
        [SerializeField] private AiMerchants aiMerchants;
        [SerializeField] private PlayerBuilder playerBuilder;

        /// <summary>
        /// The services of the loaded world, null when there is none. A service missing from the
        /// scene is null, which is logged when the world loads.
        /// </summary>
        public static SimulationServices Current { get; private set; }

        public WorldClock WorldClock => worldClock;

        public PlayerTreasury PlayerTreasury => playerTreasury;

        public PlayerNotifications PlayerNotifications => playerNotifications;

        public WorldEconomy WorldEconomy => worldEconomy;

        public ShipsView ShipsView => shipsView;

        public AiMerchants AiMerchants => aiMerchants;

        public PlayerBuilder PlayerBuilder => playerBuilder;

        // Play mode is entered without a domain reload: a static left by the last session
        // would outlive its objects.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCurrent()
        {
            Current = null;
        }

        // Awake, not Start, and before the others: they may look for it in their own Awake.
        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError("There is already a SimulationServices; this one is ignored.", this);
                return;
            }

            if (worldClock == null || playerTreasury == null || playerNotifications == null
                || worldEconomy == null || shipsView == null || aiMerchants == null
                || playerBuilder == null)
            {
                Debug.LogError(
                    "SimulationServices needs the world clock, the player's treasury and notifications, "
                    + "the world economy, the ships view, the AI merchants and the player builder.",
                    this);
            }

            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }
    }
}
