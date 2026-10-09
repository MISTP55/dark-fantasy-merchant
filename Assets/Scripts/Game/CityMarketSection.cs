using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The market rows of the city panel: one row per good with the city's stock and its
    /// prices, and, when it is given the hold of a ship in port, the barrels aboard and
    /// the buttons that buy and sell. Follows the market, the hold and the treasury.
    /// </summary>
    public sealed class CityMarketSection
    {
        /// <summary>On the section while a hold is shown: reveals the hold column and the buttons.</summary>
        public const string TradingClass = "market--trading";

        /// <summary>Written instead of a buy price when the city has no barrel to sell.</summary>
        public const string NoPrice = "—";

        private const string RowClass = "market__row";
        private const string CellClass = "market__cell";
        private const string NameCellClass = "market__cell--name";
        private const string NumberCellClass = "market__cell--number";
        private const string TradeCellClass = "market__cell--trade";
        private const string ButtonClass = "market__button";

        private sealed class Row
        {
            public Label Stock;
            public Label BuyPrice;
            public Label SellPrice;
            public Label Held;
            public Button Sell;
            public Button Buy;
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
        public CityMarketSection(
            VisualElement section,
            VisualElement rowContainer,
            Label holdLabel,
            IReadOnlyList<string> goodNames,
            Treasury treasury)
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

            for (int i = 0; i < goodNames.Count; i++)
            {
                rowContainer.Add(BuildRow(i, goodNames[i]));
            }

            Hide();
        }

        /// <summary>Barrels a click trades: 1, 10 with Shift, 100 with Ctrl.</summary>
        public static int BarrelsFor(bool shift, bool ctrl)
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

        /// <summary>Buys up to that many barrels of a good into the shown hold; nothing without one.</summary>
        public void Buy(int good, int barrels)
        {
            if (hold != null && good < market.GoodCount)
            {
                Trade.Buy(market, hold, treasury, good, barrels);
            }
        }

        /// <summary>Sells up to that many barrels of a good from the shown hold; nothing without one.</summary>
        public void Sell(int good, int barrels)
        {
            if (hold != null && good < market.GoodCount)
            {
                Trade.Sell(market, hold, treasury, good, barrels);
            }
        }

        private VisualElement BuildRow(int good, string goodName)
        {
            var element = new VisualElement();
            element.AddToClassList(RowClass);

            Label name = AddLabel(element, NameCellClass);
            name.text = goodName;

            var row = new Row
            {
                Stock = AddLabel(element, NumberCellClass),
                BuyPrice = AddLabel(element, NumberCellClass),
                SellPrice = AddLabel(element, NumberCellClass),
                Held = AddLabel(element, NumberCellClass, TradeCellClass),
            };

            row.Sell = AddButton(element, "−", evt => Sell(good, BarrelsFor(evt.shiftKey, evt.ctrlKey)));
            row.Buy = AddButton(element, "+", evt => Buy(good, BarrelsFor(evt.shiftKey, evt.ctrlKey)));
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

        // A click event, not Button.clicked: the modifier keys set the quantity.
        private static Button AddButton(VisualElement parent, string text, EventCallback<ClickEvent> onClick)
        {
            var button = new Button { text = text };

            // Buttons are clicked, not navigated: the keyboard pans the map.
            button.focusable = false;
            button.AddToClassList(ButtonClass);
            button.AddToClassList(TradeCellClass);
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
                int held = hold != null ? hold.BarrelsOf(i) : 0;

                row.Stock.text = TreasuryHudController.FormatNumber(available);
                row.BuyPrice.text = available > 0 ? TreasuryHudController.FormatNumber(buyPrice) : NoPrice;
                row.SellPrice.text = isInMarket ? TreasuryHudController.FormatNumber(market.SellPriceOf(i)) : NoPrice;
                row.Held.text = TreasuryHudController.FormatNumber(held);

                row.Sell.SetEnabled(isInMarket && held > 0);
                row.Buy.SetEnabled(
                    hold != null && available > 0 && hold.Free > 0 && treasury.CanAfford(buyPrice));
            }

            holdLabel.text = hold != null ? ShipInfoPanelController.FormatCargo(hold) : string.Empty;
        }
    }
}
