using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class HorizonChoicesTests
    {
        [Test]
        public void TheChoices_AreMonthsThenTheWholeGame()
        {
            CollectionAssert.AreEqual(
                new[] { "3 mois", "6 mois", "12 mois", "24 mois", "48 mois", "Toute la partie" },
                HorizonChoices.Labels);
        }

        [Test]
        public void TheDefault_IsTwelveMonths()
        {
            Assert.AreEqual("12 mois", HorizonChoices.Labels[HorizonChoices.DefaultIndex]);
        }

        [Test]
        public void DaysOf_CountsTheMonthsOfTheCalendar()
        {
            Assert.AreEqual(360, HorizonChoices.DaysOf(2, 30));
            Assert.AreEqual(90, HorizonChoices.DaysOf(0, 30));
            Assert.AreEqual(48 * 28, HorizonChoices.DaysOf(4, 28));
        }

        [Test]
        public void DaysOf_TheWholeGame_IsNull()
        {
            Assert.IsNull(HorizonChoices.DaysOf(5, 30));
        }
    }
}
