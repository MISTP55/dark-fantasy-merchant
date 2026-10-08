using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ShipArrivalNotifierTests
    {
        [Test]
        public void FormatArrival_NamesTheCity_ForAShipThatEnteredAPort()
        {
            Assert.AreEqual(
                "Navire marchand a jeté l'ancre à Sparia.",
                ShipArrivalNotifier.FormatArrival("Navire marchand", "Sparia"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void FormatArrival_NamesNoCity_ForAShipThatStoppedAtSea(string cityName)
        {
            Assert.AreEqual(
                "Navire marchand a atteint sa destination.",
                ShipArrivalNotifier.FormatArrival("Navire marchand", cityName));
        }
    }
}
