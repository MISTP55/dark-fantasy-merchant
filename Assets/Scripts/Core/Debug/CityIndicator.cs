namespace DarkFantasyMerchant.Core
{
    /// <summary>A figure of a whole city that the debug tools record every day.</summary>
    public enum CityIndicator
    {
        Inhabitants,
        Rich,
        Wealthy,
        Poor,
        RichRoom,
        WealthyRoom,
        PoorRoom,
        RichSatisfaction,
        WealthySatisfaction,
        PoorSatisfaction,
        Beggars,
        BeggarTarget,
        CityHouses,
    }

    /// <summary>A figure of one good in a city that the debug tools record every day.</summary>
    public enum GoodIndicator
    {
        Stock,
        T0,
        T1,
        T2,
        T3,
        BuyPrice,
        SellPrice,

        /// <summary>Units the workshop makes in a week with the workers of the day.</summary>
        NominalWeeklyProduction,

        /// <summary>Units actually produced over the last week.</summary>
        RecentProduction,

        /// <summary>Units the city consumes in a week while it has them.</summary>
        NominalWeeklyConsumption,

        /// <summary>Units actually consumed over the last week.</summary>
        RecentConsumption,

        /// <summary>Units the inhabitants needed and did not get that day.</summary>
        Missing,
        Workers,
        Reserve,

        /// <summary>Units the AI merchants carry to the city.</summary>
        UnderWay,

        /// <summary>Units the workshops take as their inputs in a week with the workers of the day.</summary>
        NominalWeeklyInputs,

        /// <summary>Units the workshops actually took as their inputs over the last week.</summary>
        RecentInputs,
    }
}
