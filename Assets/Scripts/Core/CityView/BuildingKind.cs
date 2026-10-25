using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>What a building is: the house of a class, the workshop of a good, a road, the town hall or the port.</summary>
    public readonly struct BuildingKind
    {
        private enum Type
        {
            House,
            Workshop,
            Road,
            TownHall,
            Port,
        }

        private readonly Type type;
        private readonly SocialClass housedClass;
        private readonly int good;

        private BuildingKind(Type type, SocialClass housedClass, int good)
        {
            this.type = type;
            this.housedClass = housedClass;
            this.good = good;
        }

        public static BuildingKind House(SocialClass housedClass)
        {
            return new BuildingKind(Type.House, housedClass, -1);
        }

        public static BuildingKind Workshop(int good)
        {
            return new BuildingKind(Type.Workshop, default, good);
        }

        /// <summary>A road: one cell, part of the network when joined to the town hall.</summary>
        public static BuildingKind Road()
        {
            return new BuildingKind(Type.Road, default, -1);
        }

        /// <summary>The town hall: the root of the network, at most one per grid.</summary>
        public static BuildingKind TownHall()
        {
            return new BuildingKind(Type.TownHall, default, -1);
        }

        /// <summary>The port: half on water, at most one per grid; not part of the network.</summary>
        public static BuildingKind Port()
        {
            return new BuildingKind(Type.Port, default, -1);
        }

        public bool IsHouse => type == Type.House;

        public bool IsWorkshop => type == Type.Workshop;

        public bool IsRoad => type == Type.Road;

        public bool IsTownHall => type == Type.TownHall;

        public bool IsPort => type == Type.Port;

        /// <summary>The class a house lodges; only for a house.</summary>
        public SocialClass HousedClass => IsHouse ? housedClass : throw new InvalidOperationException("Not a house.");

        /// <summary>The index of the good a workshop makes; only for a workshop.</summary>
        public int Good => IsWorkshop ? good : throw new InvalidOperationException("Not a workshop.");
    }
}
