using System.IO;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// Generates the world map scene, the city marker and ship prefabs and the sample content.
    /// Safe to run again: existing assets are left untouched, and an existing scene only
    /// gains the ships object when it has none, and the references to it that are empty.
    /// </summary>
    public static class WorldMapSetup
    {
        private const string ArtFolder = "Assets/Art/WorldMap";
        private const string CitiesFolder = "Assets/Data/Cities";
        private const string MapDataFolder = "Assets/Data/WorldMap";
        private const string PrefabFolder = "Assets/Prefabs/WorldMap";
        private const string UiFolder = "Assets/UI/WorldMap";

        private const string MapTexturePath = ArtFolder + "/WorldMap.jpg";
        private const string MarkerPrefabPath = PrefabFolder + "/CityMarker.prefab";
        private const string DefinitionPath = MapDataFolder + "/WorldMap.asset";
        private const string PanelSettingsPath = UiFolder + "/WorldMapPanelSettings.asset";
        private const string PanelTemplatePath = UiFolder + "/CityInfoPanel.uxml";
        private const string ThemePath = "Assets/UI/DefaultRuntimeTheme.tss";
        private const string ScenePath = "Assets/Scenes/WorldMap.unity";

        private const string ShipTexturePath = "Assets/Art/Ships/MerchantShip.png";
        private const string ShipSpritePrefix = "MerchantShip_";
        private const string ShipDataFolder = "Assets/Data/Ships";
        private const string ShipPrefabFolder = "Assets/Prefabs/Ships";
        private const string ShipDefinitionPath = ShipDataFolder + "/MerchantShip.asset";
        private const string ShipPrefabPath = ShipPrefabFolder + "/Ship.prefab";

        private const float MerchantShipSpeed = 1.5f;
        private const int ShipSortingOrder = 20;

        private const float MarkerPixelsPerUnit = 16f;

        private readonly struct SampleCity
        {
            public SampleCity(string name, float x, float y, CityAccess access, CitySize size, string description)
            {
                Name = name;
                Position = new Vector2(x, y);
                Access = access;
                Size = size;
                Description = description;
            }

            public string Name { get; }
            public Vector2 Position { get; }
            public CityAccess Access { get; }
            public CitySize Size { get; }
            public string Description { get; }
        }

        // Positions are read off the placeholder image and are meant to be refined
        // with the placement tool.
        private static readonly SampleCity[] SampleCities =
        {
            new SampleCity("Sparia", 0.115f, 0.645f, CityAccess.Coastal, CitySize.Town,
                "A weathered harbor town on the western cape, first landfall for ships crossing the West Aedean Sea."),
            new SampleCity("Elforth", 0.196f, 0.609f, CityAccess.Coastal, CitySize.Village,
                "A fishing village sheltered by the southern woods, known for salted cod and quiet smugglers."),
            new SampleCity("Bactfied", 0.374f, 0.554f, CityAccess.Coastal, CitySize.Town,
                "A crowded market port on the Eamiq Sea where river barges meet seagoing hulls."),
            new SampleCity("Hitrun", 0.470f, 0.598f, CityAccess.Coastal, CitySize.Village,
                "A hill village above a narrow cove, trading wool and stone to passing coasters."),
            new SampleCity("Cerbias", 0.554f, 0.500f, CityAccess.Coastal, CitySize.Village,
                "A lonely anchorage at the tip of the southern spit, last shelter before the open Rakmitag Sea."),
            new SampleCity("Hazer Empire", 0.586f, 0.627f, CityAccess.Coastal, CitySize.Capital,
                "The imperial seat guarding the strait, its customs houses taxing every hull bound for the desert coast."),
            new SampleCity("Liveria", 0.345f, 0.651f, CityAccess.River, CitySize.Capital,
                "A spired river capital at the heart of the Crownwoods, reached only by barge."),
        };

        [MenuItem("Tools/Dark Fantasy Merchant/Build World Map Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("The world map setup cannot run in Play mode.");
                return;
            }

            // Read before anything is created: building a prefab instantiates a temporary
            // object in the open scene and marks it as modified.
            Scene openMapScene = SceneManager.GetSceneByPath(ScenePath);
            bool mapSceneHadUnsavedChanges = openMapScene.isLoaded && openMapScene.isDirty;

            EnsureFolders();

            Sprite mapSprite = ImportMapSprite();
            Sprite villageSprite = CreateMarkerSprite("CityMarkerVillage", 12);
            Sprite townSprite = CreateMarkerSprite("CityMarkerTown", 16);
            Sprite capitalSprite = CreateMarkerSprite("CityMarkerCapital", 20);

            CreateMarkerPrefab(villageSprite, townSprite, capitalSprite);
            CreateDefinition(mapSprite);
            CreatePanelSettings();

            ShipDefinition shipDefinition = CreateShipDefinition();
            CreateShipPrefab(shipDefinition);

            AssetDatabase.SaveAssets();
            BuildScene();
            AddShipsToScene(mapSceneHadUnsavedChanges);
            AssetDatabase.SaveAssets();

            Debug.Log("World map setup finished.");
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[]
            {
                ArtFolder, CitiesFolder, MapDataFolder, PrefabFolder, UiFolder,
                ShipDataFolder, ShipPrefabFolder, "Assets/Scenes",
            })
            {
                Directory.CreateDirectory(folder);
            }

            AssetDatabase.Refresh();
        }

        private static Sprite ImportMapSprite()
        {
            var importer = AssetImporter.GetAtPath(MapTexturePath) as TextureImporter;

            if (importer == null)
            {
                throw new FileNotFoundException("The placeholder map image is missing.", MapTexturePath);
            }

            if (importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(MapTexturePath);
        }

        // Placeholder marker: a light disc with a dark outline, light enough to be tinted.
        private static Sprite CreateMarkerSprite(string assetName, int sizePixels)
        {
            string path = $"{ArtFolder}/{assetName}.png";

            if (!File.Exists(path))
            {
                var fill = new Color(0.96f, 0.93f, 0.86f, 1f);
                var outline = new Color(0.12f, 0.08f, 0.06f, 1f);
                var texture = new Texture2D(sizePixels, sizePixels, TextureFormat.RGBA32, false);
                float center = (sizePixels - 1) * 0.5f;
                float outerRadius = sizePixels * 0.5f;
                float innerRadius = outerRadius - 2f;

                for (int y = 0; y < sizePixels; y++)
                {
                    for (int x = 0; x < sizePixels; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                        Color color = distance <= innerRadius ? fill : distance <= outerRadius ? outline : Color.clear;
                        texture.SetPixel(x, y, color);
                    }
                }

                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = MarkerPixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static CityMarkerView CreateMarkerPrefab(Sprite village, Sprite town, Sprite capital)
        {
            var existing = AssetDatabase.LoadAssetAtPath<CityMarkerView>(MarkerPrefabPath);

            if (existing != null)
            {
                return existing;
            }

            // ObjectFactory applies the render pipeline's default sprite material.
            GameObject instance = ObjectFactory.CreateGameObject(
                "CityMarker", typeof(SpriteRenderer), typeof(CityMarkerView));

            var spriteRenderer = instance.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = village;
            spriteRenderer.sortingOrder = 10;

            var marker = instance.GetComponent<CityMarkerView>();
            SetReference(marker, "spriteRenderer", spriteRenderer);
            SetReference(marker, "villageSprite", village);
            SetReference(marker, "townSprite", town);
            SetReference(marker, "capitalSprite", capital);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, MarkerPrefabPath);
            Object.DestroyImmediate(instance);
            return prefab.GetComponent<CityMarkerView>();
        }

        private static WorldMapDefinition CreateDefinition(Sprite mapSprite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<WorldMapDefinition>(DefinitionPath);

            if (existing != null)
            {
                return existing;
            }

            var definition = ScriptableObject.CreateInstance<WorldMapDefinition>();
            AssetDatabase.CreateAsset(definition, DefinitionPath);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("mapSprite").objectReferenceValue = mapSprite;

            SerializedProperty cities = serialized.FindProperty("cities");
            cities.arraySize = SampleCities.Length;

            for (int i = 0; i < SampleCities.Length; i++)
            {
                cities.GetArrayElementAtIndex(i).objectReferenceValue = CreateCity(SampleCities[i]);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static CityDefinition CreateCity(SampleCity sample)
        {
            string path = $"{CitiesFolder}/{sample.Name.Replace(" ", string.Empty)}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<CityDefinition>(path);

            if (existing != null)
            {
                return existing;
            }

            var city = ScriptableObject.CreateInstance<CityDefinition>();
            AssetDatabase.CreateAsset(city, path);

            var serialized = new SerializedObject(city);
            serialized.FindProperty("displayName").stringValue = sample.Name;
            serialized.FindProperty("description").stringValue = sample.Description;
            serialized.FindProperty("mapPosition").vector2Value = sample.Position;
            serialized.FindProperty("access").enumValueIndex = (int)sample.Access;
            serialized.FindProperty("size").enumValueIndex = (int)sample.Size;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return city;
        }

        private static PanelSettings CreatePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            if (existing != null)
            {
                return existing;
            }

            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);

            if (theme == null)
            {
                throw new FileNotFoundException("The runtime UI theme is missing.", ThemePath);
            }

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.themeStyleSheet = theme;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            return panelSettings;
        }

        private static ShipDefinition CreateShipDefinition()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShipDefinition>(ShipDefinitionPath);

            if (existing != null)
            {
                return existing;
            }

            Object[] subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(ShipTexturePath);
            string[] directionNames = System.Enum.GetNames(typeof(CompassDirection));

            // Filled in before the asset is created, so the file is written complete.
            var definition = ScriptableObject.CreateInstance<ShipDefinition>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("displayName").stringValue = "Merchant Ship";
            serialized.FindProperty("speed").floatValue = MerchantShipSpeed;

            SerializedProperty sprites = serialized.FindProperty("directionSprites");
            sprites.arraySize = directionNames.Length;

            for (int i = 0; i < directionNames.Length; i++)
            {
                sprites.GetArrayElementAtIndex(i).objectReferenceValue =
                    FindShipSprite(subAssets, ShipSpritePrefix + directionNames[i]);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, ShipDefinitionPath);
            return definition;
        }

        private static Sprite FindShipSprite(Object[] subAssets, string spriteName)
        {
            foreach (Object subAsset in subAssets)
            {
                if (subAsset is Sprite sprite && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            throw new FileNotFoundException($"Sprite '{spriteName}' is missing from the ship sprite sheet.", ShipTexturePath);
        }

        private static ShipView CreateShipPrefab(ShipDefinition definition)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShipView>(ShipPrefabPath);

            if (existing != null)
            {
                return existing;
            }

            // ObjectFactory applies the render pipeline's default sprite material.
            GameObject instance = ObjectFactory.CreateGameObject("Ship", typeof(SpriteRenderer), typeof(ShipView));

            var spriteRenderer = instance.GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = definition.SpriteFor(CompassDirection.S);
            spriteRenderer.sortingOrder = ShipSortingOrder;

            SetReference(instance.GetComponent<ShipView>(), "spriteRenderer", spriteRenderer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, ShipPrefabPath);
            Object.DestroyImmediate(instance);
            return prefab.GetComponent<ShipView>();
        }

        private static void BuildScene()
        {
            if (File.Exists(ScenePath))
            {
                Debug.Log($"{ScenePath} already exists; scene left untouched.");
                return;
            }

            var panelTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelTemplatePath);

            if (panelTemplate == null)
            {
                throw new FileNotFoundException("The city panel UXML is missing.", PanelTemplatePath);
            }

            // Creating the scene replaces the open one; never lose unsaved work for it.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("World map scene not created: the open scene has unsaved changes.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Opening a new scene unloads unreferenced assets, which turns asset references
            // obtained earlier into null. Load them only now.
            var definition = AssetDatabase.LoadAssetAtPath<WorldMapDefinition>(DefinitionPath);
            var markerPrefab = AssetDatabase.LoadAssetAtPath<CityMarkerView>(MarkerPrefabPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            if (definition == null || markerPrefab == null || panelSettings == null)
            {
                throw new FileNotFoundException("A generated world map asset could not be loaded.");
            }

            GameObject cameraObject = ObjectFactory.CreateGameObject(
                "Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var mapCamera = cameraObject.GetComponent<Camera>();
            mapCamera.orthographic = true;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = Color.black;

            // Lit sprites render black under the 2D renderer without a light.
            GameObject lightObject = ObjectFactory.CreateGameObject("Global Light 2D", typeof(Light2D));
            lightObject.GetComponent<Light2D>().lightType = Light2D.LightType.Global;

            var mapObject = new GameObject("World Map");
            GameObject spriteObject = ObjectFactory.CreateGameObject("Map Sprite", typeof(SpriteRenderer));
            spriteObject.transform.SetParent(mapObject.transform, false);
            var markersObject = new GameObject("City Markers");
            markersObject.transform.SetParent(mapObject.transform, false);

            var mapView = mapObject.AddComponent<WorldMapView>();
            SetReference(mapView, "definition", definition);
            SetReference(mapView, "mapRenderer", spriteObject.GetComponent<SpriteRenderer>());
            SetReference(mapView, "markerPrefab", markerPrefab);
            SetReference(mapView, "markerRoot", markersObject.transform);
            mapView.LayOutMap();

            var uiObject = new GameObject("World Map UI");
            var document = uiObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = panelTemplate;

            var panelController = uiObject.AddComponent<CityInfoPanelController>();
            SetReference(panelController, "mapView", mapView);
            SetReference(panelController, "mapCamera", mapCamera);

            var input = mapObject.AddComponent<WorldMapInput>();
            SetReference(input, "ui", panelController);

            var cameraController = cameraObject.AddComponent<WorldMapCameraController>();
            SetReference(cameraController, "mapView", mapView);
            SetReference(cameraController, "input", input);

            var interaction = mapObject.AddComponent<WorldMapInteraction>();
            SetReference(interaction, "mapView", mapView);
            SetReference(interaction, "input", input);
            SetReference(interaction, "cameraController", cameraController);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = WithSceneFirst(EditorBuildSettings.scenes, ScenePath);
        }

        // Unlike the rest of the scene, the ships object is also added to a scene that
        // already exists, so a map built before ships existed gains them.
        private static void AddShipsToScene(bool sceneHadUnsavedChanges)
        {
            // The scene is missing when its creation was cancelled.
            if (!File.Exists(ScenePath))
            {
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);

            if (!scene.isLoaded)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.Log("Ships not added to the world map scene: the open scene has unsaved changes.");
                    return;
                }

                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var mapView = FindInScene<WorldMapView>(scene);

            if (mapView == null)
            {
                Debug.LogWarning($"{ScenePath} has no WorldMapView; ships not added.");
                return;
            }

            bool changed = false;
            var shipsView = FindInScene<ShipsView>(scene);

            if (shipsView == null)
            {
                // Loaded only now: opening a scene unloads unreferenced assets.
                var shipDefinition = AssetDatabase.LoadAssetAtPath<ShipDefinition>(ShipDefinitionPath);
                var shipPrefab = AssetDatabase.LoadAssetAtPath<ShipView>(ShipPrefabPath);

                if (shipDefinition == null || shipPrefab == null)
                {
                    throw new FileNotFoundException("A generated ship asset could not be loaded.");
                }

                var shipsObject = new GameObject("Ships");
                SceneManager.MoveGameObjectToScene(shipsObject, scene);

                shipsView = shipsObject.AddComponent<ShipsView>();
                SetReference(shipsView, "mapView", mapView);
                SetReference(shipsView, "shipPrefab", shipPrefab);
                SetReference(shipsView, "playerShipDefinition", shipDefinition);
                changed = true;
            }

            var interaction = FindInScene<WorldMapInteraction>(scene);

            if (interaction != null && IsReferenceEmpty(interaction, "shipsView"))
            {
                SetReference(interaction, "shipsView", shipsView);
                changed = true;
            }

            // The city panel lists the ships in port.
            var panelController = FindInScene<CityInfoPanelController>(scene);

            if (panelController != null && IsReferenceEmpty(panelController, "shipsView"))
            {
                SetReference(panelController, "shipsView", shipsView);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);

            // Saving would also write the user's own pending edits; leave that to them.
            if (sceneHadUnsavedChanges)
            {
                Debug.Log($"Ships added to {ScenePath}. The scene had unsaved changes: save it to keep them.");
                return;
            }

            EditorSceneManager.SaveScene(scene);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
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

        private static bool IsReferenceEmpty(Object target, string propertyName)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);

            if (property == null)
            {
                throw new System.MissingFieldException(target.GetType().Name, propertyName);
            }

            return property.objectReferenceValue == null;
        }

        /// <summary>
        /// Returns the build scene list with <paramref name="scenePath"/> first. A list that
        /// already contains it is returned unchanged; other scenes are always kept.
        /// </summary>
        public static EditorBuildSettingsScene[] WithSceneFirst(EditorBuildSettingsScene[] scenes, string scenePath)
        {
            foreach (EditorBuildSettingsScene existing in scenes)
            {
                if (existing.path == scenePath)
                {
                    return scenes;
                }
            }

            var result = new EditorBuildSettingsScene[scenes.Length + 1];
            result[0] = new EditorBuildSettingsScene(scenePath, true);
            scenes.CopyTo(result, 1);
            return result;
        }

        private static void SetReference(Object target, string propertyName, Object value)
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
    }
}
