using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// Tells water from land by colour: on the map the seas are grey and the land is
    /// brown or orange, so water is whatever has little saturation.
    /// </summary>
    public static class WaterColorClassifier
    {
        /// <param name="maxSaturation">Highest HSV saturation, 0 to 1, still counted as water.</param>
        public static bool IsWater(Color32 color, float maxSaturation)
        {
            int max = Math.Max(color.r, Math.Max(color.g, color.b));
            int min = Math.Min(color.r, Math.Min(color.g, color.b));
            float saturation = max == 0 ? 0f : (max - min) / (float)max;

            return saturation <= maxSaturation;
        }
    }
}
