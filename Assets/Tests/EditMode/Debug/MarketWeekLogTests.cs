using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class MarketWeekLogTests
    {
        private const double Tolerance = 1e-9;

        [Test]
        public void ANewLog_HasNothing()
        {
            var log = new MarketWeekLog(2, 7);

            Assert.AreEqual(0, log.ProducedOf(0), Tolerance);
            Assert.AreEqual(0, log.ConsumedOf(1), Tolerance);
        }

        [Test]
        public void SumsTheDaysOfTheWeek_PerGood()
        {
            var log = new MarketWeekLog(2, 7);

            log.StartDay();
            log.Record(0, 3, 2);
            log.Record(1, 0, 0.5);
            log.StartDay();
            log.Record(0, 1, 2);
            log.Record(1, 0, 0.25);

            Assert.AreEqual(4, log.ProducedOf(0), Tolerance);
            Assert.AreEqual(4, log.ConsumedOf(0), Tolerance);
            Assert.AreEqual(0, log.ProducedOf(1), Tolerance);
            Assert.AreEqual(0.75, log.ConsumedOf(1), Tolerance);
        }

        [Test]
        public void ADayOlderThanAWeek_IsForgotten()
        {
            var log = new MarketWeekLog(1, 3);

            // 1, 2, 4 and 8 produced: the first day leaves when the fourth starts.
            for (int day = 0; day < 4; day++)
            {
                log.StartDay();
                log.Record(0, 1 << day, 10 << day);
            }

            Assert.AreEqual(14, log.ProducedOf(0), Tolerance);
            Assert.AreEqual(140, log.ConsumedOf(0), Tolerance);
        }

        [Test]
        public void AddInputs_AddsUp_WithinADay_AndIsForgottenAfterAWeek()
        {
            var log = new MarketWeekLog(2, 2);

            log.StartDay();
            log.AddInputs(0, 1.5);
            log.AddInputs(0, 0.5);
            log.Record(0, 3, 2);

            Assert.AreEqual(2, log.InputsOf(0), Tolerance);
            Assert.AreEqual(2, log.ConsumedOf(0), Tolerance);
            Assert.AreEqual(0, log.InputsOf(1), Tolerance);

            log.StartDay();
            log.AddInputs(0, 1);
            log.StartDay();

            Assert.AreEqual(1, log.InputsOf(0), Tolerance);
            Assert.Throws<ArgumentOutOfRangeException>(() => log.AddInputs(2, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => log.InputsOf(-1));
            Assert.Throws<InvalidOperationException>(() => new MarketWeekLog(1, 7).AddInputs(0, 1));
        }

        [Test]
        public void ADayThatStarts_HasNothingUntilItIsRecorded()
        {
            var log = new MarketWeekLog(1, 2);

            log.StartDay();
            log.Record(0, 5, 5);
            log.StartDay();
            log.Record(0, 7, 7);

            // The day that takes the place of the first one is empty.
            log.StartDay();

            Assert.AreEqual(7, log.ProducedOf(0), Tolerance);
            Assert.AreEqual(7, log.ConsumedOf(0), Tolerance);
        }

        [Test]
        public void RejectsSizesThatMakeNoSense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketWeekLog(-1, 7));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MarketWeekLog(1, 0));
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void RejectsAGoodOutsideTheCatalogue(int good)
        {
            var log = new MarketWeekLog(1, 7);
            log.StartDay();

            Assert.Throws<ArgumentOutOfRangeException>(() => log.Record(good, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => log.ProducedOf(good));
            Assert.Throws<ArgumentOutOfRangeException>(() => log.ConsumedOf(good));
        }

        [Test]
        public void Record_BeforeAnyDay_Throws()
        {
            var log = new MarketWeekLog(1, 7);

            Assert.Throws<InvalidOperationException>(() => log.Record(0, 1, 1));
        }
    }
}
