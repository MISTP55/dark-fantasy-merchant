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
            Assert.AreEqual("Crew: 12 / 28", ShipInfoPanelController.FormatCrew(new ShipCrew(28, 12)));
        }

        [Test]
        public void FormatCrew_WritesAShipWithoutASailor()
        {
            Assert.AreEqual("Crew: 0 / 28", ShipInfoPanelController.FormatCrew(new ShipCrew(28, 0)));
        }
    }
}
