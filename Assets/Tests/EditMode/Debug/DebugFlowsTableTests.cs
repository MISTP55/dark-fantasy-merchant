using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class DebugFlowsTableTests
    {
        private const int NameCell = 0;
        private const int ReceivedCell = 1;
        private const int ShippedCell = 2;
        private const int NetCell = 3;

        private VisualElement header;
        private VisualElement rows;
        private VisualElement totals;
        private DebugFlowsTable table;

        [SetUp]
        public void SetUp()
        {
            header = new VisualElement();
            rows = new VisualElement();
            totals = new VisualElement();
            table = new DebugFlowsTable(header, rows, totals);

            // Lübeck received 40 and shipped 10; Cologne shipped 30.
            table.Write(new[] { "Lübeck", "Cologne" }, city => city == 0 ? (40L, 10L) : (0L, 30L));
        }

        private string Text(int row, int cell)
        {
            return ((Label)rows[row][cell]).text;
        }

        [Test]
        public void TheHeader_NamesTheColumns()
        {
            Assert.AreEqual("Ville", ((Label)header[0][NameCell]).text);
            Assert.AreEqual("Reçu", ((Label)header[0][ReceivedCell]).text);
            Assert.AreEqual("Expédié", ((Label)header[0][ShippedCell]).text);
            Assert.AreEqual("Net", ((Label)header[0][NetCell]).text);
        }

        [Test]
        public void Rows_AreAlphabetical()
        {
            Assert.AreEqual("Cologne", Text(0, NameCell));
            Assert.AreEqual("Lübeck", Text(1, NameCell));
        }

        [Test]
        public void Net_IsReceivedMinusShipped()
        {
            Assert.AreEqual("40", Text(1, ReceivedCell));
            Assert.AreEqual("10", Text(1, ShippedCell));
            Assert.AreEqual($"<color={DebugCitiesTable.PositiveColor}>+30</color>", Text(1, NetCell));
            Assert.AreEqual($"<color={DebugCitiesTable.NegativeColor}>-30</color>", Text(0, NetCell));
        }

        [Test]
        public void Totals()
        {
            Assert.AreEqual("Total", ((Label)totals[0][NameCell]).text);
            Assert.AreEqual("40", ((Label)totals[0][ReceivedCell]).text);
            Assert.AreEqual("40", ((Label)totals[0][ShippedCell]).text);
            Assert.AreEqual("0", ((Label)totals[0][NetCell]).text);
        }

        [Test]
        public void Write_Again_ReplacesTheRows()
        {
            table.Write(new[] { "Lübeck", "Cologne" }, city => (1L, 1L));

            Assert.AreEqual(2, rows.childCount);
            Assert.AreEqual("1", Text(0, ReceivedCell));
        }
    }
}
