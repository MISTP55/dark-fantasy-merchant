using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Editor;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class NavigationMaskSessionTests
    {
        private NavigationMaskDefinition mask;
        private WorldMapDefinition map;
        private NavigationMaskSession session;

        [SetUp]
        public void SetUp()
        {
            mask = ScriptableObject.CreateInstance<NavigationMaskDefinition>();
            mask.SetGrid(new NavigationGrid(8, 4));

            map = ScriptableObject.CreateInstance<WorldMapDefinition>();
            AssignMask(mask);

            session = new NavigationMaskSession();
            session.SetMap(map);
        }

        [TearDown]
        public void TearDown()
        {
            session.Dispose();
            Undo.ClearUndo(mask);
            Object.DestroyImmediate(mask);
            Object.DestroyImmediate(map);
        }

        private void AssignMask(NavigationMaskDefinition value)
        {
            var serialized = new SerializedObject(map);
            serialized.FindProperty("navigationMask").objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private Vector2 Cell(int x, int y)
        {
            return GridAssert.CellCenter(session.Grid, x, y);
        }

        [Test]
        public void SetMap_LoadsAWorkingCopyOfTheMask()
        {
            Assert.AreSame(map, session.Map);
            Assert.AreSame(mask, session.Mask);
            Assert.IsTrue(session.HasMask);
            Assert.AreEqual(8, session.Grid.Width);
            Assert.AreEqual(4, session.Grid.Height);
            Assert.IsFalse(session.HasNavigableCells);
        }

        [Test]
        public void WithoutAMask_ThereIsNoGrid_AndEditingDoesNothing()
        {
            AssignMask(null);
            session.SetMap(map);

            Assert.IsFalse(session.HasMask);
            Assert.IsNull(session.Grid);
            Assert.IsFalse(session.HasNavigableCells);
            Assert.IsNull(session.GetPreview());

            Assert.DoesNotThrow(() => session.Paint(Vector2.zero, Vector2.one, 1f, true));
            Assert.DoesNotThrow(() => session.Fill(Vector2.zero, true));
            Assert.IsFalse(session.Commit("Test"));
        }

        [Test]
        public void SetMap_WithNoMap_ClearsTheSession()
        {
            session.SetMap(null);

            Assert.IsNull(session.Map);
            Assert.IsFalse(session.HasMask);
            Assert.IsNull(session.Grid);
        }

        [Test]
        public void SetMap_PicksUpAMaskAssignedLater()
        {
            AssignMask(null);
            session.SetMap(map);
            Assert.IsNull(session.Grid);

            AssignMask(mask);
            session.SetMap(map);

            Assert.IsNotNull(session.Grid);
        }

        [Test]
        public void SetMap_WithTheSameMap_KeepsUncommittedPaint()
        {
            session.Paint(Cell(2, 1), Cell(2, 1), 0.5f, true);

            session.SetMap(map);

            Assert.IsTrue(session.Grid.IsNavigable(2, 1));
        }

        [Test]
        public void Paint_ChangesTheWorkingCopyOnly_UntilCommitted()
        {
            session.Paint(Cell(2, 1), Cell(5, 1), 0.5f, true);

            Assert.IsTrue(session.Grid.IsNavigable(2, 1));
            Assert.IsTrue(session.Grid.IsNavigable(5, 1));
            Assert.IsTrue(session.HasNavigableCells);
            Assert.AreEqual(0, GridAssert.CountNavigable(mask.CreateGrid()), "not written yet");

            Assert.IsTrue(session.Commit("Paint"));

            CollectionAssert.AreEqual(session.Grid.ToBytes(), mask.CreateGrid().ToBytes());
        }

        [Test]
        public void Commit_WithNothingChanged_WritesNothing()
        {
            Assert.IsFalse(session.Commit("Paint"));

            // Painting non-navigable on an empty grid changes no cell.
            session.Paint(Cell(2, 1), Cell(5, 1), 0.5f, false);
            Assert.IsFalse(session.Commit("Paint"));

            session.Paint(Cell(2, 1), Cell(2, 1), 0.5f, true);
            Assert.IsTrue(session.Commit("Paint"));
            Assert.IsFalse(session.Commit("Paint"), "already written");
        }

        [Test]
        public void Fill_ChangesTheWorkingCopy_AndIsCommitted()
        {
            session.Fill(Cell(0, 0), true);

            Assert.AreEqual(32, GridAssert.CountNavigable(session.Grid));
            Assert.IsTrue(session.Commit("Fill"));
            Assert.AreEqual(32, GridAssert.CountNavigable(mask.CreateGrid()));
        }

        [Test]
        public void AfterUndo_Reload_BringsTheWorkingCopyBackInStepWithTheAsset()
        {
            Undo.IncrementCurrentGroup();
            session.Paint(Cell(2, 1), Cell(2, 1), 0.5f, true);
            session.Commit("Paint");

            Undo.PerformUndo();
            session.Reload();

            Assert.AreEqual(0, GridAssert.CountNavigable(mask.CreateGrid()), "the asset is restored");
            Assert.IsFalse(session.Grid.IsNavigable(2, 1), "and so is the working copy");

            // The next stroke must not bring the undone one back.
            session.Paint(Cell(6, 3), Cell(6, 3), 0.5f, true);
            session.Commit("Paint");

            Assert.AreEqual(1, GridAssert.CountNavigable(mask.CreateGrid()));
        }

        [Test]
        public void Reload_DropsUncommittedPaint()
        {
            session.Paint(Cell(2, 1), Cell(2, 1), 0.5f, true);

            session.Reload();

            Assert.IsFalse(session.Grid.IsNavigable(2, 1));
            Assert.IsFalse(session.Commit("Paint"));
        }

        [Test]
        public void Replace_SwapsTheGrid_AndWritesItAtOnce()
        {
            var replacement = new NavigationGrid(16, 8);
            replacement.SetNavigable(15, 7, true);

            session.Replace(replacement, "Resize");

            Assert.AreSame(replacement, session.Grid);
            Assert.AreEqual(16, mask.Width);
            Assert.AreEqual(8, mask.Height);
            Assert.IsTrue(mask.CreateGrid().IsNavigable(15, 7));
        }

        [Test]
        public void Preview_HasOneOpaquePixelPerNavigableCell_BottomRowFirst()
        {
            session.Paint(Cell(1, 0), Cell(1, 0), 0.5f, true);
            session.Paint(Cell(7, 3), Cell(7, 3), 0.5f, true);

            Texture2D preview = session.GetPreview();

            Assert.AreEqual(8, preview.width);
            Assert.AreEqual(4, preview.height);
            Assert.AreEqual(FilterMode.Point, preview.filterMode);
            Assert.AreEqual(1f, preview.GetPixel(1, 0).a, 0.01f);
            Assert.AreEqual(1f, preview.GetPixel(7, 3).a, 0.01f);
            Assert.AreEqual(0f, preview.GetPixel(0, 0).a, 0.01f);
            Assert.AreEqual(0f, preview.GetPixel(7, 0).a, 0.01f);
        }

        [Test]
        public void Preview_FollowsLaterEdits_AndAGridOfAnotherSize()
        {
            Texture2D first = session.GetPreview();
            Assert.AreEqual(0f, first.GetPixel(2, 2).a, 0.01f);

            session.Paint(Cell(2, 2), Cell(2, 2), 0.5f, true);
            Assert.AreEqual(1f, session.GetPreview().GetPixel(2, 2).a, 0.01f);

            session.Replace(new NavigationGrid(16, 8), "Resize");
            Texture2D resized = session.GetPreview();

            Assert.AreEqual(16, resized.width);
            Assert.AreEqual(8, resized.height);
            Assert.AreEqual(0f, resized.GetPixel(2, 2).a, 0.01f);
        }
    }
}
