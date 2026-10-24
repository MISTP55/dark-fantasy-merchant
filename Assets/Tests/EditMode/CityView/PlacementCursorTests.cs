using DarkFantasyMerchant.Core;
using NUnit.Framework;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class PlacementCursorTests
    {
        [Test]
        public void Rotate_TurnsA2x1_ButNotA2x2()
        {
            var cursor = new PlacementCursor();
            cursor.Start(new Footprint(2, 1));
            cursor.Rotate();
            Assert.AreEqual(BuildingOrientation.Across, cursor.Orientation);
            cursor.Rotate();
            Assert.AreEqual(BuildingOrientation.Along, cursor.Orientation);

            cursor.Start(new Footprint(2, 2));
            cursor.Rotate();
            Assert.AreEqual(BuildingOrientation.Along, cursor.Orientation);
        }

        [Test]
        public void Rotate_DoesNothing_WhenNotPlacing()
        {
            var cursor = new PlacementCursor();
            cursor.Rotate();
            Assert.IsFalse(cursor.IsPlacing);
            Assert.AreEqual(BuildingOrientation.Along, cursor.Orientation);
        }

        [Test]
        public void Start_StartsAlong()
        {
            var cursor = new PlacementCursor();
            cursor.Start(new Footprint(2, 1));
            cursor.Rotate();
            cursor.Start(new Footprint(2, 1));
            Assert.AreEqual(BuildingOrientation.Along, cursor.Orientation);
        }

        [Test]
        public void Stop_EndsPlacing()
        {
            var cursor = new PlacementCursor();
            cursor.Start(new Footprint(2, 1));
            cursor.Stop();
            Assert.IsFalse(cursor.IsPlacing);
        }

        [Test]
        public void PlaceAt_UsesTheHoveredCellAsOriginAndTheCurrentOrientation()
        {
            var cursor = new PlacementCursor();
            cursor.Start(new Footprint(2, 1));
            cursor.Rotate();
            var place = cursor.PlaceAt(new Vector2Int(4, 7));
            Assert.AreEqual(new Vector2Int(4, 7), place.Origin);
            Assert.AreEqual(BuildingOrientation.Across, place.Orientation);
        }
    }
}
