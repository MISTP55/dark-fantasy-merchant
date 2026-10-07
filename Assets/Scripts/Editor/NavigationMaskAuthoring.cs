using System;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>Operations of the navigation mask tool on assets.</summary>
    public static class NavigationMaskAuthoring
    {
        public const string MaskAssetPath = "Assets/Data/WorldMap/NavigationMask.asset";
        public const int DefaultWidth = 1024;
        public const int MinWidth = 64;
        public const int MaxWidth = 4096;

        /// <summary>Height that keeps the cells of a grid of this width square on the map.</summary>
        public static int HeightFor(int width, float aspectRatio)
        {
            return Mathf.Clamp(Mathf.RoundToInt(width / aspectRatio), 1, NavigationGrid.MaxSize);
        }

        /// <returns>False when the map is null or has no usable sprite.</returns>
        public static bool TryGetAspectRatio(WorldMapDefinition map, out float aspectRatio)
        {
            aspectRatio = 1f;

            if (map == null || map.MapSprite == null)
            {
                return false;
            }

            Rect rect = map.MapSprite.rect;

            if (rect.width <= 0f || rect.height <= 0f)
            {
                return false;
            }

            aspectRatio = rect.width / rect.height;
            return true;
        }

        /// <summary>False after the map image was replaced by one of another shape.</summary>
        public static bool MatchesAspect(int width, int height, float aspectRatio)
        {
            return height == HeightFor(width, aspectRatio);
        }

        /// <summary>Writes a grid into a mask asset as one undo step.</summary>
        public static void Apply(NavigationMaskDefinition mask, NavigationGrid grid, string undoName)
        {
            // The whole object, not Undo.RecordObject: that one stores a change per array
            // element, and undoing a filled sea then takes most of a minute.
            Undo.RegisterCompleteObjectUndo(mask, undoName);
            mask.SetGrid(grid);
            EditorUtility.SetDirty(mask);
        }

        /// <summary>
        /// Gives a map an empty mask of the given width, stored at <paramref name="assetPath"/>,
        /// whose folder must exist. An asset already at that path is assigned as it is,
        /// never replaced: it may hold hours of painting.
        /// </summary>
        /// <returns>The mask, or null when the map has no sprite to size it from.</returns>
        public static NavigationMaskDefinition CreateMask(WorldMapDefinition map, string assetPath, int width)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (!TryGetAspectRatio(map, out float aspectRatio))
            {
                return null;
            }

            var mask = AssetDatabase.LoadAssetAtPath<NavigationMaskDefinition>(assetPath);

            if (mask == null)
            {
                mask = ScriptableObject.CreateInstance<NavigationMaskDefinition>();
                mask.SetGrid(new NavigationGrid(width, HeightFor(width, aspectRatio)));
                AssetDatabase.CreateAsset(mask, assetPath);
            }

            // SerializedObject records undo and marks the map dirty.
            var serializedMap = new SerializedObject(map);
            serializedMap.FindProperty("navigationMask").objectReferenceValue = mask;
            serializedMap.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return mask;
        }

        /// <summary>
        /// Builds a grid in which a cell is navigable when the map image is water-coloured
        /// there. Reads the sprite through a render texture, so the texture does not have
        /// to be readable.
        /// </summary>
        public static NavigationGrid Detect(Sprite sprite, int width, int height, float maxSaturation)
        {
            if (sprite == null)
            {
                throw new ArgumentNullException(nameof(sprite));
            }

            Texture2D source = sprite.texture;
            Rect rect = sprite.rect;
            var scale = new Vector2(rect.width / source.width, rect.height / source.height);
            var offset = new Vector2(rect.x / source.width, rect.y / source.height);

            // sRGB on both sides, so the bytes read back are the colours of the image and
            // the threshold means what it means in an image editor.
            RenderTexture target = RenderTexture.GetTemporary(
                width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false);

            try
            {
                Graphics.Blit(source, target, scale, offset);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);

                // Row 0 is the bottom row, like the grid.
                Color32[] pixels = readback.GetPixels32();
                var grid = new NavigationGrid(width, height);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (WaterColorClassifier.IsWater(pixels[y * width + x], maxSaturation))
                        {
                            grid.SetNavigable(x, y, true);
                        }
                    }
                }

                return grid;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(readback);
            }
        }
    }
}
