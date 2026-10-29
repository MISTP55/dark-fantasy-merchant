using DarkFantasyMerchant.Core;

namespace DarkFantasyMerchant.Game
{
    /// <summary>The units of goods, and the quantities and fills written in them, as the panels write them, in French.</summary>
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

            return $"{NumberText.Format(quantity)} {name}";
        }

        /// <summary>The fill of a store, a hold or a trading post: "1 850 / 2 000 tonneaux", the barrels of room taken out of the capacity.</summary>
        public static string FormatFill(long used, long capacity)
        {
            return $"{NumberText.Format(used)} / {NumberText.Format(capacity)} tonneaux";
        }

        /// <summary>The fill of a hold as the panels write it: "Cale : 60 / 200 tonneaux".</summary>
        public static string FormatCargo(CargoHold hold)
        {
            return $"Cale : {FormatFill(hold.Used, hold.Capacity)}";
        }
    }
}
