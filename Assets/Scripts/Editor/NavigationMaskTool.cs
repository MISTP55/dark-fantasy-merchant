using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// Scene view tool that paints the navigation mask of the active world map: where
    /// ships can sail. Its settings are in <see cref="NavigationMaskOverlay"/>.
    /// </summary>
    [EditorTool("Navigation Mask")]
    public sealed class NavigationMaskTool : EditorTool
    {
        private const string PaintUndoName = "Paint Navigation Mask";
        private const string FillUndoName = "Fill Navigation Mask";

        private static readonly Color MaskTint = new Color(0.2f, 0.6f, 1f);
        private static readonly Color BrushColor = Color.white;

        private NavigationMaskSession session;
        private GUIContent icon;
        private Vector2 lastPoint;
        private bool strokeNavigable;

        /// <summary>Session of the active tool, for its panel; null when the tool is not in use.</summary>
        public static NavigationMaskSession ActiveSession { get; private set; }

        public override GUIContent toolbarIcon
        {
            get
            {
                if (icon == null)
                {
                    icon = new GUIContent(
                        EditorGUIUtility.IconContent("Grid.PaintTool").image, "Navigation Mask");
                }

                return icon;
            }
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            ReleaseSession();
        }

        public override void OnWillBeDeactivated()
        {
            ReleaseSession();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            // The mask is authored at edit time only.
            if (!(window is SceneView sceneView) || Application.isPlaying)
            {
                return;
            }

            WorldMapView view = WorldMapEditorContext.FindView();
            WorldMapDefinition definition = WorldMapEditorContext.FindDefinition(view);

            // Created here and not on activation: a script reload keeps the tool active
            // but drops the session.
            if (session == null)
            {
                session = new NavigationMaskSession();
            }

            ActiveSession = session;

            if (definition == null || !definition.TryCreateProjection(out MapProjection projection))
            {
                session.SetMap(null);
                DrawMessage("No world map found. Open the WorldMap scene or select a World Map asset.");
                return;
            }

            session.SetMap(definition);

            if (!session.HasMask)
            {
                DrawMessage("This map has no navigation mask. Create one from the Navigation Mask panel.");
                return;
            }

            // The mask is drawn as a screen rectangle, which only lines up with the map
            // when the view looks straight at it.
            if (!sceneView.in2DMode)
            {
                DrawMessage("Switch the Scene view to 2D to paint the navigation mask.");
                return;
            }

            Event current = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            Vector2 world = HandleUtility.GUIPointToWorldRay(current.mousePosition).origin;
            Vector2 point = projection.WorldToNormalized(world);
            NavigationMaskToolMode mode = NavigationMaskToolSettings.Mode;
            float radiusInCells = NavigationMaskToolSettings.BrushSize * 0.5f;

            switch (current.GetTypeForControl(controlId))
            {
                case EventType.Layout:
                    // Keeps a click on empty space from selecting or deselecting objects.
                    HandleUtility.AddDefaultControl(controlId);
                    break;

                case EventType.MouseDown:
                    // Alt and the other buttons stay with Scene view navigation.
                    if (current.button != 0 || current.alt)
                    {
                        break;
                    }

                    if (mode == NavigationMaskToolMode.Fill)
                    {
                        session.Fill(point, !current.shift);
                        session.Commit(FillUndoName);
                    }
                    else
                    {
                        // Shift swaps brush and eraser, for the whole stroke.
                        strokeNavigable = (mode == NavigationMaskToolMode.Brush) != current.shift;
                        lastPoint = point;
                        GUIUtility.hotControl = controlId;
                        session.Paint(point, point, radiusInCells, strokeNavigable);
                    }

                    current.Use();
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        session.Paint(lastPoint, point, radiusInCells, strokeNavigable);
                        lastPoint = point;
                        current.Use();
                    }

                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        session.Commit(PaintUndoName);
                        current.Use();
                    }

                    break;

                case EventType.MouseMove:
                    // The brush outline follows the pointer.
                    sceneView.Repaint();
                    break;

                case EventType.KeyDown:
                    if (current.keyCode == KeyCode.LeftBracket)
                    {
                        NavigationMaskToolSettings.BrushSize--;
                        current.Use();
                    }
                    else if (current.keyCode == KeyCode.RightBracket)
                    {
                        NavigationMaskToolSettings.BrushSize++;
                        current.Use();
                    }

                    break;

                case EventType.Repaint:
                    DrawMask(projection.WorldRect);

                    if (mode != NavigationMaskToolMode.Fill)
                    {
                        DrawBrush(world, radiusInCells * projection.WorldRect.width / session.Grid.Width);
                    }

                    break;
            }
        }

        private void DrawMask(Rect worldRect)
        {
            Texture2D preview = session.GetPreview();

            if (preview == null)
            {
                return;
            }

            Vector2 topLeft = HandleUtility.WorldToGUIPoint(new Vector3(worldRect.xMin, worldRect.yMax, 0f));
            Vector2 bottomRight = HandleUtility.WorldToGUIPoint(new Vector3(worldRect.xMax, worldRect.yMin, 0f));
            Color previousColor = GUI.color;

            Handles.BeginGUI();
            GUI.color = new Color(MaskTint.r, MaskTint.g, MaskTint.b, NavigationMaskToolSettings.Opacity);

            // The texture's bottom row is drawn at the bottom of the rectangle, like row 0
            // of the grid on the map.
            GUI.DrawTexture(
                Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y),
                preview,
                ScaleMode.StretchToFill,
                true);

            GUI.color = previousColor;
            Handles.EndGUI();
        }

        private static void DrawBrush(Vector2 world, float worldRadius)
        {
            Handles.color = BrushColor;
            Handles.DrawWireDisc(world, Vector3.forward, worldRadius);
        }

        private static void DrawMessage(string text)
        {
            Handles.BeginGUI();
            GUI.Label(new Rect(12f, 12f, 520f, 38f), text, EditorStyles.helpBox);
            Handles.EndGUI();
        }

        private void OnUndoRedo()
        {
            if (session == null)
            {
                return;
            }

            // The asset changed under the working copy.
            session.Reload();
            SceneView.RepaintAll();
        }

        private void ReleaseSession()
        {
            if (session == null)
            {
                return;
            }

            // A stroke still in progress when the tool is switched away is kept.
            session.Commit(PaintUndoName);
            session.Dispose();

            if (ActiveSession == session)
            {
                ActiveSession = null;
            }

            session = null;
        }
    }
}
