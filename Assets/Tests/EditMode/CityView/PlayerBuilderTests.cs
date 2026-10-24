using System;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
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
