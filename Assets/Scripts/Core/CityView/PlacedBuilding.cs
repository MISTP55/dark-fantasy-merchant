using System;
using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>A building standing on the grid.</summary>
    public sealed class PlacedBuilding
    {
        private readonly PortFacing portFacing;

        internal PlacedBuilding(BuildingKind kind, BuildingOwner owner, Footprint footprint, GridPlace place)
        {
            Kind = kind;
            Owner = owner;
            Footprint = footprint;
            Place = place;
        }

        internal PlacedBuilding(BuildingKind kind, BuildingOwner owner, Footprint footprint, GridPlace place, PortFacing portFacing)
            : this(kind, owner, footprint, place)
        {
            this.portFacing = portFacing;
        }

        public BuildingKind Kind { get; }

        public BuildingOwner Owner { get; }

        public Footprint Footprint { get; }

        public GridPlace Place { get; }

        /// <summary>
        /// Whether a house or workshop touches a road of the network. Always false for roads and the town hall,
        /// which are the network, not its users, and for the port, which is neither.
        /// </summary>
        public bool IsConnected { get; internal set; }

        /// <summary>The cells covered on x and y, in the orientation of the place.</summary>
        /// <summary>The side the water rows of a port are on; only for a port.</summary>
        public PortFacing PortFacing => Kind.IsPort ? portFacing : throw new InvalidOperationException("Not a port.");

        public Vector2Int Size => Footprint.SizeFor(Place.Orientation);
    }
}
