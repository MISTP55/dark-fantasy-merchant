using System.Collections.Generic;
using DarkFantasyMerchant.Core;
using DarkFantasyMerchant.Game;
using UnityEditor;
using UnityEngine;

namespace DarkFantasyMerchant.Editor
{
    /// <summary>
    /// Designs the grid of a city: its size, its forbidden cells and its starting houses, seen from
    /// above, with whether the houses shelter the starting population. Edits go through <see cref="CityLayoutEditing"/>.
    /// A left click paints what the tool paints; a right click erases it, and only it.
    /// </summary>
    public sealed class CityLayoutWindow : EditorWindow
    {
        private enum Tool
        {
            Water,
            RichHouse,
            WealthyHouse,
            PoorHouse,
            Erase,
        }

        private static readonly string[] ToolLabels = { "Water", "Rich house", "Wealthy house", "Poor house", "Erase" };

        private static readonly Color FreeCell = new Color32(0x3B, 0x4A, 0x3A, 0xFF);
        private static readonly Color ForbiddenCell = new Color32(0x24, 0x43, 0x5E, 0xFF);
        private static readonly Color ValidGhost = new Color32(0x6F, 0xBF, 0x73, 0x99);
        private static readonly Color InvalidGhost = new Color32(0xD0, 0x50, 0x50, 0x99);
        private static readonly Color ShortageColor = new Color32(0xD0, 0x50, 0x50, 0xFF);
        private static readonly Color GridLine = new Color(0f, 0f, 0f, 0.35f);

        private const float MinCell = 4f;
        private const float MaxCell = 24f;
        private const int MaxBrush = 15;

        // Room above the grid for the fields, the tools and the rooms lines.
        private const float HeaderHeight = 230f;

        private CityDefinition city;
        private EconomyDefinition economy;
        private BuildingsDefinition buildings;
        private Tool tool = Tool.Water;
        private BuildingOrientation orientation = BuildingOrientation.Along;
        private int brushSize = 1;
        private bool roundBrush;
        private int widthField;
        private int heightField;
        private CityLayoutDefinition shownLayout;
        private Vector2 scroll;

        [MenuItem("Tools/Dark Fantasy Merchant/City Layout")]
        public static void Open()
        {
            GetWindow<CityLayoutWindow>("City Layout");
        }

        private static Color ColorOf(SocialClass housedClass)
        {
            switch (housedClass)
            {
                case SocialClass.Rich:
                    return new Color32(0xC9, 0xA2, 0x27, 0xFF);
                case SocialClass.Wealthy:
                    return new Color32(0xB8, 0x6E, 0x9E, 0xFF);
                default:
                    return new Color32(0x9A, 0x7B, 0x5B, 0xFF);
            }
        }

        private void OnEnable()
        {
            economy = FindAsset<EconomyDefinition>();
            buildings = FindAsset<BuildingsDefinition>();
            wantsMouseMove = true;
            OnSelectionChange();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is CityDefinition selected)
            {
                city = selected;
                Repaint();
            }
        }

        private void OnGUI()
        {
            city = (CityDefinition)EditorGUILayout.ObjectField("City", city, typeof(CityDefinition), false);

            if (city == null)
            {
                EditorGUILayout.HelpBox("Select a city.", MessageType.Info);
                return;
            }

            if (economy == null || buildings == null)
            {
                EditorGUILayout.HelpBox("The economy or the buildings asset is missing: build the scene first.", MessageType.Warning);
                return;
            }

            CityLayoutDefinition layout = city.Layout;

            if (layout == null)
            {
                EditorGUILayout.HelpBox("This city has no layout.", MessageType.Info);

                if (GUILayout.Button("Create layout"))
                {
                    WorldMapSetup.TryCreateLayout(city, economy, buildings, IndexInMap(city));
                    AssetDatabase.SaveAssets();
                }

                return;
            }

            if (layout != shownLayout)
            {
                shownLayout = layout;
                widthField = layout.Width;
                heightField = layout.Height;
            }

            DrawSizeFields(layout);
            DrawTools();

            if (!layout.TryCreateGrid(buildings, out CityGrid grid, out string problem))
            {
                EditorGUILayout.HelpBox("The layout cannot make its grid: " + problem, MessageType.Error);
                return;
            }

            DrawRooms(grid);
            DrawGrid(layout, grid);
        }

