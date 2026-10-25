using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// The placed buildings ranked by <see cref="IsoProjection.DrawKeyOf"/>: a building of higher rank is drawn
    /// later. The rank fits a sortingOrder where the key does not. Equal keys keep the order they were inserted in.
    /// </summary>
    public sealed class BuildingDrawOrder
    {
        private readonly List<long> keys = new List<long>();

        public int Count => keys.Count;

        public long KeyAt(int rank)
        {
            return keys[rank];
        }

        /// <summary>
        /// Ranks a building among those inserted before. The buildings at or above the returned rank move up by one.
        /// </summary>
        /// <param name="size">The covered size, already turned (<see cref="Footprint.SizeFor"/>).</param>
        public int Insert(Vector2Int origin, Vector2Int size)
        {
            long key = IsoProjection.DrawKeyOf(origin, size);

            // After every equal key: the first rank whose key is larger.
            int low = 0;
            int high = keys.Count;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (keys[middle] <= key)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            keys.Insert(low, key);
            return low;
        }

        /// <summary>
        /// How many ranked keys are smaller than this one: something of this key drawn just below that rank is drawn
        /// after every building of a smaller key and before every building of an equal or larger one.
        /// </summary>
        public int RankOf(long key)
        {
            // The first rank whose key is not smaller.
            int low = 0;
            int high = keys.Count;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (keys[middle] < key)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        public void Clear()
        {
            keys.Clear();
        }
    }
}
