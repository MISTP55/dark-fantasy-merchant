using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class BuildBarTests
    {
        [Test]
        public void OfferText_GivesTheNameAndTheGold()
        {
            Assert.AreEqual("Maison de pauvres\n5 000 or", BuildBarController.OfferText("Maison de pauvres", 5000));
        }
    }
}
