using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// Draws a draggable handle in the Scene view for each city of the active world map.
    /// Dragging writes the city's normalized map position to its asset, with undo.
    /// </summary>
    [InitializeOnLoad]
    public static class CityPlacementTool
    {
        private static readonly Color HandleColor = new Color(1f, 0.8f, 0.2f);
        private static readonly Color OutlineColor = new Color(1f, 0.8f, 0.2f, 0.5f);

        static CityPlacementTool()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            // Positions are authored at edit time only.
            if (Application.isPlaying)
            {
                return;
            }

            // A city handle under the brush would take the click meant for the mask.
            if (ToolManager.activeToolType == typeof(NavigationMaskTool))
            {
                return;
            }

            WorldMapView view = WorldMapEditorContext.FindView();
            WorldMapDefinition definition = WorldMapEditorContext.FindDefinition(view);

            if (definition == null || !definition.TryCreateProjection(out MapProjection projection))
            {
                return;
            }

            RefreshMapLayout(view);

            DrawMapOutline(projection.WorldRect);

            IReadOnlyList<CityDefinition> cities = definition.Cities;

            for (int i = 0; i < cities.Count; i++)
            {
                if (cities[i] != null)
                {
                    DrawCityHandle(cities[i], projection);
                }
            }
        }

        /// <summary>
        /// Keeps the map sprite in the scene in step with its definition, so cities are
        /// placed against a backdrop that matches the handles after the map image changes.
        /// </summary>
        public static void RefreshMapLayout(WorldMapView view)
        {
            if (view == null || view.IsMapLaidOut())
            {
                return;
            }

            view.LayOutMap();
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        }

        private static void DrawMapOutline(Rect rect)
        {
            Handles.color = OutlineColor;
            Handles.DrawWireCube(rect.center, new Vector3(rect.width, rect.height, 0f));
        }

        private static void DrawCityHandle(CityDefinition city, MapProjection projection)
        {
            Vector3 worldPosition = projection.NormalizedToWorld(city.MapPosition);
            float size = HandleUtility.GetHandleSize(worldPosition) * 0.08f;

            Handles.color = HandleColor;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(worldPosition, size, Vector3.zero, Handles.DotHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                Vector2 normalized = projection.WorldToNormalized(moved);
                normalized.x = Mathf.Clamp01(normalized.x);
                normalized.y = Mathf.Clamp01(normalized.y);

                // SerializedObject records undo and marks the asset dirty.
                var serializedCity = new SerializedObject(city);
                serializedCity.FindProperty("mapPosition").vector2Value = normalized;
                serializedCity.ApplyModifiedProperties();
            }

            Handles.Label(worldPosition + new Vector3(size * 1.5f, size * 1.5f, 0f), city.DisplayName);
        }
    }
}