        private void DrawSizeFields(CityLayoutDefinition layout)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                widthField = EditorGUILayout.IntField("Width", widthField);
                heightField = EditorGUILayout.IntField("Height", heightField);

                if (GUILayout.Button("Resize", GUILayout.Width(70f)))
                {
                    CityLayoutEditing.Resize(layout, buildings, widthField, heightField);
                    widthField = layout.Width;
                    heightField = layout.Height;
                }
            }
        }

        private void DrawTools()
        {
            tool = (Tool)GUILayout.Toolbar((int)tool, ToolLabels);

            using (new EditorGUI.DisabledScope(!UsesBrush(tool)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    brushSize = EditorGUILayout.IntSlider("Brush (Water, Erase)", brushSize, 1, MaxBrush);
                    roundBrush = GUILayout.Toggle(roundBrush, "Round", GUILayout.Width(60f));
                }
            }

            EditorGUILayout.LabelField("Left click paints, right click erases only what the selected tool paints.", EditorStyles.miniLabel);

            bool across = GUILayout.Toggle(orientation == BuildingOrientation.Across, "Across (R turns the house)");
            orientation = across ? BuildingOrientation.Across : BuildingOrientation.Along;

            Event current = Event.current;

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.R)
            {
                orientation = orientation == BuildingOrientation.Along ? BuildingOrientation.Across : BuildingOrientation.Along;
                current.Use();
                Repaint();
            }
        }

        private void DrawRooms(CityGrid grid)
        {
            PerClass<int> roomPerHouse = buildings.HouseRooms;
            PerClass<int> needed = economy.StartingRoomOf(city, roomPerHouse);

            foreach (SocialClass housedClass in new[] { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor })
            {
                int perHouse = roomPerHouse[housedClass];
                int placed = grid.HousesOf(housedClass) * perHouse;
                int need = needed[housedClass];
                int housesNeeded = perHouse > 0 ? need / perHouse : 0;
                bool isShort = placed < need;

                var style = new GUIStyle(EditorStyles.label);

                if (isShort)
                {
                    style.normal.textColor = ShortageColor;
                }

                EditorGUILayout.LabelField($"{housedClass}: rooms {placed} / {need} ({housesNeeded} houses needed)", style);
            }
        }

        private void DrawGrid(CityLayoutDefinition layout, CityGrid grid)
        {
            float available = Mathf.Min(position.width - 30f, position.height - HeaderHeight);
            float cell = Mathf.Clamp(Mathf.Floor(available / Mathf.Max(layout.Width, layout.Height)), MinCell, MaxCell);
            float gridWidth = cell * layout.Width;
            float gridHeight = cell * layout.Height;

            scroll = EditorGUILayout.BeginScrollView(scroll);

            // The grid is centred in the room left to it; it scrolls once larger than that room.
            Rect room = GUILayoutUtility.GetRect(gridWidth, gridHeight, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var area = new Rect(
                room.x + Mathf.Max(0f, Mathf.Floor((room.width - gridWidth) / 2f)),
                room.y + Mathf.Max(0f, Mathf.Floor((room.height - gridHeight) / 2f)),
                gridWidth,
                gridHeight);

            // Top-down view with y up: row 0 is drawn at the bottom.
            Rect CellRect(int x, int y)
            {
                return new Rect(area.x + x * cell, area.yMax - (y + 1) * cell, cell, cell);
            }

            EditorGUI.DrawRect(area, FreeCell);

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    if (grid.IsForbidden(new Vector2Int(x, y)))
                    {
                        EditorGUI.DrawRect(CellRect(x, y), ForbiddenCell);
                    }
                }
            }

            foreach (PlacedBuilding building in grid.Buildings)
            {
                Vector2Int origin = building.Place.Origin;
                Vector2Int size = building.Size;
                var rect = new Rect(area.x + origin.x * cell, area.yMax - (origin.y + size.y) * cell, size.x * cell, size.y * cell);
                EditorGUI.DrawRect(rect, ColorOf(building.Kind.HousedClass));
            }

            for (int x = 0; x <= layout.Width; x++)
            {
                EditorGUI.DrawRect(new Rect(area.x + x * cell, area.y, 1f, gridHeight), GridLine);
            }

            for (int y = 0; y <= layout.Height; y++)
            {
                EditorGUI.DrawRect(new Rect(area.x, area.yMax - y * cell, gridWidth, 1f), GridLine);
            }

            Event current = Event.current;
            bool inside = area.Contains(current.mousePosition);
            var hovered = new Vector2Int(
                Mathf.FloorToInt((current.mousePosition.x - area.x) / cell),
                Mathf.FloorToInt((area.yMax - current.mousePosition.y) / cell));

            if (inside && hovered.x >= 0 && hovered.y >= 0 && hovered.x < layout.Width && hovered.y < layout.Height)
            {
                DrawGhost(layout, grid, hovered, CellRect);
                HandleMouse(layout, hovered, current);
            }

            EditorGUILayout.EndScrollView();

            if (current.type == EventType.MouseMove)
            {
                Repaint();
            }
        }

        private void DrawGhost(CityLayoutDefinition layout, CityGrid grid, Vector2Int hovered, System.Func<int, int, Rect> cellRect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (HouseClassOf(tool, out SocialClass housedClass))
            {
                Footprint footprint = CityLayoutEditing.HouseFootprint(buildings, housedClass);
                Vector2Int size = footprint.SizeFor(orientation);
                bool valid = grid.CanPlace(footprint, new GridPlace(hovered, orientation));
                Rect low = cellRect(hovered.x, hovered.y);
                var rect = new Rect(low.x, low.yMax - size.y * low.height, size.x * low.width, size.y * low.height);
                EditorGUI.DrawRect(rect, valid ? ValidGhost : InvalidGhost);
            }
            else
            {
                foreach (Vector2Int cell in BrushCells(layout, hovered))
                {
                    EditorGUI.DrawRect(cellRect(cell.x, cell.y), ValidGhost);
                }
            }
        }

        private void HandleMouse(CityLayoutDefinition layout, Vector2Int hovered, Event current)
        {
            bool paints = current.button == 0;
            bool erases = current.button == 1;
            bool down = current.type == EventType.MouseDown;
            bool drag = current.type == EventType.MouseDrag;

            if ((!paints && !erases) || (!down && !drag))
            {
                return;
            }

            if (HouseClassOf(tool, out SocialClass housedClass))
            {
                if (erases)
                {
                    CityLayoutEditing.RemoveHouses(layout, buildings, new[] { hovered }, housedClass);
                }
                else if (down)
                {
                    CityLayoutEditing.TryAddHouse(layout, buildings, housedClass, new GridPlace(hovered, orientation));
                }
            }
            else
            {
                List<Vector2Int> cells = BrushCells(layout, hovered);

                if (tool == Tool.Water)
                {
                    // Painting water never frees a cell: only the right click does.
                    CityLayoutEditing.SetForbidden(layout, buildings, cells, paints);
                }
                else
                {
                    CityLayoutEditing.SetForbidden(layout, buildings, cells, false);
                    CityLayoutEditing.RemoveHouses(layout, buildings, cells);
                }
            }

            current.Use();
            Repaint();
        }

        private static bool UsesBrush(Tool tool)
        {
            return tool == Tool.Water || tool == Tool.Erase;
        }

        private List<Vector2Int> BrushCells(CityLayoutDefinition layout, Vector2Int hovered)
        {
            return CityLayoutEditing.BrushCells(hovered, brushSize, roundBrush, layout.Width, layout.Height);
        }

        private static bool HouseClassOf(Tool tool, out SocialClass housedClass)
        {
            switch (tool)
            {
                case Tool.RichHouse:
                    housedClass = SocialClass.Rich;
                    return true;
                case Tool.WealthyHouse:
                    housedClass = SocialClass.Wealthy;
                    return true;
                case Tool.PoorHouse:
                    housedClass = SocialClass.Poor;
                    return true;
                default:
                    housedClass = SocialClass.Poor;
                    return false;
            }
        }

        // The seed of the default layout is the city's index in the map, as in the scene setup.
        private static int IndexInMap(CityDefinition city)
        {
            WorldMapDefinition map = FindAsset<WorldMapDefinition>();

            if (map != null)
            {
                IReadOnlyList<MapCity> cities = map.MapCities;

                for (int i = 0; i < cities.Count; i++)
                {
                    if (cities[i].City == city)
                    {
                        return i;
                    }
                }
            }

            return 0;
        }

        private static T FindAsset<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);

            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }
    }
}
