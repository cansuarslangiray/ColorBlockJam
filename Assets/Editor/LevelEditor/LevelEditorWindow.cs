using System;
using System.Collections.Generic;
using System.IO;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEditor;
using UnityEngine;

namespace Editor.LevelEditor
{
    public class LevelEditorWindow : EditorWindow
    {
        private const float GridCellPixelSize = 26f;
        private const string ShapeJsonFolder = "Assets/Data/BlockShapes";

        private TextAsset _activeLevelJson;
        private string _activeLevelJsonPath;
        private LevelJsonData _activeLevel;
        private LevelEditorMode _editMode;
        private Vector2 _scrollPosition;
        private BlockShapeRegistry _shapeRegistry;

        private BlockColor _selectedDoorColor = BlockColor.Red;
        private int _selectedDoorWidth = 1;
        private BlockColor _selectedBlockColor = BlockColor.Red;
        private BlockMovementConstraint _selectedBlockMovementConstraint = BlockMovementConstraint.Free;
        private BlockShapeJsonData _selectedBlockShape;
        private readonly List<Vector2Int> _doorCellsBuffer = new List<Vector2Int>(8);
       
        private readonly HashSet<Vector2Int> _blockedCellLookup = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, int> _doorIndexByCell = new Dictionary<Vector2Int, int>();
        private readonly Dictionary<Vector2Int, int> _blockIndexByCell = new Dictionary<Vector2Int, int>();
        private readonly List<string> _layoutValidationIssues = new List<string>(16);
        private readonly BoardOccupancyMap _validationOccupancyMap = new BoardOccupancyMap();
        private bool _gridLookupCacheDirty = true;

