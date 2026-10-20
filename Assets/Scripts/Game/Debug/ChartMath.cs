using System;
using System.Collections.Generic;

namespace DarkFantasyMerchant.Game
{
    /// <summary>The arithmetic of the debug charts: their vertical range and where a day is drawn.</summary>
    public static class ChartMath
    {
        /// <summary>
        /// The smallest and the largest of the first <paramref name="count"/> values of every
        /// series, NaN left out; a flat range is widened by one on each side, or to [0, 1] at 0.
        /// </summary>
        /// <returns>False when no value is a number.</returns>
        public static bool TryRange(IReadOnlyList<float[]> series, int count, out float min, out float max)
        {
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;

            if (series != null)
            {
                foreach (float[] values in series)
                {
                    if (values == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < Math.Min(count, values.Length); i++)
                    {
                        float value = values[i];

                        if (float.IsNaN(value) || float.IsInfinity(value))
                        {
                            continue;
                        }

                        min = Math.Min(min, value);
                        max = Math.Max(max, value);
                    }
                }
            }

            if (min > max)
            {
                min = 0f;
                max = 1f;
                return false;
            }

            if (min == max)
            {
                if (min == 0f)
                {
                    max = 1f;
                }
                else
                {
                    min -= 1f;
                    max += 1f;
                }
            }

            return true;
        }

        /// <summary>The height from the top of a value: the maximum at the top, the minimum at the bottom.</summary>
        public static float YOf(float value, float min, float max, float height)
        {
            return max > min ? (max - value) / (max - min) * height : height;
        }

        /// <summary>Where a day is drawn: the first at the left, the last at the right, a lone one in the middle.</summary>
        public static float XOf(int index, int count, float width)
        {
            return count > 1 ? (float)index / (count - 1) * width : width / 2f;
        }

        /// <summary>The day nearest a point of the width, within the days; -1 when there is none.</summary>
        public static int DayIndexAt(float x, float width, int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            if (count == 1 || width <= 0f)
            {
                return 0;
            }

            int index = (int)Math.Round(x / width * (count - 1), MidpointRounding.AwayFromZero);

            return Math.Max(0, Math.Min(count - 1, index));
        }
    }
}
