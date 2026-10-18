using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class MapSelectionStateTests
    {
        private sealed class City
        {
        }

        [Test]
        public void StartsWithNothingHoveredOrSelected()
        {
            var state = new MapSelectionState<City>();

            Assert.IsNull(state.Hovered);
            Assert.IsNull(state.Selected);
        }

        [Test]
        public void SetHovered_UpdatesValueAndRaisesEventOnce()
        {
            var state = new MapSelectionState<City>();
            var city = new City();
            var raised = new List<City>();
            state.HoveredChanged += raised.Add;

            state.SetHovered(city);

            Assert.AreSame(city, state.Hovered);
            Assert.AreEqual(1, raised.Count);
            Assert.AreSame(city, raised[0]);
        }

        [Test]
        public void SetHovered_WithTheSameValue_RaisesNothing()
        {
            var state = new MapSelectionState<City>();
            var city = new City();
            state.SetHovered(city);
            int count = 0;
            state.HoveredChanged += _ => count++;

            state.SetHovered(city);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void SetHovered_ToNull_ClearsAndRaisesWithNull()
        {
            var state = new MapSelectionState<City>();
            state.SetHovered(new City());
            var raised = new List<City>();
            state.HoveredChanged += raised.Add;

            state.SetHovered(null);

            Assert.IsNull(state.Hovered);
            Assert.AreEqual(1, raised.Count);
            Assert.IsNull(raised[0]);
        }

        [Test]
        public void Select_UpdatesValueAndRaisesEventOnce()
        {
            var state = new MapSelectionState<City>();
            var city = new City();
            var raised = new List<City>();
            state.SelectedChanged += raised.Add;

            state.Select(city);

            Assert.AreSame(city, state.Selected);
            Assert.AreEqual(1, raised.Count);
            Assert.AreSame(city, raised[0]);
        }

        [Test]
        public void Select_AnotherItem_ReplacesTheSelection()
        {
            var state = new MapSelectionState<City>();
            var first = new City();
            var second = new City();
            state.Select(first);
            var raised = new List<City>();
            state.SelectedChanged += raised.Add;

            state.Select(second);

            Assert.AreSame(second, state.Selected);
            Assert.AreEqual(1, raised.Count);
            Assert.AreSame(second, raised[0]);
        }

        [Test]
        public void Select_TheSameItem_RaisesNothing()
        {
            var state = new MapSelectionState<City>();
            var city = new City();
            state.Select(city);
            int count = 0;
            state.SelectedChanged += _ => count++;

            state.Select(city);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void ClearSelection_ClearsAndRaisesWithNull()
        {
            var state = new MapSelectionState<City>();
            state.Select(new City());
            var raised = new List<City>();
            state.SelectedChanged += raised.Add;

            state.ClearSelection();

            Assert.IsNull(state.Selected);
            Assert.AreEqual(1, raised.Count);
            Assert.IsNull(raised[0]);
        }

        [Test]
        public void ClearSelection_WhenNothingIsSelected_RaisesNothing()
        {
            var state = new MapSelectionState<City>();
            int count = 0;
            state.SelectedChanged += _ => count++;

            state.ClearSelection();

            Assert.AreEqual(0, count);
        }

        [Test]
        public void HoverAndSelection_AreIndependent()
        {
            var state = new MapSelectionState<City>();
            var hovered = new City();
            var selected = new City();
            int selectedEvents = 0;
            state.SelectedChanged += _ => selectedEvents++;
            state.Select(selected);

            state.SetHovered(hovered);
            state.SetHovered(null);

            Assert.AreSame(selected, state.Selected);
            Assert.AreEqual(1, selectedEvents);
        }
    }
}
