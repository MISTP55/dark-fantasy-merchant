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
    /// The Road tool traces an L from the pressed cell, as the city view does, laid or removed on release.
    /// </summary>
    public sealed class CityLayoutWindow : EditorWindow
    {
        private enum Tool
        {
            Water,
            Wall,
            RichHouse,
            WealthyHouse,
            PoorHouse,
            TownHall,
            Port,
            Berth,
            Road,
            Erase,
        }

        private static readonly string[] ToolLabels = { "Water", "Wall", "Rich house", "Wealthy house", "Poor house", "Town hall", "Port", "Berth", "Road", "Erase" };

        private static readonly Color FreeCell = new Color32(0x3B, 0x4A, 0x3A, 0xFF);
        private static readonly Color ForbiddenCell = new Color32(0x24, 0x43, 0x5E, 0xFF);
        private static readonly Color ValidGhost = new Color32(0x6F, 0xBF, 0x73, 0x99);
        private static readonly Color InvalidGhost = new Color32(0xD0, 0x50, 0x50, 0x99);
        private static readonly Color ValidWaterGhost = new Color32(0x4F, 0x9F, 0xC8, 0x99);
        private static readonly Color InvalidWaterGhost = new Color32(0x90, 0x30, 0x60, 0x99);
        private static readonly Color TownHallColor = new Color32(0xC9, 0xA2, 0x27, 0xFF);
        private static readonly Color RoadColor = new Color32(0x8A, 0x7B, 0x62, 0xFF);
        private static readonly Color WallColor = new Color32(0x3A, 0x3A, 0x3A, 0xFF);
        private static readonly Color PortColor = new Color32(0x7A, 0x4E, 0x2E, 0xFF);
        private static readonly Color PortWaterColor = new Color32(0xB0, 0x8A, 0x5C, 0xFF);
        private static readonly Color BerthColor = new Color32(0xE8, 0xD0, 0x5A, 0xFF);
        private static readonly Color ShortageColor = new Color32(0xD0, 0x50, 0x50, 0xFF);
        private static readonly Color GridLine = new Color(0f, 0f, 0f, 0.35f);

        private static readonly int TraceHint = "CityLayoutRoadTrace".GetHashCode();

        private const float MinCell = 4f;
        private const float MaxCell = 24f;
        private const int MaxBrush = 15;

        // Room above the grid for the fields, the tools and the rooms lines.
        private const float HeaderHeight = 310f;

        private CityDefinition city;
        private EconomyDefinition economy;
        private BuildingsDefinition buildings;
        private Tool tool = Tool.Water;
        private BuildingOrientation orientation = BuildingOrientation.Along;
        private PortFacing portFacing = PortFacing.North;
        private int brushSize = 1;
        private bool roundBrush;
        private int widthField;
        private int heightField;
        private CityLayoutDefinition shownLayout;
        private Vector2 scroll;
        private string lastRegenerationProblem;

        // The Road tool's trace, as in the city view: an L from the pressed cell to the one under the pointer, laid
        // (left button) or removed (right button) in one edit on release.
        private bool tracing;
        private int traceButton;
        private int traceControl;
        private bool traceErases;
        private Vector2Int traceStart;
        private Vector2Int traceEnd;
        private readonly List<Vector2Int> traceCells = new List<Vector2Int>();

        // Every city asset, in the alphabetical order of their names, and those names for the drop-down.
        private readonly List<CityDefinition> cities = new List<CityDefinition>();
        private string[] cityNames = new string[0];

        [MenuItem("Tools/Dark Fantasy Merchant/City Layout")]
        public static void Open()
        {
            GetWindow<CityLayoutWindow>("City Layout");
        }

        private void OnEnable()
        {
            economy = FindAsset<EconomyDefinition>();
            buildings = FindAsset<BuildingsDefinition>();
            wantsMouseMove = true;
            FindCities();
            OnSelectionChange();
        }

        private void OnProjectChange()
        {
            FindCities();
            Repaint();
        }

        private void FindCities()
        {
            cities.Clear();

            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(CityDefinition)))
            {
                var found = AssetDatabase.LoadAssetAtPath<CityDefinition>(AssetDatabase.GUIDToAssetPath(guid));

                if (found != null)
                {
                    cities.Add(found);
                }
            }

            NameOrder.Sort(cities, NameOf);
            cityNames = cities.ConvertAll(NameOf).ToArray();
        }

        private static string NameOf(CityDefinition definition)
        {
            return string.IsNullOrEmpty(definition.DisplayName) ? definition.name : definition.DisplayName;
        }

        private void OnLostFocus()
        {
            CancelTrace();
            Repaint();
        }

        private void OnDisable()
        {
            CancelTrace();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is CityDefinition selected)
            {
                if (selected != city)
                {
                    CancelTrace();
                }

                city = selected;
                Repaint();
            }
        }

        private void OnGUI()
        {
            int chosen = EditorGUILayout.Popup("City", cities.IndexOf(city), cityNames);

            if (chosen >= 0 && chosen < cities.Count)
            {
                city = cities[chosen];
            }

            if (city == null)
            {
                CancelTrace();
                EditorGUILayout.HelpBox(cities.Count > 0 ? "Choose a city." : "There is no city asset.", MessageType.Info);
                return;
            }

            if (economy == null || buildings == null)
            {
                CancelTrace();
                EditorGUILayout.HelpBox("The economy or the buildings asset is missing: build the scene first.", MessageType.Warning);
                return;
            }

            // The tools read the footprints of the catalogue's town hall, port and houses.
            if (!buildings.TryValidate(out string buildingsProblem))
            {
                CancelTrace();
                EditorGUILayout.HelpBox($"The buildings asset cannot be used: {buildingsProblem}.", MessageType.Warning);
                return;
            }

            CityLayoutDefinition layout = city.Layout;

            if (layout == null)
            {
                CancelTrace();
                EditorGUILayout.HelpBox("This city has no layout.", MessageType.Info);

                if (GUILayout.Button("Create layout"))
                {
                    WorldMapSetup.TryCreateLayout(city, economy, buildings, MaskOfMap(), IndexInMap(city));
                    AssetDatabase.SaveAssets();
                }

                return;
            }

            if (layout != shownLayout)
            {
                CancelTrace();
                shownLayout = layout;
                lastRegenerationProblem = null;
                widthField = layout.Width;
                heightField = layout.Height;
            }

            DrawSizeFields(layout);
            DrawTools();
            DrawRegenerate(layout);

            if (!layout.HasTownHall)
            {
                EditorGUILayout.HelpBox("The layout has no town hall: place one with the Town hall tool, or regenerate the layout.", MessageType.Warning);
            }

            if (!layout.HasPort)
            {
                EditorGUILayout.HelpBox("The layout has no port: paint water, then place one with the Port tool, or regenerate the layout.", MessageType.Warning);
            }

            bool madeGrid = CityLayoutEditing.TryMakeGrid(layout, buildings, out CityGrid grid);

            if (madeGrid && layout.HasPort && !HarbourPaths.TryValidate(grid, out string harbourProblem))
            {
                EditorGUILayout.HelpBox($"Ships cannot use this layout: {harbourProblem}. Place berths with the Berth tool, or regenerate the layout.", MessageType.Warning);
            }

            if (madeGrid && layout.HasTownHall && !grid.TryValidateWalls(out string wallProblem))
            {
                EditorGUILayout.HelpBox($"The walls do not hold the city: {wallProblem}. Paint walls with the Wall tool, or regenerate the layout.", MessageType.Warning);
            }

            if (!madeGrid)
            {
                CancelTrace();
                EditorGUILayout.HelpBox("Something in the layout cannot stand: a town hall, a port, a wall, a road or a house is outside the grid, on a wrong cell or on another building.", MessageType.Error);

                // A building asset changed after the layout was made can leave the town hall or the port standing
                // wrong, and the grid is not drawn to remove them with their tool.
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (layout.HasTownHall && GUILayout.Button("Remove town hall"))
                    {
                        CityLayoutEditing.RemoveTownHall(layout);
                    }

                    if (layout.HasPort && GUILayout.Button("Remove port"))
                    {
                        CityLayoutEditing.RemovePort(layout);
                    }
                }

                return;
            }

            EditorGUILayout.LabelField($"Berths: {layout.Berths.Count}");
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

            EditorGUILayout.LabelField("Left click paints, right click erases only what the selected tool paints. Road: drag an L.", EditorStyles.miniLabel);

            if (tool == Tool.Port)
            {
                EditorGUILayout.LabelField($"Port facing: {portFacing} (R turns it a quarter turn; its water rows are on that side)");
            }
            else
            {
                bool across = GUILayout.Toggle(orientation == BuildingOrientation.Across, "Across (R turns the house or the town hall)");
                orientation = across ? BuildingOrientation.Across : BuildingOrientation.Along;
            }

            Event current = Event.current;

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.R)
            {
                if (tool == Tool.Port)
                {
                    portFacing = PortFacings.Turned(portFacing);
                }
                else
                {
                    orientation = orientation == BuildingOrientation.Along ? BuildingOrientation.Across : BuildingOrientation.Along;
                }

                current.Use();
                Repaint();
            }
        }

        private void DrawRegenerate(CityLayoutDefinition layout)
        {
            if (GUILayout.Button("Regenerate layout (water, port, town hall, houses, roads)")
                && EditorUtility.DisplayDialog(
                    "Regenerate layout",
                    $"Everything on {city.name}'s grid is replaced: water on the side of the sea, the port, the town hall, the starting houses and their roads. Undo brings it back.",
                    "Regenerate", "Cancel"))
            {
                CancelTrace();
                CityLayoutEditing.Regenerate(layout, city, economy, buildings, MaskOfMap(), IndexInMap(city), out lastRegenerationProblem);

                // This pass was laid out for the old content.
                GUIUtility.ExitGUI();
            }

            if (lastRegenerationProblem != null)
            {
                EditorGUILayout.HelpBox($"The layout was not regenerated: {lastRegenerationProblem}.", MessageType.Warning);
            }
        }

        private void DrawRooms(CityGrid grid)
        {
            PerClass<int> roomPerHouse = buildings.HouseRooms;
            PerClass<int> needed = economy.StartingRoomOf(city, roomPerHouse);

            int unconnected = 0;

            foreach (PlacedBuilding building in grid.Buildings)
            {
                if (building.Kind.IsHouse && !building.IsConnected)
                {
                    unconnected++;
                }
            }

            var unconnectedStyle = new GUIStyle(EditorStyles.label);

            if (unconnected > 0)
            {
                unconnectedStyle.normal.textColor = ShortageColor;
            }

            EditorGUILayout.LabelField($"Starting houses not connected to the town hall: {unconnected}", unconnectedStyle);

            foreach (SocialClass housedClass in new[] { SocialClass.Rich, SocialClass.Wealthy, SocialClass.Poor })
            {
                int perHouse = roomPerHouse[housedClass];
                int placed = grid.ConnectedHousesOf(housedClass) * perHouse;
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

            foreach (Vector2Int berth in grid.Berths)
            {
                EditorGUI.DrawRect(CellRect(berth.x, berth.y), BerthColor);
            }

            foreach (PlacedBuilding building in grid.Buildings)
            {
                Vector2Int origin = building.Place.Origin;
                Vector2Int size = building.Size;
                var rect = new Rect(area.x + origin.x * cell, area.yMax - (origin.y + size.y) * cell, size.x * cell, size.y * cell);

                if (building.Kind.IsRoad)
                {
                    EditorGUI.DrawRect(rect, RoadColor);
                }
                else if (building.Kind.IsTownHall)
                {
                    EditorGUI.DrawRect(rect, TownHallColor);
                }
                else if (building.Kind.IsWall)
                {
                    EditorGUI.DrawRect(rect, WallColor);
                }
                else if (building.Kind.IsPort)
                {
                    var site = new PortSite(origin, building.PortFacing);
                    PortFootprint port = CityLayoutEditing.PortFootprintOf(buildings);

                    for (int y = origin.y; y < origin.y + size.y; y++)
                    {
                        for (int x = origin.x; x < origin.x + size.x; x++)
                        {
                            EditorGUI.DrawRect(CellRect(x, y), port.IsWater(site, new Vector2Int(x, y)) ? PortWaterColor : PortColor);
                        }
                    }
                }
                else
                {
                    EditorGUI.DrawRect(rect, HouseColors.Of(building.Kind.HousedClass));

                    if (!building.IsConnected)
                    {
                        DrawOutline(rect, ShortageColor);
                    }
                }
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
            bool onGrid = inside && hovered.x >= 0 && hovered.y >= 0 && hovered.x < layout.Width && hovered.y < layout.Height;

            if (tool == Tool.Road)
            {
                HandleTrace(layout, onGrid, hovered, current, CellRect);
            }
            else
            {
                CancelTrace();
            }

            if (tool != Tool.Road && onGrid)
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

        private static void DrawOutline(Rect rect, Color color)
        {
            const float thickness = 2f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
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
            else if (tool == Tool.TownHall)
            {
                var place = new GridPlace(hovered, orientation);
                Vector2Int size = CityLayoutEditing.TownHallSize(buildings, place);
                bool valid = CityLayoutEditing.CanPlaceTownHall(layout, buildings, place);
                Rect low = cellRect(hovered.x, hovered.y);
                var rect = new Rect(low.x, low.yMax - size.y * low.height, size.x * low.width, size.y * low.height);
                EditorGUI.DrawRect(rect, valid ? ValidGhost : InvalidGhost);
            }
            else if (tool == Tool.Port)
            {
                // Cell by cell, so that the water rows show which way the port faces.
                var site = new PortSite(hovered, portFacing);
                PortFootprint port = CityLayoutEditing.PortFootprintOf(buildings);
                Vector2Int size = CityLayoutEditing.PortSize(buildings, site);
                bool valid = CityLayoutEditing.CanPlacePort(layout, buildings, site);

                for (int y = hovered.y; y < hovered.y + size.y; y++)
                {
                    for (int x = hovered.x; x < hovered.x + size.x; x++)
                    {
                        bool water = port.IsWater(site, new Vector2Int(x, y));
                        Color color = valid ? (water ? ValidWaterGhost : ValidGhost) : (water ? InvalidWaterGhost : InvalidGhost);
                        EditorGUI.DrawRect(cellRect(x, y), color);
                    }
                }
            }
            else if (tool == Tool.Berth)
            {
                bool valid = CityLayoutEditing.CanPlaceBerth(layout, buildings, hovered);
                EditorGUI.DrawRect(cellRect(hovered.x, hovered.y), valid ? ValidGhost : InvalidGhost);
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
            else if (tool == Tool.TownHall)
            {
                if (erases)
                {
                    if (CityLayoutEditing.IsUnderTownHall(layout, buildings, hovered))
                    {
                        CityLayoutEditing.RemoveTownHall(layout);
                    }
                }
                else if (down)
                {
                    CityLayoutEditing.SetTownHall(layout, buildings, new GridPlace(hovered, orientation));
                }
            }
            else if (tool == Tool.Port)
            {
                if (erases)
                {
                    if (CityLayoutEditing.IsUnderPort(layout, buildings, hovered))
                    {
                        CityLayoutEditing.RemovePort(layout);
                    }
                }
                else if (down)
                {
                    CityLayoutEditing.SetPort(layout, buildings, new PortSite(hovered, portFacing));
                }
            }
            else if (tool == Tool.Berth)
            {
                if (erases)
                {
                    CityLayoutEditing.RemoveBerth(layout, hovered);
                }
                else if (down)
                {
                    CityLayoutEditing.AddBerth(layout, buildings, hovered);
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
                else if (tool == Tool.Wall)
                {
                    CityLayoutEditing.SetWalls(layout, buildings, cells, paints);
                }
                else
                {
                    CityLayoutEditing.SetForbidden(layout, buildings, cells, false);
                    CityLayoutEditing.RemoveHouses(layout, buildings, cells);
                    CityLayoutEditing.SetRoads(layout, buildings, cells, false);
                    CityLayoutEditing.SetWalls(layout, buildings, cells, false);
                }
            }

            current.Use();
            Repaint();
        }

        // The Road tool: the press starts the L, the drag moves its end (kept on the last cell of the grid the pointer
        // was on), the release lays or removes it. The window keeps the mouse while tracing, so a release outside it
        // still ends the trace.
        private void HandleTrace(
            CityLayoutDefinition layout, bool onGrid, Vector2Int hovered, Event current, System.Func<int, int, Rect> cellRect)
        {
            int control = GUIUtility.GetControlID(TraceHint, FocusType.Passive);

            // On Layout the grid's rect is a placeholder: the hovered cell means nothing.
            if (current.type == EventType.Layout)
            {
                return;
            }

            // The mouse was taken from the trace (focus lost, another control): it is dropped, never laid later.
            if (tracing && GUIUtility.hotControl != control)
            {
                CancelTrace();
            }

            EventType type = current.GetTypeForControl(control);
            bool owns = tracing && current.button == traceButton;

            if (onGrid)
            {
                traceEnd = hovered;
            }

            if (!tracing && type == EventType.MouseDown && onGrid && (current.button == 0 || current.button == 1))
            {
                tracing = true;
                traceButton = current.button;
                traceErases = current.button == 1;
                traceStart = hovered;
                traceControl = control;
                GUIUtility.hotControl = control;
                current.Use();
            }
            else if (owns && type == EventType.MouseDrag)
            {
                current.Use();
                Repaint();
            }
            else if (owns && type == EventType.MouseUp)
            {
                CancelTrace();
                RoadTrace.Of(traceStart, traceEnd, traceCells);
                CityLayoutEditing.SetRoads(layout, buildings, traceCells, !traceErases);
                current.Use();
                Repaint();
            }

            if (current.type != EventType.Repaint || (!tracing && !onGrid))
            {
                return;
            }

            RoadTrace.Of(tracing ? traceStart : traceEnd, traceEnd, traceCells);

            foreach (Vector2Int traced in traceCells)
            {
                EditorGUI.DrawRect(cellRect(traced.x, traced.y), tracing && traceErases ? InvalidGhost : ValidGhost);
            }
        }

        // Drops the trace in progress without laying it, and gives the mouse back.
        private void CancelTrace()
        {
            if (tracing && GUIUtility.hotControl == traceControl)
            {
                GUIUtility.hotControl = 0;
            }

            tracing = false;
        }

        private static bool UsesBrush(Tool tool)
        {
            return tool == Tool.Water || tool == Tool.Wall || tool == Tool.Erase;
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
        internal static int IndexInMap(CityDefinition city)
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

        // Where the ships sail on the world map, which tells on which side of a city the sea is; null without one.
        internal static NavigationGrid MaskOfMap()
        {
            WorldMapDefinition map = FindAsset<WorldMapDefinition>();

            return map != null && map.NavigationMask != null ? map.NavigationMask.CreateGrid() : null;
        }

        internal static T FindAsset<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);

            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }
    }
}
