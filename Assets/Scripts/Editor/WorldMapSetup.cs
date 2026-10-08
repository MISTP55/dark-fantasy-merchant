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
    /// Generates the world map scene, the city marker, ship and ship route prefabs and the
    /// sample content. Safe to run again: existing assets are left untouched, and an
    /// existing scene only gains the ships, ship route, ship panel, world clock, time HUD,
    /// player treasury and treasury HUD objects when it has none, and the references to
    /// them that are empty.
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
        private const string ShipPanelTemplatePath = UiFolder + "/ShipInfoPanel.uxml";
        private const string ThemePath = "Assets/UI/DefaultRuntimeTheme.tss";
        private const string ScenePath = "Assets/Scenes/WorldMap.unity";

        private const string CalendarFolder = "Assets/Data/Calendar";
        private const string CalendarPath = CalendarFolder + "/Calendar.asset";
        private const string TimeHudTemplatePath = UiFolder + "/TimeHud.uxml";

        private const string PlayerDataFolder = "Assets/Data/Player";
        private const string PlayerStartPath = PlayerDataFolder + "/PlayerStart.asset";
        private const string TreasuryHudTemplatePath = UiFolder + "/TreasuryHud.uxml";

        private const string ShipTexturePath = "Assets/Art/Ships/MerchantShip.png";
        private const string ShipSpritePrefix = "MerchantShip_";
        private const string ShipDataFolder = "Assets/Data/Ships";
        private const string ShipPrefabFolder = "Assets/Prefabs/Ships";
        private const string ShipDefinitionPath = ShipDataFolder + "/MerchantShip.asset";
        private const string ShipPrefabPath = ShipPrefabFolder + "/Ship.prefab";

        private const string ShipArtFolder = "Assets/Art/Ships";
        private const string RouteDashTexturePath = ShipArtFolder + "/ShipRouteDash.png";
        private const string RouteMarkerSpritePath = ShipArtFolder + "/ShipRouteDestination.png";
        private const string RouteSailedMaterialPath = ShipArtFolder + "/ShipRouteSailed.mat";
        private const string RouteRemainingMaterialPath = ShipArtFolder + "/ShipRouteRemaining.mat";
        private const string ShipRoutePrefabPath = ShipPrefabFolder + "/ShipRoute.prefab";
        private const string UnlitSpriteShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

        private const float MerchantShipSpeed = 1.5f;
        private const int MerchantShipCrewCapacity = 28;
        private const int ShipSortingOrder = 20;

        // Above the map, below the city markers.
        private const int RouteSortingOrder = 5;

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
                "Une ville portuaire battue par les vents sur le cap occidental, première terre en vue des navires qui traversent la mer d'Aedean occidentale."),
            new SampleCity("Elforth", 0.196f, 0.609f, CityAccess.Coastal, CitySize.Village,
                "Un village de pêcheurs abrité par les bois du sud, connu pour sa morue salée et ses contrebandiers discrets."),
            new SampleCity("Bactfied", 0.374f, 0.554f, CityAccess.Coastal, CitySize.Town,
                "Un port marchand animé sur la mer d'Eamiq, où les barges fluviales rencontrent les navires de haute mer."),
            new SampleCity("Hitrun", 0.470f, 0.598f, CityAccess.Coastal, CitySize.Village,
                "Un village perché au-dessus d'une crique étroite, qui vend laine et pierre aux caboteurs de passage."),
            new SampleCity("Cerbias", 0.554f, 0.500f, CityAccess.Coastal, CitySize.Village,
                "Un mouillage isolé à la pointe de la langue de sable méridionale, dernier abri avant le large de la mer de Rakmitag."),
            new SampleCity("Hazer Empire", 0.586f, 0.627f, CityAccess.Coastal, CitySize.Capital,
                "La capitale impériale qui garde le détroit, dont les douanes taxent chaque navire en route vers la côte du désert."),
            new SampleCity("Liveria", 0.345f, 0.651f, CityAccess.River, CitySize.Capital,
                "Une capitale fluviale hérissée de clochers au cœur des Bois de la Couronne, que l'on ne rejoint qu'en barge."),
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
            CreateShipRoutePrefab();
            CreateCalendar();
            CreatePlayerStart();

            AssetDatabase.SaveAssets();
            BuildScene();
            AddShipsToScene(mapSceneHadUnsavedChanges);
            AddShipPanelToScene(mapSceneHadUnsavedChanges);
            AddTimeToScene(mapSceneHadUnsavedChanges);
            AddTreasuryToScene(mapSceneHadUnsavedChanges);
            AssetDatabase.SaveAssets();

            Debug.Log("World map setup finished.");
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[]
            {
                ArtFolder, CitiesFolder, MapDataFolder, PrefabFolder, UiFolder,
                ShipArtFolder, ShipDataFolder, ShipPrefabFolder, CalendarFolder, PlayerDataFolder,
                "Assets/Scenes",
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

                SaveAsPixelSprite(texture, path);
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Writes the texture to a PNG imported as one pixel art sprite, and destroys it.</summary>
        private static void SaveAsPixelSprite(Texture2D texture, string path)
        {
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

        // A new calendar definition is the game's calendar: its defaults are the content.
        private static CalendarDefinition CreateCalendar()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CalendarDefinition>(CalendarPath);

            if (existing != null)
            {
                return existing;
            }

            var calendar = ScriptableObject.CreateInstance<CalendarDefinition>();
            AssetDatabase.CreateAsset(calendar, CalendarPath);
            return calendar;
        }

        // Like the calendar, a new player start is the game's: its defaults are the content.
        private static PlayerStartDefinition CreatePlayerStart()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PlayerStartDefinition>(PlayerStartPath);

            if (existing != null)
            {
                return existing;
            }

            var playerStart = ScriptableObject.CreateInstance<PlayerStartDefinition>();
            AssetDatabase.CreateAsset(playerStart, PlayerStartPath);
            return playerStart;
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
            serialized.FindProperty("displayName").stringValue = "Navire marchand";
            serialized.FindProperty("speed").floatValue = MerchantShipSpeed;
            serialized.FindProperty("crewCapacity").intValue = MerchantShipCrewCapacity;

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

        private static ShipRouteView CreateShipRoutePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShipRouteView>(ShipRoutePrefabPath);

            if (existing != null)
            {
                return existing;
            }

            Material sailedMaterial = CreateRouteMaterial(RouteSailedMaterialPath, null);
            Material remainingMaterial = CreateRouteMaterial(RouteRemainingMaterialPath, CreateRouteDashTexture());
            Sprite markerSprite = CreateRouteMarkerSprite();

            var instance = new GameObject("ShipRoute", typeof(ShipRouteView));
            LineRenderer sailedLine = CreateRouteLine("Sailed", instance.transform, sailedMaterial);
            LineRenderer remainingLine = CreateRouteLine("Remaining", instance.transform, remainingMaterial);

            // The dashes are the material's texture, repeated along the line.
            remainingLine.textureMode = LineTextureMode.Tile;

            // ObjectFactory applies the render pipeline's default sprite material.
            GameObject markerObject = ObjectFactory.CreateGameObject("Destination", typeof(SpriteRenderer));
            markerObject.transform.SetParent(instance.transform, false);

            var markerRenderer = markerObject.GetComponent<SpriteRenderer>();
            markerRenderer.sprite = markerSprite;
            markerRenderer.sortingOrder = RouteSortingOrder + 1;

            var routeView = instance.GetComponent<ShipRouteView>();
            SetReference(routeView, "sailedLine", sailedLine);
            SetReference(routeView, "remainingLine", remainingLine);
            SetReference(routeView, "destinationMarker", markerRenderer);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, ShipRoutePrefabPath);
            Object.DestroyImmediate(instance);
            return prefab.GetComponent<ShipRouteView>();
        }

        private static LineRenderer CreateRouteLine(string objectName, Transform parent, Material material)
        {
            var lineObject = new GameObject(objectName, typeof(LineRenderer));
            lineObject.transform.SetParent(parent, false);

            var line = lineObject.GetComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.numCornerVertices = 2;
            line.sortingOrder = RouteSortingOrder;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        // Unlit: the route is drawn over the map, not lit as a part of it.
        private static Material CreateRouteMaterial(string path, Texture2D texture)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                // A material that outlived its texture would draw the dashed line solid.
                if (texture != null && existing.mainTexture == null)
                {
                    existing.mainTexture = texture;
                    EditorUtility.SetDirty(existing);
                }

                return existing;
            }

            Shader shader = Shader.Find(UnlitSpriteShaderName);

            if (shader == null)
            {
                throw new FileNotFoundException($"Shader '{UnlitSpriteShaderName}' is missing.");
            }

            var material = new Material(shader);

            if (texture != null)
            {
                material.mainTexture = texture;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // One dash and the gap after it: a white pixel and a clear one, repeated.
        private static Texture2D CreateRouteDashTexture()
        {
            if (!File.Exists(RouteDashTexturePath))
            {
                var texture = new Texture2D(2, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.SetPixel(1, 0, Color.clear);

                File.WriteAllBytes(RouteDashTexturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(RouteDashTexturePath);

                // Not a sprite, which the 2D project default would make it: a line repeats it.
                var importer = (TextureImporter)AssetImporter.GetAtPath(RouteDashTexturePath);
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Point;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(RouteDashTexturePath);
        }

        // Placeholder marker: a white diagonal cross, tinted by the route view.
        private static Sprite CreateRouteMarkerSprite()
        {
            if (!File.Exists(RouteMarkerSpritePath))
            {
                const int sizePixels = 12;
                var texture = new Texture2D(sizePixels, sizePixels, TextureFormat.RGBA32, false);

                for (int y = 0; y < sizePixels; y++)
                {
                    for (int x = 0; x < sizePixels; x++)
                    {
                        bool onCross = Mathf.Abs(x - y) <= 1 || Mathf.Abs(x + y - (sizePixels - 1)) <= 1;
                        texture.SetPixel(x, y, onCross ? Color.white : Color.clear);
                    }
                }

                SaveAsPixelSprite(texture, RouteMarkerSpritePath);
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(RouteMarkerSpritePath);
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
            if (!TryOpenMapScene("Ships", out Scene scene))
            {
                return;
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

            // Like the ships object, the route of the selected ship is added to a scene
            // built before it existed.
            if (IsReferenceEmpty(shipsView, "routeView"))
            {
                var routeView = FindInScene<ShipRouteView>(scene);

                if (routeView == null)
                {
                    var routePrefab = AssetDatabase.LoadAssetAtPath<ShipRouteView>(ShipRoutePrefabPath);

                    if (routePrefab == null)
                    {
                        throw new FileNotFoundException("The ship route prefab could not be loaded.", ShipRoutePrefabPath);
                    }

                    routeView = (ShipRouteView)PrefabUtility.InstantiatePrefab(routePrefab, shipsView.transform);
                }

                SetReference(shipsView, "routeView", routeView);
                changed = true;
            }

            // The player ship takes its starting crew from the player start, also in a
            // scene built before ships had a crew.
            if (IsReferenceEmpty(shipsView, "playerStart"))
            {
                var playerStart = AssetDatabase.LoadAssetAtPath<PlayerStartDefinition>(PlayerStartPath);

                if (playerStart == null)
                {
                    throw new FileNotFoundException("The player start could not be loaded.", PlayerStartPath);
                }

                SetReference(shipsView, "playerStart", playerStart);
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

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "Ships, their route or their starting crew");
            }
        }

        // Like the ships, the panel of the selected ship is added to a scene built before
        // it existed.
        private static void AddShipPanelToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The ship panel", out Scene scene))
            {
                return;
            }

            var shipsView = FindInScene<ShipsView>(scene);

            if (shipsView == null)
            {
                Debug.LogWarning($"{ScenePath} has no ShipsView; ship panel not added.");
                return;
            }

            var panel = FindInScene<ShipInfoPanelController>(scene);

            if (panel == null)
            {
                var panelTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ShipPanelTemplatePath);

                if (panelTemplate == null)
                {
                    throw new FileNotFoundException("The ship panel UXML is missing.", ShipPanelTemplatePath);
                }

                var panelObject = new GameObject("Ship Panel");
                SceneManager.MoveGameObjectToScene(panelObject, scene);

                var document = panelObject.AddComponent<UIDocument>();
                document.panelSettings = FindPanelSettings(scene);
                document.visualTreeAsset = panelTemplate;

                panel = panelObject.AddComponent<ShipInfoPanelController>();
            }

            // Also for a scene whose ships were deleted and made again.
            if (IsReferenceEmpty(panel, "shipsView"))
            {
                SetReference(panel, "shipsView", shipsView);
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The ship panel");
            }
        }

        // Like the ships, the world clock and the time HUD are added to a scene built
        // before they existed.
        private static void AddTimeToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The world clock", out Scene scene))
            {
                return;
            }

            bool changed = false;
            var worldClock = FindInScene<WorldClock>(scene);

            if (worldClock == null)
            {
                // Loaded only now: opening a scene unloads unreferenced assets.
                var calendar = AssetDatabase.LoadAssetAtPath<CalendarDefinition>(CalendarPath);

                if (calendar == null)
                {
                    throw new FileNotFoundException("The calendar could not be loaded.", CalendarPath);
                }

                var clockObject = new GameObject("World Clock");
                SceneManager.MoveGameObjectToScene(clockObject, scene);

                worldClock = clockObject.AddComponent<WorldClock>();
                SetReference(worldClock, "calendar", calendar);
                changed = true;
            }

            if (FindInScene<TimeHudController>(scene) == null)
            {
                var hudTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TimeHudTemplatePath);

                if (hudTemplate == null)
                {
                    throw new FileNotFoundException("The time HUD UXML is missing.", TimeHudTemplatePath);
                }

                var hudObject = new GameObject("Time HUD");
                SceneManager.MoveGameObjectToScene(hudObject, scene);

                var document = hudObject.AddComponent<UIDocument>();
                document.panelSettings = FindPanelSettings(scene);
                document.visualTreeAsset = hudTemplate;

                SetReference(hudObject.AddComponent<TimeHudController>(), "worldClock", worldClock);
                changed = true;
            }

            // Ships sail in the world's time; the map and its camera follow the fast forward.
            // The HUD is in the list for a scene whose clock was deleted and made again.
            foreach (Component follower in new Component[]
            {
                FindInScene<TimeHudController>(scene),
                FindInScene<ShipsView>(scene),
                FindInScene<WorldMapInteraction>(scene),
                FindInScene<WorldMapCameraController>(scene),
            })
            {
                if (follower != null && IsReferenceEmpty(follower, "worldClock"))
                {
                    SetReference(follower, "worldClock", worldClock);
                    changed = true;
                }
            }

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The world clock or the time HUD");
            }
        }

        // Like the world clock, the player's treasury and its HUD are added to a scene built
        // before they existed.
        private static void AddTreasuryToScene(bool sceneHadUnsavedChanges)
        {
            if (!TryOpenMapScene("The player's treasury", out Scene scene))
            {
                return;
            }

            bool changed = false;
            var playerTreasury = FindInScene<PlayerTreasury>(scene);

            if (playerTreasury == null)
            {
                // Loaded only now: opening a scene unloads unreferenced assets.
                var playerStart = AssetDatabase.LoadAssetAtPath<PlayerStartDefinition>(PlayerStartPath);

                if (playerStart == null)
                {
                    throw new FileNotFoundException("The player start could not be loaded.", PlayerStartPath);
                }

                var treasuryObject = new GameObject("Player Treasury");
                SceneManager.MoveGameObjectToScene(treasuryObject, scene);

                playerTreasury = treasuryObject.AddComponent<PlayerTreasury>();
                SetReference(playerTreasury, "playerStart", playerStart);
                changed = true;
            }

            var hud = FindInScene<TreasuryHudController>(scene);

            if (hud == null)
            {
                var hudTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TreasuryHudTemplatePath);

                if (hudTemplate == null)
                {
                    throw new FileNotFoundException("The treasury HUD UXML is missing.", TreasuryHudTemplatePath);
                }

                var hudObject = new GameObject("Treasury HUD");
                SceneManager.MoveGameObjectToScene(hudObject, scene);

                var document = hudObject.AddComponent<UIDocument>();
                document.panelSettings = FindPanelSettings(scene);
                document.visualTreeAsset = hudTemplate;

                hud = hudObject.AddComponent<TreasuryHudController>();
            }

            // Also for a scene whose treasury was deleted and made again.
            if (IsReferenceEmpty(hud, "playerTreasury"))
            {
                SetReference(hud, "playerTreasury", playerTreasury);
                changed = true;
            }

            if (changed)
            {
                SaveSceneChanges(scene, sceneHadUnsavedChanges, "The player's treasury or its HUD");
            }
        }

        // The HUDs and the ship panel share the city panel's panel, so that the map sees the pointer over
        // their buttons as it does over the panel.
        private static PanelSettings FindPanelSettings(Scene scene)
        {
            var cityPanel = FindInScene<CityInfoPanelController>(scene);
            PanelSettings panelSettings = cityPanel != null
                ? cityPanel.GetComponent<UIDocument>().panelSettings
                : null;

            if (panelSettings == null)
            {
                panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            }

            if (panelSettings == null)
            {
                throw new FileNotFoundException("The panel settings could not be loaded.", PanelSettingsPath);
            }

            return panelSettings;
        }

        /// <returns>False when the scene does not exist or could not be opened.</returns>
        private static bool TryOpenMapScene(string what, out Scene scene)
        {
            scene = default;

            // The scene is missing when its creation was cancelled.
            if (!File.Exists(ScenePath))
            {
                return false;
            }

            scene = SceneManager.GetSceneByPath(ScenePath);

            if (scene.isLoaded)
            {
                return true;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log($"{what} not added to the world map scene: the open scene has unsaved changes.");
                return false;
            }

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return true;
        }

        private static void SaveSceneChanges(Scene scene, bool sceneHadUnsavedChanges, string what)
        {
            EditorSceneManager.MarkSceneDirty(scene);

            // Saving would also write the user's own pending edits; leave that to them.
            if (sceneHadUnsavedChanges)
            {
                Debug.Log($"{what} added to {ScenePath}. The scene had unsaved changes: save it to keep them.");
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
