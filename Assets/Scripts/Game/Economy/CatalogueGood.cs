using System;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// A good of the economy's catalogue and whether it is active. A good that is not active
    /// does not exist in the game: no city stocks, consumes, produces or trades it.
    /// </summary>
    [Serializable]
    public struct CatalogueGood
    {
        [SerializeField] private GoodDefinition good;

        [SerializeField] private bool active;

        public CatalogueGood(GoodDefinition good, bool active)
        {
            this.good = good;
            this.active = active;
        }

        public GoodDefinition Good => good;

        public bool Active => active;
    }
}
