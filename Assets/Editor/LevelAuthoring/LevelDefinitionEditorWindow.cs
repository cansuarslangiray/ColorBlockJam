using System;
using System.Collections.Generic;
using Editor.DataPipeline;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEditor;
using UnityEngine;

namespace Editor.LevelAuthoring
{
    public sealed class LevelDefinitionEditorWindow : EditorWindow
    {
        private enum EditMode
        {
            BlockedCells,
            Doors,
            Blocks
        }

        private static readonly BlockColor[] AllColors = (BlockColor[])Enum.GetValues(typeof(BlockColor));
        private static readonly string[] EditModeLabels = { "Blocked", "Doors", "Blocks" };
        private static readonly Color BlockedCellColor = new(0.22f, 0.22f, 0.22f);
        private static readonly Color FrameCellColor = new(0.32f, 0.35f, 0.47f);
        private static readonly Color EmptyCellColor = new(0.89f, 0.9f, 0.92f);

        private readonly List<LevelDefinition> _levels = new();
        private readonly List<BlockShapeDefinition> _shapes = new();
        private readonly List<Vector2Int> _doorCellsBuffer = new(8);
        private readonly HashSet<Vector2Int> _blockedCellLookup = new();
        private readonly Dictionary<Vector2Int, int> _doorIndexByCell = new();
        private readonly Dictionary<Vector2Int, int> _blockIndexByCell = new();
        private readonly HashSet<Vector2Int> _cellSetBuffer = new();
        private readonly List<string> _validationMessages = new();
        private string[] _levelOptions = { "None" };
        private string[] _shapeOptions = { "None" };
        private int _selectedLevelIndex = -1;
        private int _selectedShapeIndex = -1;
        private int _newLevelNumber = 1;
        private EditMode _editMode;
        private BlockColor _selectedDoorColor = BlockColor.Red;
        private BlockColor _selectedBlockColor = BlockColor.Red;
        private BlockFeature _selectedBlockFeature = BlockFeature.Default;
        private Vector2 _scrollPosition;
        private bool _cacheDirty = true;
        private bool _forceGuiRefreshOnNextOnGUI;
        private string _lastActiveLevelPath = string.Empty;

