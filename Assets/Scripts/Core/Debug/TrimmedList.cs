using System.Collections;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// A list added to at its end and forgotten from its start, in constant time on average: the
    /// forgotten items are only skipped, and dropped for good once they are half of the list.
    /// </summary>
    internal sealed class TrimmedList<T> : IReadOnlyList<T>
    {
        private readonly List<T> items = new List<T>();
        private int start;

        public int Count => items.Count - start;

        public T this[int index] => items[start + index];

        public void Add(T item)
        {
            items.Add(item);
        }

        /// <summary>Forgets the first items.</summary>
        public void RemoveFirst(int count)
        {
            start += count;

            if (start == items.Count)
            {
                items.Clear();
                start = 0;
            }
            else if (start > items.Count / 2)
            {
                items.RemoveRange(0, start);
                start = 0;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = start; i < items.Count; i++)
            {
                yield return items[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
