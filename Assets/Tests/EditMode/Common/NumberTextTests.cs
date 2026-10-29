using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NumberTextTests
    {
        [Test]
        public void FormatGold_WritesThousandsSeparators()
        {
            Assert.AreEqual("10\u00A0000 or", NumberText.FormatGold(10000));
            Assert.AreEqual("1\u00A0234\u00A0567 or", NumberText.FormatGold(1234567));
        }

        [Test]
        public void FormatGold_WritesSmallAmountsPlainly()
        {
            Assert.AreEqual("0 or", NumberText.FormatGold(0));
            Assert.AreEqual("999 or", NumberText.FormatGold(999));
        }

        [Test]
        public void FormatGold_WritesADebtWithAMinusSign()
        {
            Assert.AreEqual("-1\u00A0250 or", NumberText.FormatGold(-1250));
        }

        [Test]
        public void Format_WritesThousandsSeparators_WithoutAUnit()
        {
            Assert.AreEqual("6\u00A0000", NumberText.Format(6000));
            Assert.AreEqual("340", NumberText.Format(340));
            Assert.AreEqual("0", NumberText.Format(0));
        }
    }
}
