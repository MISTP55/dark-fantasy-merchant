using System;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// Static definition of the navigable areas of a map: the size and the packed cells
    /// of a <see cref="NavigationGrid"/>. Painted in the Scene view; never written while
    /// the game runs.
    /// </summary>
    [CreateAssetMenu(fileName = "NavigationMask", menuName = "Dark Fantasy Merchant/Navigation Mask")]
    public sealed class NavigationMaskDefinition : ScriptableObject
    {
        // Hidden: the three fields only make sense together, and changing one by hand
        // would discard the mask.
        [SerializeField, HideInInspector] private int width = 1;
        [SerializeField, HideInInspector] private int height = 1;
        [SerializeField, HideInInspector] private byte[] bits = new byte[0];

        public int Width => width;

        public int Height => height;

        /// <summary>
        /// Builds a grid from the asset. Data that does not fit its stored size, or an
        /// invalid size, gives an empty grid instead of an error, so a damaged asset can
        /// still be opened and repainted.
        /// </summary>
        public NavigationGrid CreateGrid()
        {
            if (width < 1 || width > NavigationGrid.MaxSize || height < 1 || height > NavigationGrid.MaxSize)
            {
                return new NavigationGrid(1, 1);
            }

            if (bits == null || bits.Length != NavigationGrid.ByteCount(width, height))
            {
                return new NavigationGrid(width, height);
            }

            return new NavigationGrid(width, height, bits);
        }

        /// <summary>Copies a grid into the asset. For editor tooling only.</summary>
        public void SetGrid(NavigationGrid grid)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            width = grid.Width;
            height = grid.Height;
            bits = grid.ToBytes();
        }
    }
}
