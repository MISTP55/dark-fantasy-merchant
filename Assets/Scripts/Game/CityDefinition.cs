using System.Collections.Generic;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>Static definition of a city. Holds no runtime state.</summary>
    [CreateAssetMenu(fileName = "City", menuName = "Dark Fantasy Merchant/City")]
    public sealed class CityDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField, TextArea(3, 8)] private string description;

        [Tooltip("Normalized map position: (0,0) is the bottom-left corner, (1,1) the top-right.")]
        [SerializeField] private Vector2 mapPosition = new Vector2(0.5f, 0.5f);

        [SerializeField] private CityAccess access;
        [SerializeField] private CitySize size;

        [Tooltip("Inhabitants. Sets how much of every good the city consumes.")]
        [SerializeField, Min(0)] private int population;

        [Tooltip("Goods the city produces well above what it consumes.")]
        [SerializeField] private List<GoodDefinition> efficientGoods = new List<GoodDefinition>();

        [Tooltip("Goods the city produces barely above what it consumes.")]
        [SerializeField] private List<GoodDefinition> inefficientGoods = new List<GoodDefinition>();

        public string DisplayName => displayName;

        public string Description => description;

        public Vector2 MapPosition => mapPosition;

        public CityAccess Access => access;

        public CitySize Size => size;

        public int Population => population;

        public IReadOnlyList<GoodDefinition> EfficientGoods => efficientGoods;

        public IReadOnlyList<GoodDefinition> InefficientGoods => inefficientGoods;

        /// <summary>How the city produces a good. A good in both lists is produced efficiently.</summary>
        public GoodProduction ProductionOf(GoodDefinition good)
        {
            if (good == null)
            {
                return GoodProduction.None;
            }

            if (efficientGoods.Contains(good))
            {
                return GoodProduction.Efficient;
            }

            return inefficientGoods.Contains(good) ? GoodProduction.Inefficient : GoodProduction.None;
        }
    }
}
