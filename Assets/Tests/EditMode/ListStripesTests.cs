using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ListStripesTests
    {
        private static VisualElement ListOf(int rows)
        {
            var list = new VisualElement();

            for (int i = 0; i < rows; i++)
            {
                list.Add(new VisualElement());
            }

            return list;
        }

        [Test]
        public void Apply_StripesEveryOtherRow_FromTheSecond()
        {
            VisualElement list = ListOf(4);

            ListStripes.Apply(list);

            Assert.IsFalse(list[0].ClassListContains(ListStripes.AlternateClass));
            Assert.IsTrue(list[1].ClassListContains(ListStripes.AlternateClass));
            Assert.IsFalse(list[2].ClassListContains(ListStripes.AlternateClass));
            Assert.IsTrue(list[3].ClassListContains(ListStripes.AlternateClass));
        }

        [Test]
        public void Apply_AfterARowIsInsertedAtTheTop_ShiftsTheStripes()
        {
            VisualElement list = ListOf(2);
            ListStripes.Apply(list);

            list.Insert(0, new VisualElement());
            ListStripes.Apply(list);

            Assert.IsFalse(list[0].ClassListContains(ListStripes.AlternateClass));
            Assert.IsTrue(list[1].ClassListContains(ListStripes.AlternateClass));
            Assert.IsFalse(list[2].ClassListContains(ListStripes.AlternateClass));
        }
    }
}
