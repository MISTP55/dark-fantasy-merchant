using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Editor;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Tests.EditMode
{
    /// <summary>The default layout the scene setup gives a city without one.</summary>
    public class DefaultLayoutTests
    {
        private const string CityName = "DefaultLayoutTestsCity";
        private const string LayoutPath = "Assets/Data/CityLayouts/" + CityName + "Layout.asset";

        private CityDefinition city;
        private BuildingsDefinition buildings;

        [SetUp]
        public void SetUp()
        {
            city = ScriptableObject.CreateInstance<CityDefinition>();
            city.name = CityName;
            buildings = TestBuildings.CreateCatalogue();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(LayoutPath);
            Object.DestroyImmediate(city);
            Object.DestroyImmediate(buildings);
        }

        [Test]
        public void TryCreateLayout_GivesWaterAndAPort_AndTheLayoutIsUsable()
        {
            var economy = AssetDatabase.LoadAssetAtPath<EconomyDefinition>("Assets/Data/Economy/Economy.asset");
            Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(LayoutPath), "a layout is already at the test's path");

            Assert.IsTrue(WorldMapSetup.TryCreateLayout(city, economy, buildings, null, 0));

            CityLayoutDefinition layout = city.Layout;
            Assert.IsNotNull(layout);
            Assert.IsTrue(layout.HasPort);
            CollectionAssert.IsNotEmpty(layout.ForbiddenCells);
            Assert.IsTrue(layout.TryCreateGrid(buildings, out CityGrid grid, out string problem), problem);
            Assert.IsNotNull(grid.Port);
        }
    }
}
