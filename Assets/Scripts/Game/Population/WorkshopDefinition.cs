using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>A workshop the player can build: the good it makes and the workers it employs at most.</summary>
    [CreateAssetMenu(fileName = "Workshop", menuName = "Dark Fantasy Merchant/Workshop")]
    public sealed class WorkshopDefinition : BuildingDefinition
    {
        [Tooltip("The good the workshop makes. A good has one workshop at most.")]
        [SerializeField] private GoodDefinition good;

        [Tooltip("Workers the workshop employs at most.")]
        [SerializeField, Min(1)] private int jobs = 30;

        public GoodDefinition Good => good;

        public int Jobs => jobs;

        public override bool TryValidate(out string problem)
        {
            if (!base.TryValidate(out problem))
            {
                return false;
            }

            if (good == null)
            {
                problem = "it makes no good";
            }
            else if (jobs < 1)
            {
                problem = "it has no jobs";
            }

            return problem == null;
        }
    }
}
