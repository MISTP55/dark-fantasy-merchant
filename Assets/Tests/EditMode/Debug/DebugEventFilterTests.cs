using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class DebugEventFilterTests
    {
        private static SimulationEvent Event(SimulationEventType type, int city = 0, int good = 0)
        {
            return new SimulationEvent(1, city, good, type, 1);
        }

        [Test]
        public void Default_ShowsEverything_ButThresholdCrossings()
        {
            var filter = new DebugEventFilter();

            Assert.IsFalse(filter.Matches(Event(SimulationEventType.ThresholdCrossing)));
            Assert.IsTrue(filter.Matches(Event(SimulationEventType.Shortage)));
            Assert.IsTrue(filter.Matches(Event(SimulationEventType.BeggarsLeft, good: -1)));
        }

        [Test]
        public void CityFilter()
        {
            var filter = new DebugEventFilter { City = 2 };

            Assert.IsTrue(filter.Matches(Event(SimulationEventType.Shortage, 2)));
            Assert.IsFalse(filter.Matches(Event(SimulationEventType.Shortage, 1)));
        }

        [Test]
        public void GoodFilter_HidesCityEvents()
        {
            var filter = new DebugEventFilter { Good = 3 };

            Assert.IsTrue(filter.Matches(Event(SimulationEventType.Hired, good: 3)));
            Assert.IsFalse(filter.Matches(Event(SimulationEventType.Hired, good: 1)));
            Assert.IsFalse(filter.Matches(Event(SimulationEventType.PoorLeft, good: -1)));
        }

        [Test]
        public void TypeFilter()
        {
            var filter = new DebugEventFilter();

            filter.SetShown(SimulationEventType.Hired, false);
            filter.SetShown(SimulationEventType.ThresholdCrossing, true);

            Assert.IsFalse(filter.IsShown(SimulationEventType.Hired));
            Assert.IsFalse(filter.Matches(Event(SimulationEventType.Hired)));
            Assert.IsTrue(filter.Matches(Event(SimulationEventType.ThresholdCrossing)));
        }
    }
}
