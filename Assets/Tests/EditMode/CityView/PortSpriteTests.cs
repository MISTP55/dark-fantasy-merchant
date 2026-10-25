using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class PortSpriteTests
    {
        // Mirroring swaps the screen directions of x and y: north, at the upper left, becomes east, at the upper right.
        [TestCase(PortFacing.North, true, false)]
        [TestCase(PortFacing.East, true, true)]
        [TestCase(PortFacing.South, false, false)]
        [TestCase(PortFacing.West, false, true)]
        public void PortSpriteOf_EachFacing(PortFacing facing, bool waterAtBack, bool flipX)
        {
            (bool back, bool flip) = CityGridView.PortSpriteOf(facing);

            Assert.AreEqual(waterAtBack, back);
            Assert.AreEqual(flipX, flip);
        }
    }
}
