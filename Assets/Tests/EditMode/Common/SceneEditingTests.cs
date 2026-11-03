using DarkFantasyMerchant.Editor;
using NUnit.Framework;
using UnityEditor;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class SceneEditingTests
    {
        [Test]
        public void WithSceneAfter_InsertsTheSceneRightAfterTheOther()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/WorldMap.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/B.unity", false),
            };

            EditorBuildSettingsScene[] result = SceneEditing.WithSceneAfter(
                scenes, "Assets/Scenes/CityView.unity", "Assets/Scenes/WorldMap.unity");

            Assert.AreEqual(3, result.Length);
            Assert.AreEqual("Assets/Scenes/WorldMap.unity", result[0].path);
            Assert.AreEqual("Assets/Scenes/CityView.unity", result[1].path);
            Assert.IsTrue(result[1].enabled);
            Assert.AreEqual("Assets/Scenes/B.unity", result[2].path);
            Assert.IsFalse(result[2].enabled);
        }

        [Test]
        public void WithSceneAfter_AppendsTheScene_WhenTheOtherIsNotListed()
        {
            var scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/A.unity", true) };

            EditorBuildSettingsScene[] result = SceneEditing.WithSceneAfter(
                scenes, "Assets/Scenes/CityView.unity", "Assets/Scenes/WorldMap.unity");

            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("Assets/Scenes/A.unity", result[0].path);
            Assert.AreEqual("Assets/Scenes/CityView.unity", result[1].path);
        }

        [Test]
        public void WithSceneAfter_LeavesTheListUnchanged_WhenTheSceneIsAlreadyListed()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/CityView.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/WorldMap.unity", true),
            };

            EditorBuildSettingsScene[] result = SceneEditing.WithSceneAfter(
                scenes, "Assets/Scenes/CityView.unity", "Assets/Scenes/WorldMap.unity");

            Assert.AreSame(scenes, result);
        }
    }
}
