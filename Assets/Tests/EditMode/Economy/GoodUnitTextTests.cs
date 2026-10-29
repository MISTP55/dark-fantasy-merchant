using DarkFantasyMerchant.Core;
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

        [Test]
        public void FormatFill_WritesUsedAndCapacityTheFrenchWay()
        {
            Assert.AreEqual("1 850 / 2 000 tonneaux", GoodUnitText.FormatFill(1850, 2000));
            Assert.AreEqual("0 / 2 000 tonneaux", GoodUnitText.FormatFill(0, 2000));
        }

        [Test]
        public void FormatCargo_WritesTheBarrelsAboard_OutOfTheCapacity()
        {
            var hold = new CargoHold(200);
            hold.TryAdd(3, 60);

            Assert.AreEqual("Cale : 60 / 200 tonneaux", GoodUnitText.FormatCargo(hold));
        }

        [Test]
        public void FormatCargo_WritesThousandsSeparators()
        {
            Assert.AreEqual("Cale : 0 / 1 200 tonneaux", GoodUnitText.FormatCargo(new CargoHold(1200)));
        }
    }
}
