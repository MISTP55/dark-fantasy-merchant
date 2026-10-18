namespace DarkFantasyMerchant.Game
{
    /// <summary>The units of goods as the panels write them, in French.</summary>
    public static class GoodUnitText
    {
        public static string SingularOf(GoodUnit unit)
        {
            return unit == GoodUnit.Bushel ? "boisseau" : "tonneau";
        }

        public static string PluralOf(GoodUnit unit)
        {
            return unit == GoodUnit.Bushel ? "boisseaux" : "tonneaux";
        }

        /// <summary>"1 boisseau", "3 boisseaux", "1 200 tonneaux": singular for 0 and 1, as French counts.</summary>
        public static string FormatQuantity(long quantity, GoodUnit unit)
        {
            string name = quantity <= 1 ? SingularOf(unit) : PluralOf(unit);

            return $"{TreasuryHudController.FormatNumber(quantity)} {name}";
        }

        /// <summary>A good's name and its unit: "Céréales (boisseaux)".</summary>
        public static string NameWithUnit(string name, GoodUnit unit)
        {
            return $"{name} ({PluralOf(unit)})";
        }
    }
}
