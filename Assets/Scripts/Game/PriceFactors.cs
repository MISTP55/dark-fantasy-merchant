using System;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The five factors of a price curve, as a setting of the economy: what a good's base price
    /// is multiplied by at an empty stock and at each of the four thresholds.
    /// </summary>
    [Serializable]
    public struct PriceFactors
    {
        [Tooltip("At an empty stock.")]
        [SerializeField, Min(0f)] private double empty;

        [Tooltip("At T0. Not above the factor at an empty stock.")]
        [SerializeField, Min(0f)] private double atT0;

        [Tooltip("At T1. Not above the factor at T0.")]
        [SerializeField, Min(0f)] private double atT1;

        [Tooltip("At T2. Not above the factor at T1.")]
        [SerializeField, Min(0f)] private double atT2;

        [Tooltip("At T3 and above. Not above the factor at T2.")]
        [SerializeField, Min(0f)] private double atT3;

        public PriceFactors(double empty, double atT0, double atT1, double atT2, double atT3)
        {
            this.empty = empty;
            this.atT0 = atT0;
            this.atT1 = atT1;
            this.atT2 = atT2;
            this.atT3 = atT3;
        }

        public double Empty => empty;

        public double AtT0 => atT0;

        public double AtT1 => atT1;

        public double AtT2 => atT2;

        public double AtT3 => atT3;

        /// <summary>The curve of these factors. Throws when they are not above 0 or rise with the stock.</summary>
        public PriceCurve ToCurve()
        {
            return new PriceCurve(empty, atT0, atT1, atT2, atT3);
        }
    }
}
