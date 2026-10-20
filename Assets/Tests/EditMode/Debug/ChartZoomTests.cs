using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ChartZoomTests
    {
        private static readonly ChartCalendar Calendar = new ChartCalendar(932, 12, 30, month => "M" + month);

        private ChartZoom zoom;
        private TimeSeriesChart small;

        private static ChartSeries[] Series(params float[] values)
        {
            return new[] { new ChartSeries { Name = "Stock", Color = Color.white, Style = ChartSeriesStyle.Line, Values = values } };
        }

        [SetUp]
        public void SetUp()
        {
            zoom = new ChartZoom(Calendar);
            small = new TimeSeriesChart("Bière", Calendar);
            small.SetData(10, 3, Series(1f, 2f, 3f), null);
            zoom.Attach(small);
        }

        [Test]
        public void ItIsClosed_AtFirst()
        {
            Assert.IsFalse(zoom.IsOpen);
            Assert.AreEqual(DisplayStyle.None, zoom.style.display.value);
        }

        [Test]
        public void Show_DrawsTheChartLarge_WithItsTitleAndItsData()
        {
            zoom.Show(small);

            Assert.IsTrue(zoom.IsOpen);
            Assert.AreEqual(DisplayStyle.Flex, zoom.style.display.value);
            Assert.AreEqual("Bière", zoom.Chart.Title);
            Assert.AreEqual(10, zoom.Chart.FirstDay);
            Assert.AreEqual(3, zoom.Chart.DayCount);
        }

        [Test]
        public void TheLargeChart_FollowsTheSmallOne()
        {
            zoom.Show(small);

            small.SetData(11, 4, Series(2f, 3f, 4f, 5f), null);

            Assert.AreEqual(11, zoom.Chart.FirstDay);
            Assert.AreEqual(4, zoom.Chart.DayCount);
        }

        [Test]
        public void Close_StopsFollowing()
        {
            zoom.Show(small);

            zoom.Close();
            small.SetData(11, 4, Series(2f, 3f, 4f, 5f), null);

            Assert.IsFalse(zoom.IsOpen);
            Assert.AreEqual(DisplayStyle.None, zoom.style.display.value);
            Assert.AreEqual(3, zoom.Chart.DayCount);
        }

        [Test]
        public void AnotherChart_TakesThePlaceOfTheFirst()
        {
            var other = new TimeSeriesChart("Bois", Calendar);
            other.SetData(10, 2, Series(5f, 6f), null);
            zoom.Attach(other);
            zoom.Show(small);

            zoom.Show(other);
            small.SetData(11, 4, Series(2f, 3f, 4f, 5f), null);

            Assert.AreEqual("Bois", zoom.Chart.Title);
            Assert.AreEqual(2, zoom.Chart.DayCount);
        }

        [Test]
        public void AnAttachedChart_CanBeClicked()
        {
            Assert.IsTrue(small.ClassListContains(ChartZoom.ZoomableClass));
        }

        [Test]
        public void TheLargeChart_HasItsLegend_EvenWhenTheSmallOneHasNone()
        {
            small.ShowsLegend = false;

            zoom.Show(small);

            Assert.IsTrue(zoom.Chart.ShowsLegend);
        }
    }
}
