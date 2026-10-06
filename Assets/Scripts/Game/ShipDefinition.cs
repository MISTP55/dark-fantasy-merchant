using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>Static definition of a kind of ship. Holds no runtime state.</summary>
    [CreateAssetMenu(fileName = "Ship", menuName = "Dark Fantasy Merchant/Ship")]
    public sealed class ShipDefinition : ScriptableObject
    {
        public const int DirectionCount = 8;

        [SerializeField] private string displayName;

        [Tooltip("Sailing speed, in world units per second.")]
        [SerializeField, Min(0.01f)] private float speed = 1.5f;

        [Tooltip("One sprite per heading, clockwise from north: N, NE, E, SE, S, SW, W, NW.")]
        [SerializeField] private Sprite[] directionSprites = new Sprite[DirectionCount];

        public string DisplayName => displayName;

        public float Speed => speed;

        /// <returns>The sprite for a heading, or null when none is assigned.</returns>
        public Sprite SpriteFor(CompassDirection direction)
        {
            int index = (int)direction;

            return directionSprites != null && index >= 0 && index < directionSprites.Length
                ? directionSprites[index]
                : null;
        }
    }
}
