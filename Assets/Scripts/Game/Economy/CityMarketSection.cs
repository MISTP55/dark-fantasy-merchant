using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The market rows of the city panel: one row per good, in the alphabetical order of their
    /// names, with its icon, the city's stock and its prices, and, when it is given the hold of a
    /// ship in port, the units aboard. The prices are the buttons that trade: a click on the buy
    /// price buys, one on the sell price sells. Follows the market, the hold and the treasury.
    /// </summary>
    public sealed class CityMarketSection
    {
        /// <summary>On the section while a hold is shown: reveals the hold column.</summary>
        public const string TradingClass = "market--trading";

        /// <summary>Written instead of a buy price when the city has no whole unit to sell.</summary>
        public const string NoPrice = "—";

        private const string RowClass = "market__row";
        private const string IconClass = "market__icon";
        private const string CellClass = "market__cell";
        private const string NameCellClass = "market__cell--name";
        private const string NumberCellClass = "market__cell--number";
        private const string TradeCellClass = "market__cell--trade";
        private const string PriceCellClass = "market__cell--price";
        private const string PriceClass = "market__price";

        private sealed class Row
        {
            public Label Stock;
            public Button BuyPrice;
            public Button SellPrice;
            public Label Held;
        }

        private readonly VisualElement section;
        private readonly Label holdLabel;
        private readonly Treasury treasury;
        private readonly List<Row> rows = new List<Row>();

        private CityMarket market;
        private CargoHold hold;

        /// <param name="section">The whole section, shown and hidden with the market.</param>
        /// <param name="rowContainer">Where the rows are built, one per name.</param>
        /// <param name="goodNames">The names of the goods, in the catalogue's order.</param>
        /// <param name="treasury">What pays and is paid. Null for a market that is only read.</param>
        /// <param name="goodIcons">The icons of the goods, by the same index as the names. Null, or a
        /// null icon, leaves the icon's place empty.</param>
        public CityMarketSection(
            VisualElement section,
            VisualElement rowContainer,
            Label holdLabel,
            IReadOnlyList<string> goodNames,
            Treasury treasury,
            IReadOnlyList<Sprite> goodIcons = null)
        {
            this.section = section ?? throw new ArgumentNullException(nameof(section));
            this.holdLabel = holdLabel ?? throw new ArgumentNullException(nameof(holdLabel));
            this.treasury = treasury;

            if (rowContainer == null)
            {
                throw new ArgumentNullException(nameof(rowContainer));
            }

            if (goodNames == null)
            {
                throw new ArgumentNullException(nameof(goodNames));
            }

            rowContainer.Clear();

            // Rows are kept by good, and laid out in the alphabetical order of their names.
            var elements = new VisualElement[goodNames.Count];

            for (int i = 0; i < goodNames.Count; i++)
            {
                Sprite icon = goodIcons != null && i < goodIcons.Count ? goodIcons[i] : null;
                elements[i] = BuildRow(i, goodNames[i], icon);
            }

            foreach (int good in NameOrder.Of(goodNames))
            {
                rowContainer.Add(elements[good]);
            }

            ListStripes.Apply(rowContainer);

            Hide();
        }

        /// <summary>Units a click trades, barrels or bushels: 1, 10 with Shift, 100 with Ctrl.</summary>
        public static int UnitsFor(bool shift, bool ctrl)
        {
            if (ctrl)
            {
                return 100;
            }

            return shift ? 10 : 1;
        }

        /// <summary>Shows a city's market, and trades with a hold when one is given.</summary>
        /// <param name="hold">The hold of the ship in port that trades, or null to only read the market.</param>
        public void Show(CityMarket market, CargoHold hold)
        {
            Unbind();

            this.market = market ?? throw new ArgumentNullException(nameof(market));

            // Nothing pays without a treasury.
            this.hold = treasury != null ? hold : null;

            this.market.Changed += Refresh;

            if (this.hold != null)
            {
                this.hold.Changed += Refresh;
                treasury.Changed += OnGoldChanged;
            }

            section.EnableInClassList(TradingClass, this.hold != null);
            section.style.display = DisplayStyle.Flex;
            Refresh();
        }

        public void Hide()
        {
            Unbind();
            section.EnableInClassList(TradingClass, false);
            section.style.display = DisplayStyle.None;
        }

        /// <summary>Raised by a purchase or a sale that moved at least one unit, for the debug tools.</summary>
        public event Action<int, TradeResult, PlayerTradeKind> Traded;

        /// <summary>Buys up to that many units of a good into the shown hold; nothing without one.</summary>
        public void Buy(int good, int units)
        {
            if (hold != null && good < market.GoodCount)
            {
                Report(good, Trade.Buy(market, hold, treasury, good, units), PlayerTradeKind.Buy);
            }
        }

        /// <summary>Sells up to that many units of a good from the shown hold; nothing without one.</summary>
        public void Sell(int good, int units)
        {
            if (hold != null && good < market.GoodCount)
            {
                Report(good, Trade.Sell(market, hold, treasury, good, units), PlayerTradeKind.Sell);
            }
        }

        private void Report(int good, TradeResult result, PlayerTradeKind kind)
        {
            if (result.Units > 0)
            {
                Traded?.Invoke(good, result, kind);
            }
        }

        private VisualElement BuildRow(int good, string goodName, Sprite goodIcon)
        {
            var element = new VisualElement();
            element.AddToClassList(RowClass);

            // Always there, so that the names line up whether a good has an icon or not.
            var icon = new VisualElement();
            icon.AddToClassList(IconClass);

            if (goodIcon != null)
            {
                icon.style.backgroundImage = new StyleBackground(goodIcon);
            }

            element.Add(icon);

            Label name = AddLabel(element, NameCellClass);
            name.text = goodName;

            var row = new Row
            {
                Stock = AddLabel(element, NumberCellClass),
                BuyPrice = AddPrice(element, evt => Buy(good, UnitsFor(evt.shiftKey, evt.ctrlKey))),
                SellPrice = AddPrice(element, evt => Sell(good, UnitsFor(evt.shiftKey, evt.ctrlKey))),
                Held = AddLabel(element, NumberCellClass, TradeCellClass),
            };

            rows.Add(row);
            return element;
        }

        private static Label AddLabel(VisualElement parent, params string[] classes)
        {
            var label = new Label();
            label.AddToClassList(CellClass);

            foreach (string className in classes)
            {
                label.AddToClassList(className);
            }

            parent.Add(label);
            return label;
        }

        // A price that trades when clicked. A click event, not Button.clicked: the modifier
        // keys set the quantity. It is disabled, and then looks like plain text, whenever
        // that trade is not possible.
        private static Button AddPrice(VisualElement parent, EventCallback<ClickEvent> onClick)
        {
            var button = new Button();

            // Buttons are clicked, not navigated: the keyboard pans the map.
            button.focusable = false;
            button.AddToClassList(CellClass);
            button.AddToClassList(PriceCellClass);
            button.AddToClassList(PriceClass);
            button.RegisterCallback(onClick);
            parent.Add(button);
            return button;
        }

        private void Unbind()
        {
            if (market != null)
            {
                market.Changed -= Refresh;
            }

            if (hold != null)
            {
                hold.Changed -= Refresh;
                treasury.Changed -= OnGoldChanged;
            }

            market = null;
            hold = null;
        }

        private void OnGoldChanged(long gold)
        {
            Refresh();
        }

        // A trade changes the market, the hold and the gold: three rewrites of a few labels.
        private void Refresh()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];

                // A market made from another catalogue than the rows: nothing to show there.
                bool isInMarket = i < market.GoodCount;
                int available = isInMarket ? market.AvailableOf(i) : 0;
                long buyPrice = isInMarket ? market.BuyPriceOf(i) : 0;
                int held = hold != null ? hold.UnitsOf(i) : 0;

                row.Stock.text = TreasuryHudController.FormatNumber(available);
                row.BuyPrice.text = available > 0 ? TreasuryHudController.FormatNumber(buyPrice) : NoPrice;
                row.SellPrice.text = isInMarket ? TreasuryHudController.FormatNumber(market.SellPriceOf(i)) : NoPrice;
                row.Held.text = TreasuryHudController.FormatNumber(held);

                // Without a hold nothing is held and nothing is bought: both prices are only read.
                row.SellPrice.SetEnabled(isInMarket && held > 0);
                row.BuyPrice.SetEnabled(
                    hold != null && available > 0 && hold.RoomFor(i) > 0 && treasury.CanAfford(buyPrice));
            }

            holdLabel.text = hold != null ? ShipInfoPanelController.FormatCargo(hold) : string.Empty;
        }
    }
}
