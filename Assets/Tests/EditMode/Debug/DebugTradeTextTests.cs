using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class DebugTradeTextTests
    {
        private static string Positive(string text)
        {
            return $"<color={DebugCitiesTable.PositiveColor}>{text}</color>";
        }

        private static string Negative(string text)
        {
            return $"<color={DebugCitiesTable.NegativeColor}>{text}</color>";
        }

        [Test]
        public void AnAiSale()
        {
            var trade = new TradeRecord(3, 0, 0, Trader.AiMerchant, null, PlayerTradeKind.Sell, 40, 2640, 1480);

            Assert.AreEqual("Vente", DebugTradeText.Operation(trade));
            Assert.AreEqual("IA 7", DebugTradeText.Trader(trade, ship => "IA 7"));
            Assert.AreEqual(Positive("+1 480"), DebugTradeText.Profit(trade));
        }

        [Test]
        public void ALoss_IsInRed()
        {
            var trade = new TradeRecord(3, 0, 0, Trader.AiMerchant, null, PlayerTradeKind.Sell, 25, 550, -90);

            Assert.AreEqual(Negative("-90"), DebugTradeText.Profit(trade));
        }

        [Test]
        public void APurchase_HasNoProfit()
        {
            var trade = new TradeRecord(3, 0, 0, Trader.AiMerchant, null, PlayerTradeKind.Buy, 40, 1160, null);

            Assert.AreEqual("Achat", DebugTradeText.Operation(trade));
            Assert.AreEqual(DebugCitiesTable.NoFigure, DebugTradeText.Profit(trade));
        }

        [Test]
        public void APlayerConstruction()
        {
            var trade = new TradeRecord(3, 0, 0, Trader.Player, null, PlayerTradeKind.Construction, 6, 60, null);

            Assert.AreEqual("Joueur", DebugTradeText.Trader(trade, ship => "IA 7"));
            Assert.AreEqual("Construction", DebugTradeText.Operation(trade));
            Assert.AreEqual(DebugCitiesTable.NoFigure, DebugTradeText.Profit(trade));
        }

        [Test]
        public void UnitPrice_IsRounded()
        {
            Assert.AreEqual("29", DebugTradeText.UnitPrice(new TradeRecord(3, 0, 0, Trader.Player, null, PlayerTradeKind.Buy, 40, 1160, null)));
            Assert.AreEqual("30", DebugTradeText.UnitPrice(new TradeRecord(3, 0, 0, Trader.Player, null, PlayerTradeKind.Buy, 2, 59, null)));
        }
    }
}
