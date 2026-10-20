using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class DebugEventTextTests
    {
        private static readonly string[] Goods = { "Blé" };

        private static string Detail(SimulationEventType type, double amount, int from = 0, int to = 0, int good = 0)
        {
            return DebugEventText.Detail(new SimulationEvent(10, 0, good, type, amount, from, to), Goods);
        }

        [Test]
        public void TheTypes_HaveFrenchNames()
        {
            Assert.AreEqual("Pénurie", DebugEventText.TypeName(SimulationEventType.Shortage));
            Assert.AreEqual("Seuil", DebugEventText.TypeName(SimulationEventType.ThresholdCrossing));
            Assert.AreEqual("Embauche", DebugEventText.TypeName(SimulationEventType.Hired));
            Assert.AreEqual("Licenciement", DebugEventText.TypeName(SimulationEventType.LaidOff));
            Assert.AreEqual("Arrêt", DebugEventText.TypeName(SimulationEventType.ProductionHalted));
            Assert.AreEqual("Reprise", DebugEventText.TypeName(SimulationEventType.ProductionResumed));
            Assert.AreEqual("Départ", DebugEventText.TypeName(SimulationEventType.PoorLeft));
            Assert.AreEqual("Classes", DebugEventText.TypeName(SimulationEventType.ClassChange));
            Assert.AreEqual("Mendiants", DebugEventText.TypeName(SimulationEventType.BeggarsArrived));
            Assert.AreEqual("Mendiants", DebugEventText.TypeName(SimulationEventType.BeggarsLeft));
            Assert.AreEqual("Maison", DebugEventText.TypeName(SimulationEventType.CityHouseBuilt));
        }

        [Test]
        public void AShortage()
        {
            Assert.AreEqual("Blé : 2,0 manquants", Detail(SimulationEventType.Shortage, 2));
        }

        [Test]
        public void ACrossing()
        {
            Assert.AreEqual("Blé : le stock passe de T1–T2 à T0–T1",
                Detail(SimulationEventType.ThresholdCrossing, 0, (int)StockBand.T1ToT2, (int)StockBand.T0ToT1));
            Assert.AreEqual("Blé : le stock passe de T2–T3 à au-dessus de T3",
                Detail(SimulationEventType.ThresholdCrossing, 0, (int)StockBand.T2ToT3, (int)StockBand.AboveT3));
            Assert.AreEqual("Blé : le stock passe de T0–T1 à sous T0",
                Detail(SimulationEventType.ThresholdCrossing, 0, (int)StockBand.T0ToT1, (int)StockBand.BelowT0));
        }

        [Test]
        public void Hires_AndLayOffs()
        {
            Assert.AreEqual("Blé : 4 ouvriers embauchés", Detail(SimulationEventType.Hired, 4));
            Assert.AreEqual("Blé : 1 ouvrier embauché", Detail(SimulationEventType.Hired, 1));
            Assert.AreEqual("Blé : 8 ouvriers en réserve", Detail(SimulationEventType.LaidOff, 8));
        }

        [Test]
        public void HaltedAndResumed()
        {
            Assert.AreEqual("Blé : production arrêtée (stock au-dessus de T3)", Detail(SimulationEventType.ProductionHalted, 0));
            Assert.AreEqual("Blé : production reprise", Detail(SimulationEventType.ProductionResumed, 0));
        }

        [Test]
        public void ThePoorWhoLeave()
        {
            Assert.AreEqual("48 pauvres quittent la ville, 12 ouvriers perdus", Detail(SimulationEventType.PoorLeft, 48, 0, 12, -1));
        }

        [Test]
        public void ClassMoves()
        {
            Assert.AreEqual("12 pauvres deviennent aisés", Detail(SimulationEventType.ClassChange, 12, 0, (int)SocialClass.Wealthy, -1));
            Assert.AreEqual("3 riches redeviennent pauvres", Detail(SimulationEventType.ClassChange, -3, 0, (int)SocialClass.Rich, -1));
        }

        [Test]
        public void Beggars()
        {
            Assert.AreEqual("5 mendiants arrivent", Detail(SimulationEventType.BeggarsArrived, 5, good: -1));
            Assert.AreEqual("2 mendiants partent", Detail(SimulationEventType.BeggarsLeft, 2, good: -1));
        }

        [Test]
        public void AHouse()
        {
            Assert.AreEqual("La ville construit une maison de pauvres", Detail(SimulationEventType.CityHouseBuilt, 1, 0, (int)SocialClass.Poor, -1));
            Assert.AreEqual("La ville construit une maison d'aisés", Detail(SimulationEventType.CityHouseBuilt, 1, 0, (int)SocialClass.Wealthy, -1));
        }
    }
}
