using System;
using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using UnityEngine;

namespace DarkFantasyMerchant.Game
{
    /// <summary>
    /// The one place through which the player builds: quotes a building in a city, builds it at a
    /// place of the city's grid, lays the roads of a trace or builds the city's trading post, pays from the treasury
    /// and, first, from the player's trading post in the city, then the hold of the player's ship docked there
    /// (<see cref="MaterialsIn"/>), announces it and tells the debug tools of each material bought.
    /// The debug tools can make every building free (<see cref="SetFree"/>).
    /// </summary>
    public sealed class PlayerBuilder : MonoBehaviour
    {
        [SerializeField] private WorldEconomy worldEconomy;
        [SerializeField] private PlayerTreasury playerTreasury;
        [SerializeField] private PlayerNotifications playerNotifications;
        [SerializeField] private ShipsView shipsView;

        // Reused: asked for at each quote.
        private readonly List<Ship> shipsInPort = new List<Ship>();

        /// <summary>Raised for each material a building bought from the market: its good, units and gold. For the debug tools.</summary>
        public event Action<CityDefinition, int, int, long> MaterialBought;

        /// <summary>Raised when buildings become free or stop being free: every quote has changed.</summary>
        public event Action FreeChanged;

        /// <summary>
        /// For the debug tools: every building is quoted at 0 gold and takes neither gold nor materials,
        /// even from a treasury in debt. Off for the whole session unless they switch it on.
        /// </summary>
        public bool IsFree { get; private set; }

        /// <summary>Makes every building free, or not; only in the Editor and development builds.</summary>
        public void SetFree(bool isFree)
        {
            if (!Debug.isDebugBuild || isFree == IsFree)
            {
                return;
            }

            IsFree = isFree;
            FreeChanged?.Invoke();
        }

        /// <summary>"Construction achevée à Lübeck : Bûcheron.": a building's name has no known gender, the sentence needs none.</summary>
        public static string BuildingBuiltText(string city, string building)
        {
            return $"Construction achevée à {city} : {building}.";
        }

        /// <summary>What building a house in the city would cost now; impossible without a market, an offer with a cost or a treasury.</summary>
        public ConstructionQuote QuoteHouse(CityDefinition city, HouseOffer offer)
        {
            if (!TryGetContext(city, out CityMarket market, out Treasury treasury) || offer?.Cost == null)
            {
                return default;
            }

            return Construction.QuoteHouse(market, MaterialsIn(city), treasury, offer.Cost, IsFree);
        }

        /// <summary>What building a workshop in the city would cost now; impossible without a market, an offer with a cost or a treasury.</summary>
        public ConstructionQuote QuoteWorkshop(CityDefinition city, WorkshopOffer offer)
        {
            if (!TryGetContext(city, out CityMarket market, out Treasury treasury) || offer?.Cost == null)
            {
                return default;
            }

            return Construction.QuoteWorkshop(market, MaterialsIn(city), treasury, offer.Cost, offer.Good, IsFree);
        }

        /// <returns>True when the house was built at the place; nothing was paid otherwise.</returns>
        public bool TryBuildHouse(CityDefinition city, HouseOffer offer, GridPlace place)
        {
            if (!TryGetContext(city, out CityMarket market, out Treasury treasury) || offer?.Cost == null)
            {
                return false;
            }

            bool built = Construction.TryBuildHouse(
                market, MaterialsIn(city), treasury, offer.Cost, offer.HousedClass, offer.Room, offer.Footprint, place,
                (good, units, gold) => MaterialBought?.Invoke(city, good, units, gold), IsFree);

            if (built)
            {
                Announce(city, offer.Name);
            }

            return built;
        }

        /// <returns>True when the workshop was built at the place; nothing was paid otherwise.</returns>
        public bool TryBuildWorkshop(CityDefinition city, WorkshopOffer offer, GridPlace place)
        {
            if (!TryGetContext(city, out CityMarket market, out Treasury treasury) || offer?.Cost == null)
            {
                return false;
            }

            bool built = Construction.TryBuildWorkshop(
                market, MaterialsIn(city), treasury, offer.Cost, offer.Good, offer.Jobs, offer.Footprint, place,
                (good, units, gold) => MaterialBought?.Invoke(city, good, units, gold), IsFree);

            if (built)
            {
                Announce(city, offer.Name);
            }

            return built;
        }

