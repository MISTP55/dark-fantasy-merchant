using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ShipInfoPanelControllerTests
    {
        [Test]
        public void FormatCrew_WritesTheSailorsAboard_OutOfTheCapacity()
        {
            Assert.AreEqual("Équipage : 12 / 28", ShipInfoPanelController.FormatCrew(new ShipCrew(28, 12)));
        }

        [Test]
        public void FormatCrew_WritesAShipWithoutASailor()
        {
            Assert.AreEqual("Équipage : 0 / 28", ShipInfoPanelController.FormatCrew(new ShipCrew(28, 0)));
        }

        [Test]
        public void FormatCargo_WritesTheBarrelsAboard_OutOfTheCapacity()
        {
            var hold = new CargoHold(200);
            hold.TryAdd(3, 60);

            Assert.AreEqual("Cale : 60 / 200 tonneaux", ShipInfoPanelController.FormatCargo(hold));
        }

        [Test]
        public void FormatCargo_WritesThousandsSeparators()
        {
            Assert.AreEqual("Cale : 0 / 1 200 tonneaux", ShipInfoPanelController.FormatCargo(new CargoHold(1200)));
        }

        [Test]
        public void FormatCargoLine_WritesAGoodAndItsQuantity_InItsUnit()
        {
            Assert.AreEqual("Vin : 20 tonneaux", ShipInfoPanelController.FormatCargoLine("Vin", 20, GoodUnit.Barrel));
            Assert.AreEqual("Sel : 1 tonneau", ShipInfoPanelController.FormatCargoLine("Sel", 1, GoodUnit.Barrel));
            Assert.AreEqual("Céréales : 1 200 boisseaux", ShipInfoPanelController.FormatCargoLine("Céréales", 1200, GoodUnit.Bushel));
        }

        [Test]
        public void CargoLines_NameTheGoodsAboard_InAlphabeticalOrder()
        {
            var goods = new[]
            {
                TestEconomy.CreateGood("Vin", 220, 7),
                TestEconomy.CreateGood("Poisson", 440, 7),
                TestEconomy.CreateGood("Céréales", 110, 7),
            };

            try
            {
                var hold = new CargoHold(200);
                hold.TryAdd(0, 20);
                hold.TryAdd(2, 3);

                CollectionAssert.AreEqual(
                    new[] { "Céréales : 3 tonneaux", "Vin : 20 tonneaux" },
                    ShipInfoPanelController.CargoLines(goods, hold));
            }
            finally
            {
                foreach (GoodDefinition good in goods)
                {
                    UnityEngine.Object.DestroyImmediate(good);
                }
            }
        }
    }
}
