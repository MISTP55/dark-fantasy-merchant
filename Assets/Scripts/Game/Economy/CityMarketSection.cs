using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The market rows of the city panel: one row per good, in the alphabetical order of their
    /// names, with its icon, the city's stock and its prices, and, when it is given a store (the
    /// hold of a ship in port, or the player's trading post), the units it holds. The prices are the
    /// buttons that trade: a click on the buy price buys, one on the sell price sells. Follows the
    /// market, the store and the treasury.
    /// </summary>
    public sealed class CityMarketSection
    {
        /// <summary>On the section while a store is shown: reveals the store column.</summary>
        public const string TradingClass = "market--trading";

        /// <summary>On the fill label while the shown trading post stores more than its capacity.</summary>
        public const string OverCapacityClass = "market__hold--over";

        /// <summary>The title of the store column for a ship's hold.</summary>
        public const string HoldHeader = "Cale";

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
        private const string TipClass = "market__tip";

        private sealed class Row
        {
            public Label Stock;
            public Button BuyPrice;
            public Button SellPrice;
            public Label Held;
        }

        private readonly VisualElement section;
        private readonly Label holdLabel;
        private readonly Label storeHeaderLabel;
        private readonly Treasury treasury;
        private readonly long rentPerBarrel;
        private readonly List<Row> rows = new List<Row>();
        private readonly Label rentTip;

        private CityMarket market;
        private bool isPointerOverFill;

        // The store that trades: a ship's hold or a trading post.
        private IGoodStore hold;

        /// <param name="section">The whole section, shown and hidden with the market.</param>
        /// <param name="rowContainer">Where the rows are built, one per name.</param>
        /// <param name="goodNames">The names of the goods, in the catalogue's order.</param>
        /// <param name="treasury">What pays and is paid. Null for a market that is only read.</param>
        /// <param name="goodIcons">The icons of the goods, by the same index as the names. Null, or a
        /// null icon, leaves the icon's place empty.</param>
        /// <param name="storeHeaderLabel">The title of the store column, or null when there is none to write.</param>
        /// <param name="rentPerBarrel">Gold a week for each barrel a trading post stores above its capacity.</param>
        public CityMarketSection(
            VisualElement section,
            VisualElement rowContainer,
            Label holdLabel,
            IReadOnlyList<string> goodNames,
            Treasury treasury,
            IReadOnlyList<Sprite> goodIcons = null,
            Label storeHeaderLabel = null,
            long rentPerBarrel = 0)
        {
            this.section = section ?? throw new ArgumentNullException(nameof(section));
            this.holdLabel = holdLabel ?? throw new ArgumentNullException(nameof(holdLabel));
            this.storeHeaderLabel = storeHeaderLabel;
            this.treasury = treasury;
            this.rentPerBarrel = rentPerBarrel;

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

            // The fill's tooltip, drawn by the section: a runtime panel shows no tooltip itself.
            rentTip = new Label { pickingMode = PickingMode.Ignore };
            rentTip.AddToClassList(TipClass);
            rentTip.style.display = DisplayStyle.None;
            section.Add(rentTip);
            holdLabel.RegisterCallback<PointerEnterEvent>(OnPointerEnterFill);
            holdLabel.RegisterCallback<PointerLeaveEvent>(OnPointerLeaveFill);

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

        /// <summary>The fill of a trading post as the market writes it: "1 850 / 2 000 tonneaux".</summary>
        public static string FormatWarehouseFill(long used, int capacity)
        {
            return $"{TreasuryHudController.FormatNumber(used)} / {TreasuryHudController.FormatNumber(capacity)} tonneaux";
        }

        /// <summary>
        /// The tip of a trading post above its capacity:
        /// "150 tonneaux au-delà de la capacité : loyer de 150 or par semaine.".
        /// </summary>
        public static string RentTipText(long excess, long weeklyRent)
        {
            return $"{GoodUnitText.FormatQuantity(excess, GoodUnit.Barrel)} au-delà de la capacité : "
                + $"loyer de {TreasuryHudController.FormatGold(weeklyRent)} par semaine.";
        }

        /// <summary>Shows a city's market, and trades with a store when one is given.</summary>
        /// <param name="store">The hold of the ship in port, or the trading post, that trades; null to only read the market.</param>
        /// <param name="storeHeader">The title of the store column: <see cref="HoldHeader"/> for a hold, the trading post's for it.</param>
        public void Show(CityMarket market, IGoodStore store, string storeHeader = HoldHeader)
        {
            Unbind();

            this.market = market ?? throw new ArgumentNullException(nameof(market));

            // Nothing pays without a treasury.
            hold = treasury != null ? store : null;

            if (storeHeaderLabel != null)
            {
                storeHeaderLabel.text = storeHeader;
            }

            this.market.Changed += Refresh;

            if (hold != null)
            {
                hold.Changed += Refresh;
                treasury.Changed += OnGoldChanged;
            }

            section.EnableInClassList(TradingClass, hold != null);
            section.style.display = DisplayStyle.Flex;
            Refresh();
        }

        public void Hide()
        {
            Unbind();
            isPointerOverFill = false;
            section.EnableInClassList(TradingClass, false);
            section.style.display = DisplayStyle.None;
            ShowTip();
        }

        /// <summary>Raised by a purchase or a sale that moved at least one unit, for the debug tools.</summary>
        public event Action<int, TradeResult, PlayerTradeKind> Traded;

        /// <summary>Buys up to that many units of a good into the shown store; nothing without one.</summary>
        public void Buy(int good, int units)
        {
            if (hold != null && good < market.GoodCount)
            {
                Report(good, Trade.Buy(market, hold, treasury, good, units), PlayerTradeKind.Buy);
            }
        }

        /// <summary>Sells up to that many units of a good from the shown store; nothing without one.</summary>
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

            WriteFill();
        }

        // A ship's hold reads as its panel writes it; a trading post gives its fill, and its rent
        // while it stores more than its capacity.
        private void WriteFill()
        {
            var warehouse = hold as Warehouse;
            bool isOver = warehouse != null && warehouse.Excess > 0;

            if (hold is CargoHold cargo)
            {
                holdLabel.text = ShipInfoPanelController.FormatCargo(cargo);
            }
            else if (warehouse != null)
            {
                holdLabel.text = FormatWarehouseFill(warehouse.Used, warehouse.Capacity);
            }
            else
            {
                holdLabel.text = string.Empty;
            }

            holdLabel.EnableInClassList(OverCapacityClass, isOver);
            holdLabel.tooltip = isOver ? RentTipText(warehouse.Excess, Warehouse.WeeklyRentOf(new[] { warehouse }, rentPerBarrel)) : string.Empty;
            ShowTip();
        }

        private void OnPointerEnterFill(PointerEnterEvent evt)
        {
            isPointerOverFill = true;
            ShowTip();
        }

        private void OnPointerLeaveFill(PointerLeaveEvent evt)
        {
            isPointerOverFill = false;
            ShowTip();
        }

        // Under the fill, right-aligned with it, while the pointer is over it and it has a tip.
        private void ShowTip()
        {
            if (!isPointerOverFill || market == null || string.IsNullOrEmpty(holdLabel.tooltip))
            {
                rentTip.style.display = DisplayStyle.None;
                return;
            }

            Rect fill = holdLabel.worldBound;
            Rect area = section.worldBound;
            rentTip.text = holdLabel.tooltip;
            rentTip.style.top = fill.yMax - area.yMin + 4f;
            rentTip.style.right = area.xMax - fill.xMax;
            rentTip.style.display = DisplayStyle.Flex;
        }
    }
}
