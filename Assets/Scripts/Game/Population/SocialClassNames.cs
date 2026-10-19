using System;
using DarkFantasyMerchant.Core;

namespace DarkFantasyMerchant.Game
{
    /// <summary>What the player reads for the houses of each class.</summary>
    public static class SocialClassNames
    {
        /// <summary>"Maisons de riches", "Maisons d'aisés", "Maisons de pauvres".</summary>
        public static string HousesOf(SocialClass housedClass)
        {
            return "Maisons" + OfThe(housedClass);
        }

        /// <summary>"Maison de riches", "Maison d'aisés", "Maison de pauvres".</summary>
        public static string HouseOf(SocialClass housedClass)
        {
            return "Maison" + OfThe(housedClass);
        }

        private static string OfThe(SocialClass housedClass)
        {
            switch (housedClass)
            {
                case SocialClass.Rich:
                    return " de riches";
                case SocialClass.Wealthy:
                    return " d'aisés";
                case SocialClass.Poor:
                    return " de pauvres";
                default:
                    throw new ArgumentOutOfRangeException(nameof(housedClass));
            }
        }
    }
}