        /// <summary>
        /// What one new cell of road in the city would cost now, wherever it is laid; impossible without a market,
        /// an offer with a cost or a treasury.
        /// </summary>
        public ConstructionQuote QuoteRoadCell(CityDefinition city, RoadOffer offer)
        {
            if (!TryGetContext(city, out CityMarket market, out Treasury treasury) || offer?.CostPerCell == null)
            {
                return default;
            }

            return Construction.QuoteRoadCell(market, MaterialsIn(city), treasury, offer.CostPerCell, IsFree);
        }

        /// <summary>
        /// What laying the roads of a trace in the city would cost now: its new cells only; impossible without a
        /// market, an offer with a cost or a treasury, when a cell cannot hold a road or none is new.
        /// </summary>
        public ConstructionQuote QuoteRoads(CityDefinition city, RoadOffer offer, IReadOnlyList<Vector2Int> cells)
        {
            if (!TryGetContext(city, out CityMarket market, out Treasury treasury) || offer?.CostPerCell == null)
            {
                return default;
            }

            return Construction.QuoteRoads(market, MaterialsIn(city), treasury, offer.CostPerCell, cells, IsFree);
        }

        /// <summary>Lays the roads of a trace in the city, announced once for the whole trace.</summary>
        /// <returns>True when they were laid; nothing was paid otherwise.</returns>
        public bool TryBuildRoads(CityDefinition city, RoadOffer offer, IReadOnlyList<Vector2Int> cells)
        {
            if (!TryGetContext(city, out CityMarket market, out Treasury treasury))
            {
                return false;
            }

            NotificationFeed feed = playerNotifications != null && playerNotifications.IsReady ? playerNotifications.Feed : null;
            return TryBuildRoads(
                market, MaterialsIn(city), treasury, feed, city.DisplayName, offer, cells,
                (good, units, gold) => MaterialBought?.Invoke(city, good, units, gold), IsFree);
        }

        /// <summary>
        /// Lays the roads of a trace through <see cref="Construction.TryBuildRoads"/> and, when they are laid, posts
        /// one notification for the trace to the feed (none when it is null). <paramref name="materials"/> may be null.
        /// </summary>
        /// <returns>True when they were laid; nothing was paid otherwise, nor an offer without a cost.</returns>
        internal static bool TryBuildRoads(
            CityMarket market,
            IGoodSource materials,
            Treasury treasury,
            NotificationFeed feed,
            string cityName,
            RoadOffer offer,
            IReadOnlyList<Vector2Int> cells,
            Action<int, int, long> materialBought,
            bool isFree)
        {
            if (offer?.CostPerCell == null
                || !Construction.TryBuildRoads(market, materials, treasury, offer.CostPerCell, cells, materialBought, isFree))
            {
                return false;
            }

            feed?.Post(BuildingBuiltText(cityName, offer.Name));
            return true;
        }

        /// <summary>
        /// What building the city's trading post would cost now; impossible without a market, an offer with a cost or
        /// a treasury, and once it stands.
        /// </summary>
        public ConstructionQuote QuoteTradingPost(CityDefinition city, TradingPostOffer offer)
        {
            if (!TryGetContext(city, out CityMarket market, out Treasury treasury))
            {
                return default;
            }

            return QuoteTradingPost(market, MaterialsIn(city), treasury, offer, IsFree);
        }

        /// <summary>Builds the city's trading post on its reserved site and opens it, empty.</summary>
        /// <returns>True when it was built; nothing was paid otherwise.</returns>
        public bool TryBuildTradingPost(CityDefinition city, TradingPostOffer offer)
        {
            if (!TryGetContext(city, out CityMarket market, out Treasury treasury))
            {
                return false;
            }

            NotificationFeed feed = playerNotifications != null && playerNotifications.IsReady ? playerNotifications.Feed : null;
            return TryBuildTradingPost(
                market, MaterialsIn(city), treasury, feed, city, worldEconomy.OpenTradingPost, offer,
                (good, units, gold) => MaterialBought?.Invoke(city, good, units, gold), IsFree);
        }

