using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The colour of a house by the class it shelters, the same in the city view and the layout window: shades of
    /// yellow of one hue and lightness, the duller the poorer.
    /// </summary>
    public static class HouseColors
    {
        public static readonly Color Rich = new Color32(0xE0, 0xBC, 0x2A, 0xFF);
        public static readonly Color Wealthy = new Color32(0xBC, 0xA6, 0x4E, 0xFF);
        public static readonly Color Poor = new Color32(0x9E, 0x94, 0x6D, 0xFF);

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
