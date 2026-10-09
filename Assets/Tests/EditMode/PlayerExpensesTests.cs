using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class PlayerExpensesTests
    {
        [Test]
        public void FormatPayment_GivesTheTotalOfTheWeek()
        {
            Assert.AreEqual("Dépenses de la semaine : 120 or.", PlayerExpenses.FormatPayment(120));
        }

        [Test]
        public void FormatPayment_SeparatesThousandsWithANoBreakSpace()
        {
            Assert.AreEqual("Dépenses de la semaine : 1 200 or.", PlayerExpenses.FormatPayment(1200));
        }
    }
}
