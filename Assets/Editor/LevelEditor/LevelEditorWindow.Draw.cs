using System;
using System.Collections.Generic;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Helpers;
using UnityEditor;
using UnityEngine;

namespace Editor.LevelEditor
{
    public partial class LevelEditorWindow
    {
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
            EditorGUILayout.LabelField("Available Colors", EditorStyles.boldLabel);

            DrawAvailableColors();
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Available Shapes listesi bloklardan otomatik senkronlanir. Manuel tik secimi kaldirildi.",
                MessageType.None);

            EditorGUILayout.EndVertical();
        }

        private void DrawAvailableColors()
        {
            EditorGUILayout.LabelField("Available Colors");

            if (AllBlockColors.Length == 0)
            {
                return;
            }

            const int rowsPerColumn = 6;
            int columnCount = Mathf.CeilToInt(AllBlockColors.Length / (float)rowsPerColumn);

            EditorGUILayout.BeginHorizontal();
            for (int column = 0; column < columnCount; column++)
            {
                EditorGUILayout.BeginVertical(GUILayout.MaxWidth(170f));

                int startIndex = column * rowsPerColumn;
                int endIndex = Mathf.Min(startIndex + rowsPerColumn, AllBlockColors.Length);
                for (int i = startIndex; i < endIndex; i++)
                {
                    BlockColor color = AllBlockColors[i];
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

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEditModeToolbar()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Grid Edit Mode", EditorStyles.boldLabel);
            _editMode = (LevelEditorMode)GUILayout.Toolbar((int)_editMode, EditModeLabels);
            EditorGUILayout.EndVertical();
        }

        private void DrawModeSettings()
        {
            EditorGUILayout.BeginVertical("box");
            switch (_editMode)
            {
                case LevelEditorMode.BlockedCells:
                    EditorGUILayout.HelpBox("Grid hücresine tıklayarak blocked cell ekle/çıkar. Kenar hucreler border alani olarak ayrilir.", MessageType.None);
                    break;

                case LevelEditorMode.Doors:
                    _selectedDoorColor = DrawColorPicker("Door Color", _selectedDoorColor);
                    EditorGUILayout.HelpBox(
                        "Door sadece kenar hücrelerine (kose haric) eklenir. Kenar hücreye tıklayarak door ekle/çıkar.",
                        MessageType.None);
                    break;

                case LevelEditorMode.Blocks:
                    _selectedBlockColor = DrawColorPicker("Block Color", _selectedBlockColor);
                    _selectedBlockMovementConstraint = (BlockMovementConstraint)EditorGUILayout.EnumPopup(
                        "Movement",
                        _selectedBlockMovementConstraint);
                    DrawShapePicker();

                    EditorGUILayout.HelpBox(
                        "Grid hücresine tıklayınca blok eklenir. Aynı anchor hücresine tekrar tıklarsan blok silinir. Kenar border alanina blok konulmaz.",
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
            int selectedIndex = 0;
            if (_selectedBlockShape != null &&
                _shapeOptionIndexByKey.TryGetValue(_selectedBlockShape.ShapeKey, out int resolvedIndex))
            {
                selectedIndex = Mathf.Clamp(resolvedIndex, 0, _shapeOptionLabels.Length - 1);
            }

            int nextIndex = EditorGUILayout.Popup("Shape", selectedIndex, _shapeOptionLabels);
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

            EnsureAvailableColorOptionCache(availableColors);
            int selectedIndex = Mathf.Max(0, availableColors.IndexOf(fallback));
            selectedIndex = EditorGUILayout.Popup(label, selectedIndex, _availableColorOptionLabels);
            return availableColors[selectedIndex];
        }

        private void EnsureAvailableColorOptionCache(List<BlockColor> availableColors)
        {
            int signature = availableColors.Count;
            for (int i = 0; i < availableColors.Count; i++)
            {
                signature = (signature * 31) ^ (int)availableColors[i];
            }

            if (signature == _availableColorOptionSignature)
            {
                return;
            }

            _availableColorOptionSignature = signature;
            _availableColorOptionLabels = new string[availableColors.Count];
            for (int i = 0; i < availableColors.Count; i++)
            {
                _availableColorOptionLabels[i] = availableColors[i].ToString();
            }
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
                    bool isFrameCell = IsFrameCell(cell);
                    Rect cellRect = new Rect(
                        rect.x + (x * GridCellPixelSize),
                        rect.y + ((grid.y - 1 - y) * GridCellPixelSize),
                        GridCellPixelSize - 2f,
                        GridCellPixelSize - 2f);

                    Color previous = GUI.backgroundColor;
                    ResolveCellVisual(cell, isFrameCell, out Color cellColor, out string label);
                    GUI.backgroundColor = cellColor;

                    bool allowClick = _editMode == LevelEditorMode.Doors || !isFrameCell;
                    if (allowClick && GUI.Button(cellRect, label))
                    {
                        HandleCellClick(cell);
                    }
                    else if (!allowClick)
                    {
                        GUI.Box(cellRect, label);
                    }

                    GUI.backgroundColor = previous;
                }
            }

            EditorGUILayout.Space(4f);
            DrawGridLegend();
            EditorGUILayout.EndVertical();
        }

        private void ResolveCellVisual(Vector2Int cell, bool isFrameCell, out Color color, out string label)
        {
            if (_blockedCellLookup.Contains(cell))
            {
                color = BlockedCellColor;
                label = "X";
                return;
            }

            if (_blockIndexByCell.TryGetValue(cell, out int blockIndex))
            {
                BlockColor blockColor = _activeLevel.blocks[blockIndex].colorType;
                color = BlockColorPalette.GetColor(blockColor);
                color.a = 0.9f;
                label = "B";
                return;
            }

            if (_doorIndexByCell.TryGetValue(cell, out int doorIndex))
            {
                BlockColor doorColor = _activeLevel.doors[doorIndex].colorType;
                color = Color.Lerp(BlockColorPalette.GetColor(doorColor), Color.white, 0.35f);
                label = "D";
                return;
            }

            color = isFrameCell ? FrameCellColor : EmptyCellColor;
            label = string.Empty;
        }

        private void DrawGridLegend()
        {
            EditorGUILayout.LabelField("Legend:");
            EditorGUILayout.LabelField("X = Blocked, D = Door, B = Block coverage");
            EditorGUILayout.LabelField("Border Alani = Dis halka hucreleri (oynanamaz)");
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

        private void DrawBlockList()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Blocks In Level", EditorStyles.boldLabel);

            if (_activeLevel.blocks.Count == 0)
            {
                EditorGUILayout.LabelField("Bu levelde henuz blok yok.");
                EditorGUILayout.EndVertical();
                return;
            }

            for (int i = 0; i < _activeLevel.blocks.Count; i++)
            {
                LevelJsonBlockData block = _activeLevel.blocks[i];
                Vector2Int size = block.GetSize(_shapeRegistry);
                string resolvedShapeKey = block.ResolveShapeKey();

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    $"#{i} Pos:{block.position} Shape:{resolvedShapeKey} Size:{size.x}x{size.y} Color:{block.colorType} Move:{block.movementConstraint}");

                if (GUILayout.Button("Delete", GUILayout.Width(64f)))
                {
                    RecordLevelChange("Delete Block");
                    _activeLevel.blocks.RemoveAt(i);
                    SaveLevelChange();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                DrawBlockShapeEditor(i, block);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBlockShapeEditor(int blockIndex, LevelJsonBlockData block)
        {
            if (_shapeRegistry == null || _shapeRegistry.Shapes.Count == 0)
            {
                EditorGUILayout.HelpBox("Shape registry bulunamadi.", MessageType.Warning);
                return;
            }

            IReadOnlyList<BlockShapeJsonData> shapes = _shapeRegistry.Shapes;
            string currentShapeKey = block.ResolveShapeKey();
            bool hasShapeMatch = _shapeOptionIndexByKey.TryGetValue(currentShapeKey, out int selectedIndex);
            selectedIndex = Mathf.Clamp(selectedIndex, 0, _shapeOptionLabels.Length - 1);

            if (!hasShapeMatch && !string.IsNullOrWhiteSpace(currentShapeKey))
            {
                EditorGUILayout.HelpBox(
                    $"Mevcut shape '{currentShapeKey}' registry'de yok. Asagidan secip guncelleyebilirsin.",
                    MessageType.Warning);
            }

            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup("Shape", selectedIndex, _shapeOptionLabels);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            if (nextIndex < 0 || nextIndex >= shapes.Count)
            {
                return;
            }

            BlockShapeJsonData nextShape = shapes[nextIndex];
            string nextShapeKey = nextShape != null ? nextShape.ShapeKey : string.Empty;
            if (string.IsNullOrWhiteSpace(nextShapeKey) ||
                string.Equals(nextShapeKey, currentShapeKey, StringComparison.Ordinal))
            {
                return;
            }

            if (!CanReplaceBlockShape(blockIndex, block.position, nextShape))
            {
                ShowNotification(ShapeReplaceCollisionNotification);
                return;
            }

            RecordLevelChange("Edit Block Shape");
            block.shapeKey = nextShapeKey;
            block.blockType = BlockShapeTypeUtility.FromShapeKey(nextShapeKey);
            _activeLevel.blocks[blockIndex] = block;

            if (!ContainsShapeKey(_activeLevel.availableShapeKeys, nextShapeKey))
            {
                _activeLevel.availableShapeKeys.Add(nextShapeKey);
            }

            SaveLevelChange();
        }
    }
}
