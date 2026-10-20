using System;
using DarkFantasyMerchant.Core;

namespace DarkFantasyMerchant.Game
{
    /// <summary>The trades in the cities' markets as the debug journal writes them.</summary>
    public static class DebugTradeText
    {
        public static string Operation(TradeRecord trade)
        {
            switch (trade.Kind)
            {
                case PlayerTradeKind.Buy:
                    return "Achat";
                case PlayerTradeKind.Sell:
                    return "Vente";
                default:
                    return "Construction";
            }
        }

        /// <param name="shipName">The name of an AI ship, as the map gives it.</param>
        public static string Trader(TradeRecord trade, Func<Ship, string> shipName)
        {
            if (trade.Trader == Core.Trader.Player)
            {
                return "Joueur";
            }

            return shipName != null ? shipName(trade.Ship) : DebugCitiesTable.NoFigure;
        }

        /// <summary>The gold of a unit, on average, to the nearest.</summary>
        public static string UnitPrice(TradeRecord trade)
        {
            return trade.Units > 0
                ? TreasuryHudController.FormatNumber((long)Math.Round((double)trade.Gold / trade.Units, MidpointRounding.AwayFromZero))
                : DebugCitiesTable.NoFigure;
        }

        /// <summary>The profit of a sale, signed and coloured as the tables write a gain; a dash for none.</summary>
        public static string Profit(TradeRecord trade)
        {
            return trade.Profit.HasValue ? SignedGold(trade.Profit.Value) : DebugCitiesTable.NoFigure;
        }

        /// <summary>Gold or units signed and coloured: "+1 480" in green, "-90" in red, a plain "0".</summary>
        public static string SignedGold(long value)
        {
            string text = TreasuryHudController.FormatNumber(value);

            if (value > 0)
            {
                return $"<color={DebugCitiesTable.PositiveColor}>+{text}</color>";
            }

            return value < 0 ? $"<color={DebugCitiesTable.NegativeColor}>{text}</color>" : text;
        }
    }
}