        [MenuItem("Tools/Color Block Jam/Level Editor")]
        private static void OpenWindow()
        {
            LevelEditorWindow window = GetWindow<LevelEditorWindow>();
            window.titleContent = new GUIContent("Level Editor");
            window.minSize = new Vector2(920f, 640f);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();

            if (_activeLevel == null)
            {
                EditorGUILayout.HelpBox("Önce bir level JSON dosyası seç veya yeni oluştur.", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawBaseSettings();
            EditorGUILayout.Space(10f);
            DrawAvailabilitySettings();
            EditorGUILayout.Space(10f);
            DrawEditModeToolbar();
            EditorGUILayout.Space(8f);
            DrawModeSettings();
            EditorGUILayout.Space(8f);
            DrawGridEditor();
            EditorGUILayout.Space(8f);
            DrawLayoutValidationReport();
            EditorGUILayout.Space(12f);
            DrawBlockList();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EnsureShapeRegistryLoaded();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Color Block Jam - Data Driven Level Editor", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            TextAsset next = (TextAsset)EditorGUILayout.ObjectField("Active Level JSON", _activeLevelJson, typeof(TextAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                LoadLevelFromJsonAsset(next);
            }

            int shapeCount = _shapeRegistry != null ? _shapeRegistry.Shapes.Count : 0;
            EditorGUILayout.LabelField("Shape JSON Count", shapeCount.ToString());

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create New Level JSON", GUILayout.Height(24f)))
            {
                CreateNewLevelJson();
            }

            bool canSave = _activeLevel != null && !string.IsNullOrWhiteSpace(_activeLevelJsonPath);
            EditorGUI.BeginDisabledGroup(!canSave);
            if (GUILayout.Button("Save JSON", GUILayout.Height(24f), GUILayout.Width(120f)))
            {
                WriteActiveLevelToJson();
            }

            EditorGUI.EndDisabledGroup();

            if (_activeLevelJson != null && GUILayout.Button("Ping JSON", GUILayout.Height(24f), GUILayout.Width(120f)))
            {
                EditorGUIUtility.PingObject(_activeLevelJson);
            }

            if (GUILayout.Button("Reload Shape JSON", GUILayout.Height(24f), GUILayout.Width(150f)))
            {
                EnsureShapeRegistryLoaded(true);
                if (_activeLevelJson != null)
                {
                    LoadLevelFromJsonAsset(_activeLevelJson);
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawBaseSettings()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Level Settings", EditorStyles.boldLabel);

            int nextLevelNumber = EditorGUILayout.IntField("Level Number", _activeLevel.levelNumber);
            float nextTimer = EditorGUILayout.FloatField("Time Limit (sec)", _activeLevel.timeLimit);
            Vector2Int nextGrid = EditorGUILayout.Vector2IntField("Grid Size", _activeLevel.gridDimensions);

            nextLevelNumber = Mathf.Max(1, nextLevelNumber);
            nextTimer = Mathf.Max(1f, nextTimer);
            nextGrid.x = Mathf.Max(1, nextGrid.x);
            nextGrid.y = Mathf.Max(1, nextGrid.y);

            if (nextLevelNumber != _activeLevel.levelNumber ||
                !Mathf.Approximately(nextTimer, _activeLevel.timeLimit) ||
                nextGrid != _activeLevel.gridDimensions)
            {
                RecordLevelChange("Edit Level Settings");
                _activeLevel.levelNumber = nextLevelNumber;
                _activeLevel.timeLimit = nextTimer;
                _activeLevel.gridDimensions = nextGrid;
                ClampLevelJsonToGrid();
                SaveLevelChange();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAvailabilitySettings()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Available Colors & Shapes", EditorStyles.boldLabel);

            DrawAvailableColors();
            DrawAvailableShapes();

            EditorGUILayout.EndVertical();
        }

        private void DrawAvailableShapes()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Available Shapes");

            if (_shapeRegistry == null || _shapeRegistry.Shapes.Count == 0)
            {
                EditorGUILayout.HelpBox("Shape JSON bulunamadı. Assets/Data/BlockShapes altındaki .json dosyalarını kontrol et.", MessageType.Warning);
                return;
            }

            IReadOnlyList<BlockShapeJsonData> shapes = _shapeRegistry.Shapes;
            for (int i = 0; i < shapes.Count; i++)
            {
                BlockShapeJsonData shape = shapes[i];
                if (shape == null)
                {
                    continue;
                }

                string key = shape.ShapeKey;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                bool hasShape = ContainsShapeKey(_activeLevel.availableShapeKeys, key);
                bool nextHasShape = EditorGUILayout.ToggleLeft(key, hasShape);
                if (nextHasShape == hasShape)
                {
                    continue;
                }

                RecordLevelChange("Edit Available Shapes");
                if (nextHasShape)
                {
                    _activeLevel.availableShapeKeys.Add(key);
                }
                else
                {
                    RemoveAvailableShapeByKey(key);
                }

                SaveLevelChange();
            }
        }

        private void DrawAvailableColors()
        {
            EditorGUILayout.LabelField("Available Colors");

            BlockColor[] allColors = (BlockColor[])Enum.GetValues(typeof(BlockColor));
            for (int i = 0; i < allColors.Length; i++)
            {
                BlockColor color = allColors[i];
                bool hasColor = _activeLevel.availableColors.Contains(color);
                bool next = EditorGUILayout.ToggleLeft(color.ToString(), hasColor);

                if (next == hasColor)
                {
                    continue;
                }

                RecordLevelChange("Edit Available Colors");
                if (next)
                {
                    _activeLevel.availableColors.Add(color);
                }
                else
                {
                    _activeLevel.availableColors.Remove(color);
                    RemoveDoorsWithColor(color);
                    RemoveBlocksWithColor(color);
                }

                SaveLevelChange();
            }
        }

        private void DrawEditModeToolbar()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Grid Edit Mode", EditorStyles.boldLabel);
            _editMode = (LevelEditorMode)GUILayout.Toolbar((int)_editMode, new[]
            {
                "Blocked Cells",
                "Doors",
                "Blocks"
            });
            EditorGUILayout.EndVertical();
        }

        private void DrawModeSettings()
        {
            EditorGUILayout.BeginVertical("box");
            switch (_editMode)
            {
                case LevelEditorMode.BlockedCells:
                    EditorGUILayout.HelpBox("Grid hücresine tıklayarak blocked cell ekle/çıkar.", MessageType.None);
                    break;

                case LevelEditorMode.Doors:
                    _selectedDoorColor = DrawColorPicker("Door Color", _selectedDoorColor);
                    _selectedDoorWidth = Mathf.Max(1, EditorGUILayout.IntField("Door Width", _selectedDoorWidth));
                    EditorGUILayout.HelpBox("Door sadece kenar hücrelerine (kose haric) eklenir. Kenar hücreye tıklayarak door ekle/çıkar.", MessageType.None);
                    break;

                case LevelEditorMode.Blocks:
                    _selectedBlockColor = DrawColorPicker("Block Color", _selectedBlockColor);
                    _selectedBlockMovementConstraint = (BlockMovementConstraint)EditorGUILayout.EnumPopup(
                        "Movement",
                        _selectedBlockMovementConstraint);
                    DrawShapePicker();

                    EditorGUILayout.HelpBox(
                        "Grid hücresine tıklayınca blok eklenir. Aynı anchor hücresine tekrar tıklarsan blok silinir.",
                        MessageType.None);

                    if (_selectedBlockShape == null)
                    {
                        EditorGUILayout.HelpBox("Blok eklemek için Shape seçmelisin.", MessageType.Warning);
                    }

                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawShapePicker()
        {
            if (_shapeRegistry == null || _shapeRegistry.Shapes.Count == 0)
            {
                _selectedBlockShape = null;
                EditorGUILayout.HelpBox("Shape JSON bulunamadı.", MessageType.Warning);
                return;
            }

            IReadOnlyList<BlockShapeJsonData> shapes = _shapeRegistry.Shapes;
            string[] options = new string[shapes.Count];
            int selectedIndex = -1;

            for (int i = 0; i < shapes.Count; i++)
            {
                BlockShapeJsonData shape = shapes[i];
                string key = shape != null ? shape.ShapeKey : string.Empty;
                options[i] = string.IsNullOrWhiteSpace(key) ? $"Shape_{i}" : key;

                if (_selectedBlockShape != null && shape != null &&
                    string.Equals(_selectedBlockShape.ShapeKey, shape.ShapeKey, StringComparison.Ordinal))
                {
                    selectedIndex = i;
                }
            }

            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            int nextIndex = EditorGUILayout.Popup("Shape", selectedIndex, options);
            _selectedBlockShape = nextIndex >= 0 && nextIndex < shapes.Count ? shapes[nextIndex] : null;
        }

        private BlockColor DrawColorPicker(string label, BlockColor fallback)
        {
            List<BlockColor> availableColors = _activeLevel.availableColors;
            if (availableColors == null || availableColors.Count == 0)
            {
                EditorGUILayout.HelpBox("AvailableColors boş. Önce renk seçimi yapmalısın.", MessageType.Warning);
                return fallback;
            }

            int selectedIndex = Mathf.Max(0, availableColors.IndexOf(fallback));
            string[] colorNames = new string[availableColors.Count];
            for (int i = 0; i < availableColors.Count; i++)
            {
                colorNames[i] = availableColors[i].ToString();
            }

            selectedIndex = EditorGUILayout.Popup(label, selectedIndex, colorNames);
            return availableColors[selectedIndex];
        }

        private void DrawGridEditor()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
            EnsureGridLookupCache();

            Vector2Int grid = _activeLevel.gridDimensions;
            float rowWidth = grid.x * GridCellPixelSize;
            Rect rect = GUILayoutUtility.GetRect(rowWidth, grid.y * GridCellPixelSize);

            for (int y = grid.y - 1; y >= 0; y--)
            {
                for (int x = 0; x < grid.x; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    Rect cellRect = new Rect(
                        rect.x + (x * GridCellPixelSize),
                        rect.y + ((grid.y - 1 - y) * GridCellPixelSize),
                        GridCellPixelSize - 2f,
                        GridCellPixelSize - 2f);

                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = GetCellColor(cell);

                    string label = GetCellLabel(cell);
                    if (GUI.Button(cellRect, label))
                    {
                        HandleCellClick(cell);
                    }

                    GUI.backgroundColor = previous;
                }
            }

            EditorGUILayout.Space(4f);
            DrawGridLegend();
            EditorGUILayout.EndVertical();
        }

        private void DrawGridLegend()
        {
            EditorGUILayout.LabelField("Legend:");
            EditorGUILayout.LabelField("X = Blocked, D = Door, B = Block coverage");
        }

        private void DrawLayoutValidationReport()
        {
            EnsureGridLookupCache();
            if (_layoutValidationIssues.Count == 0)
            {
                return;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Layout Validation", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Yerlesimde {_layoutValidationIssues.Count} sorun bulundu. Bu kayitlar runtime sirasinda atlanabilir.",
                MessageType.Warning);

            int visibleIssueCount = Mathf.Min(5, _layoutValidationIssues.Count);
            for (int i = 0; i < visibleIssueCount; i++)
            {
                EditorGUILayout.LabelField($"- {_layoutValidationIssues[i]}");
            }

            if (_layoutValidationIssues.Count > visibleIssueCount)
            {
                EditorGUILayout.LabelField($"... +{_layoutValidationIssues.Count - visibleIssueCount} ek kayit");
            }

            if (GUILayout.Button("Raporu Console'a Yaz", GUILayout.Height(22f)))
            {
                string levelKey = string.IsNullOrWhiteSpace(_activeLevel.levelKey) ? "<unnamed>" : _activeLevel.levelKey;
                for (int i = 0; i < _layoutValidationIssues.Count; i++)
                {
                    Debug.LogWarning($"[LevelEditor][{levelKey}] {_layoutValidationIssues[i]}");
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void EnsureGridLookupCache()
        {
            if (!_gridLookupCacheDirty || _activeLevel == null)
            {
                return;
            }

            _gridLookupCacheDirty = false;
            _blockedCellLookup.Clear();
            _doorIndexByCell.Clear();
            _blockIndexByCell.Clear();
            _layoutValidationIssues.Clear();

            if (_activeLevel.blockedCells != null)
            {
                for (int i = 0; i < _activeLevel.blockedCells.Count; i++)
                {
                    _blockedCellLookup.Add(_activeLevel.blockedCells[i]);
                }
            }

            BuildDoorLookup();
            BuildBlockLookupAndValidation();
        }

        private void BuildDoorLookup()
        {
            if (_activeLevel.doors == null)
            {
                return;
            }

            for (int i = 0; i < _activeLevel.doors.Count; i++)
            {
                DoorData door = _activeLevel.doors[i];
                _doorCellsBuffer.Clear();
                if (!DoorOpeningMap.TryCollectDoorCells(door, _activeLevel.gridDimensions, _doorCellsBuffer))
                {
                    _layoutValidationIssues.Add(
                        $"Door #{i} gecersiz: Pos={door.position}, Width={Mathf.Max(1, door.openingWidth)}");
                    continue;
                }

                for (int cellIndex = 0; cellIndex < _doorCellsBuffer.Count; cellIndex++)
                {
                    Vector2Int doorCell = _doorCellsBuffer[cellIndex];
                    if (_doorIndexByCell.ContainsKey(doorCell))
                    {
                        _layoutValidationIssues.Add($"Door #{i} hucre cakismasi: {doorCell}");
                        continue;
                    }

                    _doorIndexByCell.Add(doorCell, i);
                }
            }
        }

        private void BuildBlockLookupAndValidation()
        {
            Vector2Int grid = _activeLevel.gridDimensions;
            _validationOccupancyMap.Configure(grid.x, grid.y);
            _validationOccupancyMap.MarkBlockedCells(_activeLevel.blockedCells);

            if (_activeLevel.blocks == null)
            {
                return;
            }

            for (int i = 0; i < _activeLevel.blocks.Count; i++)
            {
                LevelJsonBlockData block = _activeLevel.blocks[i];

                if (!string.IsNullOrWhiteSpace(block.shapeKey) &&
                    (_shapeRegistry == null || !_shapeRegistry.TryResolveShape(block.shapeKey, out _)))
                {
                    _layoutValidationIssues.Add(
                        $"Blok #{i} cozulemeyen shape '{block.shapeKey.Trim()}'. Runtime 1x1 fallback uygular.");
                }

                Vector2Int[] localCells = block.GetLocalCells(_shapeRegistry);
                if (!_validationOccupancyMap.CanPlace(i, block.position, localCells))
                {
                    string shapeLabel = string.IsNullOrWhiteSpace(block.shapeKey) ? "1x1(default)" : block.shapeKey.Trim();
                    _layoutValidationIssues.Add(
                        $"Blok #{i} yerlestirilemedi: Shape={shapeLabel}, Pos={block.position}, Cells={FormatWorldCells(block.position, localCells)}");
                    continue;
                }

                _validationOccupancyMap.FillBlock(i, block.position, localCells);
                for (int cellIndex = 0; cellIndex < localCells.Length; cellIndex++)
                {
                    Vector2Int worldCell = block.position + localCells[cellIndex];
                    if (!_blockIndexByCell.ContainsKey(worldCell))
                    {
                        _blockIndexByCell.Add(worldCell, i);
                    }
                }
            }
        }

        private bool IsBlockedCell(Vector2Int cell)
        {
            EnsureGridLookupCache();
            return _blockedCellLookup.Contains(cell);
        }

        private void MarkGridLookupCacheDirty()
        {
            _gridLookupCacheDirty = true;
        }

        private static string FormatWorldCells(Vector2Int anchorPosition, Vector2Int[] localCells)
        {
            if (localCells == null || localCells.Length == 0)
            {
                return "[]";
            }

            var worldCells = new string[localCells.Length];
            for (int i = 0; i < localCells.Length; i++)
            {
                Vector2Int worldCell = anchorPosition + localCells[i];
                worldCells[i] = $"({worldCell.x},{worldCell.y})";
            }

            return $"[{string.Join(", ", worldCells)}]";
        }

        private void DrawBlockList()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Blocks In Level", EditorStyles.boldLabel);

            for (int i = 0; i < _activeLevel.blocks.Count; i++)
            {
                LevelJsonBlockData block = _activeLevel.blocks[i];
                Vector2Int size = block.GetSize(_shapeRegistry);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    $"#{i} Pos:{block.position} Size:{size.x}x{size.y} Color:{block.colorType} Move:{block.movementConstraint}");

                if (GUILayout.Button("Delete", GUILayout.Width(64f)))
                {
                    RecordLevelChange("Delete Block");
                    _activeLevel.blocks.RemoveAt(i);
                    SaveLevelChange();
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void HandleCellClick(Vector2Int cell)
        {
            switch (_editMode)
            {
                case LevelEditorMode.BlockedCells:
                    ToggleBlockedCell(cell);
                    break;
                case LevelEditorMode.Doors:
                    ToggleDoor(cell);
                    break;
                case LevelEditorMode.Blocks:
                    ToggleBlock(cell);
                    break;
            }
        }

        private void ToggleBlockedCell(Vector2Int cell)
        {
            int index = _activeLevel.blockedCells.IndexOf(cell);
            RecordLevelChange("Toggle Blocked Cell");

            if (index >= 0)
            {
                _activeLevel.blockedCells.RemoveAt(index);
            }
            else
            {
                _activeLevel.blockedCells.Add(cell);
                RemoveDoorsOnCell(cell);
                RemoveBlocksIntersectingCell(cell);
            }

            SaveLevelChange();
        }

        private void ToggleDoor(Vector2Int cell)
        {
            int existingIndex = GetDoorIndexAtCell(cell);
            if (existingIndex >= 0)
            {
                RecordLevelChange("Toggle Door");
                _activeLevel.doors.RemoveAt(existingIndex);
                SaveLevelChange();
                return;
            }

            if (!IsEdgeCell(cell))
            {
                ShowNotification(new GUIContent("Door sadece kenar hücresine konabilir."));
                return;
            }

            if (IsCornerCell(cell))
            {
                ShowNotification(new GUIContent("Door kose hucreye konamaz. Kosenin yanindaki kenar hucreyi sec."));
                return;
            }

            DoorData nextDoor = new DoorData
            {
                position = cell,
                colorType = _selectedDoorColor,
                openingWidth = _selectedDoorWidth
            };

            _doorCellsBuffer.Clear();
            if (!DoorOpeningMap.TryCollectDoorCells(nextDoor, _activeLevel.gridDimensions, _doorCellsBuffer))
            {
                ShowNotification(new GUIContent("Door width bu kenar için uygun değil."));
                return;
            }

            RecordLevelChange("Toggle Door");
            RemoveDoorsOnCells(_doorCellsBuffer);

            for (int i = 0; i < _doorCellsBuffer.Count; i++)
            {
                Vector2Int doorCell = _doorCellsBuffer[i];
                _activeLevel.blockedCells.Remove(doorCell);
                RemoveBlocksIntersectingCell(doorCell);
            }

            _activeLevel.doors.Add(nextDoor);
            SaveLevelChange();
        }

        private void ToggleBlock(Vector2Int anchorCell)
        {
            int existingAnchorIndex = GetBlockByAnchor(anchorCell);

            if (existingAnchorIndex >= 0)
            {
                RecordLevelChange("Toggle Block");
                _activeLevel.blocks.RemoveAt(existingAnchorIndex);
                SaveLevelChange();
                return;
            }

            if (_selectedBlockShape == null)
            {
                return;
            }

            if (!CanPlaceShape(anchorCell, _selectedBlockShape))
            {
                return;
            }

            RecordLevelChange("Toggle Block");

            if (!ContainsShapeKey(_activeLevel.availableShapeKeys, _selectedBlockShape.ShapeKey))
            {
                _activeLevel.availableShapeKeys.Add(_selectedBlockShape.ShapeKey);
            }

            LevelJsonBlockData block = new LevelJsonBlockData
            {
                position = anchorCell,
                shapeKey = _selectedBlockShape.ShapeKey,
                blockType = BlockShapeTypeUtility.FromShapeKey(_selectedBlockShape.ShapeKey),
                movementConstraint = _selectedBlockMovementConstraint,
                colorType = _selectedBlockColor
            };

            _activeLevel.blocks.Add(block);
            SaveLevelChange();
        }

        private bool CanPlaceShape(Vector2Int anchorPosition, BlockShapeJsonData shape)
        {
            if (shape == null)
            {
                return false;
            }

            Vector2Int[] localCells = shape.GetLocalCells();

            for (int i = 0; i < localCells.Length; i++)
            {
                Vector2Int worldCell = anchorPosition + localCells[i];
                if (worldCell.x < 0 || worldCell.y < 0 ||
                    worldCell.x >= _activeLevel.gridDimensions.x ||
                    worldCell.y >= _activeLevel.gridDimensions.y)
                {
                    return false;
                }

                if (IsBlockedCell(worldCell))
                {
                    return false;
                }

                if (GetBlockAtCell(worldCell) >= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private Color GetCellColor(Vector2Int cell)
        {
            if (IsBlockedCell(cell))
            {
                return new Color(0.22f, 0.22f, 0.22f);
            }

            int blockIndex = GetBlockAtCell(cell);
            if (blockIndex >= 0)
            {
                BlockColor color = _activeLevel.blocks[blockIndex].colorType;
                Color baseColor = BlockColorUtility.GetColor(color);
                baseColor.a = 0.9f;
                return baseColor;
            }

            int doorIndex = GetDoorIndexAtCell(cell);
            if (doorIndex >= 0)
            {
                BlockColor color = _activeLevel.doors[doorIndex].colorType;
                Color doorColor = BlockColorUtility.GetColor(color);
                return Color.Lerp(doorColor, Color.white, 0.35f);
            }

            return new Color(0.9f, 0.9f, 0.9f);
        }

        private string GetCellLabel(Vector2Int cell)
        {
            if (IsBlockedCell(cell))
            {
                return "X";
            }

            if (GetBlockAtCell(cell) >= 0)
            {
                return "B";
            }

            if (GetDoorIndexAtCell(cell) >= 0)
            {
                return "D";
            }

            return string.Empty;
        }

        private int GetDoorIndexAtCell(Vector2Int cell)
        {
            EnsureGridLookupCache();
            return _doorIndexByCell.TryGetValue(cell, out int index) ? index : -1;
        }

        private int GetBlockByAnchor(Vector2Int anchor)
        {
            for (int i = 0; i < _activeLevel.blocks.Count; i++)
            {
                if (_activeLevel.blocks[i].position == anchor)
                {
                    return i;
                }
            }

            return -1;
        }

        private int GetBlockAtCell(Vector2Int cell)
        {
            EnsureGridLookupCache();
            return _blockIndexByCell.TryGetValue(cell, out int index) ? index : -1;
        }

        private void RemoveDoorsOnCell(Vector2Int cell)
        {
            while (true)
            {
                int index = GetDoorIndexAtCell(cell);
                if (index < 0)
                {
                    return;
                }

                _activeLevel.doors.RemoveAt(index);
                MarkGridLookupCacheDirty();
            }
        }

        private void RemoveDoorsOnCells(List<Vector2Int> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                RemoveDoorsOnCell(cells[i]);
            }
        }

        private void RemoveBlocksIntersectingCell(Vector2Int cell)
        {
            while (true)
            {
                int index = GetBlockAtCell(cell);
                if (index < 0)
                {
                    return;
                }

                _activeLevel.blocks.RemoveAt(index);
                MarkGridLookupCacheDirty();
            }
        }

        private void RemoveDoorsWithColor(BlockColor color)
        {
            for (int i = _activeLevel.doors.Count - 1; i >= 0; i--)
            {
                if (_activeLevel.doors[i].colorType == color)
                {
                    _activeLevel.doors.RemoveAt(i);
                }
            }
        }

        private void RemoveBlocksWithColor(BlockColor color)
        {
            for (int i = _activeLevel.blocks.Count - 1; i >= 0; i--)
            {
                if (_activeLevel.blocks[i].colorType == color)
                {
                    _activeLevel.blocks.RemoveAt(i);
                }
            }
        }

        private void ClampLevelJsonToGrid()
        {
            Vector2Int grid = _activeLevel.gridDimensions;

            for (int i = _activeLevel.blockedCells.Count - 1; i >= 0; i--)
            {
                Vector2Int cell = _activeLevel.blockedCells[i];
                if (cell.x < 0 || cell.y < 0 || cell.x >= grid.x || cell.y >= grid.y)
                {
                    _activeLevel.blockedCells.RemoveAt(i);
                }
            }

            for (int i = _activeLevel.doors.Count - 1; i >= 0; i--)
            {
                DoorData door = _activeLevel.doors[i];
                if (!DoorOpeningMap.TryCollectDoorCells(door, grid, _doorCellsBuffer))
                {
                    _activeLevel.doors.RemoveAt(i);
                }
            }

            for (int i = _activeLevel.blocks.Count - 1; i >= 0; i--)
            {
                LevelJsonBlockData block = _activeLevel.blocks[i];
                if (!IsBlockWithinGrid(block, grid))
                {
                    _activeLevel.blocks.RemoveAt(i);
                }
            }

            MarkGridLookupCacheDirty();
        }

        private bool IsBlockWithinGrid(LevelJsonBlockData block, Vector2Int gridSize)
        {
            Vector2Int[] localCells = block.GetLocalCells(_shapeRegistry);
            for (int i = 0; i < localCells.Length; i++)
            {
                Vector2Int cell = block.position + localCells[i];
                if (cell.x < 0 || cell.y < 0 || cell.x >= gridSize.x || cell.y >= gridSize.y)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsEdgeCell(Vector2Int cell)
        {
            Vector2Int grid = _activeLevel.gridDimensions;
            return cell.x == 0 ||
                   cell.y == 0 ||
                   cell.x == grid.x - 1 ||
                   cell.y == grid.y - 1;
        }

        private bool IsCornerCell(Vector2Int cell)
        {
            return DoorOpeningMap.IsCornerCell(cell, _activeLevel.gridDimensions);
        }

        private void RecordLevelChange(string action)
        {
            if (_activeLevel == null)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(this, action);
        }

        private void SaveLevelChange()
        {
            if (_activeLevel == null)
            {
                return;
            }

            _activeLevel.Sanitize();
            MarkGridLookupCacheDirty();
            WriteActiveLevelToJson();
            Repaint();
        }

        private void CreateNewLevelJson()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Level JSON",
                "Level",
                "json",
                "Yeni level json kaydet");

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            _activeLevel = new LevelJsonData
            {
                levelKey = Path.GetFileNameWithoutExtension(path)
            };
            _activeLevel.Sanitize();
            MarkGridLookupCacheDirty();

            _activeLevelJsonPath = path;
            WriteActiveLevelToJson();
            AssetDatabase.Refresh();

            _activeLevelJson = AssetDatabase.LoadAssetAtPath<TextAsset>(_activeLevelJsonPath);
            if (_activeLevelJson != null)
            {
                EditorGUIUtility.PingObject(_activeLevelJson);
            }
        }

        private void LoadLevelFromJsonAsset(TextAsset jsonAsset)
        {
            EnsureShapeRegistryLoaded();

            _activeLevelJson = jsonAsset;
            _activeLevelJsonPath = jsonAsset != null ? AssetDatabase.GetAssetPath(jsonAsset) : string.Empty;

            if (jsonAsset == null)
            {
                _activeLevel = null;
                MarkGridLookupCacheDirty();
                return;
            }

            _activeLevel = LevelJsonSerialization.Deserialize(jsonAsset.text, jsonAsset.name);
            MarkGridLookupCacheDirty();
        }

        private void WriteActiveLevelToJson()
        {
            if (_activeLevel == null || string.IsNullOrWhiteSpace(_activeLevelJsonPath))
            {
                return;
            }

            string json = LevelJsonSerialization.Serialize(_activeLevel, true);
            File.WriteAllText(_activeLevelJsonPath, json);
            AssetDatabase.ImportAsset(_activeLevelJsonPath, ImportAssetOptions.ForceUpdate);
            _activeLevelJson = AssetDatabase.LoadAssetAtPath<TextAsset>(_activeLevelJsonPath);
        }

        private void EnsureShapeRegistryLoaded(bool forceReload = false)
        {
            if (_shapeRegistry != null && !forceReload)
            {
                return;
            }

            var shapeJsonFiles = new List<TextAsset>();
            if (AssetDatabase.IsValidFolder(ShapeJsonFolder))
            {
                string[] shapeGuids = AssetDatabase.FindAssets("t:TextAsset", new[] { ShapeJsonFolder });
                var shapePaths = new List<string>(shapeGuids.Length);
                for (int i = 0; i < shapeGuids.Length; i++)
                {
                    shapePaths.Add(AssetDatabase.GUIDToAssetPath(shapeGuids[i]));
                }

                shapePaths.Sort(StringComparer.Ordinal);

                for (int i = 0; i < shapePaths.Count; i++)
                {
                    string path = shapePaths[i];
                    if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    TextAsset shapeJson = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    if (shapeJson != null)
                    {
                        shapeJsonFiles.Add(shapeJson);
                    }
                }
            }

            _shapeRegistry = BlockShapeRegistry.FromJsonAssets(shapeJsonFiles);
            MarkGridLookupCacheDirty();

            BlockShapeJsonData resolvedShape = null;
            if (_selectedBlockShape != null && !_shapeRegistry.TryResolveShape(_selectedBlockShape.ShapeKey, out resolvedShape))
            {
                _selectedBlockShape = _shapeRegistry.Shapes.Count > 0 ? _shapeRegistry.Shapes[0] : null;
                return;
            }

            if (resolvedShape != null)
            {
                _selectedBlockShape = resolvedShape;
            }
            else if (_selectedBlockShape == null && _shapeRegistry.Shapes.Count > 0)
            {
                _selectedBlockShape = _shapeRegistry.Shapes[0];
            }
        }

        private static bool ContainsShapeKey(List<string> shapeKeys, string shapeKey)
        {
            if (shapeKeys == null || string.IsNullOrWhiteSpace(shapeKey))
            {
                return false;
            }

            for (int i = 0; i < shapeKeys.Count; i++)
            {
                if (string.Equals(shapeKeys[i], shapeKey, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveAvailableShapeByKey(string shapeKey)
        {
            if (_activeLevel.availableShapeKeys == null || string.IsNullOrWhiteSpace(shapeKey))
            {
                return;
            }

            for (int i = _activeLevel.availableShapeKeys.Count - 1; i >= 0; i--)
            {
                var currentKey = _activeLevel.availableShapeKeys[i];
                if (string.IsNullOrWhiteSpace(currentKey) || string.Equals(currentKey, shapeKey, StringComparison.Ordinal))
                {
                    _activeLevel.availableShapeKeys.RemoveAt(i);
                }
            }
        }
    }
}
