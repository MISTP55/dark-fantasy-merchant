using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Runtime hover and selection state of the world map. Events carry the new value,
    /// or null when cleared, and are raised only on an actual change.
    /// </summary>
    public sealed class MapSelectionState<T> where T : class
    {
        public T Hovered { get; private set; }

        public T Selected { get; private set; }

        public event Action<T> HoveredChanged;

        public event Action<T> SelectedChanged;

        public void SetHovered(T item)
        {
            if (ReferenceEquals(Hovered, item))
            {
                return;
            }

            Hovered = item;
            HoveredChanged?.Invoke(item);
        }

        public void Select(T item)
        {
            if (ReferenceEquals(Selected, item))
            {
                return;
            }

            Selected = item;
            SelectedChanged?.Invoke(item);
        }

        public void ClearSelection()
        {
            Select(null);
        }
    }
}
