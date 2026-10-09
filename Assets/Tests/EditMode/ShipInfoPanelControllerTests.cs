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
        public void FormatCargoLine_WritesAGoodAndItsBarrels()
        {
            Assert.AreEqual("Vin : 20", ShipInfoPanelController.FormatCargoLine("Vin", 20));
            Assert.AreEqual("Blé : 1 200", ShipInfoPanelController.FormatCargoLine("Blé", 1200));
        }
    }
}
