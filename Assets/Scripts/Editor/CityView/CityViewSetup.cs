using System.IO;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// Builds the city view scene, its placeholder sprites and its layer, and lists the scene after the map's in the
    /// build list. Like <see cref="WorldMapSetup"/>, which calls it, it creates what is missing and leaves what
    /// exists untouched.
    /// </summary>
    public static class CityViewSetup
    {
        public const string ScenePath = "Assets/Scenes/CityView.unity";

        /// <summary>The layer of everything the city view draws: only its camera draws it, never the map's.</summary>
        public const string LayerName = "CityView";

        private const string ArtFolder = "Assets/Art/CityView";
        private const string UiFolder = "Assets/UI/CityView";
        private const string TemplatePath = UiFolder + "/CityView.uxml";
        private const string MaterialPath = ArtFolder + "/CityViewSprite.mat";
        private const string CellSpritePath = ArtFolder + "/Cell.png";
        private const string Block2x1SpritePath = ArtFolder + "/Block2x1.png";
        private const string Block1x2SpritePath = ArtFolder + "/Block1x2.png";
        private const string Block2x2SpritePath = ArtFolder + "/Block2x2.png";

        // A cell is 1 world unit across (IsoProjection.CellWidth): 64 by 32 pixels.
        private const int PixelsPerUnit = 64;
        private const int BlockHeightPixels = 32;
        private const int FirstUserLayer = 8;

        // Below the grid, where nothing is drawn.
        private static readonly Color BackgroundColor = new Color(0.06f, 0.05f, 0.04f, 1f);

        public static void Build()
        {
            Directory.CreateDirectory(ArtFolder);
            AssetDatabase.Refresh();

            int layer = EnsureLayer();

            if (layer < 0)
            {
                Debug.LogError($"No free layer for '{LayerName}'; the city view scene is not built.");
                return;
            }

            CreateSprite(CellSpritePath, 1, 1, 0);
            CreateSprite(Block2x1SpritePath, 2, 1, BlockHeightPixels);
            CreateSprite(Block1x2SpritePath, 1, 2, BlockHeightPixels);
            CreateSprite(Block2x2SpritePath, 2, 2, BlockHeightPixels);
            CreateMaterial();
            AssetDatabase.SaveAssets();

            BuildScene(layer);
            EditorBuildSettings.scenes =
                WorldMapSetup.WithSceneAfter(EditorBuildSettings.scenes, ScenePath, WorldMapSetup.ScenePath);
        }

        /// <returns>The layer's index, made in the first free user layer when missing; -1 when none is free.</returns>
        private static int EnsureLayer()
        {
            int existing = LayerMask.NameToLayer(LayerName);

            if (existing >= 0)
            {
                return existing;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = FirstUserLayer; i < layers.arraySize; i++)
            {
                SerializedProperty entry = layers.GetArrayElementAtIndex(i);

                if (string.IsNullOrEmpty(entry.stringValue))
                {
                    entry.stringValue = LayerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }

            return -1;
        }

        private static void CreateMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
            {
                return;
            }

            Shader shader = Shader.Find(WorldMapSetup.UnlitSpriteShaderName);

            if (shader == null)
            {
                throw new FileNotFoundException($"Shader '{WorldMapSetup.UnlitSpriteShaderName}' is missing.");
            }

            AssetDatabase.CreateAsset(new Material(shader), MaterialPath);
        }

        // Placeholder: a white block over a footprint covering sizeX by sizeY cells, its sides shaded and its edges
        // outlined so that a tint shows its shape; a flat diamond without height. The pivot is the centre of the
        // footprint on the ground, where IsoProjection.CenterOf(origin, size) puts it.
        private static void CreateSprite(string path, int sizeX, int sizeY, int heightPixels)
        {
            if (File.Exists(path))
            {
                return;
            }

            const int halfCellWidth = PixelsPerUnit / 2;
            const int quarterCellWidth = PixelsPerUnit / 4;
            int width = (sizeX + sizeY) * halfCellWidth;
            int groundHeight = (sizeX + sizeY) * quarterCellWidth;
            int height = groundHeight + heightPixels;

            // The corners of the footprint on the ground.
            var top = new Vector2(sizeY * halfCellWidth, groundHeight);
            var right = new Vector2(width, sizeY * quarterCellWidth);
            var bottom = new Vector2(sizeX * halfCellWidth, 0f);
            var left = new Vector2(0f, sizeX * quarterCellWidth);
            var up = new Vector2(0f, heightPixels);

            const int empty = 0;
            const int topFace = 1;
            const int leftFace = 2;
            const int rightFace = 3;
            var faces = new int[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    faces[x, y] = IsInside(point, top + up, right + up, bottom + up, left + up) ? topFace
                        : heightPixels > 0 && IsInside(point, left + up, left, bottom, bottom + up) ? leftFace
                        : heightPixels > 0 && IsInside(point, bottom + up, bottom, right, right + up) ? rightFace
                        : empty;
                }
            }

            var fill = new[] { Color.clear, Color.white, new Color(0.78f, 0.78f, 0.78f, 1f), new Color(0.62f, 0.62f, 0.62f, 1f) };
            Color outline = heightPixels > 0 ? new Color(0.35f, 0.35f, 0.35f, 1f) : new Color(0.75f, 0.75f, 0.75f, 1f);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int face = faces[x, y];
                    bool isEdge = face != empty
                        && (FaceAt(faces, x - 1, y) != face || FaceAt(faces, x + 1, y) != face
                            || FaceAt(faces, x, y - 1) != face || FaceAt(faces, x, y + 1) != face);
                    texture.SetPixel(x, y, isEdge ? outline : fill[face]);
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, groundHeight * 0.5f / height);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static int FaceAt(int[,] faces, int x, int y)
        {
            bool inside = x >= 0 && y >= 0 && x < faces.GetLength(0) && y < faces.GetLength(1);
            return inside ? faces[x, y] : 0;
        }

        // The corners go round the quadrilateral, which is convex, in either direction.
        private static bool IsInside(Vector2 point, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float ab = Cross(b - a, point - a);
            float bc = Cross(c - b, point - b);
            float cd = Cross(d - c, point - c);
            float da = Cross(a - d, point - d);
            return (ab >= 0f && bc >= 0f && cd >= 0f && da >= 0f) || (ab <= 0f && bc <= 0f && cd <= 0f && da <= 0f);
        }

        private static float Cross(Vector2 u, Vector2 v)
        {
            return u.x * v.y - u.y * v.x;
        }

        private static void BuildScene(int layer)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.isLoaded;
            bool hadUnsavedChanges = wasLoaded && scene.isDirty;

            if (!wasLoaded)
            {
                // A scene is created or opened additively only beside saved scenes.
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                    {
                        Debug.Log("City view scene not built: an untitled scene is open.");
                        return;
                    }
                }

                if (File.Exists(ScenePath))
                {
                    scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                }
                else
                {
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    EditorSceneManager.SaveScene(scene, ScenePath);
                }
            }

            bool changed = FillScene(scene, layer);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);

                if (hadUnsavedChanges)
                {
                    Debug.Log($"The city view added to {ScenePath}. The scene had unsaved changes: save it to keep them.");
                }
                else
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }

            if (!wasLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <returns>True when something was added.</returns>
        private static bool FillScene(Scene scene, int layer)
        {
            // Loaded only now: opening a scene unloads unreferenced assets.
            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TemplatePath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(WorldMapSetup.PanelSettingsPath);

            if (template == null || panelSettings == null)
            {
                throw new FileNotFoundException("The city view UXML or the panel settings could not be loaded.");
            }

            bool changed = false;

            var input = WorldMapSetup.FindInScene<CityViewInput>(scene);

            if (input == null)
            {
                input = WorldMapSetup.CreateInGroup(scene, WorldMapSetup.WorldGroup, "City View Input")
                    .AddComponent<CityViewInput>();
                changed = true;
            }

            var viewCamera = WorldMapSetup.FindInScene<CityViewCamera>(scene);

            if (viewCamera == null)
            {
                GameObject cameraObject = WorldMapSetup.CreateInGroup(scene, WorldMapSetup.RenderingGroup, "City View Camera");
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);

                // The map's camera, which carries the other listener, is off while the view is open.
                cameraObject.AddComponent<AudioListener>();

                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = BackgroundColor;

                // The map's objects stay where they are, under the view: only the view's layer is drawn.
                camera.cullingMask = 1 << layer;

                viewCamera = cameraObject.AddComponent<CityViewCamera>();
                changed = true;
            }

            var gridView = WorldMapSetup.FindInScene<CityGridView>(scene);

            if (gridView == null)
            {
                GameObject gridObject = WorldMapSetup.CreateInGroup(scene, WorldMapSetup.WorldGroup, "City Grid");
                gridObject.layer = layer;
                gridView = gridObject.AddComponent<CityGridView>();
                changed = true;
            }

            var placement = WorldMapSetup.FindInScene<CityPlacementController>(scene);

            if (placement == null)
            {
                placement = WorldMapSetup.CreateInGroup(scene, WorldMapSetup.WorldGroup, "City Placement")
                    .AddComponent<CityPlacementController>();
                changed = true;
            }

            var controller = WorldMapSetup.FindInScene<CityViewController>(scene);

            if (controller == null)
            {
                GameObject uiObject = WorldMapSetup.CreateInGroup(scene, WorldMapSetup.UiGroup, "City View UI");

                // The panel every game document shares: the view's input sees the HUDs as UI too.
                var document = uiObject.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = template;

                controller = uiObject.AddComponent<CityViewController>();
                changed = true;
            }

            changed |= WorldMapSetup.FillReference(viewCamera, "input", input);
            changed |= WorldMapSetup.FillReference(input, "document", controller.GetComponent<UIDocument>());
            changed |= WorldMapSetup.FillReference(controller, "gridView", gridView);
            changed |= WorldMapSetup.FillReference(controller, "viewCamera", viewCamera);
            changed |= WorldMapSetup.FillReference(controller, "placement", placement);
            changed |= WorldMapSetup.FillReference(placement, "input", input);
            changed |= WorldMapSetup.FillReference(placement, "viewCamera", viewCamera);
            changed |= WorldMapSetup.FillReference(placement, "gridView", gridView);
            changed |= WorldMapSetup.FillReference(gridView, "spriteMaterial", AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
            changed |= WorldMapSetup.FillReference(gridView, "cellSprite", AssetDatabase.LoadAssetAtPath<Sprite>(CellSpritePath));
            changed |= WorldMapSetup.FillReference(gridView, "block2x1Sprite", AssetDatabase.LoadAssetAtPath<Sprite>(Block2x1SpritePath));
            changed |= WorldMapSetup.FillReference(gridView, "block1x2Sprite", AssetDatabase.LoadAssetAtPath<Sprite>(Block1x2SpritePath));
            changed |= WorldMapSetup.FillReference(gridView, "block2x2Sprite", AssetDatabase.LoadAssetAtPath<Sprite>(Block2x2SpritePath));
            return changed;
        }
    }
}
