using System;
using System.IO;
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
        private string _shapeCreatorKey = "Shape_Custom_1";
        private bool _shapeCreatorUseCustom = true;
        private int _shapeCreatorWidth = 2;
        private int _shapeCreatorHeight = 2;
        private int _shapeCreatorGridWidth = 4;
        private int _shapeCreatorGridHeight = 4;
        private readonly List<Vector2Int> _shapeCreatorCustomCells = new() { Vector2Int.zero };
        private bool _showShapeCreator = true;

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
            DrawAvailableShapes();
            EditorGUILayout.Space(4f);

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

        private void DrawAvailableShapes()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Available Shapes");

            if (_shapeRegistry == null || _shapeRegistry.Shapes.Count == 0)
            {
                EditorGUILayout.HelpBox("Shape JSON bulunamadi. Önce Shape JSON dosyalarını ekleyin.", MessageType.Info);
                return;
            }

            IReadOnlyList<BlockShapeJsonData> shapes = _shapeRegistry.Shapes;
            const int rowsPerColumn = 8;
            int columnCount = Mathf.CeilToInt(shapes.Count / (float)rowsPerColumn);

            EditorGUILayout.BeginHorizontal();
            for (int column = 0; column < columnCount; column++)
            {
                EditorGUILayout.BeginVertical(GUILayout.MaxWidth(170f));

                int startIndex = column * rowsPerColumn;
                int endIndex = Mathf.Min(startIndex + rowsPerColumn, shapes.Count);
                for (int i = startIndex; i < endIndex; i++)
                {
                    BlockShapeJsonData shape = shapes[i];
                    string shapeKey = shape != null ? shape.ShapeKey : string.Empty;
                    string shapeLabel = string.IsNullOrWhiteSpace(shapeKey) ? $"Shape_{i}" : shapeKey;

                    if (string.IsNullOrWhiteSpace(shapeKey))
                    {
                        EditorGUILayout.LabelField($"{shapeLabel} (invalid key)");
                        continue;
                    }

                    bool hasShape = ContainsShapeKey(_activeLevel.availableShapeKeys, shapeKey);
                    bool next = EditorGUILayout.ToggleLeft(shapeLabel, hasShape);

                    if (next == hasShape)
                    {
                        continue;
                    }

                    RecordLevelChange("Edit Available Shapes");
                    if (next)
                    {
                        if (!ContainsShapeKey(_activeLevel.availableShapeKeys, shapeKey))
                        {
                            _activeLevel.availableShapeKeys.Add(shapeKey);
                        }
                    }
                    else
                    {
                        _activeLevel.availableShapeKeys.RemoveAll(key => string.Equals(key, shapeKey, StringComparison.Ordinal));
                    }

                    SaveLevelChange();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawShapeCreator()
        {
            EditorGUILayout.Space(10f);
            _showShapeCreator = EditorGUILayout.BeginFoldoutHeaderGroup(_showShapeCreator, "Shape Oluşturma");

            if (!_showShapeCreator)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Yeni Shape Oluştur", EditorStyles.boldLabel);

            _shapeCreatorKey = EditorGUILayout.TextField("Shape Key", _shapeCreatorKey);
            _shapeCreatorUseCustom = EditorGUILayout.Toggle("useCustomShape", _shapeCreatorUseCustom);

            if (_shapeCreatorUseCustom)
            {
                _shapeCreatorGridWidth = Mathf.Max(1, EditorGUILayout.IntField("Grid Width", _shapeCreatorGridWidth));
                _shapeCreatorGridHeight = Mathf.Max(1, EditorGUILayout.IntField("Grid Height", _shapeCreatorGridHeight));
                DrawShapeCreatorGrid();
            }
            else
            {
                _shapeCreatorWidth = Mathf.Max(1, EditorGUILayout.IntField("Width", _shapeCreatorWidth));
                _shapeCreatorHeight = Mathf.Max(1, EditorGUILayout.IntField("Height", _shapeCreatorHeight));
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Shape JSON Oluştur", GUILayout.Height(26f), GUILayout.Width(170f)))
            {
                TryCreateShapeFromTool();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawShapeCreatorGrid()
        {
            EnsureShapeCreatorGridCellsValid();

            EditorGUILayout.LabelField("Shape Hücreleri (tıklayarak ekle/çıkar)");
            const float cellSize = 24f;
            int gridWidth = _shapeCreatorGridWidth;
            int gridHeight = _shapeCreatorGridHeight;
            Color previousColor = GUI.backgroundColor;

            for (int y = gridHeight - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < gridWidth; x++)
                {
                    Vector2Int cell = new(x, y);
                    bool isSelected = _shapeCreatorCustomCells.Contains(cell);
                    GUI.backgroundColor = isSelected
                        ? new Color(0.25f, 0.50f, 0.95f, 1f)
                        : new Color(0.86f, 0.88f, 0.92f, 1f);
                    bool clicked = GUILayout.Button(
                        isSelected ? "■" : string.Empty,
                        GUILayout.Width(cellSize),
                        GUILayout.Height(cellSize));

                    if (clicked)
                    {
                        if (!isSelected)
                        {
                            _shapeCreatorCustomCells.Add(cell);
                        }
                        else
                        {
                            _shapeCreatorCustomCells.Remove(cell);
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            GUI.backgroundColor = previousColor;
        }

        private void EnsureShapeCreatorGridCellsValid()
        {
            int gridWidth = Mathf.Max(1, _shapeCreatorGridWidth);
            int gridHeight = Mathf.Max(1, _shapeCreatorGridHeight);
            _shapeCreatorGridWidth = gridWidth;
            _shapeCreatorGridHeight = gridHeight;

            for (int i = _shapeCreatorCustomCells.Count - 1; i >= 0; i--)
            {
                Vector2Int cell = _shapeCreatorCustomCells[i];
                if (cell.x < 0 || cell.y < 0 || cell.x >= gridWidth || cell.y >= gridHeight)
                {
                    _shapeCreatorCustomCells.RemoveAt(i);
                }
            }

            if (_shapeCreatorCustomCells.Count == 0)
            {
                _shapeCreatorCustomCells.Add(Vector2Int.zero);
            }
        }

        private void TryCreateShapeFromTool()
        {
            if (_shapeCreatorKey == null)
            {
                return;
            }

            string shapeKey = _shapeCreatorKey.Trim();
            if (string.IsNullOrWhiteSpace(shapeKey))
            {
                EditorGUILayout.HelpBox("Shape Key boş olamaz.", MessageType.Warning);
                return;
            }

            if (!IsValidShapeKey(shapeKey))
            {
                EditorGUILayout.HelpBox("Shape Key yalnızca harf, rakam, '_' ve '-' içerebilir.", MessageType.Warning);
                return;
            }

            if (!AssetDatabase.IsValidFolder(ShapeJsonFolder))
            {
                EditorGUILayout.HelpBox($"Shape klasörü bulunamadı: {ShapeJsonFolder}", MessageType.Error);
                return;
            }

            string path = $"{ShapeJsonFolder}/{shapeKey}.json";
            if (File.Exists(path) &&
                !EditorUtility.DisplayDialog(
                    "Shape zaten var",
                    $"{shapeKey}.json zaten mevcut. Üzerine yazılsın mı?",
                    "Evet",
                    "Hayır"))
            {
                return;
            }

            List<Vector2Int> customCells = _shapeCreatorCustomCells;
            if (_shapeCreatorUseCustom && (customCells == null || customCells.Count == 0))
            {
                EditorGUILayout.HelpBox("Özel shape için en az bir hücre seçmelisin.", MessageType.Warning);
                return;
            }

            int width = _shapeCreatorUseCustom ? _shapeCreatorGridWidth : _shapeCreatorWidth;
            int height = _shapeCreatorUseCustom ? _shapeCreatorGridHeight : _shapeCreatorHeight;

            var shape = new BlockShapeJsonData
            {
                shapeKey = shapeKey,
                width = Mathf.Max(1, width),
                height = Mathf.Max(1, height),
                useCustomShape = _shapeCreatorUseCustom,
                customCells = _shapeCreatorUseCustom
                    ? new List<Vector2Int>(_shapeCreatorCustomCells)
                    : new List<Vector2Int> { Vector2Int.zero }
            };

            string json = BlockShapeJsonSerialization.Serialize(shape, true);
            File.WriteAllText(path, json);
            AssetDatabase.ImportAsset(path);

            MarkShapeRegistryCacheDirty();
            EnsureShapeRegistryLoaded(true);

            if (_activeLevel != null && !ContainsShapeKey(_activeLevel.availableShapeKeys, shapeKey))
            {
                RecordLevelChange("Edit Available Shapes");
                _activeLevel.availableShapeKeys.Add(shapeKey);
                SaveLevelChange();
            }

            _selectedBlockShape = _shapeRegistry != null && _shapeRegistry.TryResolveShape(shapeKey, out BlockShapeJsonData createdShape)
                ? createdShape
                : null;
            var createdAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (createdAsset != null)
            {
                EditorGUIUtility.PingObject(createdAsset);
            }
        }

        private static bool IsValidShapeKey(string shapeKey)
        {
            if (string.IsNullOrWhiteSpace(shapeKey))
            {
                return false;
            }

            for (int i = 0; i < shapeKey.Length; i++)
            {
                char c = shapeKey[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                {
                    return false;
                }
            }

            return true;
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
                    _selectedBlockFeatures = DrawBlockFeaturePopup("Block Features", _selectedBlockFeatures);
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
                color = ResolvePaletteColor(blockColor);
                color.a = 0.9f;
                label = "B";
                return;
            }

            if (_doorIndexByCell.TryGetValue(cell, out int doorIndex))
            {
                BlockColor doorColor = _activeLevel.doors[doorIndex].colorType;
                color = Color.Lerp(ResolvePaletteColor(doorColor), Color.white, 0.35f);
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
                    $"#{i} Pos:{block.position} Shape:{resolvedShapeKey} Size:{size.x}x{size.y} Color:{block.colorType} Features:{block.blockFeatures} Move:{block.movementConstraint}");

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
                DrawBlockFeatureEditor(i, block);
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

        private void DrawBlockFeatureEditor(int blockIndex, LevelJsonBlockData block)
        {
            EditorGUI.BeginChangeCheck();
            var nextFeatures = DrawBlockFeaturePopup("Block Features", block.blockFeatures);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            RecordLevelChange("Edit Block Features");
            block.blockFeatures = nextFeatures;
            block.NormalizeMovementConstraint();
            _activeLevel.blocks[blockIndex] = block;
            SaveLevelChange();
        }

        private static BlockFeature DrawBlockFeaturePopup(string label, BlockFeature currentFeature)
        {
            return (BlockFeature)EditorGUILayout.EnumPopup(label, ToSingleBlockFeature(currentFeature));
        }

        private static BlockFeature ToSingleBlockFeature(BlockFeature features)
        {
            if (features == BlockFeature.Default)
            {
                return BlockFeature.Default;
            }

            foreach (BlockFeature feature in Enum.GetValues(typeof(BlockFeature)))
            {
                if (feature == BlockFeature.Default)
                {
                    continue;
                }

                if ((features & feature) == feature)
                {
                    return feature;
                }
            }

            return BlockFeature.Default;
        }
    }
}
