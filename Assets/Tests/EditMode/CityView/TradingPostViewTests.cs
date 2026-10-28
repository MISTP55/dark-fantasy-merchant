using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class TradingPostViewTests
    {
        private static readonly Color Free = new Color(0.25f, 0.3f, 0.2f, 1f);
        private static readonly Color Water = new Color(0.1f, 0.2f, 0.4f, 1f);
        private static readonly Color Reserved = new Color(0.4f, 0.75f, 0.7f, 1f);
        private static readonly Color Post = new Color(0.2f, 0.55f, 0.5f, 1f);

        // A 2 by 3 site at (4, 4): cells x 4..5, y 4..6.
        private static readonly Footprint PostFootprint = new Footprint(2, 3);
        private static readonly GridPlace Site = new GridPlace(new Vector2Int(4, 4), BuildingOrientation.Along);

        private GameObject viewObject;
        private Texture2D texture;
        private Sprite sprite;

        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(4, 4);
            sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            viewObject = new GameObject("City grid view");
            CityGridView view = viewObject.AddComponent<CityGridView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("cellSprite").objectReferenceValue = sprite;
            serialized.FindProperty("block2x3Sprite").objectReferenceValue = sprite;
            serialized.FindProperty("freeCellColor").colorValue = Free;
            serialized.FindProperty("forbiddenCellColor").colorValue = Water;
            serialized.FindProperty("reservedColor").colorValue = Reserved;
            serialized.FindProperty("tradingPostColor").colorValue = Post;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(viewObject);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        private static CityGrid GridWithSite()
        {
            var grid = new CityGrid(10, 10, new[] { new Vector2Int(0, 0) }, RoadContact.SidesOnly);
            grid.ReserveTradingPostSite(PostFootprint, Site);
            return grid;
        }

        [Test]
        public void ShowReservedCells_TintsOnlyReservedCellsAndRestoresThem()
        {
            CityGrid grid = GridWithSite();
            CityGridView view = viewObject.GetComponent<CityGridView>();
            view.Show(grid, null, null);

            view.ShowReservedCells(true);

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    Color expected = grid.IsReserved(cell) ? Reserved : grid.IsForbidden(cell) ? Water : Free;
                    Assert.AreEqual(expected, view.Ground.ColorOf(cell), $"cell {cell}");
                }
            }

            // Shown twice, hidden twice: the cells come back to what they were.
            view.ShowReservedCells(true);
            view.ShowReservedCells(false);
            view.ShowReservedCells(false);

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    Assert.AreEqual(grid.IsForbidden(cell) ? Water : Free, view.Ground.ColorOf(cell), $"cell {cell}");
                }
            }
        }

        [Test]
        public void ATradingPost_IsDrawnAsItsBlockInItsColour_AndNeverShaded()
        {
            CityGrid grid = GridWithSite();
            CityGridView view = viewObject.GetComponent<CityGridView>();
            view.Show(grid, null, null);

            grid.PlaceTradingPost();

            Assert.AreEqual(1, view.BuildingRenderers.Count);
            SpriteRenderer post = view.BuildingRenderers[0];
            Assert.AreEqual("Player Trading post 4,4", post.name);
            Assert.AreSame(sprite, post.sprite);
            Assert.AreEqual(Post, post.color);
            Assert.AreEqual((Vector3)IsoProjection.CenterOf(Site.Origin, new Vector2Int(2, 3)), post.transform.localPosition);
        }
    }
}
