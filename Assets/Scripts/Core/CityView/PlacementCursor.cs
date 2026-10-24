using UnityEngine;

namespace DarkFantasyMerchant.Core
{
    /// <summary>The footprint being placed and the way it is turned.</summary>
    public sealed class PlacementCursor
    {
        public bool IsPlacing { get; private set; }

        public Footprint Footprint { get; private set; }

        public BuildingOrientation Orientation { get; private set; }

        public void Start(Footprint footprint)
        {
            Footprint = footprint;
            Orientation = BuildingOrientation.Along;
            IsPlacing = true;
        }

        /// <summary>Turns the footprint; nothing happens when not placing or when it is square.</summary>
        public void Rotate()
        {
            if (!IsPlacing || Footprint.IsSquare)
            {
                return;
            }

            Orientation = Orientation == BuildingOrientation.Along ? BuildingOrientation.Across : BuildingOrientation.Along;
        }

        public void Stop()
        {
            IsPlacing = false;
            Orientation = BuildingOrientation.Along;
        }

        /// <summary>The place whose origin is the hovered cell.</summary>
        public GridPlace PlaceAt(Vector2Int hoveredCell)
        {
            return new GridPlace(hoveredCell, Orientation);
        }
    }
}
