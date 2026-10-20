using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>The arithmetic of the debug charts: their vertical range and its steps, where a day is drawn, and the dashes of a line.</summary>
    public static class ChartMath
    {
        // A dash and its gap, in pixels, about.
        private const float DashPixels = 7f;

        // Not the French culture: its separators and its minus sign depend on the platform.
        private static readonly NumberFormatInfo TickFormat = new NumberFormatInfo
        {
            NumberGroupSeparator = " ",
            NumberDecimalSeparator = ",",
        };

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

        /// <summary>
        /// A range around [<paramref name="min"/>, <paramref name="max"/>] from a multiple of a round
        /// step to another: 1, 2, 2.5 or 5 times a power of ten, the smallest that makes at most
        /// <paramref name="maxTicks"/> steps.
        /// </summary>
        public static void NiceScale(float min, float max, int maxTicks, out float niceMin, out float niceMax, out float step)
        {
            if (!(max > min))
            {
                min -= 1f;
                max += 1f;
            }

            double raw = (max - (double)min) / Math.Max(1, maxTicks);
            double power = Math.Pow(10d, Math.Floor(Math.Log10(raw)));
            double nice = power * 10d;

            foreach (double factor in new[] { 1d, 2d, 2.5d, 5d, 10d })
            {
                // Slack for the floats, so that a step of 0.200000003 is taken for 0.2.
                if (factor * power >= raw * (1d - 1e-6))
                {
                    nice = factor * power;
                    break;
                }
            }

            step = (float)nice;
            // Slack for the floats: 0.8 over 0.2 is 4.0000001.
            niceMin = (float)(Math.Floor(min / nice + 1e-6) * nice);
            niceMax = (float)(Math.Ceiling(max / nice - 1e-6) * nice);
        }

        /// <summary>A value of a scale as its axis writes it: as many decimals as its step needs, the French way.</summary>
        public static string FormatTick(float value, float step)
        {
            // The fewest decimals that write the step whole: 0,25 needs two, 2,5 one.
            int decimals = 0;

            while (decimals < 6 && step > 0f && Math.Abs(step * Math.Pow(10d, decimals) - Math.Round(step * Math.Pow(10d, decimals))) > 1e-4)
            {
                decimals++;
            }

            // Rounded to the decimals of the step: 0,6000001 is 0,6, and -0 is 0.
            double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);

            return (rounded == 0d ? 0d : rounded).ToString("N" + decimals, TickFormat);
        }

        /// <summary>
        /// The days a dash and its gap last: a power of two near seven pixels. Days are what the
        /// dashes are anchored to, so that they move with the curve rather than along it.
        /// </summary>
        public static float DashPeriodDays(float pixelsPerDay)
        {
            if (pixelsPerDay <= 0f)
            {
                return 1f;
            }

            return Mathf.Pow(2f, Mathf.Ceil(Mathf.Log(DashPixels / pixelsPerDay, 2f) - 1e-4f));
        }

        /// <summary>
        /// The dashes of a line from one day to another, days counted from the start of the game:
        /// each period starts with a dash that lasts its share of it, whatever day the line starts.
        /// </summary>
        public static void DashSpans(double from, double to, double period, double dashShare, List<(double, double)> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            if (period <= 0d || to <= from)
            {
                return;
            }

            for (double start = Math.Floor(from / period) * period; start < to; start += period)
            {
                double dashStart = Math.Max(start, from);
                double dashEnd = Math.Min(start + period * dashShare, to);

                if (dashEnd > dashStart)
                {
                    into.Add((dashStart, dashEnd));
                }
            }
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
