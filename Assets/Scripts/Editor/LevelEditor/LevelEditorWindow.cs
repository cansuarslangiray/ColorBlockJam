using System;
using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEditor;
using UnityEngine;

namespace Editor.LevelEditor
{
    public class LevelEditorWindow : EditorWindow
    {
        private const float GridCellPixelSize = 26f;

        private LevelData _activeLevel;
        private LevelEditorMode _editMode;
        private Vector2 _scrollPosition;

        private BlockColor _selectedDoorColor = BlockColor.Red;
        private int _selectedDoorWidth = 1;
        private BlockColor _selectedBlockColor = BlockColor.Red;
        private BlockMovementConstraint _selectedBlockMovementConstraint = BlockMovementConstraint.Free;
        private BlockShapeData _selectedBlockShape;
        private readonly List<Vector2Int> _doorCellsBuffer = new List<Vector2Int>(8);

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
                EditorGUILayout.HelpBox("Önce bir LevelData seç veya yeni oluştur.", MessageType.Info);
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
            EditorGUILayout.Space(12f);
            DrawBlockList();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Color Block Jam - Data Driven Level Editor", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            LevelData next = (LevelData)EditorGUILayout.ObjectField("Active Level", _activeLevel, typeof(LevelData), false);
            if (EditorGUI.EndChangeCheck())
            {
                _activeLevel = next;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create New Level Asset", GUILayout.Height(24f)))
            {
                CreateNewLevelAsset();
            }

            if (_activeLevel != null && GUILayout.Button("Ping Asset", GUILayout.Height(24f), GUILayout.Width(120f)))
            {
                EditorGUIUtility.PingObject(_activeLevel);
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
                ClampLevelDataToGrid();
                SaveLevelChange();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAvailabilitySettings()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Available Colors & Shapes", EditorStyles.boldLabel);

            DrawAvailableColors();

            SerializedObject so = new SerializedObject(_activeLevel);
            SerializedProperty shapesProperty = so.FindProperty(nameof(LevelData.availableShapes));
            if (shapesProperty == null)
            {
                EditorGUILayout.HelpBox("availableShapes alanı bulunamadı.", MessageType.Error);
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(shapesProperty, true);
            if (EditorGUI.EndChangeCheck())
            {
                RecordLevelChange("Edit Available Shapes");
                so.ApplyModifiedProperties();
                SaveLevelChange();
            }

            EditorGUILayout.EndVertical();
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
                    _selectedBlockShape = (BlockShapeData)EditorGUILayout.ObjectField(
                        "Shape",
                        _selectedBlockShape,
                        typeof(BlockShapeData),
                        false);

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

        private void DrawBlockList()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Blocks In Level", EditorStyles.boldLabel);

            for (int i = 0; i < _activeLevel.blocks.Count; i++)
            {
                BlockData block = _activeLevel.blocks[i];
                Vector2Int size = block.GetSize();

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

            if (!_activeLevel.availableShapes.Contains(_selectedBlockShape))
            {
                _activeLevel.availableShapes.Add(_selectedBlockShape);
            }

            BlockData block = new BlockData
            {
                position = anchorCell,
                shape = _selectedBlockShape,
                movementConstraint = _selectedBlockMovementConstraint,
                colorType = _selectedBlockColor
            };

            _activeLevel.blocks.Add(block);
            SaveLevelChange();
        }

        private bool CanPlaceShape(Vector2Int anchorPosition, BlockShapeData shape)
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

                if (_activeLevel.blockedCells.Contains(worldCell))
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
            if (_activeLevel.blockedCells.Contains(cell))
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
            if (_activeLevel.blockedCells.Contains(cell))
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
            for (int i = 0; i < _activeLevel.doors.Count; i++)
            {
                if (DoesDoorContainCell(_activeLevel.doors[i], cell))
                {
                    return i;
                }
            }

            return -1;
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
            for (int i = 0; i < _activeLevel.blocks.Count; i++)
            {
                BlockData block = _activeLevel.blocks[i];
                if (IsCellInsideBlock(block, cell))
                {
                    return i;
                }
            }

            return -1;
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
            for (int i = _activeLevel.blocks.Count - 1; i >= 0; i--)
            {
                BlockData block = _activeLevel.blocks[i];
                if (IsCellInsideBlock(block, cell))
                {
                    _activeLevel.blocks.RemoveAt(i);
                }
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

        private void ClampLevelDataToGrid()
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
                BlockData block = _activeLevel.blocks[i];
                if (!IsBlockWithinGrid(block, grid))
                {
                    _activeLevel.blocks.RemoveAt(i);
                }
            }
        }

        private static bool IsCellInsideBlock(BlockData block, Vector2Int worldCell)
        {
            Vector2Int[] localCells = block.GetLocalCells();
            for (int i = 0; i < localCells.Length; i++)
            {
                Vector2Int cell = block.position + localCells[i];
                if (cell == worldCell)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBlockWithinGrid(BlockData block, Vector2Int gridSize)
        {
            Vector2Int[] localCells = block.GetLocalCells();
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

        private bool DoesDoorContainCell(DoorData door, Vector2Int cell)
        {
            _doorCellsBuffer.Clear();
            if (!DoorOpeningMap.TryCollectDoorCells(door, _activeLevel.gridDimensions, _doorCellsBuffer))
            {
                return false;
            }

            for (int i = 0; i < _doorCellsBuffer.Count; i++)
            {
                if (_doorCellsBuffer[i] == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private void RecordLevelChange(string action)
        {
            Undo.RecordObject(_activeLevel, action);
        }

        private void SaveLevelChange()
        {
            EditorUtility.SetDirty(_activeLevel);
            Repaint();
        }

        private void CreateNewLevelAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create LevelData",
                "LevelData",
                "asset",
                "Yeni level asset kaydet");

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            LevelData newLevel = CreateInstance<LevelData>();
            AssetDatabase.CreateAsset(newLevel, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _activeLevel = newLevel;
            EditorGUIUtility.PingObject(_activeLevel);
        }
    }
}
