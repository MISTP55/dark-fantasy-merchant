using DarkFantasyMerchant.Core;

namespace DarkFantasyMerchant.Game
{
    /// <summary>A house the player can build, as the city panel shows it. A null cost: it cannot be built.</summary>
    public sealed class HouseOffer
    {
        public HouseOffer(SocialClass housedClass, string name, int room, BuildingCost cost, string costText)
        {
            HousedClass = housedClass;
            Name = name;
            Room = room;
            Cost = cost;
            CostText = costText;
        }

        public SocialClass HousedClass { get; }

        public string Name { get; }

        public int Room { get; }

        public BuildingCost Cost { get; }

        /// <summary>"5 000 or, 10 bois, 10 métal", or "indisponible".</summary>
        public string CostText { get; }
    }

    /// <summary>A workshop the player can build, as the city panel shows it. A null cost: it cannot be built.</summary>
    public sealed class WorkshopOffer
    {
        /// <param name="good">The good it makes, by its index among the active goods.</param>
        public WorkshopOffer(int good, string name, int jobs, BuildingCost cost, string costText)
        {
            Good = good;
            Name = name;
            Jobs = jobs;
            Cost = cost;
            CostText = costText;
        }

        /// <summary>The good it makes, by its index among the active goods.</summary>
        public int Good { get; }

        public string Name { get; }

        public int Jobs { get; }

        public BuildingCost Cost { get; }

        /// <summary>"10 000 or, 10 bois, 30 métal", or "indisponible".</summary>
        public string CostText { get; }
    }
}
