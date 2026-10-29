using System;
using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class CityMarketPlayerWorkshopsTests
    {
        private const int Made = 0;
        private const int Input = 1;
        private const int Other = 2;

        // Each workshop of a three-goods market starts with a few workers.
        private static WorkforceRule Workforce => new WorkforceRule(16, 8, 4, 1, 1, 30);

        private static MerchantOutputRule Bonus => new MerchantOutputRule(3, 1.03, 6, 1.06, 9, 1.1);

        // Consumed by no one; the city's workshop has one worker, so that the beggars are left to the player's,
        // and a merchant's worker makes the given units a day.
        private static MarketGood Good(double merchantOutput)
        {
            return new MarketGood(10, 0, 1, 0.1, merchantOutput);
        }

        // Made from the recipe, with a merchant's worker making the given units a day; Input and Other need nothing.
        private static MarketGood[] Goods(double merchantOutput, params MarketInput[] recipe)
        {
            return new[] { Good(merchantOutput).WithInputs(recipe), Good(merchantOutput), Good(merchantOutput) };
        }

        // 200 beggars ready to be hired, and room for many more.
        private static CityPopulation Roomy => new CityPopulation(1000, 200, 2000);

        private static CityMarket Market(MarketGood[] goods, MerchantOutputRule merchantOutput = null)
        {
            return new CityMarket(goods, TestMarkets.Thresholds, Workforce, 100, TestMarkets.Pricing, 7, Roomy,
                TestMarkets.Labour, grid: TestMarkets.GridWithRoad(), placement: TestMarkets.Placement, merchantOutput: merchantOutput ?? Bonus);
        }

        private static Warehouse PostOf(CityMarket market, int capacity = 2000)
        {
            var post = new Warehouse(capacity);
            market.SetPlayerTradingPost(post);
            return post;
        }

        private static void Days(CityMarket market, int days)
        {
            for (int i = 0; i < days; i++)
            {
                market.StartDay();
            }
        }

        [Test]
        public void AWorkshop_DeliversWholeUnits_AndCarriesTheFraction()
        {
            // 10 workers making 0.015 each, with nothing to take: 0.15 a day.
            CityMarket market = Market(Goods(0.015));
            Warehouse post = PostOf(market);
            TestMarkets.AddPlayerWorkshop(market, Made, 10);

            Days(market, 6);

            Assert.AreEqual(10, market.PlayerWorkersOf(Made));
            Assert.AreEqual(0, post.UnitsOf(Made));

            Days(market, 1);

            Assert.AreEqual(1, post.UnitsOf(Made));

            Days(market, 7);

            Assert.AreEqual(2, post.UnitsOf(Made));
        }

        [Test]
        public void Inputs_AreTakenFromThePost_NeverFromTheMarket()
        {
            // 1 a day, 2 inputs a unit.
            MarketGood[] goods = Goods(0.1, new MarketInput(Input, 2));
            CityMarket market = Market(goods);
            CityMarket withoutPlayer = Market(goods);
            Warehouse post = PostOf(market);
            post.TryAdd(Input, 100);
            TestMarkets.AddPlayerWorkshop(market, Made, 10);

            Days(market, 10);
            Days(withoutPlayer, 10);

            Assert.AreEqual(10, post.UnitsOf(Made));
            Assert.AreEqual(80, post.UnitsOf(Input));
            Assert.AreEqual(withoutPlayer.StockOf(Input), market.StockOf(Input), 1e-9);
            Assert.AreEqual(withoutPlayer.StockOf(Made), market.StockOf(Made), 1e-9);
        }

        [Test]
        public void ShortOfAnInput_ProducesInProportionToTheScarcest()
        {
            // 4 a day, one of each input a unit: 2 Other allow half.
            CityMarket market = Market(Goods(0.4, new MarketInput(Input, 1), new MarketInput(Other, 1)));
            Warehouse post = PostOf(market);
            post.TryAdd(Input, 10);
            post.TryAdd(Other, 2);
            TestMarkets.AddPlayerWorkshop(market, Made, 10);

            Days(market, 1);

            Assert.AreEqual(2, post.UnitsOf(Made));
            Assert.AreEqual(8, post.UnitsOf(Input));
            Assert.AreEqual(0, post.UnitsOf(Other));

            Days(market, 3);

            Assert.AreEqual(2, post.UnitsOf(Made));
            Assert.AreEqual(8, post.UnitsOf(Input));
        }

        [Test]
        public void AnInputUnit_IsTakenOnlyOnceAWholeUnitIsOwed()
        {
            // 0.1 a day, one input a unit.
            CityMarket market = Market(Goods(0.01, new MarketInput(Input, 1)));
            Warehouse post = PostOf(market);
            post.TryAdd(Input, 5);
            TestMarkets.AddPlayerWorkshop(market, Made, 10);

            Days(market, 9);

            Assert.AreEqual(5, post.UnitsOf(Input));
            Assert.AreEqual(0, post.UnitsOf(Made));

            Days(market, 1);

            Assert.AreEqual(4, post.UnitsOf(Input));
            Assert.AreEqual(1, post.UnitsOf(Made));
        }

        [Test]
        public void InputTakenAway_WhileOwed_ProductionWaits()
        {
            CityMarket market = Market(Goods(0.01, new MarketInput(Input, 1)));
            Warehouse post = PostOf(market);
            post.TryAdd(Input, 1);
            TestMarkets.AddPlayerWorkshop(market, Made, 10);
            Days(market, 1);

            Assert.IsTrue(post.TryRemove(Input, 1));

            Assert.DoesNotThrow(() => Days(market, 20));
            Assert.AreEqual(0, post.UnitsOf(Input));
            Assert.AreEqual(0, post.UnitsOf(Made));

            // Brought back, the unit pays what was owed first.
            post.TryAdd(Input, 1);
            Days(market, 9);

            Assert.AreEqual(0, post.UnitsOf(Input));
            Assert.AreEqual(1, post.UnitsOf(Made));
        }

        [Test]
        public void AnInputSharedByTwoGoods_BacksOnlyOneDebtAtATime()
        {
            // Made and Other each take one Input a unit, 0.5 and 0.6 a day; the post holds one Input, which both
            // count on the first day.
            var made = new MarketGood(10, 0, 1, 0.1, 0.05).WithInputs(new[] { new MarketInput(Input, 1) });
            var other = new MarketGood(10, 0, 1, 0.1, 0.06).WithInputs(new[] { new MarketInput(Input, 1) });
            CityMarket market = Market(new[] { made, Good(0.05), other });
            Warehouse post = PostOf(market);
            post.TryAdd(Input, 1);
            TestMarkets.AddPlayerWorkshop(market, Made, 10);
            TestMarkets.AddPlayerWorkshop(market, Other, 10);

            Days(market, 30);

            // One Input makes one unit at most, whatever the goods that share it.
            Assert.AreEqual(1, post.UnitsOf(Made) + post.UnitsOf(Other) + post.UnitsOf(Input));
            Assert.AreEqual(0, post.UnitsOf(Input));
        }

        [Test]
        public void AnInputMadeEarlier_ServesTheSameDay()
        {
            // 1 a day of each; Made takes one Input a unit.
            CityMarket market = Market(Goods(0.1, new MarketInput(Input, 1)));
            Warehouse post = PostOf(market);
            TestMarkets.AddPlayerWorkshop(market, Input, 10);
            TestMarkets.AddPlayerWorkshop(market, Made, 10);

            Days(market, 1);

            Assert.AreEqual(1, post.UnitsOf(Made));
            Assert.AreEqual(0, post.UnitsOf(Input));
        }

        [Test]
        public void AnInputMadeLater_ServesTheNextDay()
        {
            CityMarket market = Market(Goods(0.1, new MarketInput(Input, 1)));
            Warehouse post = PostOf(market);
            TestMarkets.AddPlayerWorkshop(market, Made, 10);
            TestMarkets.AddPlayerWorkshop(market, Input, 10);

            Days(market, 1);

            Assert.AreEqual(0, post.UnitsOf(Made));
            Assert.AreEqual(1, post.UnitsOf(Input));

            Days(market, 1);

            Assert.AreEqual(1, post.UnitsOf(Made));
            Assert.AreEqual(1, post.UnitsOf(Input));
        }

        [Test]
        public void UnconnectedWorkshop_MakesNothing()
        {
            CityGrid grid = TestMarkets.GridWithTownHall(12, 12);
            var market = new CityMarket(Goods(0.1), TestMarkets.Thresholds, Workforce, 100, TestMarkets.Pricing, 7,
                Roomy, TestMarkets.Labour, null, null, grid, new RoadsidePlacement(new System.Random(1)), Bonus);
            Warehouse post = PostOf(market);
            market.AddPlayerWorkshop(Made, 10, new Footprint(2, 2), new GridPlace(new Vector2Int(5, 0), BuildingOrientation.Along));

            Days(market, 10);

            Assert.AreEqual(0, post.UnitsOf(Made));
        }

        [Test]
        public void WithoutTradingPost_MakesNothing_ButHires()
        {
            CityMarket market = Market(Goods(0.1));
            TestMarkets.AddPlayerWorkshop(market, Made, 10);

            Days(market, 10);

            Assert.IsNull(market.PlayerTradingPost);
            Assert.AreEqual(10, market.PlayerWorkersOf(Made));
        }

        [Test]
        public void WithoutMerchantOutputRule_MakesNothing()
        {
            var market = new CityMarket(Goods(0.1), TestMarkets.Thresholds, Workforce, 100, TestMarkets.Pricing, 7,
                Roomy, TestMarkets.Labour, grid: TestMarkets.GridWithRoad(), placement: TestMarkets.Placement);
            Warehouse post = PostOf(market);
            TestMarkets.AddPlayerWorkshop(market, Made, 10);

            Days(market, 10);

            Assert.AreEqual(0, market.PlayerDailyOutputOf(Made));
            Assert.AreEqual(0, post.UnitsOf(Made));
        }

        [Test]
        public void TheBonus_AppliesToTheWorkshopsOfAGood()
        {
            // 3 workshops of 10: 3 a day, 3 % more.
            CityMarket market = Market(Goods(0.1));
            Warehouse post = PostOf(market);
            TestMarkets.AddPlayerWorkshop(market, Made, 10);
            TestMarkets.AddPlayerWorkshop(market, Made, 10);
            TestMarkets.AddPlayerWorkshop(market, Made, 10);

            Days(market, 10);

            Assert.AreEqual(30, market.PlayerWorkersOf(Made));
            Assert.AreEqual(30 * 0.1 * 1.03, market.PlayerDailyOutputOf(Made), 1e-9);
            Assert.AreEqual(30, post.UnitsOf(Made));
        }

        [Test]
        public void ProductionEnters_AboveCapacity()
        {
            CityMarket market = Market(Goods(0.1));
            Warehouse post = PostOf(market, 0);
            TestMarkets.AddPlayerWorkshop(market, Made, 10);

            Days(market, 5);

            Assert.AreEqual(5, post.UnitsOf(Made));
        }

        [Test]
        public void PlayerProduction_IsNotInTheWeekLogNorTheDayReport()
        {
            MarketGood[] goods = Goods(0.1, new MarketInput(Input, 1));
            CityMarket market = Market(goods);
            CityMarket withoutPlayer = Market(goods);
            Warehouse post = PostOf(market);
            post.TryAdd(Input, 100);
            TestMarkets.AddPlayerWorkshop(market, Made, 10);

            Days(market, 7);
            Days(withoutPlayer, 7);

            Assert.AreEqual(withoutPlayer.RecentProductionOf(Made), market.RecentProductionOf(Made), 1e-9);
            Assert.AreEqual(withoutPlayer.RecentInputsOf(Input), market.RecentInputsOf(Input), 1e-9);
            Assert.AreEqual(withoutPlayer.LastDay.InputsConsumedOf(Input), market.LastDay.InputsConsumedOf(Input), 1e-9);
        }

        [Test]
        public void SetPlayerTradingPost_RefusesNullAndASecondOne()
        {
            CityMarket market = Market(Goods(0.1));

            Assert.Throws<ArgumentNullException>(() => market.SetPlayerTradingPost(null));

            PostOf(market);

            Assert.Throws<InvalidOperationException>(() => market.SetPlayerTradingPost(new Warehouse(10)));
        }
    }
}
