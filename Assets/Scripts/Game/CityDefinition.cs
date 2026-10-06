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

        public string DisplayName => displayName;

        public string Description => description;

        public Vector2 MapPosition => mapPosition;

        public CityAccess Access => access;

        public CitySize Size => size;
    }
}
