using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;

namespace DarkFantasyMerchant.Game
{
    /// <summary>The events of the cities' days as the debug journal writes them.</summary>
    public static class DebugEventText
    {
        public static string TypeName(SimulationEventType type)
        {
            switch (type)
            {
                case SimulationEventType.Shortage:
                    return "Pénurie";
                case SimulationEventType.ThresholdCrossing:
                    return "Seuil";
                case SimulationEventType.Hired:
                    return "Embauche";
                case SimulationEventType.LaidOff:
                    return "Licenciement";
                case SimulationEventType.ProductionHalted:
                    return "Arrêt";
                case SimulationEventType.ProductionResumed:
                    return "Reprise";
                case SimulationEventType.PoorLeft:
                    return "Départ";
                case SimulationEventType.ClassChange:
                    return "Classes";
                case SimulationEventType.BeggarsArrived:
                case SimulationEventType.BeggarsLeft:
                    return "Mendiants";
                case SimulationEventType.CityHouseBuilt:
                    return "Maison";
                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        /// <param name="goodNames">The names of the goods, in the catalogue's order.</param>
        public static string Detail(SimulationEvent simulationEvent, IReadOnlyList<string> goodNames)
        {
            if (goodNames == null)
            {
                throw new ArgumentNullException(nameof(goodNames));
            }

            int amount = (int)Math.Round(Math.Abs(simulationEvent.Amount));
            string good = simulationEvent.Good >= 0 && simulationEvent.Good < goodNames.Count ? goodNames[simulationEvent.Good] : string.Empty;

            switch (simulationEvent.Type)
            {
                case SimulationEventType.Shortage:
                    return $"{good} : {DebugCitiesTable.FormatQuantity(simulationEvent.Amount)} manquants";
                case SimulationEventType.ThresholdCrossing:
                    return $"{good} : le stock passe de {BandName(simulationEvent.From)} à {BandName(simulationEvent.To)}";
                case SimulationEventType.Hired:
                    return $"{good} : {Count(amount, "ouvrier")} {Plural(amount, "embauché")}";
                case SimulationEventType.LaidOff:
                    return $"{good} : {Count(amount, "ouvrier")} en réserve";
                case SimulationEventType.ProductionHalted:
                    return $"{good} : production arrêtée (stock au-dessus de T3)";
                case SimulationEventType.ProductionResumed:
                    return $"{good} : production reprise";
                case SimulationEventType.PoorLeft:
                    return $"{Count(amount, "pauvre")} {(amount > 1 ? "quittent" : "quitte")} la ville, "
                        + $"{Count(simulationEvent.To, "ouvrier")} {Plural(simulationEvent.To, "perdu")}";
                case SimulationEventType.ClassChange:
                    string housedClass = ClassName((SocialClass)simulationEvent.To);
                    return simulationEvent.Amount > 0
                        ? $"{Count(amount, "pauvre")} {(amount > 1 ? "deviennent" : "devient")} {housedClass}"
                        : $"{amount} {housedClass} {(amount > 1 ? "redeviennent pauvres" : "redevient pauvre")}";
                case SimulationEventType.BeggarsArrived:
                    return $"{Count(amount, "mendiant")} {(amount > 1 ? "arrivent" : "arrive")}";
                case SimulationEventType.BeggarsLeft:
                    return $"{Count(amount, "mendiant")} {(amount > 1 ? "partent" : "part")}";
                case SimulationEventType.CityHouseBuilt:
                    string ofClass = HouseOf((SocialClass)simulationEvent.To);
                    return amount > 1 ? $"La ville construit {amount} maisons {ofClass}" : $"La ville construit une maison {ofClass}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(simulationEvent));
            }
        }

        public static string BandName(int band)
        {
            switch ((StockBand)band)
            {
                case StockBand.BelowT0:
                    return "sous T0";
                case StockBand.T0ToT1:
                    return "T0–T1";
                case StockBand.T1ToT2:
                    return "T1–T2";
                case StockBand.T2ToT3:
                    return "T2–T3";
                default:
                    return "au-dessus de T3";
            }
        }

        // The class as a plural adjective: "riches", "aisés".
        private static string ClassName(SocialClass housedClass)
        {
            switch (housedClass)
            {
                case SocialClass.Rich:
                    return "riches";
                case SocialClass.Wealthy:
                    return "aisés";
                default:
                    return "pauvres";
            }
        }

        private static string HouseOf(SocialClass housedClass)
        {
            switch (housedClass)
            {
                case SocialClass.Rich:
                    return "de riches";
                case SocialClass.Wealthy:
                    return "d'aisés";
                default:
                    return "de pauvres";
            }
        }

        private static string Count(int count, string noun)
        {
            return $"{TreasuryHudController.FormatNumber(count)} {Plural(count, noun)}";
        }

        private static string Plural(int count, string word)
        {
            return count > 1 ? word + "s" : word;
        }
    }
}