        /// <summary>Quotes the trading post through <see cref="Construction.QuoteTradingPost"/>; impossible for an offer without a cost.</summary>
        internal static ConstructionQuote QuoteTradingPost(
            CityMarket market, IGoodSource materials, Treasury treasury, TradingPostOffer offer, bool isFree)
        {
            return offer?.Cost == null ? default : Construction.QuoteTradingPost(market, materials, treasury, offer.Cost, isFree);
        }

        /// <summary>
        /// Builds the trading post through <see cref="Construction.TryBuildTradingPost"/> and, when it stands, opens the
        /// city's warehouse and posts the notification to the feed (none when it is null).
        /// </summary>
        /// <returns>True when it was built; nothing was paid otherwise, nor for an offer without a cost.</returns>
        internal static bool TryBuildTradingPost(
            CityMarket market,
            IGoodSource materials,
            Treasury treasury,
            NotificationFeed feed,
            CityDefinition city,
            PlayerTradingPosts posts,
            TradingPostOffer offer,
            Action<int, int, long> materialBought,
            bool isFree)
        {
            return TryBuildTradingPost(
                market, materials, treasury, feed, city, builtIn => posts.Open(builtIn, market.Grid), offer, materialBought, isFree);
        }

        private static bool TryBuildTradingPost(
            CityMarket market,
            IGoodSource materials,
            Treasury treasury,
            NotificationFeed feed,
            CityDefinition city,
            Func<CityDefinition, Warehouse> open,
            TradingPostOffer offer,
            Action<int, int, long> materialBought,
            bool isFree)
        {
            if (offer?.Cost == null
                || !Construction.TryBuildTradingPost(market, materials, treasury, offer.Cost, materialBought, isFree))
            {
                return false;
            }

            open(city);
            feed?.Post(BuildingBuiltText(city.DisplayName, offer.Name));
            return true;
        }

        private bool TryGetContext(CityDefinition city, out CityMarket market, out Treasury treasury)
        {
            market = null;
            treasury = null;

            if (city == null || worldEconomy == null || !worldEconomy.IsReady
                || playerTreasury == null || !playerTreasury.IsReady)
            {
                return false;
            }

            market = worldEconomy.MarketOf(city);
            treasury = playerTreasury.Treasury;
            return market != null;
        }

        /// <summary>
        /// Where a building's materials come from in the city before the rest is bought: the player's trading post
        /// there, then the hold of <see cref="HoldIn"/>; either may be missing.
        /// </summary>
        public IGoodSource MaterialsIn(CityDefinition city)
        {
            return MaterialsOf(worldEconomy != null ? worldEconomy.TradingPostOf(city) : null, HoldIn(city));
        }

        /// <summary>The trading post's warehouse, then the hold, as one source; either may be null.</summary>
        internal static IGoodSource MaterialsOf(Warehouse tradingPost, CargoHold hold)
        {
            return new MaterialSources(tradingPost, hold);
        }

        /// <summary>
        /// The hold the materials come from after the trading post's: that of the player's ship that trades in the city's
        /// port (the first by name), or null when none lies there: everything is then bought.
        /// </summary>
        public CargoHold HoldIn(CityDefinition city)
        {
            if (shipsView == null)
            {
                return null;
            }

            shipsView.Docking.GetShipsIn(city, shipsInPort);
            NameOrder.Sort(shipsInPort, shipsView.DisplayNameOf);

            return TradingShipChoice.Choose(shipsInPort, shipsView.IsPlayerShip, null)?.Cargo;
        }

        private void Announce(CityDefinition city, string building)
        {
            if (playerNotifications != null && playerNotifications.IsReady)
            {
                playerNotifications.Feed.Post(BuildingBuiltText(city.DisplayName, building));
            }
        }
    }
}
