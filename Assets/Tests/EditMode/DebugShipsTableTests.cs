using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class DebugShipsTableTests
    {
        // Children of a row, in order.
        private const int NameCell = 0;
        private const int OwnerCell = 1;
        private const int StateCell = 2;
        private const int PlaceCell = 3;
        private const int DestinationCell = 4;
        private const int CrewCell = 5;
        private const int SpeedCell = 6;
        private const int HoldCell = 7;
        private const int FirstGoodCell = 8;
        private const int ProfitCell = 10;

        private VisualElement header;
        private VisualElement rows;
        private DebugShipsTable table;
        private List<DebugShipInfo> ships;

        [SetUp]
        public void SetUp()
        {
            header = new VisualElement();
            rows = new VisualElement();
            table = new DebugShipsTable(header, rows, new[] { "Poisson", "Fer" });
            ships = new List<DebugShipInfo>();
        }

        private static Ship CreateShip()
        {
            return new Ship(
                new MapProjection(40f, 2f), new Vector2(0.25f, 0.5f), 4f, new ShipCrew(28, 14), null, new CargoHold(200));
        }

        private string Text(int row, int cell)
        {
            return ((Label)rows[row][cell]).text;
        }

        [Test]
        public void TheHeader_NamesTheColumns_TheGoodsAmongThem()
        {
            Assert.AreEqual(1, header.childCount);
            Assert.AreEqual(ProfitCell + 1, header[0].childCount);
            Assert.AreEqual("Navire", ((Label)header[0][NameCell]).text);
            Assert.AreEqual("Propriétaire", ((Label)header[0][OwnerCell]).text);
            Assert.AreEqual("État", ((Label)header[0][StateCell]).text);
            Assert.AreEqual("Lieu", ((Label)header[0][PlaceCell]).text);
            Assert.AreEqual("Destination", ((Label)header[0][DestinationCell]).text);
            Assert.AreEqual("Équipage", ((Label)header[0][CrewCell]).text);
            Assert.AreEqual("Vitesse", ((Label)header[0][SpeedCell]).text);
            Assert.AreEqual("Cale", ((Label)header[0][HoldCell]).text);
            Assert.AreEqual("Poisson", ((Label)header[0][FirstGoodCell]).text);
            Assert.AreEqual("Fer", ((Label)header[0][FirstGoodCell + 1]).text);
            Assert.AreEqual("Profit", ((Label)header[0][ProfitCell]).text);
        }

        [Test]
        public void AShipIdleAtSea_IsWrittenWithItsPosition()
        {
            ships.Add(new DebugShipInfo("Navire marchand", true, CreateShip(), null, null, null));

            table.Refresh(ships);

            Assert.AreEqual(1, rows.childCount);
            Assert.AreEqual("Navire marchand", Text(0, NameCell));
            Assert.AreEqual("Joueur", Text(0, OwnerCell));
            Assert.AreEqual("À l'arrêt", Text(0, StateCell));
            Assert.AreEqual("0,250 ; 0,500", Text(0, PlaceCell));
            Assert.AreEqual(DebugShipsTable.NoValue, Text(0, DestinationCell));
            Assert.AreEqual("14 / 28", Text(0, CrewCell));
            Assert.AreEqual("100\u00A0%", Text(0, SpeedCell));
            Assert.AreEqual("0 / 200", Text(0, HoldCell));
            Assert.AreEqual("0", Text(0, FirstGoodCell));
            Assert.AreEqual(DebugShipsTable.NoValue, Text(0, ProfitCell));
        }

        [Test]
        public void AShipUnderWay_IsWrittenWithWhereItGoes()
        {
            Ship ship = CreateShip();
            ship.SetDestination(new Vector2(0.75f, 0.5f));
            ship.Cargo.TryAdd(1, 60);
            ships.Add(new DebugShipInfo("Marchand indépendant 1", false, ship, null, null, -1250));

            table.Refresh(ships);

            Assert.AreEqual("IA", Text(0, OwnerCell));
            Assert.AreEqual("En mer", Text(0, StateCell));
            Assert.AreEqual("0,750 ; 0,500", Text(0, DestinationCell));
            Assert.AreEqual("60 / 200", Text(0, HoldCell));
            Assert.AreEqual("0", Text(0, FirstGoodCell));
            Assert.AreEqual("60", Text(0, FirstGoodCell + 1));
            Assert.AreEqual("-1\u00A0250 or", Text(0, ProfitCell));
        }

        [Test]
        public void TheCitiesOfAShip_AreNamed()
        {
            Ship sailing = CreateShip();
            sailing.SetDestination(new Vector2(0.75f, 0.5f));
            ships.Add(new DebugShipInfo("A", false, sailing, null, "Sparia", 0));
            ships.Add(new DebugShipInfo("B", false, CreateShip(), "Elforth", null, 0));

            table.Refresh(ships);

            Assert.AreEqual("Sparia", Text(0, DestinationCell));
            Assert.AreEqual("Au port", Text(1, StateCell));
            Assert.AreEqual("Elforth", Text(1, PlaceCell));
            Assert.AreEqual("0 or", Text(1, ProfitCell));
        }

        [Test]
        public void Refresh_FollowsTheShips_AndTheirNumber()
        {
            Ship ship = CreateShip();
            ships.Add(new DebugShipInfo("A", true, ship, null, null, null));
            ships.Add(new DebugShipInfo("B", false, CreateShip(), null, null, 0));
            table.Refresh(ships);
            Assert.AreEqual(2, rows.childCount);

            ship.Cargo.TryAdd(0, 5);
            ships.RemoveAt(1);
            table.Refresh(ships);

            Assert.AreEqual(1, rows.childCount);
            Assert.AreEqual("5", Text(0, FirstGoodCell));

            ships.Add(new DebugShipInfo("C", false, CreateShip(), null, null, 0));
            table.Refresh(ships);

            Assert.AreEqual(2, rows.childCount);
            Assert.AreEqual("C", Text(1, NameCell));
        }

        [Test]
        public void Formats_AreWrittenTheFrenchWay()
        {
            Assert.AreEqual("Joueur", DebugShipsTable.FormatOwner(true));
            Assert.AreEqual("IA", DebugShipsTable.FormatOwner(false));
            Assert.AreEqual("Au port", DebugShipsTable.FormatState(true, false));
            Assert.AreEqual("En mer", DebugShipsTable.FormatState(false, true));
            Assert.AreEqual("À l'arrêt", DebugShipsTable.FormatState(false, false));
            Assert.AreEqual("0,412 ; 0,733", DebugShipsTable.FormatPosition(new Vector2(0.4124f, 0.7331f)));
            Assert.AreEqual("12 / 28", DebugShipsTable.FormatCrew(new ShipCrew(28, 12)));
            Assert.AreEqual("50\u00A0%", DebugShipsTable.FormatSpeed(0.5f));
            Assert.AreEqual("0\u00A0%", DebugShipsTable.FormatSpeed(0f));
            Assert.AreEqual("1\u00A0200 / 2\u00A0000", DebugShipsTable.FormatHold(HoldWith(2000, 1200)));
            Assert.AreEqual("1\u00A0250 or", DebugShipsTable.FormatProfit(1250));
            Assert.AreEqual(DebugShipsTable.NoValue, DebugShipsTable.FormatProfit(null));
        }

        [Test]
        public void WithoutAWayToRemoveShips_TheRowsHaveNoButton()
        {
            ships.Add(new DebugShipInfo("A", false, CreateShip(), null, null, 0));
            table.Refresh(ships);

            Assert.AreEqual(ProfitCell + 1, rows[0].childCount);
            Assert.DoesNotThrow(() => table.RequestRemoval(0));
        }

        [Test]
        public void TheButtonOfARow_RemovesItsShip_ButNotThePlayers()
        {
            var removed = new List<Ship>();
            table = new DebugShipsTable(header, rows, new[] { "Poisson", "Fer" }, removed.Add);
            Ship players = CreateShip();
            Ship merchants = CreateShip();
            ships.Add(new DebugShipInfo("Navire marchand", true, players, null, null, null));
            ships.Add(new DebugShipInfo("Marchand indépendant 1", false, merchants, null, null, 0));
            table.Refresh(ships);

            // A last cell: empty in the header, with the button in the rows.
            Assert.AreEqual(ProfitCell + 2, header[0].childCount);
            Assert.AreEqual(0, header[0][ProfitCell + 1].childCount);
            var playersButton = (Button)rows[0][ProfitCell + 1][0];
            var merchantsButton = (Button)rows[1][ProfitCell + 1][0];
            Assert.AreEqual("Retirer", merchantsButton.text);
            Assert.AreEqual(DisplayStyle.None, playersButton.style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, merchantsButton.style.display.value);

            table.RequestRemoval(0);
            Assert.AreEqual(0, removed.Count);

            table.RequestRemoval(1);
            CollectionAssert.AreEqual(new[] { merchants }, removed);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => table.RequestRemoval(2));
        }

        [Test]
        public void ARowsButton_FollowsTheShipTheRowIsWrittenFor()
        {
            var removed = new List<Ship>();
            table = new DebugShipsTable(header, rows, new[] { "Poisson", "Fer" }, removed.Add);
            Ship first = CreateShip();
            Ship second = CreateShip();
            ships.Add(new DebugShipInfo("A", false, first, null, null, 0));
            ships.Add(new DebugShipInfo("B", false, second, null, null, 0));
            table.Refresh(ships);

            // The first ship is gone: its row now shows the second.
            ships.RemoveAt(0);
            table.Refresh(ships);
            table.RequestRemoval(0);

            CollectionAssert.AreEqual(new[] { second }, removed);
        }

        private static CargoHold HoldWith(int capacity, int barrels)
        {
            var hold = new CargoHold(capacity);
            hold.TryAdd(0, barrels);
            return hold;
        }
    }
}
