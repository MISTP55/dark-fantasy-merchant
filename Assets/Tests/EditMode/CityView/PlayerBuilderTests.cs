using System;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class PlayerBuilderTests
    {
        private static readonly RoadOffer Road = new RoadOffer("Route", new BuildingCost(50, Array.Empty<MaterialCost>()), "50 or");

        [Test]
        public void BuildingBuiltText_NamesTheCityAndTheBuilding()
        {
            Assert.AreEqual("Construction achevée à Lübeck : Bûcheron.", PlayerBuilder.BuildingBuiltText("Lübeck", "Bûcheron"));
            Assert.AreEqual("Construction achevée à Riga : Maison d'aisés.", PlayerBuilder.BuildingBuiltText("Riga", "Maison d'aisés"));
        }

        [Test]
        public void TryBuildRoads_PostsOneNotification()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(10, 6);
            var house = new CityHousingRule(SocialClass.Poor, 0.95, 100, new MaterialCost[0], new Footprint(1, 1));
            var market = new CityMarket(new[] { new MarketGood(10, 2, 0, 0) },
                TestMarkets.Thresholds, TestMarkets.Workforce, 30, TestMarkets.Pricing, 7,
                new CityPopulation(new PerClass<int>(0, 0, 100), 0, new PerClass<int>(0, 0, 100)), TestMarkets.Labour,
                new[] { house }, null, grid, new RandomPlacement(new System.Random(1)));
            var treasury = new Treasury(1000);
            var feed = new NotificationFeed(5f, 1f);
            var trace = new[] { new Vector2Int(3, 0), new Vector2Int(4, 0), new Vector2Int(5, 0) };

            bool built = PlayerBuilder.TryBuildRoads(market, null, treasury, feed, "Riga", Road, trace, null, false);

            Assert.IsTrue(built);
            Assert.AreEqual(850, treasury.Gold);
            Assert.AreEqual(1, feed.Notifications.Count);
            Assert.AreEqual("Construction achevée à Riga : Route.", feed.Notifications[0].Text);
            Assert.IsFalse(PlayerBuilder.TryBuildRoads(market, null, treasury, feed, "Riga", Road, trace, null, false));
            Assert.AreEqual(1, feed.Notifications.Count);
        }

        private static readonly TradingPostOffer Post = new TradingPostOffer(
            "Comptoir", new BuildingCost(500, new[] { new MaterialCost(0, 3) }), "500 or, 3 bois", new Footprint(3, 2));

        // A market on a grid whose trading post site is reserved at (1, 1).
        private static CityMarket PostMarket()
        {
            var grid = new CityGrid(8, 8, new Vector2Int[0]);
            grid.ReserveTradingPostSite(new Footprint(3, 2), new GridPlace(new Vector2Int(1, 1), BuildingOrientation.Along));
            var house = new CityHousingRule(SocialClass.Poor, 0.95, 100, new MaterialCost[0], new Footprint(1, 1));

            return new CityMarket(new[] { new MarketGood(10, 2, 0, 0) },
                TestMarkets.Thresholds, TestMarkets.Workforce, 30, TestMarkets.Pricing, 7,
                new CityPopulation(new PerClass<int>(0, 0, 100), 0, new PerClass<int>(0, 0, 100)), TestMarkets.Labour,
                new[] { house }, null, grid, new RandomPlacement(new System.Random(1)));
        }

        private static CityDefinition City(string displayName)
        {
            var city = ScriptableObject.CreateInstance<CityDefinition>();
            var serialized = new SerializedObject(city);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return city;
        }

        [Test]
        public void QuoteTradingPost_CostsTheOffersGoldAndMaterials()
        {
            CityMarket market = PostMarket();
            var treasury = new Treasury(10000);
            long expected = 500 + market.BuyPriceOf(0, 0) + market.BuyPriceOf(0, 1) + market.BuyPriceOf(0, 2);

            ConstructionQuote quote = PlayerBuilder.QuoteTradingPost(market, null, treasury, Post, false);
            var unavailable = new TradingPostOffer("Comptoir", null, "indisponible", new Footprint(3, 2));

            Assert.IsTrue(quote.IsPossible);
            Assert.AreEqual(expected, quote.Gold);
            Assert.IsFalse(PlayerBuilder.QuoteTradingPost(market, null, treasury, unavailable, false).IsPossible);
        }

        [Test]
        public void TryBuildTradingPost_OpensTheCitysTradingPostAndAnnouncesIt()
        {
            CityMarket market = PostMarket();
            CityDefinition city = City("Riga");

            try
            {
                var posts = new PlayerTradingPosts(2000, null);
                var treasury = new Treasury(10000);
                var feed = new NotificationFeed(5f, 1f);
                CityDefinition opened = null;
                int bought = 0;
                posts.Opened += openedCity => opened = openedCity;
                var hold = new CargoHold(100);
                hold.TryAdd(0, 1);

                bool built = PlayerBuilder.TryBuildTradingPost(
                    market, PlayerBuilder.MaterialsOf(null, hold), treasury, feed, city, posts, Post,
                    (good, units, gold) => bought += units, false);

                Assert.IsTrue(built);
                Assert.IsNotNull(market.Grid.TradingPost);
                Assert.IsNotNull(posts.Of(city));
                Assert.AreEqual(2000, posts.Of(city).Capacity);
                Assert.AreSame(city, opened);
                Assert.AreEqual(0, hold.UnitsOf(0));
                Assert.AreEqual(2, bought);
                Assert.AreEqual(1, feed.Notifications.Count);
                Assert.AreEqual("Construction achevée à Riga : Comptoir.", feed.Notifications[0].Text);
            }
            finally
            {
                Object.DestroyImmediate(city);
            }
        }

        [Test]
        public void TryBuildTradingPost_WhenOneStands_IsRefused()
        {
            CityMarket market = PostMarket();
            CityDefinition city = City("Riga");

            try
            {
                var posts = new PlayerTradingPosts(2000, null);
                var treasury = new Treasury(10000);
                var feed = new NotificationFeed(5f, 1f);
                PlayerBuilder.TryBuildTradingPost(market, null, treasury, feed, city, posts, Post, null, false);
                long gold = treasury.Gold;
                Warehouse first = posts.Of(city);

                bool again = PlayerBuilder.TryBuildTradingPost(market, null, treasury, feed, city, posts, Post, null, false);

                Assert.IsFalse(again);
                Assert.AreEqual(gold, treasury.Gold);
                Assert.AreSame(first, posts.Of(city));
                Assert.AreEqual(1, feed.Notifications.Count);
                Assert.Throws<InvalidOperationException>(() => posts.Open(city, market.Grid));
            }
            finally
            {
                Object.DestroyImmediate(city);
            }
        }

        [Test]
        public void QuoteHouse_CountsMaterialsInTheTradingPost()
        {
            CityMarket market = PostMarket();
            var post = new Warehouse(2000);
            post.TryAdd(0, 2);
            var hold = new CargoHold(100);
            hold.TryAdd(0, 1);
            var cost = new BuildingCost(100, new[] { new MaterialCost(0, 3) });

            ConstructionQuote quote = Construction.QuoteHouse(market, PlayerBuilder.MaterialsOf(post, hold), new Treasury(10000), cost);

            Assert.IsTrue(quote.IsPossible);
            Assert.AreEqual(100, quote.Gold);
        }

        [Test]
        public void TryGiveTradingPost_PaysNothingAndStartsEmpty()
        {
            CityMarket market = PostMarket();
            CityDefinition city = City("Riga");

            try
            {
                var posts = new PlayerTradingPosts(2000, null);
                CityDefinition opened = null;
                posts.Opened += openedCity => opened = openedCity;
                int stock = market.AvailableOf(0);

                Assert.IsTrue(posts.TryGive(city, market.Grid));

                Assert.IsNotNull(market.Grid.TradingPost);
                Assert.AreEqual(BuildingOwner.Player, market.Grid.TradingPost.Owner);
                Assert.AreSame(city, opened);
                Assert.AreEqual(0, posts.Of(city).Used);
                Assert.AreEqual(stock, market.AvailableOf(0));
                Assert.IsFalse(posts.TryGive(city, market.Grid), "already given");
                Assert.IsFalse(posts.TryGive(city, null), "no grid");
                CollectionAssert.AreEqual(new[] { posts.Of(city) }, posts.All);
            }
            finally
            {
                Object.DestroyImmediate(city);
            }
        }

        [Test]
        public void QuoteRoads_NoMarket_IsImpossible()
        {
            var builderObject = new GameObject("PlayerBuilder");
            var city = ScriptableObject.CreateInstance<CityDefinition>();

            try
            {
                PlayerBuilder builder = builderObject.AddComponent<PlayerBuilder>();

                Assert.IsFalse(builder.QuoteRoads(city, Road, new[] { new Vector2Int(3, 0) }).IsPossible);
                Assert.IsFalse(builder.TryBuildRoads(city, Road, new[] { new Vector2Int(3, 0) }));
            }
            finally
            {
                Object.DestroyImmediate(builderObject);
                Object.DestroyImmediate(city);
            }
        }
    }
}
