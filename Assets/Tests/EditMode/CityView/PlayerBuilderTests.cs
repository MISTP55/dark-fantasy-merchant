using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class PlayerBuilderTests
    {
        [Test]
        public void BuildingBuiltText_NamesTheCityAndTheBuilding()
        {
            Assert.AreEqual("Construction achevée à Lübeck : Bûcheron.", PlayerBuilder.BuildingBuiltText("Lübeck", "Bûcheron"));
            Assert.AreEqual("Construction achevée à Riga : Maison d'aisés.", PlayerBuilder.BuildingBuiltText("Riga", "Maison d'aisés"));
        }
    }
}
