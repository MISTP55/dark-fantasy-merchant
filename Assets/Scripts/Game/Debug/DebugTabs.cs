using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// A row of tab buttons over their pages, for the debug panel: one page shown at a time, its
    /// button marked. The buttons are clicked, not navigated: the keyboard pans the map.
    /// </summary>
    public sealed class DebugTabs
    {
        public const string SelectedClass = "debug-panel__tab--selected";

        private readonly IReadOnlyList<Button> buttons;
        private readonly IReadOnlyList<VisualElement> pages;

        /// <param name="buttons">One button per page, in the same order.</param>
        public DebugTabs(IReadOnlyList<Button> buttons, IReadOnlyList<VisualElement> pages)
        {
            this.buttons = buttons ?? throw new ArgumentNullException(nameof(buttons));
            this.pages = pages ?? throw new ArgumentNullException(nameof(pages));

            if (buttons.Count != pages.Count)
            {
                throw new ArgumentException("There must be one button per page.");
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                int index = i;
                buttons[i].focusable = false;
                buttons[i].clicked += () => Select(index);
            }
        }

        /// <summary>The page shown; -1 before the first is selected.</summary>
        public int Selected { get; private set; } = -1;

        /// <summary>Raised when another page is shown, with its index.</summary>
        public event Action<int> Changed;

        public void Select(int index)
        {
            if (index < 0 || index >= pages.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
                buttons[i].EnableInClassList(SelectedClass, i == index);
            }

            if (index != Selected)
            {
                Selected = index;
                Changed?.Invoke(index);
            }
        }
    }
}
