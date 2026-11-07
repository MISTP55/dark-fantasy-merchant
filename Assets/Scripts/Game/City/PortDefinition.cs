using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The port: the building every city has, half on water, half on land. Placed by the city's layout; nobody
    /// builds it.
    /// </summary>
    [CreateAssetMenu(fileName = "Port", menuName = "Dark Fantasy Merchant/Port")]
    public sealed class PortDefinition : BuildingDefinition
    {
        [Tooltip("The front rows of the footprint, on the side the port faces, that stand on water; the others stand on land. At least 1 and under the depth.")]
        [SerializeField, Min(1)] private int waterRows = 2;

        [Tooltip("Cells per second of world time at which ships cross a city's harbour.")]
        [SerializeField, Min(0.01f)] private float shipCellsPerSecond = 2f;

        public int WaterRows => waterRows;

        /// <summary>The speed of a ship crossing a harbour, in cells per second of world time, whatever its crew.</summary>
        public float ShipCellsPerSecond => shipCellsPerSecond;

        /// <summary>The footprint split into water and land rows. Only for a port that <see cref="TryValidate"/> accepts.</summary>
        public PortFootprint PortFootprint => new PortFootprint(Footprint, waterRows);

        public override bool TryValidate(out string problem)
        {
            if (!base.TryValidate(out problem))
            {
                return false;
            }

            int depth = FootprintSize.y;

            if (waterRows < 1 || waterRows >= depth)
            {
                problem = $"its water rows are {waterRows}, not between 1 and {depth - 1}";
                return false;
            }

            // The negated comparison also rejects NaN.
            if (!(shipCellsPerSecond > 0f) || float.IsInfinity(shipCellsPerSecond))
            {
                problem = $"its ships cross the harbour at {shipCellsPerSecond} cells per second, not a positive speed";
                return false;
            }

            return true;
        }
    }
}
