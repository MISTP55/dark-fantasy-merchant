using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>The arithmetic of the debug charts: their vertical range and its steps, where a day is drawn, and the dashes of a line.</summary>
    public static class ChartMath
    {
        // The least room between two anchors of the dashes, in pixels.
        private const float AnchorPixels = 24f;

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
        /// The days between two anchors of the dashes of a line: a power of two of at least
        /// 24 pixels. Anchors fall on days of the game, so that the dashes stay where they are as
        /// days come and go.
        /// </summary>
        public static int AnchorPeriodDays(float pixelsPerDay)
        {
            if (pixelsPerDay <= 0f)
            {
                return 1;
            }

            int days = 1;

            while (days * pixelsPerDay < AnchorPixels - 1e-4f && days < 1 << 20)
            {
                days *= 2;
            }

            return days;
        }

        /// <summary>
        /// The dashes of a line, measured along it: a dash starts at each anchor, and the points
        /// before the first anchor are dashed backwards from it, starting with a gap. Whatever
        /// point the line starts at, its dashes from that anchor on stay the same: they do not
        /// crawl along it when the chart loses its first day. Without an anchor, the dashes start
        /// at the first point.
        /// </summary>
        /// <param name="anchors">One per point: whether the pattern starts again there.</param>
        /// <param name="into">The dashes are added to it, each from one point to another.</param>
        public static void DashPath(IReadOnlyList<Vector2> points, IReadOnlyList<bool> anchors, float dash, float gap, List<(Vector2, Vector2)> into)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (anchors == null || anchors.Count < points.Count)
            {
                throw new ArgumentException("One anchor flag per point.", nameof(anchors));
            }

            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            if (points.Count < 2 || dash <= 0f || gap < 0f)
            {
                return;
            }

            int first = 0;

            while (first < points.Count && !anchors[first])
            {
                first++;
            }

            if (first == points.Count)
            {
                first = 0;
            }

            // Backwards from the first anchor, a gap first: the dash after the anchor follows on.
            var walker = new DashWalker(dash, gap, false);

            for (int i = first; i > 0; i--)
            {
                walker.Walk(points[i], points[i - 1], into);
            }

            walker = new DashWalker(dash, gap, true);

            for (int i = first; i < points.Count - 1; i++)
            {
                if (anchors[i])
                {
                    walker = new DashWalker(dash, gap, true);
                }

                walker.Walk(points[i], points[i + 1], into);
            }
        }

        // Walks segments one after the other, switching between dash and gap along their length.
        private struct DashWalker
        {
            private readonly float dash;
            private readonly float gap;
            private bool inDash;
            private float left;

            public DashWalker(float dash, float gap, bool startsWithDash)
            {
                this.dash = dash;
                this.gap = gap;
                inDash = startsWithDash;
                left = startsWithDash ? dash : gap;
            }

            public void Walk(Vector2 from, Vector2 to, List<(Vector2, Vector2)> into)
            {
                float length = Vector2.Distance(from, to);

                while (length > 0f)
                {
                    float step = Math.Min(left, length);
                    Vector2 next = Vector2.MoveTowards(from, to, step);

                    if (inDash && step > 0f)
                    {
                        into.Add((from, next));
                    }

                    from = next;
                    length -= step;
                    left -= step;

                    if (left <= 1e-5f)
                    {
                        inDash = !inDash;
                        left = inDash ? dash : gap;
                    }
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
