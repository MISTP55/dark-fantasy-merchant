using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The colour of a house by the class it shelters, the same in the city view and the layout window: shades of
    /// blue, from light for the poor to dark for the rich.
    /// </summary>
    public static class HouseColors
    {
        public static readonly Color Rich = new Color32(0x1A, 0x3A, 0x8C, 0xFF);
        public static readonly Color Wealthy = new Color32(0x3F, 0x7F, 0xD0, 0xFF);
        public static readonly Color Poor = new Color32(0x9F, 0xCB, 0xF0, 0xFF);

        public static Color Of(SocialClass housedClass)
        {
            switch (housedClass)
            {
                case SocialClass.Rich:
                    return Rich;
                case SocialClass.Wealthy:
                    return Wealthy;
                default:
                    return Poor;
            }
        }
    }
}
