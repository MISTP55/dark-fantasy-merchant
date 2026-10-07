using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Editor;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationMaskAuthoringTests
    {
        private const string TempFolderName = "NavigationMaskAuthoringTestsTemp";
        private const string TempFolder = "Assets/" + TempFolderName;
        private const string TempMaskPath = TempFolder + "/Mask.asset";

        private static readonly Color32 Grey = new Color32(128, 128, 128, 255);
        private static readonly Color32 Orange = new Color32(220, 140, 60, 255);

        private NavigationMaskDefinition mask;
        private WorldMapDefinition map;
        private Texture2D texture;
        private Sprite sprite;

        [SetUp]
        public void SetUp()
        {
            mask = ScriptableObject.CreateInstance<NavigationMaskDefinition>();
            map = ScriptableObject.CreateInstance<WorldMapDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearUndo(mask);
            Undo.ClearUndo(map);
            Object.DestroyImmediate(mask);
            Object.DestroyImmediate(map);

            if (sprite != null)
            {
                Object.DestroyImmediate(sprite);
            }

            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.DeleteAsset(TempFolder);
        }

        /// <summary>Texture with one grey pixel; every other pixel is orange.</summary>
        private void CreateTexture(int width, int height, int greyX, int greyY)
        {
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
            };

            var pixels = new Color32[width * height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Orange;
            }

            pixels[greyY * width + greyX] = Grey;
            texture.SetPixels32(pixels);
            texture.Apply();
        }

        private void CreateSprite(Rect rect)
        {
            sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 1f);
        }

        private void AssignSpriteToMap()
        {
            var serialized = new SerializedObject(map);
            serialized.FindProperty("mapSprite").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TestCase(1024, 2048f / 1758f, 879)]
        [TestCase(100, 1f, 100)]
        [TestCase(10, 3f, 3)]
        [TestCase(64, 1000f, 1)]
        [TestCase(4096, 0.001f, NavigationGrid.MaxSize)]
        public void HeightFor_FollowsTheAspectRatio_WithinTheGridLimits(int width, float aspect, int expected)
        {
            Assert.AreEqual(expected, NavigationMaskAuthoring.HeightFor(width, aspect));
        }

        [Test]
        public void MatchesAspect_IsTrueOnlyForTheHeightOfThatWidth()
        {
            Assert.IsTrue(NavigationMaskAuthoring.MatchesAspect(1024, 879, 2048f / 1758f));
            Assert.IsFalse(NavigationMaskAuthoring.MatchesAspect(1024, 1024, 2048f / 1758f));
        }

        [Test]
        public void TryGetAspectRatio_UsesTheSpriteRectangle_AndFailsWithoutASprite()
        {
            Assert.IsFalse(NavigationMaskAuthoring.TryGetAspectRatio(map, out _));
            Assert.IsFalse(NavigationMaskAuthoring.TryGetAspectRatio(null, out _));

            CreateTexture(8, 2, 0, 0);
            CreateSprite(new Rect(0f, 0f, 4f, 2f));
            AssignSpriteToMap();

            Assert.IsTrue(NavigationMaskAuthoring.TryGetAspectRatio(map, out float aspect));
            Assert.AreEqual(2f, aspect, 1e-6f);
        }

        [Test]
        public void Apply_WritesTheGrid_AndCanBeUndone()
        {
            var grid = new NavigationGrid(4, 4);
            grid.SetNavigable(1, 2, true);

            Undo.IncrementCurrentGroup();
            NavigationMaskAuthoring.Apply(mask, grid, "Test Navigation Mask");

            Assert.AreEqual(4, mask.Width);
            CollectionAssert.AreEqual(grid.ToBytes(), mask.CreateGrid().ToBytes());

            Undo.PerformUndo();

            Assert.AreEqual(1, mask.Width, "back to a new definition");
            Assert.AreEqual(0, GridAssert.CountNavigable(mask.CreateGrid()));

            Undo.PerformRedo();

            CollectionAssert.AreEqual(grid.ToBytes(), mask.CreateGrid().ToBytes());
        }

        [Test]
        public void Apply_OfALargeChange_IsUndoneAndRedoneQuickly()
        {
            // Half of a full-size grid at once, as a fill does. Recorded property by
            // property, undoing this took more than ten seconds.
            mask.SetGrid(new NavigationGrid(1024, 879));
            var grid = new NavigationGrid(1024, 879);

            for (int y = 0; y < 440; y++)
            {
                grid.PaintStroke(new Vector2(0f, (y + 0.5f) / 879f), new Vector2(1f, (y + 0.5f) / 879f), 0f, true);
            }

            Undo.IncrementCurrentGroup();
            NavigationMaskAuthoring.Apply(mask, grid, "Test Navigation Mask");
            Undo.FlushUndoRecordObjects();

            double start = EditorApplication.timeSinceStartup;
            Undo.PerformUndo();
            Undo.PerformRedo();
            double seconds = EditorApplication.timeSinceStartup - start;

            Assert.Less(seconds, 1.0, "undo and redo of a large change");
            CollectionAssert.AreEqual(grid.ToBytes(), mask.CreateGrid().ToBytes());

            Undo.PerformUndo();
            Assert.AreEqual(0, GridAssert.CountNavigable(mask.CreateGrid()));
        }

        [Test]
        public void Detect_PutsCellZeroAtTheBottomLeftOfTheImage()
        {
            CreateTexture(2, 2, 0, 0);
            CreateSprite(new Rect(0f, 0f, 2f, 2f));

            NavigationGrid grid = NavigationMaskAuthoring.Detect(sprite, 2, 2, 0.2f);

            Assert.IsTrue(grid.IsNavigable(0, 0), "the grey pixel");
            Assert.AreEqual(1, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void Detect_ReadsOnlyTheRectangleOfTheSprite()
        {
            // The sprite is the right half of a 4 x 2 texture; the grey pixel is the
            // top-left pixel of that half.
            CreateTexture(4, 2, 2, 1);
            CreateSprite(new Rect(2f, 0f, 2f, 2f));

            NavigationGrid grid = NavigationMaskAuthoring.Detect(sprite, 2, 2, 0.2f);

            Assert.IsTrue(grid.IsNavigable(0, 1));
            Assert.AreEqual(1, GridAssert.CountNavigable(grid));
        }

        [Test]
        public void Detect_UsesTheThreshold()
        {
            CreateTexture(2, 2, 0, 0);
            CreateSprite(new Rect(0f, 0f, 2f, 2f));

            // Orange has a saturation of about 0.73.
            NavigationGrid everything = NavigationMaskAuthoring.Detect(sprite, 2, 2, 0.9f);

            Assert.AreEqual(4, GridAssert.CountNavigable(everything));
        }

        [Test]
        public void CreateMask_CreatesTheAsset_SizedForTheMap_AndAssignsIt()
        {
            AssetDatabase.CreateFolder("Assets", TempFolderName);
            CreateTexture(8, 2, 0, 0);
            CreateSprite(new Rect(0f, 0f, 8f, 2f));
            AssignSpriteToMap();

            NavigationMaskDefinition created = NavigationMaskAuthoring.CreateMask(map, TempMaskPath, 64);

            Assert.IsNotNull(created);
            Assert.AreEqual(TempMaskPath, AssetDatabase.GetAssetPath(created));
            Assert.AreEqual(64, created.Width);
            Assert.AreEqual(16, created.Height);
            Assert.AreEqual(0, GridAssert.CountNavigable(created.CreateGrid()));
            Assert.AreSame(created, map.NavigationMask);
        }

        [Test]
        public void CreateMask_KeepsAnAssetThatAlreadyExistsAtThePath()
        {
            AssetDatabase.CreateFolder("Assets", TempFolderName);
            CreateTexture(8, 2, 0, 0);
            CreateSprite(new Rect(0f, 0f, 8f, 2f));
            AssignSpriteToMap();

            NavigationMaskDefinition first = NavigationMaskAuthoring.CreateMask(map, TempMaskPath, 64);
            NavigationGrid painted = first.CreateGrid();
            painted.SetNavigable(3, 3, true);
            first.SetGrid(painted);

            NavigationMaskDefinition second = NavigationMaskAuthoring.CreateMask(map, TempMaskPath, 128);

            Assert.AreSame(first, second);
            Assert.AreEqual(64, second.Width);
            Assert.IsTrue(second.CreateGrid().IsNavigable(3, 3), "the painted mask is not replaced");
        }

        [Test]
        public void CreateMask_ForAMapWithoutASprite_CreatesNothing()
        {
            AssetDatabase.CreateFolder("Assets", TempFolderName);

            Assert.IsNull(NavigationMaskAuthoring.CreateMask(map, TempMaskPath, 64));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<NavigationMaskDefinition>(TempMaskPath));
            Assert.IsNull(map.NavigationMask);
        }
    }
}
