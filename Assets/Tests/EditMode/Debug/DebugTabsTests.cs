using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class DebugTabsTests
    {
        private Button[] buttons;
        private VisualElement[] pages;
        private DebugTabs tabs;

        [SetUp]
        public void SetUp()
        {
            buttons = new[] { new Button(), new Button(), new Button() };
            pages = new[] { new VisualElement(), new VisualElement(), new VisualElement() };
            tabs = new DebugTabs(buttons, pages);
        }

        [Test]
        public void Select_ShowsOnlyItsPage()
        {
            tabs.Select(1);

            Assert.AreEqual(DisplayStyle.None, pages[0].style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, pages[1].style.display.value);
            Assert.AreEqual(DisplayStyle.None, pages[2].style.display.value);
            Assert.AreEqual(1, tabs.Selected);
        }

        [Test]
        public void Select_MarksOnlyItsButton()
        {
            tabs.Select(2);
            tabs.Select(0);

            Assert.IsTrue(buttons[0].ClassListContains(DebugTabs.SelectedClass));
            Assert.IsFalse(buttons[1].ClassListContains(DebugTabs.SelectedClass));
            Assert.IsFalse(buttons[2].ClassListContains(DebugTabs.SelectedClass));
        }

        [Test]
        public void Changed_IsRaisedOnlyOnAChange()
        {
            int changes = 0;
            int last = -1;
            tabs.Changed += index =>
            {
                changes++;
                last = index;
            };

            tabs.Select(1);
            tabs.Select(1);

            Assert.AreEqual(1, changes);
            Assert.AreEqual(1, last);
        }

        [Test]
        public void Buttons_AreNotFocusable()
        {
            foreach (Button button in buttons)
            {
                Assert.IsFalse(button.focusable);
            }
        }

        [Test]
        public void NoTab_IsSelectedAtFirst()
        {
            Assert.AreEqual(-1, tabs.Selected);
        }
    }
}
