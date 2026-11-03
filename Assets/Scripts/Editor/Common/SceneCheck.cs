using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// Checks the world map and city view scenes, and changes nothing: their root holds only the groups
    /// (<see cref="SceneEditing.Groups"/>), no script is missing, no component of the game has an empty or broken
    /// object reference, and every UI document has its UXML and the shared panel settings. Run it after editing a
    /// scene.
    /// </summary>
    public static class SceneCheck
    {
        private const string GameNamespace = "DarkFantasyMerchant";

        [MenuItem("Tools/Dark Fantasy Merchant/Check Scenes")]
        public static void CheckFromMenu()
        {
            List<string> problems = Run();

            if (problems.Count == 0)
            {
                Debug.Log("Scene check: no problem found.");
                return;
            }

            foreach (string problem in problems)
            {
                Debug.LogError($"Scene check: {problem}");
            }
        }

        /// <summary>The problems found; a scene that is not open is opened beside the others, then closed.</summary>
        public static List<string> Run()
        {
            var problems = new List<string>();
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(SceneEditing.PanelSettingsPath);

            if (panelSettings == null)
            {
                problems.Add($"the shared panel settings are missing ({SceneEditing.PanelSettingsPath}).");
            }

            foreach (string path in new[] { SceneEditing.WorldMapScenePath, CityViewSetup.ScenePath })
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool wasLoaded = scene.isLoaded;

                if (!wasLoaded)
                {
                    if (!File.Exists(path))
                    {
                        problems.Add($"{path} is missing.");
                        continue;
                    }

                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                try
                {
                    Check(scene, panelSettings, problems);
                }
                finally
                {
                    if (!wasLoaded)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }

            return problems;
        }

        private static void Check(Scene scene, PanelSettings panelSettings, List<string> problems)
        {
            var groups = new HashSet<string>(SceneEditing.Groups);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!groups.Contains(root.name))
                {
                    problems.Add($"{scene.path}: '{root.name}' is at the root, outside the groups.");
                }

                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    string where = $"{scene.path}: {PathOf(transform)}";
                    bool missingScript = false;

                    // A component whose script is missing is listed as null.
                    foreach (Component component in transform.GetComponents<Component>())
                    {
                        if (component == null)
                        {
                            missingScript = true;
                        }
                        else
                        {
                            CheckComponent(where, component, panelSettings, problems);
                        }
                    }

                    if (missingScript)
                    {
                        problems.Add($"{where} has a missing script.");
                    }
                }
            }
        }

        private static void CheckComponent(string where, Component component, PanelSettings panelSettings, List<string> problems)
        {
            if (component is UIDocument document)
            {
                if (document.visualTreeAsset == null)
                {
                    problems.Add($"{where}: the UI document has no UXML.");
                }

                if (document.panelSettings != panelSettings)
                {
                    problems.Add($"{where}: the UI document does not use the shared panel settings.");
                }

                return;
            }

            string typeNamespace = component.GetType().Namespace ?? "";

            if (!typeNamespace.StartsWith(GameNamespace))
            {
                return;
            }

            SerializedProperty property = new SerializedObject(component).GetIterator();

            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference
                    && property.propertyPath != "m_Script"
                    && property.objectReferenceValue == null)
                {
                    string state = property.objectReferenceEntityIdValue.IsValid() ? "broken" : "empty";
                    problems.Add($"{where} ({component.GetType().Name}): '{property.propertyPath}' is {state}.");
                }
            }
        }

        private static string PathOf(Transform transform)
        {
            string path = transform.name;

            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = $"{parent.name}/{path}";
            }

            return path;
        }
    }
}
