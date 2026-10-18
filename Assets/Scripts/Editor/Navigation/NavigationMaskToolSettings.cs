using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Editor
{
    public enum NavigationMaskToolMode
    {
        Brush,
        Eraser,
        Fill,
    }

    /// <summary>
    /// Settings of the navigation mask tool. They are preferences of this Editor, not
    /// part of the mask asset.
    /// </summary>
    public static class NavigationMaskToolSettings
    {
        public const int MinBrushSize = 1;
        public const int MaxBrushSize = 64;

        private const string Prefix = "DarkFantasyMerchant.NavigationMask.";
        private const string ModeKey = Prefix + "Mode";
        private const string BrushSizeKey = Prefix + "BrushSize";
        private const string OpacityKey = Prefix + "Opacity";
        private const string MaxSaturationKey = Prefix + "MaxSaturation";

        public static NavigationMaskToolMode Mode
        {
            get => (NavigationMaskToolMode)Mathf.Clamp(EditorPrefs.GetInt(ModeKey, 0), 0, 2);
            set => EditorPrefs.SetInt(ModeKey, (int)value);
        }

        /// <summary>Diameter of the brush, in cells.</summary>
        public static int BrushSize
        {
            get => Mathf.Clamp(EditorPrefs.GetInt(BrushSizeKey, 8), MinBrushSize, MaxBrushSize);
            set => EditorPrefs.SetInt(BrushSizeKey, Mathf.Clamp(value, MinBrushSize, MaxBrushSize));
        }

        /// <summary>Opacity of the mask drawn over the map, 0 to 1.</summary>
        public static float Opacity
        {
            get => Mathf.Clamp01(EditorPrefs.GetFloat(OpacityKey, 0.5f));
            set => EditorPrefs.SetFloat(OpacityKey, Mathf.Clamp01(value));
        }

        /// <summary>Highest saturation the detection still counts as water, 0 to 1.</summary>
        public static float MaxSaturation
        {
            get => Mathf.Clamp01(EditorPrefs.GetFloat(MaxSaturationKey, 0.2f));
            set => EditorPrefs.SetFloat(MaxSaturationKey, Mathf.Clamp01(value));
        }
    }
}