        [MenuItem("Tools/Color Block Jam/Level Editor")]
        private static void OpenWindow()
        {
            var window = GetWindow<LevelDefinitionEditorWindow>();
            window.titleContent = new GUIContent("Level Editor");
            window.minSize = new Vector2(1020f, 680f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged += HandleProjectChanged;
            RefreshCaches(selectFirstIfEmpty: true);
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= HandleProjectChanged;
        }

        private void OnGUI()
        {
            if (DrawToolbar())
            {
                GUIUtility.ExitGUI();
            }

            if (DrawHeader())
            {
                GUIUtility.ExitGUI();
            }

            var activeLevel = ActiveLevel;
            SyncActiveLevelGuiState(activeLevel);
            if (activeLevel == null)
            {
                EditorGUILayout.HelpBox("Aktif level seç veya yeni bir level asset oluştur.", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawLevelSettings(activeLevel);
            EditorGUILayout.Space(8f);
            DrawAvailableColors(activeLevel);
            EditorGUILayout.Space(8f);
            DrawEditModeToolbar();
            EditorGUILayout.Space(6f);
            DrawEditModeSettings(activeLevel);
            EditorGUILayout.Space(8f);
            DrawGrid(activeLevel);
            EditorGUILayout.Space(8f);
            DrawValidation(activeLevel);
            EditorGUILayout.Space(8f);
            DrawBlockList(activeLevel);

            EditorGUILayout.EndScrollView();
        }

        private bool DrawToolbar()
        {
            var shouldExitGui = false;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(64f)))
            {
                RefreshCaches(selectFirstIfEmpty: true, refreshAssetDatabase: true);
                shouldExitGui = true;
            }

            if (GUILayout.Button("Open Shape Editor", EditorStyles.toolbarButton, GUILayout.Width(132f)))
            {
                EditorApplication.ExecuteMenuItem("Tools/Color Block Jam/Shape Editor");
            }

            if (GUILayout.Button("Sync Collection", EditorStyles.toolbarButton, GUILayout.Width(112f)))
            {
                SaveActiveLevel();
                LevelContentPipelineTool.SyncCollectionFromAssets();
                RefreshCaches(selectFirstIfEmpty: false);
                shouldExitGui = true;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            return shouldExitGui;
        }

        private bool DrawHeader()
        {
            var shouldExitGui = false;
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Level Assets", EditorStyles.boldLabel);

            var previousLevel = ActiveLevel;
            var activeOption = _selectedLevelIndex + 1;
            var nextOption = EditorGUILayout.Popup("Active Level", activeOption, _levelOptions);
            if (nextOption != activeOption)
            {
                _selectedLevelIndex = nextOption - 1;
                MarkLookupDirty();
                _forceGuiRefreshOnNextOnGUI = true;
                shouldExitGui = true;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.SetNextControlName("LevelEditor_NewLevelNumber");
                _newLevelNumber = Mathf.Max(1, EditorGUILayout.IntField("New Level Number", _newLevelNumber));
                if (GUILayout.Button("Create Level Asset", GUILayout.Height(22f)))
                {
                    CreateLevelAsset(_newLevelNumber);
                    shouldExitGui = true;
                }

                using (new EditorGUI.DisabledScope(ActiveLevel == null))
                {
                    if (GUILayout.Button("Ping", GUILayout.Height(22f), GUILayout.Width(84f)))
                    {
                        EditorGUIUtility.PingObject(ActiveLevel);
                    }
                }
            }

            if (previousLevel != ActiveLevel)
            {
                GUI.FocusControl(string.Empty);
            }

            EditorGUILayout.HelpBox(
                "Level ve shape sayısı sabit değil. Yeni asset ekledikçe runtime koleksiyonuna otomatik dahil edebilirsin.",
                MessageType.Info);
            EditorGUILayout.EndVertical();
            return shouldExitGui;
        }

        private void DrawLevelSettings(LevelDefinition level)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Level Settings", EditorStyles.boldLabel);

            var previousKey = level.levelKey;
            var previousNumber = level.levelNumber;
            EditorGUI.BeginChangeCheck();
            var levelControlPrefix = BuildLevelControlPrefix(level);
            GUI.SetNextControlName($"{levelControlPrefix}_LevelKey");
            var nextKey = EditorGUILayout.TextField("Level Key", level.levelKey);
            GUI.SetNextControlName($"{levelControlPrefix}_LevelNumber");
            var nextNumber = Mathf.Max(1, EditorGUILayout.IntField("Level Number", level.levelNumber));
            GUI.SetNextControlName($"{levelControlPrefix}_TimeLimit");
            var nextTimeLimit = Mathf.Max(1f, EditorGUILayout.FloatField("Time Limit", level.timeLimit));
            GUI.SetNextControlName($"{levelControlPrefix}_GridSize");
            var nextGrid = EditorGUILayout.Vector2IntField("Grid Size", level.gridDimensions);
            nextGrid.x = Mathf.Max(3, nextGrid.x);
            nextGrid.y = Mathf.Max(3, nextGrid.y);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(level, "Edit Level Settings");
                level.levelKey = nextKey;
                level.levelNumber = nextNumber;
                level.timeLimit = nextTimeLimit;
                level.gridDimensions = nextGrid;
                level.Sanitize();
                RemoveOutOfBoundsData(level);
                SaveLevelChange(level);
                if (!string.Equals(previousKey, level.levelKey, StringComparison.Ordinal) ||
                    previousNumber != level.levelNumber)
                {
                    UpdateSelectedLevelOptionLabel(level);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAvailableColors(LevelDefinition level)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Available Colors", EditorStyles.boldLabel);

            const int rowsPerColumn = 6;
            var columnCount = Mathf.CeilToInt(AllColors.Length / (float)rowsPerColumn);

            EditorGUILayout.BeginHorizontal();
            for (var column = 0; column < columnCount; column++)
            {
                EditorGUILayout.BeginVertical(GUILayout.MaxWidth(200f));
                var startIndex = column * rowsPerColumn;
                var endIndex = Mathf.Min(startIndex + rowsPerColumn, AllColors.Length);
                for (var i = startIndex; i < endIndex; i++)
                {
                    var color = AllColors[i];
                    var hasColor = level.availableColors != null && level.availableColors.Contains(color);
                    var next = EditorGUILayout.ToggleLeft(color.ToString(), hasColor);
                    if (next == hasColor)
                    {
                        continue;
                    }

                    Undo.RecordObject(level, "Edit Available Colors");
                    level.availableColors ??= new List<BlockColor>();

                    if (next)
                    {
                        if (!level.availableColors.Contains(color))
                        {
                            level.availableColors.Add(color);
                        }
                    }
                    else
                    {
                        if (CountAvailableColors(level) <= 1)
                        {
                            ShowNotification(new GUIContent("En az bir renk seçili kalmalı."));
                            continue;
                        }

                        level.availableColors.RemoveAll(value => value == color);
                        RemoveDoorsByColor(level, color);
                        RemoveBlocksByColor(level, color);
                    }

                    ClampSelectedColorsToAvailable(level);
                    SaveLevelChange(level);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawEditModeToolbar()
        {
            EditorGUILayout.BeginVertical("box");
            _editMode = (EditMode)GUILayout.Toolbar((int)_editMode, EditModeLabels);
            EditorGUILayout.EndVertical();
        }

        private void DrawEditModeSettings(LevelDefinition level)
        {
            EditorGUILayout.BeginVertical("box");
            ClampSelectedColorsToAvailable(level);

            switch (_editMode)
            {
                case EditMode.BlockedCells:
                    EditorGUILayout.HelpBox(
                        "Grid hücresine tıklayarak blocked cell ekle/çıkar. En dış halka daima border'dır.",
                        MessageType.None);
                    break;
                case EditMode.Doors:
                    _selectedDoorColor = DrawAvailableColorPopup(level, "Door Color", _selectedDoorColor);
                    EditorGUILayout.HelpBox(
                        "Door sadece kenar hücresine (köşe hariç) eklenebilir. Aynı hücrede blocked/block varsa temizlenir.",
                        MessageType.None);
                    break;
                case EditMode.Blocks:
                    _selectedShapeIndex = Mathf.Clamp(_selectedShapeIndex, -1, _shapes.Count - 1);
                    var selectedShapeOption = _selectedShapeIndex + 1;
                    var nextShapeOption = EditorGUILayout.Popup("Shape", selectedShapeOption, _shapeOptions);
                    if (nextShapeOption != selectedShapeOption)
                    {
                        _selectedShapeIndex = nextShapeOption - 1;
                    }

                    _selectedBlockColor = DrawAvailableColorPopup(level, "Block Color", _selectedBlockColor);
                    _selectedBlockFeature = DrawFeatureSelector(_selectedBlockFeature);

                    EditorGUILayout.HelpBox(
                        "Grid'e tıklayınca seçili shape anchor pozisyonuna yerleşir. Border hücrelerine block yerleşmez.",
                        MessageType.None);
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGrid(LevelDefinition level)
        {
            if (level.gridDimensions.x <= 0 || level.gridDimensions.y <= 0)
            {
                return;
            }

            EnsureLookupCache(level);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Grid Editor", EditorStyles.boldLabel);

            var cellSize = ResolveCellSize(level.gridDimensions);
            var previousColor = GUI.backgroundColor;
            for (var y = level.gridDimensions.y - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                for (var x = 0; x < level.gridDimensions.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var label = ResolveCellLabel(level, cell, out var color);
                    GUI.backgroundColor = color;
                    if (GUILayout.Button(label, GUILayout.Width(cellSize), GUILayout.Height(cellSize)))
                    {
                        HandleCellClick(level, cell);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            GUI.backgroundColor = previousColor;
            EditorGUILayout.EndVertical();
        }

        private void DrawValidation(LevelDefinition level)
        {
            _validationMessages.Clear();
            ValidateLevel(level, _validationMessages);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            if (_validationMessages.Count == 0)
            {
                EditorGUILayout.HelpBox("No issues found.", MessageType.Info);
            }
            else
            {
                for (var i = 0; i < _validationMessages.Count; i++)
                {
                    EditorGUILayout.HelpBox(_validationMessages[i], MessageType.Warning);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBlockList(LevelDefinition level)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Blocks", EditorStyles.boldLabel);

            if (level.blocks == null || level.blocks.Count == 0)
            {
                EditorGUILayout.HelpBox("Henüz block yok.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            for (var i = 0; i < level.blocks.Count; i++)
            {
                var block = level.blocks[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Block #{i + 1}");

                EditorGUI.BeginChangeCheck();
                var position = EditorGUILayout.Vector2IntField("Anchor", block.position);
                var shape = EditorGUILayout.ObjectField("Shape", block.shapeDefinition, typeof(BlockShapeDefinition), false) as BlockShapeDefinition;
                var color = DrawAvailableColorPopup(level, "Color", block.colorType);
                var features = DrawFeatureSelector(block.blockFeatures);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(level, "Edit Block Entry");
                    block.position = position;
                    block.shapeDefinition = shape;
                    block.shapeKey = shape ? shape.ShapeKey : block.shapeKey;
                    block.colorType = color;
                    block.blockFeatures = features.Sanitize();
                    level.blocks[i] = block;
                    SaveLevelChange(level);
                }

                if (GUILayout.Button("Remove Block", GUILayout.Height(20f)))
                {
                    Undo.RecordObject(level, "Remove Block");
                    level.blocks.RemoveAt(i);
                    SaveLevelChange(level);
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private void HandleCellClick(LevelDefinition level, Vector2Int cell)
        {
            switch (_editMode)
            {
                case EditMode.BlockedCells:
                    ToggleBlockedCell(level, cell);
                    break;
                case EditMode.Doors:
                    ToggleDoor(level, cell);
                    break;
                case EditMode.Blocks:
                    ToggleBlock(level, cell);
                    break;
            }
        }

        private void ToggleBlockedCell(LevelDefinition level, Vector2Int cell)
        {
            if (IsFrameCell(level, cell))
            {
                ShowNotification(new GUIContent("En dış halka border olarak sabittir."));
                return;
            }

            Undo.RecordObject(level, "Toggle Blocked Cell");
            var index = level.blockedCells.IndexOf(cell);
            if (index >= 0)
            {
                level.blockedCells.RemoveAt(index);
            }
            else
            {
                level.blockedCells.Add(cell);
                RemoveDoorsOnCells(level, cell);
                RemoveBlocksIntersectingCells(level, cell);
            }

            SaveLevelChange(level);
        }

        private void ToggleDoor(LevelDefinition level, Vector2Int cell)
        {
            var existingDoorIndex = GetDoorIndexAtCell(level, cell);
            Undo.RecordObject(level, "Toggle Door");

            if (existingDoorIndex >= 0)
            {
                level.doors.RemoveAt(existingDoorIndex);
                SaveLevelChange(level);
                return;
            }

            if (!IsFrameCell(level, cell))
            {
                ShowNotification(new GUIContent("Door sadece kenar hücresine konabilir."));
                return;
            }

            if (DoorOpeningMap.IsCornerCell(cell, level.gridDimensions))
            {
                ShowNotification(new GUIContent("Door köşe hücresine konamaz."));
                return;
            }

            var nextDoor = new DoorData
            {
                position = cell,
                colorType = _selectedDoorColor
            };

            _doorCellsBuffer.Clear();
            if (!DoorOpeningMap.TryCollectDoorCells(nextDoor, level.gridDimensions, _doorCellsBuffer))
            {
                ShowNotification(new GUIContent("Door bu hücreye eklenemiyor."));
                return;
            }

            RemoveDoorsOnCells(level, _doorCellsBuffer);
            RemoveBlockedCells(level, _doorCellsBuffer);
            RemoveBlocksIntersectingCells(level, _doorCellsBuffer);
            level.doors.Add(nextDoor);
            SaveLevelChange(level);
        }

        private void ToggleBlock(LevelDefinition level, Vector2Int anchorCell)
        {
            var existingAnchorIndex = GetBlockIndexByAnchor(level, anchorCell);
            if (existingAnchorIndex >= 0)
            {
                Undo.RecordObject(level, "Remove Block");
                level.blocks.RemoveAt(existingAnchorIndex);
                SaveLevelChange(level);
                return;
            }

            var shape = SelectedShape;
            if (!shape)
            {
                ShowNotification(new GUIContent("Önce bir shape seçmelisin."));
                return;
            }

            if (!CanPlaceShape(level, anchorCell, shape, ignoredBlockIndex: -1))
            {
                ShowNotification(new GUIContent("Shape bu hücreye yerleşmiyor."));
                return;
            }

            Undo.RecordObject(level, "Add Block");
            var block = new LevelBlockEntry
            {
                position = anchorCell,
                shapeKey = shape.ShapeKey,
                shapeDefinition = shape,
                blockFeatures = _selectedBlockFeature.Sanitize(),
                colorType = _selectedBlockColor
            };
            block.Normalize();
            level.blocks.Add(block);
            SaveLevelChange(level);
        }

        private void SaveLevelChange(LevelDefinition level)
        {
            if (!level)
            {
                return;
            }

            level.Sanitize();
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            MarkLookupDirty();
            Repaint();
        }

        private void SaveActiveLevel()
        {
            var level = ActiveLevel;
            if (!level)
            {
                return;
            }

            level.Sanitize();
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
        }

        private void EnsureLookupCache(LevelDefinition level)
        {
            if (!_cacheDirty)
            {
                return;
            }

            _cacheDirty = false;
            _blockedCellLookup.Clear();
            _doorIndexByCell.Clear();
            _blockIndexByCell.Clear();

            if (level.blockedCells != null)
            {
                for (var i = 0; i < level.blockedCells.Count; i++)
                {
                    _blockedCellLookup.Add(level.blockedCells[i]);
                }
            }

            if (level.doors != null)
            {
                for (var i = 0; i < level.doors.Count; i++)
                {
                    var door = level.doors[i];
                    _doorCellsBuffer.Clear();
                    if (!DoorOpeningMap.TryCollectDoorCells(door, level.gridDimensions, _doorCellsBuffer))
                    {
                        continue;
                    }

                    for (var cellIndex = 0; cellIndex < _doorCellsBuffer.Count; cellIndex++)
                    {
                        _doorIndexByCell[_doorCellsBuffer[cellIndex]] = i;
                    }
                }
            }

            if (level.blocks != null)
            {
                for (var i = 0; i < level.blocks.Count; i++)
                {
                    var block = level.blocks[i];
                    var cells = block.GetLocalCells(ShapeCatalog);
                    for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                    {
                        var worldCell = block.position + cells[cellIndex];
                        _blockIndexByCell[worldCell] = i;
                    }
                }
            }
        }

        private string ResolveCellLabel(LevelDefinition level, Vector2Int cell, out Color color)
        {
            EnsureLookupCache(level);

            if (_doorIndexByCell.TryGetValue(cell, out var doorIndex) &&
                level.doors != null &&
                doorIndex >= 0 &&
                doorIndex < level.doors.Count)
            {
                color = Color.Lerp(ResolvePaletteColor(level.doors[doorIndex].colorType), Color.white, 0.35f);
                return "D";
            }

            if (_blockIndexByCell.TryGetValue(cell, out var blockIndex) &&
                level.blocks != null &&
                blockIndex >= 0 &&
                blockIndex < level.blocks.Count)
            {
                color = ResolvePaletteColor(level.blocks[blockIndex].colorType);
                color.a = 0.9f;
                return blockIndex.ToString();
            }

            if (_blockedCellLookup.Contains(cell))
            {
                color = BlockedCellColor;
                return "X";
            }

            if (IsFrameCell(level, cell))
            {
                color = FrameCellColor;
                return "B";
            }

            color = EmptyCellColor;
            return string.Empty;
        }

        private bool IsDoorCell(LevelDefinition level, Vector2Int cell)
        {
            EnsureLookupCache(level);
            return _doorIndexByCell.ContainsKey(cell);
        }

        private int GetDoorIndexAtCell(LevelDefinition level, Vector2Int cell)
        {
            EnsureLookupCache(level);
            return _doorIndexByCell.TryGetValue(cell, out var index) ? index : -1;
        }

        private int GetBlockIndexAtCell(LevelDefinition level, Vector2Int cell)
        {
            EnsureLookupCache(level);
            return _blockIndexByCell.TryGetValue(cell, out var index) ? index : -1;
        }

        private int GetBlockIndexByAnchor(LevelDefinition level, Vector2Int anchor)
        {
            if (level.blocks == null)
            {
                return -1;
            }

            for (var i = 0; i < level.blocks.Count; i++)
            {
                if (level.blocks[i].position == anchor)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool CanPlaceShape(LevelDefinition level, Vector2Int anchor, BlockShapeDefinition shape, int ignoredBlockIndex)
        {
            var localCells = shape.GetLocalCells();
            for (var i = 0; i < localCells.Length; i++)
            {
                var worldCell = anchor + localCells[i];
                if (!IsInGrid(level, worldCell))
                {
                    return false;
                }

                if (IsFrameCell(level, worldCell))
                {
                    return false;
                }

                if (_blockedCellLookup.Contains(worldCell))
                {
                    return false;
                }

                if (IsDoorCell(level, worldCell))
                {
                    return false;
                }

                var occupiedBlockIndex = GetBlockIndexAtCell(level, worldCell);
                if (occupiedBlockIndex >= 0 && occupiedBlockIndex != ignoredBlockIndex)
                {
                    return false;
                }
            }

            return true;
        }

        private void RemoveDoorsByColor(LevelDefinition level, BlockColor color)
        {
            if (level.doors == null || level.doors.Count == 0)
            {
                return;
            }

            level.doors.RemoveAll(door => door.colorType == color);
        }

        private void RemoveBlocksByColor(LevelDefinition level, BlockColor color)
        {
            if (level.blocks == null || level.blocks.Count == 0)
            {
                return;
            }

            for (var i = level.blocks.Count - 1; i >= 0; i--)
            {
                if (level.blocks[i].colorType == color)
                {
                    level.blocks.RemoveAt(i);
                }
            }
        }

        private void RemoveDoorsOnCells(LevelDefinition level, Vector2Int cell)
        {
            _cellSetBuffer.Clear();
            _cellSetBuffer.Add(cell);
            RemoveDoorsOnCells(level, _cellSetBuffer);
        }

        private void RemoveDoorsOnCells(LevelDefinition level, List<Vector2Int> cells)
        {
            _cellSetBuffer.Clear();
            for (var i = 0; i < cells.Count; i++)
            {
                _cellSetBuffer.Add(cells[i]);
            }

            RemoveDoorsOnCells(level, _cellSetBuffer);
        }

        private void RemoveDoorsOnCells(LevelDefinition level, HashSet<Vector2Int> cells)
        {
            if (cells.Count == 0 || level.doors == null || level.doors.Count == 0)
            {
                return;
            }

            for (var i = level.doors.Count - 1; i >= 0; i--)
            {
                var door = level.doors[i];
                _doorCellsBuffer.Clear();
                if (!DoorOpeningMap.TryCollectDoorCells(door, level.gridDimensions, _doorCellsBuffer))
                {
                    continue;
                }

                var shouldRemove = false;
                for (var cellIndex = 0; cellIndex < _doorCellsBuffer.Count; cellIndex++)
                {
                    if (!cells.Contains(_doorCellsBuffer[cellIndex]))
                    {
                        continue;
                    }

                    shouldRemove = true;
                    break;
                }

                if (shouldRemove)
                {
                    level.doors.RemoveAt(i);
                }
            }
        }

        private void RemoveBlockedCells(LevelDefinition level, List<Vector2Int> cells)
        {
            if (level.blockedCells == null || level.blockedCells.Count == 0)
            {
                return;
            }

            for (var i = 0; i < cells.Count; i++)
            {
                level.blockedCells.Remove(cells[i]);
            }
        }

        private void RemoveBlocksIntersectingCells(LevelDefinition level, Vector2Int cell)
        {
            _cellSetBuffer.Clear();
            _cellSetBuffer.Add(cell);
            RemoveBlocksIntersectingCells(level, _cellSetBuffer);
        }

        private void RemoveBlocksIntersectingCells(LevelDefinition level, List<Vector2Int> cells)
        {
            _cellSetBuffer.Clear();
            for (var i = 0; i < cells.Count; i++)
            {
                _cellSetBuffer.Add(cells[i]);
            }

            RemoveBlocksIntersectingCells(level, _cellSetBuffer);
        }

        private void RemoveBlocksIntersectingCells(LevelDefinition level, HashSet<Vector2Int> cells)
        {
            if (level.blocks == null || level.blocks.Count == 0 || cells.Count == 0)
            {
                return;
            }

            for (var i = level.blocks.Count - 1; i >= 0; i--)
            {
                var block = level.blocks[i];
                var localCells = block.GetLocalCells(ShapeCatalog);
                var intersects = false;
                for (var cellIndex = 0; cellIndex < localCells.Length; cellIndex++)
                {
                    var worldCell = block.position + localCells[cellIndex];
                    if (!cells.Contains(worldCell))
                    {
                        continue;
                    }

                    intersects = true;
                    break;
                }

                if (intersects)
                {
                    level.blocks.RemoveAt(i);
                }
            }
        }

        private void RemoveOutOfBoundsData(LevelDefinition level)
        {
            if (!level)
            {
                return;
            }

            if (level.blockedCells != null)
            {
                level.blockedCells.RemoveAll(cell => !IsInGrid(level, cell) || IsFrameCell(level, cell));
            }

            if (level.doors != null)
            {
                level.doors.RemoveAll(door =>
                    !IsInGrid(level, door.position) || !IsFrameCell(level, door.position) || DoorOpeningMap.IsCornerCell(door.position, level.gridDimensions));
            }

            if (level.blocks != null)
            {
                MarkLookupDirty();
                EnsureLookupCache(level);
                for (var i = level.blocks.Count - 1; i >= 0; i--)
                {
                    var block = level.blocks[i];
                    var shape = block.shapeDefinition;
                    if (!shape)
                    {
                        level.blocks.RemoveAt(i);
                        continue;
                    }

                    if (!CanPlaceShape(level, block.position, shape, i))
                    {
                        level.blocks.RemoveAt(i);
                    }
                }
            }
        }

        private static void ValidateLevel(LevelDefinition level, List<string> issues)
        {
            if (level == null || issues == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(level.levelKey))
            {
                issues.Add("Level key boş.");
            }

            if (level.availableColors == null || level.availableColors.Count == 0)
            {
                issues.Add("Available colors boş.");
            }

            if (level.blocks != null)
            {
                for (var i = 0; i < level.blocks.Count; i++)
                {
                    var block = level.blocks[i];
                    if (block.shapeDefinition == null)
                    {
                        issues.Add($"Block #{i + 1}: Shape referansı eksik.");
                    }
                }
            }
        }

        private static BlockFeature DrawFeatureSelector(BlockFeature currentFeature)
        {
            var currentIndex = FeatureToIndex(currentFeature.Sanitize());
            var nextIndex = EditorGUILayout.Popup("Movement", currentIndex, new[] { "Default", "Horizontal", "Vertical" });
            return nextIndex switch
            {
                1 => BlockFeature.Horizontal,
                2 => BlockFeature.Vertical,
                _ => BlockFeature.Default
            };
        }

        private static int FeatureToIndex(BlockFeature feature)
        {
            if (feature.HasFeature(BlockFeature.Horizontal))
            {
                return 1;
            }

            if (feature.HasFeature(BlockFeature.Vertical))
            {
                return 2;
            }

            return 0;
        }

        private static BlockColor DrawAvailableColorPopup(LevelDefinition level, string label, BlockColor currentColor)
        {
            var colorOptions = ResolveAvailableColorOptions(level);
            if (colorOptions.Length == 0)
            {
                EditorGUILayout.HelpBox("Available Colors boş. Önce checkbox ile en az bir renk seç.", MessageType.Warning);
                return currentColor;
            }

            var labels = new string[colorOptions.Length];
            for (var i = 0; i < colorOptions.Length; i++)
            {
                labels[i] = colorOptions[i].ToString();
            }

            var selectedIndex = Array.IndexOf(colorOptions, currentColor);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            var nextIndex = EditorGUILayout.Popup(label, selectedIndex, labels);
            nextIndex = Mathf.Clamp(nextIndex, 0, colorOptions.Length - 1);
            return colorOptions[nextIndex];
        }

        private void ClampSelectedColorsToAvailable(LevelDefinition level)
        {
            var colorOptions = ResolveAvailableColorOptions(level);
            if (colorOptions.Length == 0)
            {
                return;
            }

            if (Array.IndexOf(colorOptions, _selectedDoorColor) < 0)
            {
                _selectedDoorColor = colorOptions[0];
            }

            if (Array.IndexOf(colorOptions, _selectedBlockColor) < 0)
            {
                _selectedBlockColor = colorOptions[0];
            }
        }

        private static BlockColor[] ResolveAvailableColorOptions(LevelDefinition level)
        {
            if (level?.availableColors == null || level.availableColors.Count == 0)
            {
                return Array.Empty<BlockColor>();
            }

            var colorOptions = new List<BlockColor>(level.availableColors.Count);
            for (var i = 0; i < AllColors.Length; i++)
            {
                var color = AllColors[i];
                if (level.availableColors.Contains(color))
                {
                    colorOptions.Add(color);
                }
            }

            return colorOptions.ToArray();
        }

        private static int CountAvailableColors(LevelDefinition level)
        {
            return ResolveAvailableColorOptions(level).Length;
        }

        private static Color ResolvePaletteColor(BlockColor color)
        {
            return color switch
            {
                BlockColor.Red => new Color(0.90f, 0.25f, 0.25f),
                BlockColor.Blue => new Color(0.20f, 0.45f, 0.95f),
                BlockColor.Green => new Color(0.20f, 0.78f, 0.35f),
                BlockColor.Yellow => new Color(0.95f, 0.82f, 0.20f),
                BlockColor.Purple => new Color(0.62f, 0.32f, 0.88f),
                BlockColor.Orange => new Color(0.95f, 0.56f, 0.20f),
                BlockColor.Cyan => new Color(0.20f, 0.84f, 0.95f),
                BlockColor.Pink => new Color(0.96f, 0.45f, 0.72f),
                BlockColor.Mint => new Color(0.45f, 0.92f, 0.72f),
                BlockColor.Indigo => new Color(0.35f, 0.35f, 0.82f),
                BlockColor.Coral => new Color(0.95f, 0.47f, 0.41f),
                BlockColor.Lime => new Color(0.67f, 0.88f, 0.22f),
                _ => Color.white
            };
        }

        private void CreateLevelAsset(int levelNumber)
        {
            EnsureFolderExists(LevelContentPipelineTool.LevelDefinitionFolder);
            levelNumber = Mathf.Max(1, levelNumber);

            var level = CreateInstance<LevelDefinition>();
            InitializeNewLevelFromActive(level, levelNumber);
            level.Sanitize();

            var assetPath =
                AssetDatabase.GenerateUniqueAssetPath($"{LevelContentPipelineTool.LevelDefinitionFolder}/{level.levelKey}.asset");
            AssetDatabase.CreateAsset(level, assetPath);
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            LevelContentPipelineTool.SyncCollectionFromAssets();
            RefreshCaches(selectFirstIfEmpty: false, preferredLevelPath: assetPath);
            _newLevelNumber = levelNumber + 1;
            EditorGUIUtility.PingObject(level);
        }

        private void InitializeNewLevelFromActive(LevelDefinition level, int levelNumber)
        {
            level.levelNumber = levelNumber;
            level.levelKey = $"Level{levelNumber}";

            var template = ActiveLevel;
            if (!template)
            {
                level.gridDimensions = new Vector2Int(6, 8);
                level.timeLimit = 60f;
                return;
            }

            level.gridDimensions = template.gridDimensions;
            level.timeLimit = template.timeLimit;
            level.blockedCells = template.blockedCells != null
                ? new List<Vector2Int>(template.blockedCells)
                : new List<Vector2Int>();
            level.availableColors = template.availableColors != null
                ? new List<BlockColor>(template.availableColors)
                : new List<BlockColor>();
            level.doors = template.doors != null
                ? new List<DoorData>(template.doors)
                : new List<DoorData>();
            level.blocks = template.blocks != null
                ? new List<LevelBlockEntry>(template.blocks)
                : new List<LevelBlockEntry>();
        }

        private void RefreshCaches(bool selectFirstIfEmpty, bool refreshAssetDatabase = false, string preferredLevelPath = null)
        {
            var currentLevelPath = !string.IsNullOrWhiteSpace(preferredLevelPath)
                ? preferredLevelPath
                : ActiveLevel ? AssetDatabase.GetAssetPath(ActiveLevel) : string.Empty;
            var currentShapeKey = SelectedShape ? SelectedShape.ShapeKey : string.Empty;

            if (refreshAssetDatabase)
            {
                SaveActiveLevel();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            LoadLevels();
            LoadShapes();
            SelectLevelByPath(currentLevelPath, selectFirstIfEmpty);
            SelectShapeByKey(currentShapeKey);
            ClampSelectedColorsToAvailable(ActiveLevel);
            MarkLookupDirty();
            _forceGuiRefreshOnNextOnGUI = true;
            Repaint();
        }

        private void LoadLevels()
        {
            _levels.Clear();
            var guids = AssetDatabase.FindAssets($"t:{nameof(LevelDefinition)}",
                new[] { LevelContentPipelineTool.LevelDefinitionFolder });
            Array.Sort(guids, StringComparer.Ordinal);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (level)
                {
                    _levels.Add(level);
                }
            }

            _levels.Sort((left, right) =>
            {
                var byNumber = left.levelNumber.CompareTo(right.levelNumber);
                return byNumber != 0 ? byNumber : string.CompareOrdinal(left.levelKey, right.levelKey);
            });

            _levelOptions = new string[_levels.Count + 1];
            _levelOptions[0] = "None";
            for (var i = 0; i < _levels.Count; i++)
            {
                var level = _levels[i];
                _levelOptions[i + 1] = FormatLevelOption(level);
            }
        }

        private void UpdateSelectedLevelOptionLabel(LevelDefinition level)
        {
            if (_selectedLevelIndex < 0 ||
                _selectedLevelIndex >= _levels.Count ||
                _selectedLevelIndex + 1 >= _levelOptions.Length ||
                _levels[_selectedLevelIndex] != level)
            {
                return;
            }

            _levelOptions[_selectedLevelIndex + 1] = FormatLevelOption(level);
        }

        private static string FormatLevelOption(LevelDefinition level)
        {
            return level ? $"{level.levelNumber:000} - {level.levelKey}" : "Missing Level";
        }

        private void LoadShapes()
        {
            _shapes.Clear();
            var guids = AssetDatabase.FindAssets($"t:{nameof(BlockShapeDefinition)}",
                new[] { LevelContentPipelineTool.ShapeDefinitionFolder });
            Array.Sort(guids, StringComparer.Ordinal);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var shape = AssetDatabase.LoadAssetAtPath<BlockShapeDefinition>(path);
                if (shape)
                {
                    _shapes.Add(shape);
                }
            }

            _shapes.Sort((left, right) => string.CompareOrdinal(left.ShapeKey, right.ShapeKey));
            _shapeOptions = new string[_shapes.Count + 1];
            _shapeOptions[0] = "None";
            for (var i = 0; i < _shapes.Count; i++)
            {
                _shapeOptions[i + 1] = _shapes[i].ShapeKey;
            }

            if (_selectedShapeIndex >= _shapes.Count)
            {
                _selectedShapeIndex = _shapes.Count - 1;
            }
        }

        private void SelectLevel(LevelDefinition level, bool selectFirstIfEmpty = false)
        {
            if (!level)
            {
                if (_levels.Count == 0)
                {
                    _selectedLevelIndex = -1;
                    return;
                }

                if (selectFirstIfEmpty || _selectedLevelIndex < 0 || _selectedLevelIndex >= _levels.Count)
                {
                    _selectedLevelIndex = 0;
                }

                return;
            }

            for (var i = 0; i < _levels.Count; i++)
            {
                if (_levels[i] == level)
                {
                    _selectedLevelIndex = i;
                    return;
                }
            }

            if (selectFirstIfEmpty && _levels.Count > 0)
            {
                _selectedLevelIndex = 0;
            }
        }

        private void SelectLevelByPath(string levelPath, bool selectFirstIfEmpty)
        {
            if (!string.IsNullOrWhiteSpace(levelPath))
            {
                for (var i = 0; i < _levels.Count; i++)
                {
                    var level = _levels[i];
                    if (level && string.Equals(AssetDatabase.GetAssetPath(level), levelPath, StringComparison.Ordinal))
                    {
                        _selectedLevelIndex = i;
                        return;
                    }
                }
            }

            SelectLevel(null, selectFirstIfEmpty);
        }

        private void SelectShapeByKey(string shapeKey)
        {
            if (!string.IsNullOrWhiteSpace(shapeKey))
            {
                for (var i = 0; i < _shapes.Count; i++)
                {
                    var shape = _shapes[i];
                    if (shape && string.Equals(shape.ShapeKey, shapeKey, StringComparison.Ordinal))
                    {
                        _selectedShapeIndex = i;
                        return;
                    }
                }
            }

            _selectedShapeIndex = Mathf.Clamp(_selectedShapeIndex, -1, _shapes.Count - 1);
        }

        private static float ResolveCellSize(Vector2Int gridDimensions)
        {
            var maxDimension = Mathf.Max(gridDimensions.x, gridDimensions.y);
            if (maxDimension <= 8) return 34f;
            if (maxDimension <= 12) return 28f;
            if (maxDimension <= 16) return 24f;
            return 20f;
        }

        private static bool IsFrameCell(LevelDefinition level, Vector2Int cell)
        {
            return BoardFrameMap.IsFrameCell(cell, level.gridDimensions);
        }

        private static bool IsInGrid(LevelDefinition level, Vector2Int cell)
        {
            return cell.x >= 0 &&
                   cell.y >= 0 &&
                   cell.x < level.gridDimensions.x &&
                   cell.y < level.gridDimensions.y;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var folders = folderPath.Split('/');
            var currentPath = folders[0];
            for (var i = 1; i < folders.Length; i++)
            {
                var nextPath = $"{currentPath}/{folders[i]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }

                currentPath = nextPath;
            }
        }

        private void SyncActiveLevelGuiState(LevelDefinition level)
        {
            var activeLevelPath = level ? AssetDatabase.GetAssetPath(level) : string.Empty;
            if (!_forceGuiRefreshOnNextOnGUI &&
                string.Equals(_lastActiveLevelPath, activeLevelPath, StringComparison.Ordinal))
            {
                return;
            }

            _lastActiveLevelPath = activeLevelPath;
            _forceGuiRefreshOnNextOnGUI = false;
            ResetGuiControlState();
            ClampSelectedColorsToAvailable(level);
            MarkLookupDirty();
            _scrollPosition = Vector2.zero;
            Repaint();
        }

        private static void ResetGuiControlState()
        {
            GUI.FocusControl(string.Empty);
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
        }

        private static string BuildLevelControlPrefix(LevelDefinition level)
        {
            var path = level ? AssetDatabase.GetAssetPath(level) : string.Empty;
            return string.IsNullOrWhiteSpace(path)
                ? "LevelEditor_Level_None"
                : $"LevelEditor_Level_{path.GetHashCode()}";
        }

        private void MarkLookupDirty()
        {
            _cacheDirty = true;
        }

        private void HandleProjectChanged()
        {
            RefreshCaches(selectFirstIfEmpty: false);
            Repaint();
        }

        private LevelDefinition ActiveLevel =>
            _selectedLevelIndex >= 0 && _selectedLevelIndex < _levels.Count
                ? _levels[_selectedLevelIndex]
                : null;

        private BlockShapeDefinition SelectedShape =>
            _selectedShapeIndex >= 0 && _selectedShapeIndex < _shapes.Count
                ? _shapes[_selectedShapeIndex]
                : null;

        private BlockShapeCatalog ShapeCatalog =>
            AssetDatabase.LoadAssetAtPath<BlockShapeCatalog>(LevelContentPipelineTool.ShapeCatalogAssetPath);
    }
}
