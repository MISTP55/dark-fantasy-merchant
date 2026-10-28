using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Two sources of goods used as one: a building's materials are taken from the first, then from the second
    /// (the trading post's warehouse, then a ship's hold). Either may be null.
    /// </summary>
    public sealed class MaterialSources : IGoodSource
    {
        private readonly IGoodSource _first;
        private readonly IGoodSource _second;

        public MaterialSources(IGoodSource first, IGoodSource second)
        {
            _first = first;
            _second = second;
        }

        public int UnitsOf(int good)
        {
            return (_first?.UnitsOf(good) ?? 0) + (_second?.UnitsOf(good) ?? 0);
        }

        /// <summary>Takes from the first source, then the second; all or nothing.</summary>
        public bool TryRemove(int good, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (UnitsOf(good) < count)
            {
                return false;
            }

            int fromFirst = Math.Min(_first?.UnitsOf(good) ?? 0, count);

            if (fromFirst > 0)
            {
                _first.TryRemove(good, fromFirst);
            }

            if (count > fromFirst)
            {
                _second.TryRemove(good, count - fromFirst);
            }

            return true;
        }
    }
}
