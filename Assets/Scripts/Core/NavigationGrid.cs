using System;
using System.Collections.Generic;
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

        // Distance between two discs of a stroke, in cells.
        private const double StrokeSpacing = 0.5;

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

        /// <summary>
        /// Sets every cell whose center is within the radius of a normalized point, and
        /// always the cell under the point.
        /// </summary>
        /// <returns>True when at least one cell changed.</returns>
        public bool PaintDisc(Vector2 center, float radiusInCells, bool navigable)
        {
            if (!IsFinite(center) || !IsFinite(radiusInCells))
            {
                return false;
            }

            return PaintDiscAt(center.x * Width, center.y * Height, radiusInCells, navigable);
        }

        /// <summary>Paints discs along a segment, close enough to leave no gap.</summary>
        /// <returns>True when at least one cell changed.</returns>
        public bool PaintStroke(Vector2 from, Vector2 to, float radiusInCells, bool navigable)
        {
            if (!IsFinite(from) || !IsFinite(to) || !IsFinite(radiusInCells))
            {
                return false;
            }

            // In doubles: a pointer dragged far outside the map gives coordinates whose
            // float precision is coarser than a cell.
            double startX = (double)from.x * Width;
            double startY = (double)from.y * Height;
            double deltaX = (double)to.x * Width - startX;
            double deltaY = (double)to.y * Height - startY;

            // Only the part of the segment within reach of the grid can paint anything;
            // walking the rest would cost time in proportion to the pointer's distance.
            double reach = Math.Max(radiusInCells, 0f) + 1.0;
            double first = 0.0;
            double last = 1.0;

            if (!ClipToRange(startX, deltaX, -reach, Width + reach, ref first, ref last)
                || !ClipToRange(startY, deltaY, -reach, Height + reach, ref first, ref last))
            {
                return false;
            }

            double length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY) * (last - first);
            int steps = Math.Max(1, (int)Math.Ceiling(length / StrokeSpacing));
            bool changed = false;

            for (int i = 0; i <= steps; i++)
            {
                double t = first + (last - first) * i / steps;

                changed |= PaintDiscAt(
                    (float)(startX + deltaX * t),
                    (float)(startY + deltaY * t),
                    radiusInCells,
                    navigable);
            }

            return changed;
        }

        /// <summary>
        /// Sets the region of cells connected to the one under a normalized point that
        /// share its value. Cells are connected by their sides, not their corners, so a
        /// one-cell-thick diagonal coastline holds.
        /// </summary>
        /// <returns>True when at least one cell changed.</returns>
        public bool Fill(Vector2 point, bool navigable)
        {
            if (!TryGetCell(point, out int startX, out int startY) || IsNavigable(startX, startY) == navigable)
            {
                return false;
            }

            // Iterative: a sea can hold hundreds of thousands of cells. A cell is set when
            // it is pushed, so it is never pushed twice.
            var pending = new Stack<int>();
            SetNavigable(startX, startY, navigable);
            pending.Push(startY * Width + startX);

            while (pending.Count > 0)
            {
                int index = pending.Pop();
                int x = index % Width;
                int y = index / Width;

                FillNeighbour(x - 1, y, navigable, pending);
                FillNeighbour(x + 1, y, navigable, pending);
                FillNeighbour(x, y - 1, navigable, pending);
                FillNeighbour(x, y + 1, navigable, pending);
            }

            return true;
        }

        /// <summary>
        /// A grid of another size in which each cell takes the value of the cell of this
        /// grid under its center.
        /// </summary>
        public NavigationGrid Resampled(int width, int height)
        {
            var result = new NavigationGrid(width, height);

            for (int y = 0; y < height; y++)
            {
                int sourceY = Math.Min((int)((y + 0.5) * Height / height), Height - 1);

                for (int x = 0; x < width; x++)
                {
                    int sourceX = Math.Min((int)((x + 0.5) * Width / width), Width - 1);

                    if (IsNavigable(sourceX, sourceY))
                    {
                        result.SetNavigable(x, y, true);
                    }
                }
            }

            return result;
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

        /// <param name="centerX">In cells: cell (x, y) spans x to x + 1.</param>
        private bool PaintDiscAt(float centerX, float centerY, float radiusInCells, bool navigable)
        {
            float radius = Mathf.Max(radiusInCells, 0f);
            bool changed = false;

            // The cell under the point is always painted, so a brush smaller than a cell
            // still draws.
            if (centerX >= 0f && centerX <= Width && centerY >= 0f && centerY <= Height)
            {
                changed |= SetNavigable(
                    Mathf.Min((int)centerX, Width - 1),
                    Mathf.Min((int)centerY, Height - 1),
                    navigable);
            }

            int minX = FloorClamped(centerX - radius, Width - 1);
            int maxX = FloorClamped(centerX + radius, Width - 1);
            int minY = FloorClamped(centerY - radius, Height - 1);
            int maxY = FloorClamped(centerY + radius, Height - 1);
            float squaredRadius = radius * radius;

            for (int y = minY; y <= maxY; y++)
            {
                float offsetY = y + 0.5f - centerY;

                for (int x = minX; x <= maxX; x++)
                {
                    float offsetX = x + 0.5f - centerX;

                    if (offsetX * offsetX + offsetY * offsetY <= squaredRadius)
                    {
                        changed |= SetNavigable(x, y, navigable);
                    }
                }
            }

            return changed;
        }

        // Clamped before the conversion: casting a float beyond the int range is undefined.
        private static int FloorClamped(float value, int max)
        {
            return (int)Mathf.Floor(Mathf.Clamp(value, 0f, max));
        }

        /// <summary>
        /// Narrows the part [first, last] of a segment to where one of its coordinates,
        /// start + delta * t, lies in [min, max]. False when no part remains.
        /// </summary>
        private static bool ClipToRange(
            double start, double delta, double min, double max, ref double first, ref double last)
        {
            if (delta == 0.0)
            {
                return start >= min && start <= max;
            }

            double enter = (min - start) / delta;
            double exit = (max - start) / delta;

            if (enter > exit)
            {
                double swap = enter;
                enter = exit;
                exit = swap;
            }

            first = Math.Max(first, enter);
            last = Math.Min(last, exit);
            return first <= last;
        }

        // SetNavigable ignores cells outside the grid and cells that already have the value.
        private void FillNeighbour(int x, int y, bool navigable, Stack<int> pending)
        {
            if (SetNavigable(x, y, navigable))
            {
                pending.Push(y * Width + x);
            }
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
