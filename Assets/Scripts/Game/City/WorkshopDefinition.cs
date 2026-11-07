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

        [Tooltip("Gold coins the workshop costs a week to run, apart from its workers' wages.")]
        [SerializeField, Min(0)] private long weeklyRunningCost = 200;

        public GoodDefinition Good => good;

        public int Jobs => jobs;

        public long WeeklyRunningCost => weeklyRunningCost;

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
            else if (weeklyRunningCost < 0)
            {
                problem = "its running cost is negative";
            }

            return problem == null;
        }
    }
}
