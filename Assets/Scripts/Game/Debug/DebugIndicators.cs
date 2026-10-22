using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;

namespace DarkFantasyMerchant.Game
{
    /// <summary>A figure the debug curves can show: of a whole city, or of one of its goods.</summary>
    public readonly struct DebugIndicator
    {
        public DebugIndicator(CityIndicator city, string name, bool addsUp)
        {
            IsGood = false;
            City = city;
            Good = default;
            Name = name;
            AddsUp = addsUp;
        }

        public DebugIndicator(GoodIndicator good, string name, bool addsUp)
        {
            IsGood = true;
            City = default;
            Good = good;
            Name = name;
            AddsUp = addsUp;
        }

        /// <summary>A figure of a good, which needs one to be chosen.</summary>
        public bool IsGood { get; }

        public CityIndicator City { get; }

        public GoodIndicator Good { get; }

        public string Name { get; }

        /// <summary>The figures of every city add up into the world's: not a satisfaction nor a price.</summary>
        public bool AddsUp { get; }
    }

    /// <summary>Every figure of the record, with its French name, and how to read and write it.</summary>
    public static class DebugIndicators
    {
        /// <summary>Every figure of a city, then of a good, in alphabetical order.</summary>
        public static IReadOnlyList<DebugIndicator> All { get; } = Sorted(new[]
        {
            new DebugIndicator(CityIndicator.Inhabitants, "Habitants", true),
            new DebugIndicator(CityIndicator.Rich, "Riches", true),
            new DebugIndicator(CityIndicator.Wealthy, "Aisés", true),
            new DebugIndicator(CityIndicator.Poor, "Pauvres", true),
            new DebugIndicator(CityIndicator.RichRoom, "Place des riches", true),
            new DebugIndicator(CityIndicator.WealthyRoom, "Place des aisés", true),
            new DebugIndicator(CityIndicator.PoorRoom, "Place des pauvres", true),
            new DebugIndicator(CityIndicator.RichSatisfaction, "Satisfaction des riches", false),
            new DebugIndicator(CityIndicator.WealthySatisfaction, "Satisfaction des aisés", false),
            new DebugIndicator(CityIndicator.PoorSatisfaction, "Satisfaction des pauvres", false),
            new DebugIndicator(CityIndicator.Beggars, "Mendiants", true),
            new DebugIndicator(CityIndicator.BeggarTarget, "Cible des mendiants", true),
            new DebugIndicator(CityIndicator.CityHouses, "Maisons bâties par la ville", true),
            new DebugIndicator(GoodIndicator.Stock, "Stock", true),
            new DebugIndicator(GoodIndicator.T0, "Seuil T0", true),
            new DebugIndicator(GoodIndicator.T1, "Seuil T1", true),
            new DebugIndicator(GoodIndicator.T2, "Seuil T2", true),
            new DebugIndicator(GoodIndicator.T3, "Seuil T3", true),
            new DebugIndicator(GoodIndicator.BuyPrice, "Prix d'achat", false),
            new DebugIndicator(GoodIndicator.SellPrice, "Prix de vente", false),
            new DebugIndicator(GoodIndicator.NominalWeeklyProduction, "Production théorique (semaine)", true),
            new DebugIndicator(GoodIndicator.RecentProduction, "Production réelle (7 jours)", true),
            new DebugIndicator(GoodIndicator.NominalWeeklyConsumption, "Consommation théorique (semaine)", true),
            new DebugIndicator(GoodIndicator.RecentConsumption, "Consommation réelle (7 jours)", true),
            new DebugIndicator(GoodIndicator.NominalWeeklyInputs, "Intrants théoriques (semaine)", true),
            new DebugIndicator(GoodIndicator.RecentInputs, "Intrants réels (7 jours)", true),
            new DebugIndicator(GoodIndicator.Missing, "Manque (jour)", true),
            new DebugIndicator(GoodIndicator.Workers, "Ouvriers", true),
            new DebugIndicator(GoodIndicator.Reserve, "Réserve", true),
            new DebugIndicator(GoodIndicator.UnderWay, "En route (IA)", true),
        });

        /// <param name="index">The day's index in the history.</param>
        /// <param name="good">The good of a good's figure; ignored for a city's.</param>
        public static float ValueOf(SimulationHistory history, int index, int city, DebugIndicator indicator, int good)
        {
            if (history == null)
            {
                throw new ArgumentNullException(nameof(history));
            }

            return indicator.IsGood ? history.ValueOf(index, city, indicator.Good, good) : history.ValueOf(index, city, indicator.City);
        }

        /// <summary>The figure of every city added up, NaN left out; NaN for a figure that does not add up.</summary>
        public static float WorldValueOf(SimulationHistory history, int index, int cityCount, DebugIndicator indicator, int good)
        {
            if (!indicator.AddsUp)
            {
                return float.NaN;
            }

            float sum = 0f;

            for (int city = 0; city < cityCount; city++)
            {
                float value = ValueOf(history, index, city, indicator, good);

                if (!float.IsNaN(value))
                {
                    sum += value;
                }
            }

            return sum;
        }

        /// <summary>A value as the debug tables write its kind: units with a decimal, or whole people, gold and points.</summary>
        public static string Format(DebugIndicator indicator, float value)
        {
            if (float.IsNaN(value))
            {
                return DebugCitiesTable.NoFigure;
            }

            if (indicator.IsGood && indicator.Good != GoodIndicator.BuyPrice && indicator.Good != GoodIndicator.SellPrice
                && indicator.Good != GoodIndicator.Workers && indicator.Good != GoodIndicator.Reserve)
            {
                return DebugCitiesTable.FormatQuantity(value);
            }

            return TreasuryHudController.FormatNumber((long)Math.Round(value));
        }

        private static IReadOnlyList<DebugIndicator> Sorted(DebugIndicator[] indicators)
        {
            var sorted = new List<DebugIndicator>(indicators);
            NameOrder.Sort(sorted, indicator => indicator.Name);
            return sorted;
        }
    }
}
