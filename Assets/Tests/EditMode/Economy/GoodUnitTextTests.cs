using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class GoodUnitTextTests
    {
        [Test]
        public void AQuantity_IsWrittenWithItsUnit_SingularUpToOne()
        {
            Assert.AreEqual("1 boisseau", GoodUnitText.FormatQuantity(1, GoodUnit.Bushel));
            Assert.AreEqual("3 boisseaux", GoodUnitText.FormatQuantity(3, GoodUnit.Bushel));
            Assert.AreEqual("0 tonneau", GoodUnitText.FormatQuantity(0, GoodUnit.Barrel));
            Assert.AreEqual("1 200 tonneaux", GoodUnitText.FormatQuantity(1200, GoodUnit.Barrel));
        }
    }
}
