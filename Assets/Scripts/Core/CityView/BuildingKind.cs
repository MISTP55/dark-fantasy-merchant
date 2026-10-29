using System;

namespace DarkFantasyMerchant.Core
{
    /// <summary>
    /// What a building is: the house of a class, the workshop of a good, a road, the town hall, the port, a wall or the
    /// trading post, and what it does on the grid. The grid asks what a building does (<see cref="CarriesNetwork"/>,
    /// <see cref="NeedsConnection"/>, <see cref="CoversOneCell"/>, <see cref="ClosesWalls"/>), never which one it is,
    /// so that a new kind of building is given its behaviour here, in <see cref="TraitsOf"/>, and nowhere else.
    /// </summary>
    public readonly struct BuildingKind
    {
        private enum Type
        {
            House,
            Workshop,
            Road,
            TownHall,
            Port,
            Wall,
            TradingPost,
        }

        // What each type does, in the order of Type.
        private static readonly Traits[] TraitsOf =
        {
            new Traits("house", carriesNetwork: false, needsConnection: true, coversOneCell: false, closesWalls: false),
            new Traits("workshop", carriesNetwork: false, needsConnection: true, coversOneCell: false, closesWalls: false),
            new Traits("road", carriesNetwork: true, needsConnection: false, coversOneCell: true, closesWalls: false),
            new Traits("town hall", carriesNetwork: true, needsConnection: false, coversOneCell: false, closesWalls: false),
            new Traits("port", carriesNetwork: false, needsConnection: false, coversOneCell: false, closesWalls: false),
            new Traits("wall", carriesNetwork: false, needsConnection: false, coversOneCell: true, closesWalls: true),
            new Traits("trading post", carriesNetwork: false, needsConnection: false, coversOneCell: false, closesWalls: false),
        };

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

        /// <summary>A wall: one cell, closing in with the water the space where the other buildings stand.</summary>
        public static BuildingKind Wall()
        {
            return new BuildingKind(Type.Wall, default, -1);
        }

        /// <summary>The player's trading post: at most one per grid, on the reserved site; not part of the network.</summary>
        public static BuildingKind TradingPost()
        {
            return new BuildingKind(Type.TradingPost, default, -1);
        }

        public bool IsHouse => type == Type.House;

        public bool IsWorkshop => type == Type.Workshop;

        public bool IsRoad => type == Type.Road;

        public bool IsTownHall => type == Type.TownHall;

        public bool IsPort => type == Type.Port;

        public bool IsWall => type == Type.Wall;

        public bool IsTradingPost => type == Type.TradingPost;

        /// <summary>The class a house lodges; only for a house.</summary>
        public SocialClass HousedClass => IsHouse ? housedClass : throw new InvalidOperationException("Not a house.");

        /// <summary>The index of the good a workshop makes; only for a workshop.</summary>
        public int Good => IsWorkshop ? good : throw new InvalidOperationException("Not a workshop.");

        /// <summary>What the messages of the grid call it, in English: "town hall".</summary>
        public string Name => TraitsOf[(int)type].Name;

        /// <summary>
        /// Part of the road network once joined to the town hall, and what joins the others to it: the road and the
        /// town hall. Placing one floods the network again.
        /// </summary>
        public bool CarriesNetwork => TraitsOf[(int)type].CarriesNetwork;

        /// <summary>
        /// Works only once connected, by touching a network road: the house, which gives its room then, and the
        /// workshop, which hires then. Nothing else is ever connected.
        /// </summary>
        public bool NeedsConnection => TraitsOf[(int)type].NeedsConnection;

        /// <summary>Covers exactly one cell: the road and the wall.</summary>
        public bool CoversOneCell => TraitsOf[(int)type].CoversOneCell;

        /// <summary>
        /// Closes, with the water, the space inside the walls: the flood of the enclosure stops at it, and it stands
        /// outside the walls itself. The wall alone.
        /// </summary>
        public bool ClosesWalls => TraitsOf[(int)type].ClosesWalls;

        private readonly struct Traits
        {
            public Traits(string name, bool carriesNetwork, bool needsConnection, bool coversOneCell, bool closesWalls)
            {
                Name = name;
                CarriesNetwork = carriesNetwork;
                NeedsConnection = needsConnection;
                CoversOneCell = coversOneCell;
                ClosesWalls = closesWalls;
            }

            public string Name { get; }

            public bool CarriesNetwork { get; }

            public bool NeedsConnection { get; }

            public bool CoversOneCell { get; }

            public bool ClosesWalls { get; }
        }
    }
}
