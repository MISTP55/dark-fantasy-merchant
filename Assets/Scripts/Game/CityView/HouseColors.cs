using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>The colour of a house by the class it shelters, the same in the city view and the layout window.</summary>
    public static class HouseColors
    {
        public static readonly Color Rich = new Color32(0xC9, 0xA2, 0x27, 0xFF);
        public static readonly Color Wealthy = new Color32(0xB8, 0x6E, 0x9E, 0xFF);
        public static readonly Color Poor = new Color32(0x9A, 0x7B, 0x5B, 0xFF);

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
