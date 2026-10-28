using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class TradingPostTransferSectionTests
    {
        private const int Iron = 0;
        private const int Beer = 1;

        private static readonly string[] GoodNames = { "Fer", "Bière", "Bois" };

        [Test]
        public void GoodsShown_ListsGoodsInEitherStoreByName()
        {
            var hold = new CargoHold(100);
            var post = new Warehouse(1000);
            hold.TryAdd(Iron, 5);
            post.TryAdd(Beer, 3);

            List<int> goods = TradingPostTransferSection.GoodsShown(hold, post, GoodNames);

            CollectionAssert.AreEqual(new[] { Beer, Iron }, goods);
        }

        [Test]
        public void GoodsShown_Empty_WhenBothEmpty()
        {
            List<int> goods = TradingPostTransferSection.GoodsShown(new CargoHold(100), new Warehouse(1000), GoodNames);

            CollectionAssert.IsEmpty(goods);
        }
    }
}
