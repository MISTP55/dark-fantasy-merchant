using DarkFantasyMerchant.Core;

namespace DarkFantasyMerchant.Game
{
    /// <summary>A house the player can build, as the city panel shows it. A null cost: it cannot be built.</summary>
    public sealed class HouseOffer
    {
        public HouseOffer(SocialClass housedClass, string name, int room, BuildingCost cost, string costText, Footprint footprint = default)
        {
            Footprint = footprint;
            HousedClass = housedClass;
            Name = name;
            Room = room;
            Cost = cost;
            CostText = costText;
        }

        public SocialClass HousedClass { get; }

        public string Name { get; }

        public int Room { get; }

        /// <summary>The cells the house covers on the city grid.</summary>
        public Footprint Footprint { get; }

        public BuildingCost Cost { get; }

        /// <summary>"5 000 or, 10 bois, 10 métal", or "indisponible".</summary>
        public string CostText { get; }
    }

    /// <summary>The road the player can build, as the city view shows it: a cost per cell. A null cost: it cannot be built.</summary>
    public sealed class RoadOffer
    {
        public RoadOffer(string name, BuildingCost costPerCell, string costText)
        {
            Name = name;
            CostPerCell = costPerCell;
            CostText = costText;
        }

        public string Name { get; }

        /// <summary>The cost of one cell of road.</summary>
        public BuildingCost CostPerCell { get; }

        /// <summary>The cost of one cell, "50 or", or "indisponible".</summary>
        public string CostText { get; }
    }

    /// <summary>The trading post the player can build, one in each city, on its reserved site. A null cost: it cannot be built.</summary>
    public sealed class TradingPostOffer
    {
        public TradingPostOffer(string name, BuildingCost cost, string costText, Footprint footprint)
        {
            Name = name;
            Cost = cost;
            CostText = costText;
            Footprint = footprint;
        }

        public string Name { get; }

        public BuildingCost Cost { get; }

        /// <summary>"5 000 or, 20 bois", or "indisponible".</summary>
        public string CostText { get; }

        /// <summary>The cells the trading post covers, those of the site reserved for it.</summary>
        public Footprint Footprint { get; }
    }

    /// <summary>A workshop the player can build, as the city panel shows it. A null cost: it cannot be built.</summary>
    public sealed class WorkshopOffer
    {
        /// <param name="good">The good it makes, by its index among the active goods.</param>
        public WorkshopOffer(int good, string name, int jobs, BuildingCost cost, string costText, Footprint footprint = default)
        {
            Footprint = footprint;
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

        /// <summary>The cells the workshop covers on the city grid.</summary>
        public Footprint Footprint { get; }

        public BuildingCost Cost { get; }

        /// <summary>"10 000 or, 10 bois, 30 métal", or "indisponible".</summary>
        public string CostText { get; }
    }
}
