using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class TreasuryHudControllerTests
    {
        [Test]
        public void FormatGold_WritesThousandsSeparators()
        {
            Assert.AreEqual("10,000 gold", TreasuryHudController.FormatGold(10000));
            Assert.AreEqual("1,234,567 gold", TreasuryHudController.FormatGold(1234567));
        }

        [Test]
        public void FormatGold_WritesSmallAmountsPlainly()
        {
            Assert.AreEqual("0 gold", TreasuryHudController.FormatGold(0));
            Assert.AreEqual("999 gold", TreasuryHudController.FormatGold(999));
        }

        [Test]
        public void FormatGold_WritesADebtWithAMinusSign()
        {
            Assert.AreEqual("-1,250 gold", TreasuryHudController.FormatGold(-1250));
        }
    }
}
