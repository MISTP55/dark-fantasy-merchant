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
            root.Add(new Label { name = "build-cost-tip" });

            var builderObject = new GameObject("PlayerBuilder");
            var city = ScriptableObject.CreateInstance<CityDefinition>();

            try
            {
                var bar = new BuildBarController(root);
                var road = new RoadOffer("Route", new BuildingCost(50, Array.Empty<MaterialCost>()), "50 or");

                // A builder without a world quotes nothing: the offer shows its name alone, disabled.
                bar.Show(city, builderObject.AddComponent<PlayerBuilder>(), null, null, null,
                    Array.Empty<HouseOffer>(), Array.Empty<WorkshopOffer>(), road);
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
    }
}
