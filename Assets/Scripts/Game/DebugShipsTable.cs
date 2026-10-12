using System;
using System.Collections.Generic;
using System.Globalization;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>What the ships debug table writes of one ship.</summary>
    public readonly struct DebugShipInfo
    {
        /// <param name="portName">Name of the city whose port the ship lies in; null at sea.</param>
        /// <param name="destinationName">Name of the city whose port the ship will enter; null for none.</param>
        /// <param name="profit">Cumulated profit of a merchant that trades on its own; null for the player's ships.</param>
        public DebugShipInfo(string name, bool isPlayer, Ship ship, string portName, string destinationName, long? profit)
        {
            Name = name;
            IsPlayer = isPlayer;
            Ship = ship ?? throw new ArgumentNullException(nameof(ship));
            PortName = portName;
            DestinationName = destinationName;
            Profit = profit;
        }

        public string Name { get; }

        public bool IsPlayer { get; }

        public Ship Ship { get; }

        public string PortName { get; }

        public string DestinationName { get; }

        public long? Profit { get; }
    }

    /// <summary>
    /// The table of the ships debug tab: one row per ship, the player's and the others,
    /// with its owner, what it is doing, where it is and goes, its crew, its hold and,
    /// for a merchant that trades on its own, its profit. Rewritten by every refresh.
    /// </summary>
    public sealed class DebugShipsTable
    {
        /// <summary>Written in a cell that has nothing to show.</summary>
        public const string NoValue = "—";

        // The rows and cells of the cities table, with the widths of this one.
        private const string RowClass = "debug-cities__row";
        private const string HeaderRowClass = "debug-cities__row--header";
        private const string CellClass = "debug-cities__cell";
        private const string NameCellClass = "debug-ships__cell--name";
        private const string TextCellClass = "debug-ships__cell--text";
        private const string NumberCellClass = "debug-ships__cell--number";

        // Not the French culture: its separators depend on the platform.
        private static readonly NumberFormatInfo PositionFormat = new NumberFormatInfo { NumberDecimalSeparator = "," };

        private sealed class Row
        {
            public VisualElement Element;
            public Label Name;
            public Label Owner;
            public Label State;
            public Label Place;
            public Label Destination;
            public Label Crew;
            public Label Speed;
            public Label Hold;
            public Label[] Goods;
            public Label Profit;
        }

        private readonly VisualElement rowContainer;
        private readonly int goodCount;
        private readonly List<Row> rows = new List<Row>();

        /// <param name="header">Where the header row is built.</param>
        /// <param name="rowContainer">Where the rows are built, one per ship.</param>
        /// <param name="goodNames">The names of the goods, in the catalogue's order.</param>
        public DebugShipsTable(VisualElement header, VisualElement rowContainer, IReadOnlyList<string> goodNames)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            if (goodNames == null)
            {
                throw new ArgumentNullException(nameof(goodNames));
            }

            this.rowContainer = rowContainer ?? throw new ArgumentNullException(nameof(rowContainer));
            goodCount = goodNames.Count;

            header.Clear();
            rowContainer.Clear();

            Row titles = BuildRow();
            titles.Element.AddToClassList(HeaderRowClass);
            titles.Name.text = "Navire";
            titles.Owner.text = "Propriétaire";
            titles.State.text = "État";
            titles.Place.text = "Lieu";
            titles.Destination.text = "Destination";
            titles.Crew.text = "Équipage";
            titles.Speed.text = "Vitesse";
            titles.Hold.text = "Cale";
            titles.Profit.text = "Profit";

            for (int i = 0; i < goodCount; i++)
            {
                titles.Goods[i].text = goodNames[i];
            }

            header.Add(titles.Element);
        }

        public static string FormatOwner(bool isPlayer)
        {
            return isPlayer ? "Joueur" : "IA";
        }

        /// <summary>What a ship is doing: "Au port", "En mer" under way, "À l'arrêt" idle at sea.</summary>
        public static string FormatState(bool isDocked, bool isMoving)
        {
            if (isDocked)
            {
                return "Au port";
            }

            return isMoving ? "En mer" : "À l'arrêt";
        }

        /// <summary>A normalized map position: "0,412 ; 0,733".</summary>
        public static string FormatPosition(Vector2 position)
        {
            return $"{position.x.ToString("F3", PositionFormat)} ; {position.y.ToString("F3", PositionFormat)}";
        }

        /// <summary>The sailors aboard out of the capacity: "14 / 28".</summary>
        public static string FormatCrew(ShipCrew crew)
        {
            return $"{crew.Count} / {crew.Capacity}";
        }

        /// <summary>The share of its speed a ship sails at: "100 %", with a no-break space.</summary>
        public static string FormatSpeed(float speedFactor)
        {
            return $"{Mathf.RoundToInt(speedFactor * 100f)}\u00A0%";
        }

        /// <summary>The barrels aboard out of the capacity: "60 / 200".</summary>
        public static string FormatHold(CargoHold hold)
        {
            return $"{TreasuryHudController.FormatNumber(hold.Used)} / {TreasuryHudController.FormatNumber(hold.Capacity)}";
        }

        /// <summary>A cumulated profit: "1 250 or", or nothing for a ship that has none.</summary>
        public static string FormatProfit(long? profit)
        {
            return profit.HasValue ? TreasuryHudController.FormatGold(profit.Value) : NoValue;
        }

        /// <summary>Writes a row per ship, in order: rows are added and removed to match.</summary>
        public void Refresh(IReadOnlyList<DebugShipInfo> ships)
        {
            if (ships == null)
            {
                throw new ArgumentNullException(nameof(ships));
            }

            while (rows.Count < ships.Count)
            {
                Row row = BuildRow();
                rows.Add(row);
                rowContainer.Add(row.Element);
            }

            while (rows.Count > ships.Count)
            {
                Row last = rows[rows.Count - 1];
                rowContainer.Remove(last.Element);
                rows.RemoveAt(rows.Count - 1);
            }

            for (int i = 0; i < ships.Count; i++)
            {
                Write(rows[i], ships[i]);
            }
        }

        private void Write(Row row, DebugShipInfo info)
        {
            Ship ship = info.Ship;
            bool isDocked = info.PortName != null;

            row.Name.text = info.Name;
            row.Owner.text = FormatOwner(info.IsPlayer);
            row.State.text = FormatState(isDocked, ship.IsMoving);
            row.Place.text = isDocked ? info.PortName : FormatPosition(ship.Position);
            row.Destination.text = DestinationOf(info);
            row.Crew.text = FormatCrew(ship.Crew);
            row.Speed.text = FormatSpeed(ship.Crew.SpeedFactor);
            row.Hold.text = FormatHold(ship.Cargo);
            row.Profit.text = FormatProfit(info.Profit);

            for (int i = 0; i < goodCount; i++)
            {
                row.Goods[i].text = TreasuryHudController.FormatNumber(ship.Cargo.BarrelsOf(i));
            }
        }

        private static string DestinationOf(DebugShipInfo info)
        {
            if (info.DestinationName != null)
            {
                return info.DestinationName;
            }

            Vector2? destination = info.Ship.Destination;

            return destination.HasValue ? FormatPosition(destination.Value) : NoValue;
        }

        private Row BuildRow()
        {
            var element = new VisualElement();
            element.AddToClassList(RowClass);

            var row = new Row
            {
                Element = element,
                Name = AddLabel(element, NameCellClass),
                Owner = AddLabel(element, TextCellClass),
                State = AddLabel(element, TextCellClass),
                Place = AddLabel(element, TextCellClass),
                Destination = AddLabel(element, TextCellClass),
                Crew = AddLabel(element, NumberCellClass),
                Speed = AddLabel(element, NumberCellClass),
                Hold = AddLabel(element, NumberCellClass),
                Goods = new Label[goodCount],
            };

            for (int i = 0; i < goodCount; i++)
            {
                row.Goods[i] = AddLabel(element, NumberCellClass);
            }

            row.Profit = AddLabel(element, NumberCellClass);
            return row;
        }

        private static Label AddLabel(VisualElement parent, string cellClass)
        {
            var label = new Label();
            label.AddToClassList(CellClass);
            label.AddToClassList(cellClass);
            parent.Add(label);
            return label;
        }
    }
}
