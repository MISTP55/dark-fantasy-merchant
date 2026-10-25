using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityInfoPanelShipRowTests
    {
        [Test]
        public void ShipStateText_Docked_IsAQuai()
        {
            Assert.AreEqual("à quai", CityInfoPanelController.ShipStateText(HarbourState.Docked));
        }

        [Test]
        public void ShipStateText_Waiting_IsEnAttente()
        {
            Assert.AreEqual("en attente", CityInfoPanelController.ShipStateText(HarbourState.Waiting));
        }

        [Test]
        public void ShipStateText_Arriving_IsArrive()
        {
            Assert.AreEqual("arrive", CityInfoPanelController.ShipStateText(HarbourState.Arriving));
        }

        [Test]
        public void ShipStateText_Leaving_IsRepart()
        {
            Assert.AreEqual("repart", CityInfoPanelController.ShipStateText(HarbourState.Leaving));
        }

        [Test]
        public void ShipRowText_JoinsNameAndState()
        {
            Assert.AreEqual("Aurore — à quai", CityInfoPanelController.ShipRowText("Aurore", HarbourState.Docked));
        }
    }
}
