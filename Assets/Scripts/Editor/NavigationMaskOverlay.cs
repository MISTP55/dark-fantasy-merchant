using DarkFantasyMerchant.Core;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// Scene view panel of <see cref="NavigationMaskTool"/>: mode, brush, detection and
    /// grid size. Shown only while that tool is active.
    /// </summary>
    [Overlay(typeof(SceneView), OverlayId, "Navigation Mask")]
    public sealed class NavigationMaskOverlay : Overlay, ITransientOverlay
    {
        private const string OverlayId = "dark-fantasy-merchant-navigation-mask";
        private const long RefreshIntervalMilliseconds = 200;

        private Label message;
        private Button createButton;
        private VisualElement editing;
        private EnumField modeField;
        private SliderInt brushSizeSlider;
        private Slider opacitySlider;
        private Slider saturationSlider;
        private Label gridLabel;
        private IntegerField widthField;
        private HelpBox aspectWarning;
        private int shownWidth;

        public bool visible => ToolManager.activeToolType == typeof(NavigationMaskTool);

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();
            root.style.minWidth = 280f;
            root.style.paddingLeft = 6f;
            root.style.paddingRight = 6f;
            root.style.paddingTop = 4f;
            root.style.paddingBottom = 6f;

            message = new Label("No world map found. Open the WorldMap scene or select a World Map asset.");
            message.style.whiteSpace = WhiteSpace.Normal;
            root.Add(message);

            createButton = new Button(CreateMask) { text = "Create mask" };
            root.Add(createButton);

            editing = new VisualElement();
            root.Add(editing);

            modeField = new EnumField("Mode", NavigationMaskToolSettings.Mode);
            modeField.RegisterValueChangedCallback(
                change => NavigationMaskToolSettings.Mode = (NavigationMaskToolMode)change.newValue);
            editing.Add(modeField);

            brushSizeSlider = new SliderInt(
                "Brush size", NavigationMaskToolSettings.MinBrushSize, NavigationMaskToolSettings.MaxBrushSize)
            {
                showInputField = true,
                tooltip = "Diameter in cells. Shortcuts: [ and ].",
            };
            brushSizeSlider.RegisterValueChangedCallback(change =>
            {
                NavigationMaskToolSettings.BrushSize = change.newValue;
                SceneView.RepaintAll();
            });
            editing.Add(brushSizeSlider);

            opacitySlider = new Slider("Opacity", 0f, 1f);
            opacitySlider.RegisterValueChangedCallback(change =>
            {
                NavigationMaskToolSettings.Opacity = change.newValue;
                SceneView.RepaintAll();
            });
            editing.Add(opacitySlider);

            editing.Add(CreateHeader("Detection"));

            saturationSlider = new Slider("Max saturation", 0f, 1f)
            {
                showInputField = true,
                tooltip = "Pixels of the map image with at most this saturation are water.",
            };
            saturationSlider.RegisterValueChangedCallback(
                change => NavigationMaskToolSettings.MaxSaturation = change.newValue);
            editing.Add(saturationSlider);
            editing.Add(new Button(Detect) { text = "Detect from map" });

            editing.Add(CreateHeader("Grid"));

            gridLabel = new Label();
            editing.Add(gridLabel);

            widthField = new IntegerField("Width")
            {
                tooltip = $"{NavigationMaskAuthoring.MinWidth} to {NavigationMaskAuthoring.MaxWidth} cells. "
                    + "The height follows the map.",
            };
            editing.Add(widthField);
            editing.Add(new Button(Resize) { text = "Resize" });

            aspectWarning = new HelpBox(
                "The grid no longer has the shape of the map image. Resize it to fit.",
                HelpBoxMessageType.Warning);
            editing.Add(aspectWarning);

            var clearButton = new Button(Clear) { text = "Clear" };
            clearButton.style.marginTop = 8f;
            editing.Add(clearButton);

            // The tool, its keyboard shortcuts and undo all change what is shown here,
            // and none of them knows about the panel.
            shownWidth = 0;
            root.schedule.Execute(Refresh).Every(RefreshIntervalMilliseconds);
            Refresh();
            return root;
        }

        private static Label CreateHeader(string text)
        {
            var header = new Label(text);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 8f;
            return header;
        }

        private static void SetShown(VisualElement element, bool shown)
        {
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Refresh()
        {
            NavigationMaskSession session = NavigationMaskTool.ActiveSession;
            bool hasMap = session != null && session.Map != null;
            bool hasMask = hasMap && session.HasMask;

            SetShown(message, !hasMap);
            SetShown(createButton, hasMap && !hasMask);
            SetShown(editing, hasMask);

            if (!hasMask)
            {
                shownWidth = 0;
                return;
            }

            modeField.SetValueWithoutNotify(NavigationMaskToolSettings.Mode);
            brushSizeSlider.SetValueWithoutNotify(NavigationMaskToolSettings.BrushSize);
            opacitySlider.SetValueWithoutNotify(NavigationMaskToolSettings.Opacity);
            saturationSlider.SetValueWithoutNotify(NavigationMaskToolSettings.MaxSaturation);

            NavigationGrid grid = session.Grid;
            gridLabel.text = $"{grid.Width} x {grid.Height} cells";

            // Only when the grid itself changes, so a width being typed is not overwritten.
            if (shownWidth != grid.Width)
            {
                shownWidth = grid.Width;
                widthField.SetValueWithoutNotify(grid.Width);
            }

            bool fitsMap = !NavigationMaskAuthoring.TryGetAspectRatio(session.Map, out float aspectRatio)
                || NavigationMaskAuthoring.MatchesAspect(grid.Width, grid.Height, aspectRatio);
            SetShown(aspectWarning, !fitsMap);
        }

        private static bool TryGetEditableSession(out NavigationMaskSession session)
        {
            session = NavigationMaskTool.ActiveSession;

            return session != null && session.Map != null && session.HasMask;
        }

        private void CreateMask()
        {
            NavigationMaskSession session = NavigationMaskTool.ActiveSession;

            if (session == null || session.Map == null)
            {
                return;
            }

            if (NavigationMaskAuthoring.CreateMask(
                    session.Map, NavigationMaskAuthoring.MaskAssetPath, NavigationMaskAuthoring.DefaultWidth) == null)
            {
                Debug.LogError(
                    $"Cannot create a navigation mask: world map '{session.Map.name}' has no map sprite.",
                    session.Map);
            }

            // The tool picks the new mask up on its next Scene view event.
            SceneView.RepaintAll();
            Refresh();
        }

        private void Detect()
        {
            if (!TryGetEditableSession(out NavigationMaskSession session) || session.Map.MapSprite == null)
            {
                return;
            }

            if (session.HasNavigableCells
                && !EditorUtility.DisplayDialog(
                    "Detect from map",
                    "This replaces the whole navigation mask with one detected from the map image. "
                        + "It can be undone.",
                    "Replace",
                    "Cancel"))
            {
                return;
            }

            NavigationGrid detected = NavigationMaskAuthoring.Detect(
                session.Map.MapSprite,
                session.Grid.Width,
                session.Grid.Height,
                NavigationMaskToolSettings.MaxSaturation);

            session.Replace(detected, "Detect Navigation Mask");
            SceneView.RepaintAll();
        }

        private void Resize()
        {
            if (!TryGetEditableSession(out NavigationMaskSession session)
                || !NavigationMaskAuthoring.TryGetAspectRatio(session.Map, out float aspectRatio))
            {
                return;
            }

            int width = Mathf.Clamp(
                widthField.value, NavigationMaskAuthoring.MinWidth, NavigationMaskAuthoring.MaxWidth);
            int height = NavigationMaskAuthoring.HeightFor(width, aspectRatio);

            // Show the width actually used when the typed one was out of range.
            widthField.SetValueWithoutNotify(width);

            if (width == session.Grid.Width && height == session.Grid.Height)
            {
                return;
            }

            session.Replace(session.Grid.Resampled(width, height), "Resize Navigation Mask");
            SceneView.RepaintAll();
            Refresh();
        }

        private void Clear()
        {
            if (!TryGetEditableSession(out NavigationMaskSession session) || !session.HasNavigableCells)
            {
                return;
            }

            session.Replace(
                new NavigationGrid(session.Grid.Width, session.Grid.Height), "Clear Navigation Mask");
            SceneView.RepaintAll();
        }
    }
}
