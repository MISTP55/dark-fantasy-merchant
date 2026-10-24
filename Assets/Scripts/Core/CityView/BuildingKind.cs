using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>What a building is: the house of a class, or the workshop of a good.</summary>
    public readonly struct BuildingKind
    {
        private readonly bool isHouse;
        private readonly SocialClass housedClass;
        private readonly int good;

        private BuildingKind(bool isHouse, SocialClass housedClass, int good)
        {
            this.isHouse = isHouse;
            this.housedClass = housedClass;
            this.good = good;
        }

        public static BuildingKind House(SocialClass housedClass)
        {
            return new BuildingKind(true, housedClass, -1);
        }

        public static BuildingKind Workshop(int good)
        {
            return new BuildingKind(false, default, good);
        }

        public bool IsHouse => isHouse;

        /// <summary>The class a house lodges; only for a house.</summary>
        public SocialClass HousedClass => isHouse ? housedClass : throw new InvalidOperationException("Not a house.");

        /// <summary>The index of the good a workshop makes; only for a workshop.</summary>
        public int Good => !isHouse ? good : throw new InvalidOperationException("Not a workshop.");
    }
}
