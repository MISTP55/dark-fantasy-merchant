using System.Text.RegularExpressions;
using DarkFantasyMerchant.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DarkFantasyMerchant.Tests.EditMode
{
    public class WorldMapViewTests
    {
        private Texture2D texture;
        private Sprite sprite;
        private WorldMapDefinition definition;
        private GameObject viewObject;
        private SpriteRenderer mapRenderer;
        private WorldMapView view;
        private Scene previewScene;

        [SetUp]
        public void SetUp()
        {
            // An 8 x 4 pixel sprite at 4 pixels per unit: 2 x 1 world units, pivot bottom-left.
            texture = new Texture2D(8, 4);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 4f), Vector2.zero, 4f);
            definition = ScriptableObject.CreateInstance<WorldMapDefinition>();

            // A preview scene keeps the test objects, and any dirty flag, out of the open scene.
            previewScene = EditorSceneManager.NewPreviewScene();
            viewObject = new GameObject("World Map");
            SceneManager.MoveGameObjectToScene(viewObject, previewScene);
            var spriteObject = new GameObject("Map Sprite");
            spriteObject.transform.SetParent(viewObject.transform, false);
            mapRenderer = spriteObject.AddComponent<SpriteRenderer>();

            view = viewObject.AddComponent<WorldMapView>();
            SetReference(view, "definition", definition);
            SetReference(view, "mapRenderer", mapRenderer);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(viewObject);
            EditorSceneManager.ClosePreviewScene(previewScene);
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void LayOutMap_HidesTheMap_WhenTheDefinitionHasNoSprite()
        {
            mapRenderer.sprite = sprite; // A sprite left over from an earlier layout.
            LogAssert.Expect(LogType.Error, new Regex("no map sprite"));

            bool laidOut = view.LayOutMap();

            Assert.IsFalse(laidOut);
            Assert.IsNull(mapRenderer.sprite);
        }

        [Test]
        public void IsMapLaidOut_IsFalse_WhenTheSceneSpriteDoesNotMatchTheDefinition()
        {
            SetReference(definition, "mapSprite", sprite);
            mapRenderer.sprite = sprite; // Right sprite, but still at scale 1 around its pivot.

            Assert.IsFalse(view.IsMapLaidOut());
        }

        [Test]
        public void LayOutMap_SizesAndCentersTheSprite_AndIsThenLaidOut()
        {
            SetReference(definition, "mapSprite", sprite);

            Assert.IsTrue(view.LayOutMap());

            // Default world width is 40 and the sprite is 2 units wide.
            Transform mapTransform = mapRenderer.transform;
            Assert.AreSame(sprite, mapRenderer.sprite);
            Assert.AreEqual(20f, mapTransform.localScale.x, 1e-4f);
            Assert.AreEqual(mapRenderer.bounds.center.x, 0f, 1e-3f);
            Assert.AreEqual(mapRenderer.bounds.center.y, 0f, 1e-3f);
            Assert.IsTrue(view.IsMapLaidOut());
        }

        [Test]
        public void RefreshMapLayout_FixesAStaleLayout()
        {
            SetReference(definition, "mapSprite", sprite);
            mapRenderer.sprite = sprite;

            DarkFantasyMerchant.Editor.CityPlacementTool.RefreshMapLayout(view);

            Assert.IsTrue(view.IsMapLaidOut());
            Assert.AreEqual(20f, mapRenderer.transform.localScale.x, 1e-4f);
        }

        [Test]
        public void WithSceneFirst_InsertsTheSceneAndKeepsTheOthers()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/A.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/B.unity", false),
            };

            EditorBuildSettingsScene[] result =
                DarkFantasyMerchant.Editor.WorldMapSetup.WithSceneFirst(scenes, "Assets/Scenes/WorldMap.unity");

            Assert.AreEqual(3, result.Length);
            Assert.AreEqual("Assets/Scenes/WorldMap.unity", result[0].path);
            Assert.IsTrue(result[0].enabled);
            Assert.AreEqual("Assets/Scenes/A.unity", result[1].path);
            Assert.AreEqual("Assets/Scenes/B.unity", result[2].path);
            Assert.IsFalse(result[2].enabled);
        }

        [Test]
        public void WithSceneFirst_LeavesTheListUnchanged_WhenTheSceneIsAlreadyListed()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/A.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/WorldMap.unity", true),
            };

            EditorBuildSettingsScene[] result =
                DarkFantasyMerchant.Editor.WorldMapSetup.WithSceneFirst(scenes, "Assets/Scenes/WorldMap.unity");

            Assert.AreSame(scenes, result);
        }

        private static void SetReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
