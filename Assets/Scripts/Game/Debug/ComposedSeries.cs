using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>A curve of the composed chart: a figure of a city, or of one of its goods.</summary>
    public readonly struct ComposedSeries
    {
        /// <param name="good">The good of a good's figure; ignored for a city's.</param>
        public ComposedSeries(int city, DebugIndicator indicator, int good)
        {
            City = city;
            Indicator = indicator;
            Good = indicator.IsGood ? good : -1;
        }

        /// <summary>The city's index in the record.</summary>
        public int City { get; }

        public DebugIndicator Indicator { get; }

        /// <summary>The good's index; -1 for a figure of the city.</summary>
        public int Good { get; }

        public bool IsSameAs(ComposedSeries other)
        {
            return City == other.City && Good == other.Good && Indicator.IsGood == other.Indicator.IsGood
                && (Indicator.IsGood ? Indicator.Good == other.Indicator.Good : Indicator.City == other.Indicator.City);
        }
    }

    /// <summary>The curves of the composed chart, in the order they were added, each with its colour.</summary>
    public sealed class ComposedSeriesList
    {
        private readonly List<ComposedSeries> items = new List<ComposedSeries>();

        public IReadOnlyList<ComposedSeries> Items => items;

        /// <returns>False for a curve already there.</returns>
        public bool TryAdd(ComposedSeries series)
        {
            if (items.Exists(item => item.IsSameAs(series)))
            {
                return false;
            }

            items.Add(series);
            return true;
        }

        public void Remove(int index)
        {
            items.RemoveAt(index);
        }

        /// <summary>The colour of the curve at an index: the palette's, in turn.</summary>
        public Color ColorOf(int index)
        {
            return DebugChartColors.Palette[index % DebugChartColors.Palette.Length];
        }

        /// <summary>A curve as its chip names it: "Corvaux · Blé · Prix d'achat", without a good for a figure of the city.</summary>
        public static string LabelOf(ComposedSeries series, IReadOnlyList<string> cityNames, IReadOnlyList<string> goodNames)
        {
            if (cityNames == null)
            {
                throw new ArgumentNullException(nameof(cityNames));
            }

            if (goodNames == null)
            {
                throw new ArgumentNullException(nameof(goodNames));
            }

            return series.Indicator.IsGood
                ? $"{cityNames[series.City]} · {goodNames[series.Good]} · {series.Indicator.Name}"
                : $"{cityNames[series.City]} · {series.Indicator.Name}";
        }
    }
}
