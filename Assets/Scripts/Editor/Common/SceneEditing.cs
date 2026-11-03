using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// What the code that edits the game's scenes shares: the city view setup, and the one-off scripts that add an
    /// object to a scene (run in the Editor, their result committed). Public, so that those scripts, compiled
    /// apart, can call it.
    /// </summary>
    public static class SceneEditing
    {
        public const string WorldMapScenePath = "Assets/Scenes/WorldMap.unity";

        /// <summary>The panel settings every UI document of the game shares.</summary>
        public const string PanelSettingsPath = "Assets/UI/WorldMap/WorldMapPanelSettings.asset";

        // The groups at the root of a scene, which hold all its objects.
        public const string RenderingGroup = "Rendering";
        public const string WorldGroup = "World";
        public const string UiGroup = "UI";
        public const string SimulationGroup = "Simulation";
        public const string DebugGroup = "Debug";

        public static readonly string[] Groups = { RenderingGroup, WorldGroup, UiGroup, SimulationGroup, DebugGroup };

        /// <summary>A new object, last in its group; the group is made when missing.</summary>
        public static GameObject CreateInGroup(Scene scene, string group, string name)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(SceneGroup(scene, group), false);
            return gameObject;
        }

        // A group missing from an existing scene is added after the others.
        private static Transform SceneGroup(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root.transform;
                }
            }

            var group = new GameObject(name);
            SceneManager.MoveGameObjectToScene(group, scene);
            return group.transform;
        }

        public static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);

                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        public static bool IsReferenceEmpty(Object target, string propertyName)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);

            if (property == null)
            {
                throw new System.MissingFieldException(target.GetType().Name, propertyName);
            }

            return property.objectReferenceValue == null;
        }

        /// <returns>True when the reference was empty and is now set.</returns>
        public static bool FillReference(Object target, string propertyName, Object value)
        {
            if (value == null || !IsReferenceEmpty(target, propertyName))
            {
                return false;
            }

            SetReference(target, propertyName, value);
            return true;
        }

        public static void SetReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);

            if (property == null)
            {
                throw new System.MissingFieldException(target.GetType().Name, propertyName);
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Returns the build scene list with <paramref name="scenePath"/> right after <paramref name="afterPath"/>,
        /// or last when that one is not listed. A list that already contains it is returned unchanged; other scenes
        /// are always kept, in their order.
        /// </summary>
        public static EditorBuildSettingsScene[] WithSceneAfter(
            EditorBuildSettingsScene[] scenes, string scenePath, string afterPath)
        {
            int insertAt = scenes.Length;

            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    return scenes;
                }

                if (scenes[i].path == afterPath)
                {
                    insertAt = i + 1;
                }
            }

            var result = new List<EditorBuildSettingsScene>(scenes);
            result.Insert(insertAt, new EditorBuildSettingsScene(scenePath, true));
            return result.ToArray();
        }
    }
}
