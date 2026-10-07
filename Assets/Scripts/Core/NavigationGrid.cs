using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Which cells of the map ships can sail on. Cell (0, 0) is the bottom-left one,
    /// matching normalized map coordinates; the grid covers the whole map.
    /// </summary>
    public sealed class NavigationGrid
    {
        /// <summary>Largest width or height, so that cell indexes fit in an int.</summary>
        public const int MaxSize = 16384;

        // Row by row from the bottom row, eight cells per byte, least significant bit
        // first. Unused bits of the last byte stay at zero.
        private readonly byte[] bits;

        /// <summary>Creates a grid in which no cell is navigable.</summary>
        public NavigationGrid(int width, int height)
        {
            if (width < 1 || width > MaxSize)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height < 1 || height > MaxSize)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            Width = width;
            Height = height;
            bits = new byte[ByteCount(width, height)];
        }

        /// <param name="bits">Packed cells, as returned by <see cref="ToBytes"/>; copied.</param>
        public NavigationGrid(int width, int height, byte[] bits)
            : this(width, height)
        {
            if (bits == null)
            {
                throw new ArgumentNullException(nameof(bits));
            }

            if (bits.Length != this.bits.Length)
            {
                throw new ArgumentException(
                    $"Expected {this.bits.Length} bytes for a {width} x {height} grid, got {bits.Length}.",
                    nameof(bits));
            }

            Array.Copy(bits, this.bits, bits.Length);
            ClearUnusedBits();
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>Number of bytes that hold a grid of this size.</summary>
        public static int ByteCount(int width, int height)
        {
            return (width * height + 7) / 8;
        }

        /// <returns>False outside the grid.</returns>
        public bool IsNavigable(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return false;
            }

            int index = y * Width + x;

            return (bits[index >> 3] & (1 << (index & 7))) != 0;
        }

        /// <returns>False outside the map and for a NaN point.</returns>
        public bool IsNavigable(Vector2 normalized)
        {
            return TryGetCell(normalized, out int x, out int y) && IsNavigable(x, y);
        }

        /// <returns>True when the cell changed. A cell outside the grid is ignored.</returns>
        public bool SetNavigable(int x, int y, bool navigable)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                return false;
            }

            int index = y * Width + x;
            int mask = 1 << (index & 7);
            bool current = (bits[index >> 3] & mask) != 0;

            if (current == navigable)
            {
                return false;
            }

            bits[index >> 3] ^= (byte)mask;
            return true;
        }

        public void Clear(bool navigable)
        {
            byte value = navigable ? (byte)0xFF : (byte)0x00;

            for (int i = 0; i < bits.Length; i++)
            {
                bits[i] = value;
            }

            ClearUnusedBits();
        }

        /// <returns>A copy of the packed cells.</returns>
        public byte[] ToBytes()
        {
            return (byte[])bits.Clone();
        }

        private bool TryGetCell(Vector2 normalized, out int x, out int y)
        {
            x = 0;
            y = 0;

            // Written so that a NaN component fails the test.
            if (!(normalized.x >= 0f && normalized.x <= 1f && normalized.y >= 0f && normalized.y <= 1f))
            {
                return false;
            }

            // A coordinate of exactly 1 belongs to the last cell.
            x = Mathf.Min((int)(normalized.x * Width), Width - 1);
            y = Mathf.Min((int)(normalized.y * Height), Height - 1);
            return true;
        }

        private void ClearUnusedBits()
        {
            int usedBits = (Width * Height) & 7;

            if (usedBits != 0)
            {
                bits[bits.Length - 1] &= (byte)((1 << usedBits) - 1);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }
    }
}
