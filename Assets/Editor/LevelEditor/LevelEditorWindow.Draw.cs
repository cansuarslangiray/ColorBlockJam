using System;
using System.Collections.Generic;
using Runtime.Data;
using Runtime.Domain.Enums;
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
                    bool allowClick = _editMode == LevelEditorMode.Doors || !IsFrameCell(cell);
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
    }
}
