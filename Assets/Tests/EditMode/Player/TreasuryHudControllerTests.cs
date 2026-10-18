using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class TreasuryHudControllerTests
    {
        [Test]
        public void FormatGold_WritesThousandsSeparators()
        {
            Assert.AreEqual("10\u00A0000 or", TreasuryHudController.FormatGold(10000));
            Assert.AreEqual("1\u00A0234\u00A0567 or", TreasuryHudController.FormatGold(1234567));
        }

        [Test]
        public void FormatGold_WritesSmallAmountsPlainly()
        {
            Assert.AreEqual("0 or", TreasuryHudController.FormatGold(0));
            Assert.AreEqual("999 or", TreasuryHudController.FormatGold(999));
        }

        [Test]
        public void FormatGold_WritesADebtWithAMinusSign()
        {
            Assert.AreEqual("-1\u00A0250 or", TreasuryHudController.FormatGold(-1250));
        }

        [Test]
        public void FormatNumber_WritesThousandsSeparators_WithoutAUnit()
        {
            Assert.AreEqual("6\u00A0000", TreasuryHudController.FormatNumber(6000));
            Assert.AreEqual("340", TreasuryHudController.FormatNumber(340));
            Assert.AreEqual("0", TreasuryHudController.FormatNumber(0));
        }
    }
}
