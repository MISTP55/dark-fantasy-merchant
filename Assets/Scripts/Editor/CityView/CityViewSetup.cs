using System.Collections.Generic;
using System.IO;
using DarkFantasyMerchant.Core;
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
        private const string Block3x2SpritePath = ArtFolder + "/Block3x2.png";
        private const string Block2x3SpritePath = ArtFolder + "/Block2x3.png";
        private const string PortBackSpritePath = ArtFolder + "/PortBack.png";
        private const string PortFrontSpritePath = ArtFolder + "/PortFront.png";
        private const string WallSpritePrefix = ArtFolder + "/Wall";
        private const string ShipSpritePath = ArtFolder + "/ShipCylinder.png";

        // A cell is 1 world unit across (IsoProjection.CellWidth): 64 by 32 pixels.
        private const int PixelsPerUnit = 64;
        private const int BlockHeightPixels = 32;

        // A cell's height is half its width on screen, as a block's is. A straight wall is 2 cells high, a tower 3.
        private const int CellHeightPixels = PixelsPerUnit / 2;
        private const int StraightWallCells = 2;
        private const int TowerCells = 3;

        // A tower is drawn as a block scaled by CityGridView's default buildingScale would be, its gap with the cells
        // beside it drawn in; the heights of the walls are scaled the same. A straight section and a tower's joins
        // reach the cells of their neighbours.
        private const float WallScale = CityGridView.DefaultBuildingScale;

        // The thickness of a straight section and of a tower's joins, in cells: thinner than a tower, so that it shows.
        private const float StraightWallThickness = 0.6f;

        // How far, in cells, a wall's faces are taken to go on into the cell of a neighbour for their outline: no
        // outline is drawn where two walls meet, and the wall looks of one piece.
        private const float WallOverhang = 0.1f;

        private const int ShipHeightPixels = 20;
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
            CreateSprite(Block3x2SpritePath, 3, 2, BlockHeightPixels);
            CreateSprite(Block2x3SpritePath, 2, 3, BlockHeightPixels);
            CreatePortSprite(PortBackSpritePath, true);
            CreatePortSprite(PortFrontSpritePath, false);

            for (int sides = 0; sides < WallShapes.Count; sides++)
            {
                CreateWallSprite(WallSpritePathOf((WallSides)sides), (WallSides)sides);
            }

            CreateShipSprite(ShipSpritePath);
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

            // The corners of the footprint on the ground: x goes to the upper right, y to the upper left.
            var top = new Vector2(sizeX * halfCellWidth, groundHeight);
            var right = new Vector2(width, sizeX * quarterCellWidth);
            var bottom = new Vector2(sizeY * halfCellWidth, 0f);
            var left = new Vector2(0f, sizeY * quarterCellWidth);
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
            WriteSprite(path, faces, fill, outline, groundHeight);
        }

        /// <summary>
        /// The port's placeholder, 4 cells on x and 3 on y, drawn Along: its 2 water rows a flat deck on the ground,
        /// its land row a block. With the water at the back, the water rows are those of largest y (facing north);
        /// otherwise those of smallest y (facing south). Mirrored, it shows the east and west facings.
        /// </summary>
        private static void CreatePortSprite(string path, bool waterAtBack)
        {
            if (File.Exists(path))
            {
                return;
            }

            const int sizeX = 4;
            const int sizeY = 3;
            const int waterRows = 2;
            const int halfCellWidth = PixelsPerUnit / 2;
            const int quarterCellWidth = PixelsPerUnit / 4;
            int width = (sizeX + sizeY) * halfCellWidth;
            int groundHeight = (sizeX + sizeY) * quarterCellWidth;
            int height = groundHeight + BlockHeightPixels;

            // A point of the ground, in cells, on the texture: x goes to the upper right, y to the upper left.
            Vector2 At(float x, float y)
            {
                return new Vector2((x - y + sizeY) * halfCellWidth, (x + y) * quarterCellWidth);
            }

            float landFrom = waterAtBack ? 0f : waterRows;
            float landTo = waterAtBack ? sizeY - waterRows : sizeY;
            float waterFrom = waterAtBack ? sizeY - waterRows : 0f;
            float waterTo = waterAtBack ? sizeY : waterRows;
            var up = new Vector2(0f, BlockHeightPixels);

            const int empty = 0;
            const int topFace = 1;
            const int leftFace = 2;
            const int rightFace = 3;
            const int deck = 4;
            var faces = new int[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);

                    // The block is drawn over the deck: in front of it, it hides it; behind it, they never overlap.
                    faces[x, y] = IsInside(point, At(sizeX, landTo) + up, At(sizeX, landFrom) + up, At(0f, landFrom) + up, At(0f, landTo) + up) ? topFace
                        : IsInside(point, At(0f, landTo) + up, At(0f, landTo), At(0f, landFrom), At(0f, landFrom) + up) ? leftFace
                        : IsInside(point, At(0f, landFrom) + up, At(0f, landFrom), At(sizeX, landFrom), At(sizeX, landFrom) + up) ? rightFace
                        : IsInside(point, At(sizeX, waterTo), At(sizeX, waterFrom), At(0f, waterFrom), At(0f, waterTo)) ? deck
                        : empty;
                }
            }

            var fill = new[]
            {
                Color.clear, Color.white, new Color(0.78f, 0.78f, 0.78f, 1f), new Color(0.62f, 0.62f, 0.62f, 1f),
                new Color(0.55f, 0.55f, 0.55f, 1f),
            };
            WriteSprite(path, faces, fill, new Color(0.35f, 0.35f, 0.35f, 1f), groundHeight);
        }

        /// <summary>
        /// The ship's placeholder: a white upright cylinder standing on one cell, the circle inscribed in the cell
        /// seen in the projection (an ellipse twice as wide as high), its side shaded and its edges outlined. The pivot
        /// is the centre of the cell on the ground, as for the blocks.
        /// </summary>
        private static void CreateShipSprite(string path)
        {
            if (File.Exists(path))
            {
                return;
            }

            const int width = PixelsPerUnit;
            const int groundHeight = PixelsPerUnit / 2;
            const int height = groundHeight + ShipHeightPixels;

            // The circle inscribed in the cell on the ground: its half-width is half the cell's width over the root of 2.
            float halfWidth = PixelsPerUnit * 0.25f * Mathf.Sqrt(2f);
            float halfHeight = halfWidth * 0.5f;
            var groundCentre = new Vector2(width * 0.5f, groundHeight * 0.5f);
            var topCentre = groundCentre + new Vector2(0f, ShipHeightPixels);

            const int empty = 0;
            const int topFace = 1;
            const int sideFace = 2;
            var faces = new int[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    float across = (point.x - groundCentre.x) / halfWidth;
                    bool inTop = IsInEllipse(point, topCentre, halfWidth, halfHeight);
                    bool inSide = Mathf.Abs(across) <= 1f && point.y <= topCentre.y
                        && (point.y >= groundCentre.y || IsInEllipse(point, groundCentre, halfWidth, halfHeight));
                    faces[x, y] = inTop ? topFace : inSide ? sideFace : empty;
                }
            }

            var fill = new[] { Color.clear, Color.white, new Color(0.7f, 0.7f, 0.7f, 1f) };
            WriteSprite(path, faces, fill, new Color(0.35f, 0.35f, 0.35f, 1f), groundHeight);
        }

        /// <summary>
        /// The path of a wall's placeholder: "Wall_" and the sides on which another wall stands (E, N, W, S), or
        /// "Wall_Alone".
        /// </summary>
        private static string WallSpritePathOf(WallSides sides)
        {
            string name = (sides & WallSides.East) != 0 ? "E" : "";
            name += (sides & WallSides.North) != 0 ? "N" : "";
            name += (sides & WallSides.West) != 0 ? "W" : "";
            name += (sides & WallSides.South) != 0 ? "S" : "";
            return $"{WallSpritePrefix}_{(name.Length == 0 ? "Alone" : name)}.png";
        }

        /// <summary>
        /// A wall's placeholder on one cell, for the walls beside it (<see cref="WallShapes"/>): a white block, its
        /// sides shaded, outlined except where it meets a neighbour. A straight section runs across the whole cell between
        /// its two neighbours, <see cref="StraightWallThickness"/> thick, 2 cells high; a tower is a scaled block 3
        /// cells high, joined to each neighbour by a section as thick and high as a straight one, from the tower to the
        /// edge of the cell.
        /// </summary>
        private static void CreateWallSprite(string path, WallSides sides)
        {
            if (File.Exists(path))
            {
                return;
            }

            const int halfCellWidth = PixelsPerUnit / 2;
            const int quarterCellWidth = PixelsPerUnit / 4;
            const int width = PixelsPerUnit;
            const int groundHeight = PixelsPerUnit / 2;
            int straightHeight = Mathf.RoundToInt(StraightWallCells * CellHeightPixels * WallScale);
            int towerHeight = Mathf.RoundToInt(TowerCells * CellHeightPixels * WallScale);
            int height = groundHeight + towerHeight;

            // The gap between a scaled block and the edge of its cell, in cells; the block lies between near and far.
            const float near = (1f - WallScale) * 0.5f;
            const float far = 1f - near;

            // A straight section or a join lies between these across its length.
            const float thinNear = (1f - StraightWallThickness) * 0.5f;
            const float thinFar = 1f - thinNear;

            // Back to front: what lies farther on x or y is drawn first, the nearer blocks over it.
            var blocks = new List<(Rect ground, int heightPixels)>();

            if (WallShapes.IsStraight(sides) && (sides & WallSides.East) != 0)
            {
                blocks.Add((Rect.MinMaxRect(0f, thinNear, 1f, thinFar), straightHeight));
            }
            else if (WallShapes.IsStraight(sides))
            {
                blocks.Add((Rect.MinMaxRect(thinNear, 0f, thinFar, 1f), straightHeight));
            }
            else
            {
                if ((sides & WallSides.East) != 0)
                {
                    blocks.Add((Rect.MinMaxRect(far, thinNear, 1f, thinFar), straightHeight));
                }

                if ((sides & WallSides.North) != 0)
                {
                    blocks.Add((Rect.MinMaxRect(thinNear, far, thinFar, 1f), straightHeight));
                }

                blocks.Add((Rect.MinMaxRect(near, near, far, far), towerHeight));

                if ((sides & WallSides.West) != 0)
                {
                    blocks.Add((Rect.MinMaxRect(0f, thinNear, near, thinFar), straightHeight));
                }

                if ((sides & WallSides.South) != 0)
                {
                    blocks.Add((Rect.MinMaxRect(thinNear, 0f, thinFar, near), straightHeight));
                }
            }

            // A point of the ground, in cells, on the texture: x goes to the upper right, y to the upper left.
            Vector2 At(float x, float y)
            {
                return new Vector2((x - y + 1f) * halfCellWidth, (x + y) * quarterCellWidth);
            }

            // Only a block's side reaching the edge of the cell meets a neighbour: there, its faces go on.
            Rect Continued(Rect ground)
            {
                return Rect.MinMaxRect(
                    ground.xMin <= 0f ? -WallOverhang : ground.xMin, ground.yMin <= 0f ? -WallOverhang : ground.yMin,
                    ground.xMax >= 1f ? 1f + WallOverhang : ground.xMax, ground.yMax >= 1f ? 1f + WallOverhang : ground.yMax);
            }

            var shown = new int[width, height];
            var faces = new int[width, height];

            foreach ((Rect ground, int heightPixels) in blocks)
            {
                DrawWallBlock(shown, ground, heightPixels, At);
                DrawWallBlock(faces, Continued(ground), heightPixels, At);
            }

            // The faces going on are outlined as one with the neighbours', but drawn only over the cell's own blocks.
            var fill = new[] { Color.clear, Color.white, new Color(0.78f, 0.78f, 0.78f, 1f), new Color(0.62f, 0.62f, 0.62f, 1f) };
            WriteSprite(path, faces, fill, new Color(0.35f, 0.35f, 0.35f, 1f), groundHeight, shown);
        }

        // Draws a block over the faces drawn so far, hiding what it covers of them: blocks go back to front.
        private static void DrawWallBlock(int[,] faces, Rect ground, int heightPixels, System.Func<float, float, Vector2> at)
        {
            const int empty = 0;
            const int topFace = 1;
            const int leftFace = 2;
            const int rightFace = 3;
            var up = new Vector2(0f, heightPixels);
            Vector2 farCorner = at(ground.xMax, ground.yMax);
            Vector2 eastCorner = at(ground.xMax, ground.yMin);
            Vector2 nearCorner = at(ground.xMin, ground.yMin);
            Vector2 northCorner = at(ground.xMin, ground.yMax);

            for (int y = 0; y < faces.GetLength(1); y++)
            {
                for (int x = 0; x < faces.GetLength(0); x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    int face = IsInside(point, farCorner + up, eastCorner + up, nearCorner + up, northCorner + up) ? topFace
                        : IsInside(point, northCorner + up, northCorner, nearCorner, nearCorner + up) ? leftFace
                        : IsInside(point, nearCorner + up, nearCorner, eastCorner, eastCorner + up) ? rightFace
                        : empty;

                    if (face != empty)
                    {
                        faces[x, y] = face;
                    }
                }
            }
        }

        private static bool IsInEllipse(Vector2 point, Vector2 centre, float halfWidth, float halfHeight)
        {
            float dx = (point.x - centre.x) / halfWidth;
            float dy = (point.y - centre.y) / halfHeight;
            return dx * dx + dy * dy <= 1f;
        }

        // Writes the faces, each in its fill and outlined, and imports the texture as a sprite whose pivot is the
        // centre of the footprint on the ground. With shown, only the pixels where it has a face are drawn.
        private static void WriteSprite(string path, int[,] faces, Color[] fill, Color outline, int groundHeight, int[,] shown = null)
        {
            const int empty = 0;
            int width = faces.GetLength(0);
            int height = faces.GetLength(1);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int face = faces[x, y];
                    bool isEdge = face != empty
                        && (FaceAt(faces, x - 1, y) != face || FaceAt(faces, x + 1, y) != face
                            || FaceAt(faces, x, y - 1) != face || FaceAt(faces, x, y + 1) != face);
                    bool isShown = shown == null || shown[x, y] != empty;
                    texture.SetPixel(x, y, !isShown ? Color.clear : isEdge ? outline : fill[face]);
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

            var harbourView = WorldMapSetup.FindInScene<HarbourView>(scene);

            if (harbourView == null)
            {
                GameObject harbourObject = WorldMapSetup.CreateInGroup(scene, WorldMapSetup.WorldGroup, "City Harbour");
                harbourObject.layer = layer;
                harbourView = harbourObject.AddComponent<HarbourView>();
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
            changed |= WorldMapSetup.FillReference(controller, "harbourView", harbourView);
            changed |= WorldMapSetup.FillReference(harbourView, "gridView", gridView);
            changed |= WorldMapSetup.FillReference(harbourView, "spriteMaterial", AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
            changed |= WorldMapSetup.FillReference(harbourView, "shipSprite", AssetDatabase.LoadAssetAtPath<Sprite>(ShipSpritePath));
            changed |= WorldMapSetup.FillReference(placement, "input", input);
            changed |= WorldMapSetup.FillReference(placement, "viewCamera", viewCamera);
            changed |= WorldMapSetup.FillReference(placement, "gridView", gridView);
            changed |= WorldMapSetup.FillReference(gridView, "spriteMaterial", AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
            changed |= WorldMapSetup.FillReference(gridView, "cellSprite", AssetDatabase.LoadAssetAtPath<Sprite>(CellSpritePath));
            changed |= WorldMapSetup.FillReference(gridView, "block2x1Sprite", AssetDatabase.LoadAssetAtPath<Sprite>(Block2x1SpritePath));
            changed |= WorldMapSetup.FillReference(gridView, "block1x2Sprite", AssetDatabase.LoadAssetAtPath<Sprite>(Block1x2SpritePath));
            changed |= WorldMapSetup.FillReference(gridView, "block2x2Sprite", AssetDatabase.LoadAssetAtPath<Sprite>(Block2x2SpritePath));
            changed |= WorldMapSetup.FillReference(gridView, "block3x2Sprite", AssetDatabase.LoadAssetAtPath<Sprite>(Block3x2SpritePath));
            changed |= WorldMapSetup.FillReference(gridView, "block2x3Sprite", AssetDatabase.LoadAssetAtPath<Sprite>(Block2x3SpritePath));
            changed |= WorldMapSetup.FillReference(gridView, "portBackSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PortBackSpritePath));
            changed |= WorldMapSetup.FillReference(gridView, "portFrontSprite", AssetDatabase.LoadAssetAtPath<Sprite>(PortFrontSpritePath));
            changed |= FillWallSprites(gridView);
            return changed;
        }

        /// <returns>True when the list of wall sprites was short or a missing one is now set.</returns>
        private static bool FillWallSprites(CityGridView gridView)
        {
            var serialized = new SerializedObject(gridView);
            SerializedProperty sprites = serialized.FindProperty("wallSprites");
            bool changed = false;

            if (sprites.arraySize < WallShapes.Count)
            {
                sprites.arraySize = WallShapes.Count;
                changed = true;
            }

            for (int sides = 0; sides < WallShapes.Count; sides++)
            {
                SerializedProperty entry = sprites.GetArrayElementAtIndex(sides);

                if (entry.objectReferenceValue == null)
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WallSpritePathOf((WallSides)sides));
                    entry.objectReferenceValue = sprite;
                    changed |= sprite != null;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }
    }
}
