using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class WallSpriteTests
    {
        private GameObject viewObject;
        private Texture2D texture;
        private Sprite wallSprite;

        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(4, 4);
            wallSprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.25f));
            viewObject = new GameObject("City grid view");
            CityGridView view = viewObject.AddComponent<CityGridView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("wallSprite").objectReferenceValue = wallSprite;
            serialized.FindProperty("cellSprite").objectReferenceValue = wallSprite;
            serialized.FindProperty("wallColor").colorValue = new Color(0.2f, 0.2f, 0.2f, 1f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(viewObject);
            Object.DestroyImmediate(wallSprite);
            Object.DestroyImmediate(texture);
        }

        private SpriteRenderer RendererOf(string name)
        {
            foreach (SpriteRenderer renderer in viewObject.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.name == name)
                {
                    return renderer;
                }
            }

            return null;
        }

        [Test]
        public void AWall_IsDrawnWithTheWallSpriteAndColour_AndNeverShaded()
        {
            var grid = new CityGrid(10, 10, new Vector2Int[0], RoadContact.SidesOnly);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, new Footprint(3, 2), new GridPlace(new Vector2Int(4, 4), BuildingOrientation.Along));
            grid.Place(BuildingKind.Wall(), BuildingOwner.City, new Footprint(1, 1), new GridPlace(new Vector2Int(2, 2), BuildingOrientation.Along));

            viewObject.GetComponent<CityGridView>().Show(grid, null, null);

            SpriteRenderer wall = RendererOf("City Wall 2,2");
            Assert.IsNotNull(wall);
            Assert.AreSame(wallSprite, wall.sprite);
            Assert.AreEqual(new Color(0.2f, 0.2f, 0.2f, 1f), wall.color);

            // A road joining the wall to the town hall connects nothing and leaves the wall as it is.
            grid.Place(BuildingKind.Road(), BuildingOwner.City, new Footprint(1, 1), new GridPlace(new Vector2Int(3, 2), BuildingOrientation.Along));
            Assert.AreEqual(new Color(0.2f, 0.2f, 0.2f, 1f), wall.color);
        }
    }
}
