using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>The colours of the debug charts, those of the mockups.</summary>
    public static class DebugChartColors
    {
        public static readonly Color Rich = Hex(0xD6B85A);
        public static readonly Color Wealthy = Hex(0x7AB0E0);
        public static readonly Color Poor = Hex(0xE0705A);
        public static readonly Color Beggars = Hex(0x8F7F69);
        public static readonly Color Workers = Hex(0x8FCB7A);
        public static readonly Color Reserve = Hex(0xBFAE94);
        public static readonly Color Stock = Hex(0xE8DCC8);
        public static readonly Color Price = Hex(0xE0A050);

        // The bands of a stock: under T0, T0-T1, T1-T2, T2-T3.
        public static readonly Color BelowT0 = new Color(0.88f, 0.44f, 0.35f, 0.18f);
        public static readonly Color T0ToT1 = new Color(0.88f, 0.71f, 0.35f, 0.12f);
        public static readonly Color T1ToT2 = new Color(0.56f, 0.80f, 0.48f, 0.10f);
        public static readonly Color T2ToT3 = new Color(0.48f, 0.69f, 0.88f, 0.10f);

        /// <summary>The colours of the curves of a composed chart, in turn.</summary>
        public static readonly Color[] Palette =
        {
            Hex(0xE0A050), Hex(0x7AB0E0), Hex(0x8FCB7A), Hex(0xE0705A),
            Hex(0xD6B85A), Hex(0xB48EDB), Hex(0x6CCFC4), Hex(0xE8DCC8),
        };

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }
    }
}
