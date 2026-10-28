using System;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class BuildBarTests
    {
        [Test]
        public void OfferText_GivesTheNameAndTheGold()
        {
            Assert.AreEqual("Maison de pauvres\n5 000 or", BuildBarController.OfferText("Maison de pauvres", 5000));
        }

        [Test]
        public void RoadsCategory_ShowsTheRoadOffer()
        {
            var root = new VisualElement();
            var offers = new VisualElement { name = "build-offers" };
            var roadsButton = new Button { name = "roads-button" };
            root.Add(offers);
            root.Add(new Button { name = "houses-button" });
            root.Add(new Button { name = "workshops-button" });
            root.Add(roadsButton);
            root.Add(new Button { name = "category-trading-post" });
            root.Add(new Label { name = "build-cost-tip" });

            var builderObject = new GameObject("PlayerBuilder");
            var city = ScriptableObject.CreateInstance<CityDefinition>();

            try
            {
                var bar = new BuildBarController(root);
                var road = new RoadOffer("Route", new BuildingCost(50, Array.Empty<MaterialCost>()), "50 or");

                // A builder without a world quotes nothing: the offer shows its name alone, disabled.
                bar.Show(city, builderObject.AddComponent<PlayerBuilder>(), null, null, null,
                    Array.Empty<HouseOffer>(), Array.Empty<WorkshopOffer>(), road, null, null, null);
                bar.Choose(BuildBarController.Category.Roads);

                Assert.AreEqual("Routes", roadsButton.text);
                Assert.AreEqual(1, offers.childCount);
                Button offer = offers.Q<Button>();
                Assert.AreEqual("Route", offer.text);
                Assert.IsFalse(offer.enabledSelf);
                Assert.AreEqual("50 or par case", bar.CostTipAt(0));
            }
            finally
            {
                Object.DestroyImmediate(builderObject);
                Object.DestroyImmediate(city);
            }
        }

        [Test]
        public void OfferState_WorkshopWithoutTradingPost_IsDisabledWithItsTip()
        {
            var root = new VisualElement();
            var offers = new VisualElement { name = "build-offers" };
            root.Add(offers);
            root.Add(new Button { name = "houses-button" });
            root.Add(new Button { name = "workshops-button" });
            root.Add(new Button { name = "roads-button" });
            root.Add(new Button { name = "category-trading-post" });
            root.Add(new Label { name = "build-cost-tip" });

            var builderObject = new GameObject("PlayerBuilder");
            var city = ScriptableObject.CreateInstance<CityDefinition>();

            try
            {
                var bar = new BuildBarController(root);
                PlayerBuilder builder = builderObject.AddComponent<PlayerBuilder>();
                var workshop = new WorkshopOffer(0, "Brasserie", 30, new BuildingCost(5000, Array.Empty<MaterialCost>()), "5 000 or");
                var market = new CityMarket(new[] { new MarketGood(10, 2, 30, 0.1) }, TestMarkets.Thresholds,
                    TestMarkets.Workforce, 30, TestMarkets.Pricing, 7, new CityPopulation(1000));

                bar.Show(city, builder, market, null, null, Array.Empty<HouseOffer>(), new[] { workshop }, null, null, null, null);
                bar.Choose(BuildBarController.Category.Workshops);

                Assert.AreEqual(1, offers.childCount);
                Assert.IsFalse(offers.Q<Button>().enabledSelf);
                Assert.AreEqual(BuildBarController.WorkshopNeedsTradingPostText, bar.CostTipAt(0));
                Assert.AreEqual(
                    "Il faut un comptoir dans cette ville pour y construire un atelier.",
                    BuildBarController.WorkshopNeedsTradingPostText);

                market.SetPlayerTradingPost(new Warehouse(2000));

                Assert.AreEqual("5 000 or", bar.CostTipAt(0));
            }
            finally
            {
                Object.DestroyImmediate(builderObject);
                Object.DestroyImmediate(city);
            }
        }

        [Test]
        public void OfferState_TradingPostBuilt_IsDisabledWithItsTip()
        {
            var root = new VisualElement();
            var offers = new VisualElement { name = "build-offers" };
            var postButton = new Button { name = "category-trading-post" };
            root.Add(offers);
            root.Add(new Button { name = "houses-button" });
            root.Add(new Button { name = "workshops-button" });
            root.Add(new Button { name = "roads-button" });
            root.Add(postButton);
            root.Add(new Label { name = "build-cost-tip" });

            var builderObject = new GameObject("PlayerBuilder");
            var city = ScriptableObject.CreateInstance<CityDefinition>();

            try
            {
                var bar = new BuildBarController(root);
                var footprint = new Footprint(2, 3);
                var post = new TradingPostOffer("Comptoir", new BuildingCost(5000, Array.Empty<MaterialCost>()), "5 000 or", footprint);
                var grid = new CityGrid(10, 10, new Vector2Int[0], RoadContact.SidesOnly);
                grid.ReserveTradingPostSite(footprint, new GridPlace(new Vector2Int(4, 4), BuildingOrientation.Along));

                bar.Show(city, builderObject.AddComponent<PlayerBuilder>(), null, null, null,
                    Array.Empty<HouseOffer>(), Array.Empty<WorkshopOffer>(), null, post, grid, null);
                bar.Choose(BuildBarController.Category.TradingPost);

                Assert.AreEqual("Comptoir", postButton.text);
                Assert.AreEqual(1, offers.childCount);
                Assert.AreEqual("5 000 or", bar.CostTipAt(0));

                grid.PlaceTradingPost();

                Button offer = offers.Q<Button>();
                Assert.AreEqual("Comptoir", offer.text);
                Assert.IsFalse(offer.enabledSelf);
                Assert.AreEqual(BuildBarController.TradingPostBuiltText, bar.CostTipAt(0));
                Assert.AreEqual("Votre comptoir est déjà construit dans cette ville.", BuildBarController.TradingPostBuiltText);
            }
            finally
            {
                Object.DestroyImmediate(builderObject);
                Object.DestroyImmediate(city);
            }
        }
    }
}
