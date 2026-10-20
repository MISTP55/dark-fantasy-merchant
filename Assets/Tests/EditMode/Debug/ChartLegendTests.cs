using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class ChartLegendTests
    {
        private static readonly ChartLegendEntry[] Entries =
        {
            new ChartLegendEntry("Stock", Color.white, ChartSeriesStyle.Line),
            new ChartLegendEntry("Prix d'achat", Color.red, ChartSeriesStyle.Dashed),
            new ChartLegendEntry("Sous T0", Color.blue, ChartSeriesStyle.StackedArea),
        };

        [Test]
        public void AnEntry_HasItsSwatchInItsColour_AndItsName()
        {
            var legend = new ChartLegend();

            legend.SetEntries(Entries);

            Assert.AreEqual(3, legend.childCount);
            Assert.AreEqual("Prix d'achat", legend[1].Q<Label>().text);
            Assert.IsTrue(legend[1].Q(className: ChartLegend.SwatchClass).ClassListContains(ChartLegend.SwatchClass + "--dashed"));
            Assert.IsTrue(legend[2].Q(className: ChartLegend.SwatchClass).ClassListContains(ChartLegend.SwatchClass + "--area"));
        }

        [Test]
        public void TheSameEntries_KeepTheSameElements()
        {
            var legend = new ChartLegend();
            legend.SetEntries(Entries);
            VisualElement first = legend[0];

            legend.SetEntries((ChartLegendEntry[])Entries.Clone());

            Assert.AreSame(first, legend[0]);
        }

        [Test]
        public void OtherEntries_ReplaceThem()
        {
            var legend = new ChartLegend();
            legend.SetEntries(Entries);

            legend.SetEntries(new[] { new ChartLegendEntry("Ouvriers", Color.green, ChartSeriesStyle.Line) });

            Assert.AreEqual(1, legend.childCount);
            Assert.AreEqual("Ouvriers", legend[0].Q<Label>().text);
        }
    }
}
