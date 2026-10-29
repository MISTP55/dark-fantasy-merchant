using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The transfer rows of the city panel: one row per good that the hold of a ship in port or
    /// the player's trading post holds, in the alphabetical order of their names, with its icon,
    /// the units in the hold, the two buttons that move it ("→" to the trading post, "←" to the
    /// hold) and the units in the trading post. Moving is free. Follows the hold and the trading post.
    /// </summary>
    public sealed class TradingPostTransferSection
    {
        /// <summary>The button that moves a good from the hold to the trading post.</summary>
        public const string ToPostText = "→";

        /// <summary>The button that moves a good from the trading post to the hold.</summary>
        public const string ToHoldText = "←";

        private const string RowClass = "market__row";
        private const string IconClass = "market__icon";
        private const string CellClass = "market__cell";
        private const string NameCellClass = "market__cell--name";
        private const string NumberCellClass = "market__cell--number";
        private const string MoveCellClass = "transfer__move";
        private const string PriceClass = "market__price";

        private sealed class Row
        {
            public VisualElement Element;
            public Label InHold;
            public Button ToPost;
            public Button ToHold;
            public Label InPost;
        }

        private readonly VisualElement section;
        private readonly VisualElement rowContainer;
        private readonly Label holdHeader;
        private readonly IReadOnlyList<string> goodNames;
        private readonly IReadOnlyList<Sprite> goodIcons;

        // Built the first time their good is shown, and kept by good.
        private readonly Row[] rows;

        private CargoHold hold;
        private Warehouse post;

        /// <param name="section">The whole section, shown and hidden with the trading post.</param>
        /// <param name="rowContainer">Where the rows are laid out.</param>
        /// <param name="holdHeader">The title of the hold's column, which names the ship.</param>
        /// <param name="goodNames">The names of the goods, in the catalogue's order.</param>
        /// <param name="goodIcons">The icons of the goods, by the same index as the names. Null, or a
        /// null icon, leaves the icon's place empty.</param>
        public TradingPostTransferSection(
            VisualElement section,
            VisualElement rowContainer,
            Label holdHeader,
            IReadOnlyList<string> goodNames,
            IReadOnlyList<Sprite> goodIcons = null)
        {
            this.section = section ?? throw new ArgumentNullException(nameof(section));
            this.rowContainer = rowContainer ?? throw new ArgumentNullException(nameof(rowContainer));
            this.holdHeader = holdHeader ?? throw new ArgumentNullException(nameof(holdHeader));
            this.goodNames = goodNames ?? throw new ArgumentNullException(nameof(goodNames));
            this.goodIcons = goodIcons;
            rows = new Row[goodNames.Count];

            Hide();
        }

        /// <summary>The goods with at least one unit in either store, in the alphabetical order of their names.</summary>
        public static List<int> GoodsShown(IGoodSource hold, IGoodSource post, IReadOnlyList<string> goodNames)
        {
            var goods = new List<int>();

            foreach (int good in NameOrder.Of(goodNames))
            {
                if (hold.UnitsOf(good) > 0 || post.UnitsOf(good) > 0)
                {
                    goods.Add(good);
                }
            }

            return goods;
        }

        /// <summary>Shows what a ship in port and the trading post hold, and moves goods between them.</summary>
        /// <param name="hold">The hold of the player's ship in port; null for none, and then no rows.</param>
        /// <param name="shipName">The name of that ship, the title of the hold's column.</param>
        public void Show(CargoHold hold, string shipName, Warehouse post)
        {
            Unbind();

            this.post = post ?? throw new ArgumentNullException(nameof(post));
            this.hold = hold;
            holdHeader.text = hold != null ? shipName : CityMarketSection.HoldHeader;

            if (hold != null)
            {
                hold.Changed += Refresh;
                post.Changed += Refresh;
            }

            section.style.display = DisplayStyle.Flex;
            Refresh();
        }

        public void Hide()
        {
            Unbind();
            rowContainer.Clear();
            section.style.display = DisplayStyle.None;
        }

        private void Unbind()
        {
            if (hold != null)
            {
                hold.Changed -= Refresh;
                post.Changed -= Refresh;
            }

            hold = null;
            post = null;
        }

        // A good appears or leaves with its first or last unit: the rows are laid out again.
        private void Refresh()
        {
            rowContainer.Clear();

            if (hold == null)
            {
                return;
            }

            foreach (int good in GoodsShown(hold, post, goodNames))
            {
                Row row = rows[good] ?? (rows[good] = BuildRow(good));
                int inHold = hold.UnitsOf(good);
                int inPost = post.UnitsOf(good);

                row.InHold.text = NumberText.Format(inHold);
                row.InPost.text = NumberText.Format(inPost);
                row.ToPost.SetEnabled(inHold > 0);
                row.ToHold.SetEnabled(inPost > 0 && hold.RoomFor(good) > 0);
                rowContainer.Add(row.Element);
            }

            ListStripes.Apply(rowContainer);
        }

        private Row BuildRow(int good)
        {
            var element = new VisualElement();
            element.AddToClassList(RowClass);

            // Always there, so that the names line up whether a good has an icon or not.
            var icon = new VisualElement();
            icon.AddToClassList(IconClass);
            Sprite goodIcon = goodIcons != null && good < goodIcons.Count ? goodIcons[good] : null;

            if (goodIcon != null)
            {
                icon.style.backgroundImage = new StyleBackground(goodIcon);
            }

            element.Add(icon);
            AddLabel(element, NameCellClass).text = goodNames[good];

            return new Row
            {
                Element = element,
                InHold = AddLabel(element, NumberCellClass),
                ToPost = AddMove(element, ToPostText, evt => MoveToPost(good, CityMarketSection.UnitsFor(evt.shiftKey, evt.ctrlKey))),
                ToHold = AddMove(element, ToHoldText, evt => MoveToHold(good, CityMarketSection.UnitsFor(evt.shiftKey, evt.ctrlKey))),
                InPost = AddLabel(element, NumberCellClass),
            };
        }

        private void MoveToPost(int good, int units)
        {
            if (hold != null)
            {
                Transfer.Move(hold, post, good, units);
            }
        }

        private void MoveToHold(int good, int units)
        {
            if (hold != null)
            {
                Transfer.Move(post, hold, good, units);
            }
        }

        private static Label AddLabel(VisualElement parent, string className)
        {
            var label = new Label();
            label.AddToClassList(CellClass);
            label.AddToClassList(className);
            parent.Add(label);
            return label;
        }

        // A click event, not Button.clicked: the modifier keys set the quantity. Styled as the
        // market's prices: a disabled one reads as plain text.
        private static Button AddMove(VisualElement parent, string text, EventCallback<ClickEvent> onClick)
        {
            var button = new Button { text = text };

            // Buttons are clicked, not navigated: the keyboard pans the map.
            button.focusable = false;
            button.AddToClassList(CellClass);
            button.AddToClassList(MoveCellClass);
            button.AddToClassList(PriceClass);
            button.RegisterCallback(onClick);
            parent.Add(button);
            return button;
        }
    }
}
