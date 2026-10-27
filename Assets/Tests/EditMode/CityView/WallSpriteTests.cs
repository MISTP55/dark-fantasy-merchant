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
        private Sprite[] wallSprites;

        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(4, 4);
            wallSprites = new Sprite[WallShapes.Count];
            viewObject = new GameObject("City grid view");
            CityGridView view = viewObject.AddComponent<CityGridView>();
            var serialized = new SerializedObject(view);
            SerializedProperty sprites = serialized.FindProperty("wallSprites");
            sprites.arraySize = WallShapes.Count;

            for (int i = 0; i < WallShapes.Count; i++)
            {
                wallSprites[i] = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.25f));
                wallSprites[i].name = $"Wall {i}";
                sprites.GetArrayElementAtIndex(i).objectReferenceValue = wallSprites[i];
            }

            serialized.FindProperty("cellSprite").objectReferenceValue = wallSprites[0];
            serialized.FindProperty("wallColor").colorValue = new Color(0.2f, 0.2f, 0.2f, 1f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(viewObject);

            foreach (Sprite sprite in wallSprites)
            {
                Object.DestroyImmediate(sprite);
            }

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

        private static void PlaceWall(CityGrid grid, int x, int y)
        {
            grid.Place(BuildingKind.Wall(), BuildingOwner.City, new Footprint(1, 1), new GridPlace(new Vector2Int(x, y), BuildingOrientation.Along));
        }

        [Test]
        public void AWall_IsDrawnWithTheWallColour_AndNeverShaded()
        {
            var grid = new CityGrid(10, 10, new Vector2Int[0], RoadContact.SidesOnly);
            grid.Place(BuildingKind.TownHall(), BuildingOwner.City, new Footprint(3, 2), new GridPlace(new Vector2Int(4, 4), BuildingOrientation.Along));
            PlaceWall(grid, 2, 2);

            viewObject.GetComponent<CityGridView>().Show(grid, null, null);

            SpriteRenderer wall = RendererOf("City Wall 2,2");
            Assert.IsNotNull(wall);
            Assert.AreSame(wallSprites[(int)WallSides.None], wall.sprite);
            Assert.AreEqual(new Color(0.2f, 0.2f, 0.2f, 1f), wall.color);

            // A road joining the wall to the town hall connects nothing and leaves the wall as it is.
            grid.Place(BuildingKind.Road(), BuildingOwner.City, new Footprint(1, 1), new GridPlace(new Vector2Int(3, 2), BuildingOrientation.Along));
            Assert.AreEqual(new Color(0.2f, 0.2f, 0.2f, 1f), wall.color);
        }

        [Test]
        public void EachWall_TakesTheSpriteOfTheWallsBesideIt_Unscaled()
        {
            // A line from (1, 1) to (3, 1), turning north at (3, 1) up to (3, 2).
            var grid = new CityGrid(6, 6, new Vector2Int[0], RoadContact.SidesOnly);
            PlaceWall(grid, 1, 1);
            PlaceWall(grid, 2, 1);
            PlaceWall(grid, 3, 1);
            PlaceWall(grid, 3, 2);

            viewObject.GetComponent<CityGridView>().Show(grid, null, null);

            Assert.AreSame(wallSprites[(int)WallSides.East], RendererOf("City Wall 1,1").sprite);
            Assert.AreSame(wallSprites[(int)(WallSides.East | WallSides.West)], RendererOf("City Wall 2,1").sprite);
            Assert.AreSame(wallSprites[(int)(WallSides.West | WallSides.North)], RendererOf("City Wall 3,1").sprite);
            Assert.AreSame(wallSprites[(int)WallSides.South], RendererOf("City Wall 3,2").sprite);
            Assert.AreEqual(Vector3.one, RendererOf("City Wall 2,1").transform.localScale);
            Assert.IsFalse(RendererOf("City Wall 2,1").flipX);
        }
    }
}
