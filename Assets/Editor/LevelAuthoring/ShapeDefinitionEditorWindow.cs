using System;
using System.Collections.Generic;
using Editor.DataPipeline;
using Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace Editor.LevelAuthoring
{
    public sealed class ShapeDefinitionEditorWindow : EditorWindow
    {
        private enum WorkspaceTab
        {
            EditExisting,
            CreateNew
        }

        private const float GridCellSize = 24f;
        private const int MaxGridDimension = 12;
        private const int MinGridDimension = 1;
        private static readonly Color FilledCellColor = new(0.22f, 0.56f, 0.94f, 1f);
        private static readonly Color EmptyCellColor = new(0.88f, 0.9f, 0.93f, 1f);

        private readonly List<BlockShapeDefinition> _shapeAssets = new();
        private readonly List<Vector2Int> _editCells = new();
        private string[] _shapeOptions = { "None" };
        private int _selectedShapeIndex = -1;

        private string _newShapeKey = "Shape_Custom_1";
        private bool _newShapeUseRectangle = true;
        private int _newRectangleWidth = 2;
        private int _newRectangleHeight = 2;
        private int _newCustomGridWidth = 4;
        private int _newCustomGridHeight = 4;
        private readonly List<Vector2Int> _newCustomCells = new() { Vector2Int.zero };

        private int _editRectangleWidth = 2;
        private int _editRectangleHeight = 2;
        private int _editGridWidth = 4;
        private int _editGridHeight = 4;
        private string _editShapeKey = string.Empty;
        private Vector2 _scrollPosition;
        private WorkspaceTab _workspaceTab;
        private bool _showCoordinateLabels = true;
        private bool _isDirty;

        [MenuItem("Tools/Color Block Jam/Shape Editor")]
        private static void OpenWindow()
        {
            var window = GetWindow<ShapeDefinitionEditorWindow>();
            window.titleContent = new GUIContent("Shape Editor");
            window.minSize = new Vector2(760f, 540f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged += HandleProjectChanged;
            RefreshShapeAssetCache();
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= HandleProjectChanged;
        }

        private void OnGUI()
        {
            DrawToolbar();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawWorkspaceTabs();
            EditorGUILayout.Space(8f);

            if (_workspaceTab == WorkspaceTab.EditExisting)
            {
                DrawShapeSelection();
                EditorGUILayout.Space(8f);
                DrawActiveShapeEditor();
            }
            else
            {
                DrawShapeCreationPanel();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(72f)))
            {
                RefreshShapeAssetCache();
            }

            if (GUILayout.Button("Save & Sync", EditorStyles.toolbarButton, GUILayout.Width(104f)))
            {
                SaveCurrentShapeIfDirty();
                LevelContentPipelineTool.SyncCollectionFromAssets();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawWorkspaceTabs()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Workspace", EditorStyles.boldLabel);
            _workspaceTab = (WorkspaceTab)GUILayout.Toolbar((int)_workspaceTab, new[] { "Edit Existing", "Create New" });
            EditorGUILayout.EndVertical();
        }

        private void DrawShapeSelection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Shape Selection", EditorStyles.boldLabel);

            var previousIndex = _selectedShapeIndex;
            var activeOption = _selectedShapeIndex + 1;
            var nextOption = EditorGUILayout.Popup("Selected Shape", activeOption, _shapeOptions);
            if (nextOption != activeOption)
            {
                SaveCurrentShapeIfDirty();
                _selectedShapeIndex = nextOption - 1;
                LoadSelectedShapeToEditor();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(_isDirty ? "Unsaved" : "Saved",
                    _isDirty ? EditorStyles.boldLabel : EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_selectedShapeIndex < 0 || _selectedShapeIndex >= _shapeAssets.Count))
                {
                    if (GUILayout.Button("Ping", GUILayout.Height(22f), GUILayout.Width(72f)))
                    {
                        EditorGUIUtility.PingObject(_shapeAssets[_selectedShapeIndex]);
                    }

                    if (GUILayout.Button("Save", GUILayout.Height(22f), GUILayout.Width(84f)))
                    {
                        SaveCurrentShapeIfDirty(force: true);
                    }
                }
            }

            if (_shapeAssets.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No shape assets found. Switch to 'Create New' and create your first shape.",
                    MessageType.Info);
            }

            if (previousIndex != _selectedShapeIndex)
            {
                GUI.FocusControl(string.Empty);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActiveShapeEditor()
        {
            if (_selectedShapeIndex < 0 || _selectedShapeIndex >= _shapeAssets.Count)
            {
                return;
            }

            var shape = _shapeAssets[_selectedShapeIndex];
            if (!shape)
            {
                return;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Shape Editor", EditorStyles.boldLabel);

            var nextShapeKey = EditorGUILayout.TextField("Shape ID", _editShapeKey);
            if (!string.Equals(nextShapeKey, _editShapeKey, StringComparison.Ordinal))
            {
                _editShapeKey = nextShapeKey;
                MarkDirty();
            }

            DrawEditGridControls();
            DrawEditableGrid(_editGridWidth, _editGridHeight, _editCells, MarkDirty, _showCoordinateLabels);

            EditorGUILayout.Space(6f);
            DrawShapeStats(_editShapeKey, _editCells);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Revert", GUILayout.Height(22f)))
            {
                LoadSelectedShapeToEditor();
            }

            if (GUILayout.Button("Save", GUILayout.Height(22f)))
            {
                SaveCurrentShapeIfDirty(force: true);
            }

            if (GUILayout.Button("Save & Sync", GUILayout.Height(22f)))
            {
                SaveCurrentShapeIfDirty(force: true);
                LevelContentPipelineTool.SyncCollectionFromAssets();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawEditGridControls()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Grid Controls", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _editGridWidth = Mathf.Clamp(EditorGUILayout.IntField("Grid Width", _editGridWidth), MinGridDimension, MaxGridDimension);
                _editGridHeight = Mathf.Clamp(EditorGUILayout.IntField("Grid Height", _editGridHeight), MinGridDimension, MaxGridDimension);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _editRectangleWidth = Mathf.Clamp(EditorGUILayout.IntField("Rect W", _editRectangleWidth), 1, MaxGridDimension);
                _editRectangleHeight = Mathf.Clamp(EditorGUILayout.IntField("Rect H", _editRectangleHeight), 1, MaxGridDimension);
                if (GUILayout.Button("Fill Rect", GUILayout.Width(88f), GUILayout.Height(20f)))
                {
                    FillEditCellsWithRectangle(_editRectangleWidth, _editRectangleHeight);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _showCoordinateLabels = EditorGUILayout.ToggleLeft("Show Coordinates", _showCoordinateLabels, GUILayout.Width(140f));
                if (GUILayout.Button("Fit Grid", GUILayout.Width(80f), GUILayout.Height(20f)))
                {
                    FitEditGridToCells();
                }

                if (GUILayout.Button("Normalize", GUILayout.Width(80f), GUILayout.Height(20f)))
                {
                    NormalizeCellBuffer(_editCells);
                    _isDirty = true;
                }

                if (GUILayout.Button("Clear", GUILayout.Width(80f), GUILayout.Height(20f)))
                {
                    _editCells.Clear();
                    _editCells.Add(Vector2Int.zero);
                    _isDirty = true;
                }
            }

            ClampCellsToGrid(_editCells, _editGridWidth, _editGridHeight);
        }

        private void DrawShapeCreationPanel()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Create New Shape", EditorStyles.boldLabel);

            _newShapeKey = EditorGUILayout.TextField("Shape ID", _newShapeKey);
            _newShapeUseRectangle = GUILayout.Toolbar(_newShapeUseRectangle ? 0 : 1, new[] { "Rectangle", "Custom Cells" }) == 0;

            if (_newShapeUseRectangle)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _newRectangleWidth = Mathf.Clamp(EditorGUILayout.IntField("Width", _newRectangleWidth), 1, MaxGridDimension);
                    _newRectangleHeight = Mathf.Clamp(EditorGUILayout.IntField("Height", _newRectangleHeight), 1, MaxGridDimension);
                }

                DrawPreviewGrid(_newRectangleWidth, _newRectangleHeight);
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _newCustomGridWidth =
                        Mathf.Clamp(EditorGUILayout.IntField("Grid Width", _newCustomGridWidth), MinGridDimension, MaxGridDimension);
                    _newCustomGridHeight =
                        Mathf.Clamp(EditorGUILayout.IntField("Grid Height", _newCustomGridHeight), MinGridDimension, MaxGridDimension);
                }

                ClampCellsToGrid(_newCustomCells, _newCustomGridWidth, _newCustomGridHeight);
                DrawEditableGrid(_newCustomGridWidth, _newCustomGridHeight, _newCustomCells, () => { }, showCoordinates: true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Shape", GUILayout.Height(24f)))
                {
                    CreateShapeAsset();
                }

                if (GUILayout.Button("Reset Draft", GUILayout.Height(24f)))
                {
                    ResetCreateShapeDraft();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewGrid(int width, int height)
        {
            var previewCells = new List<Vector2Int>(width * height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    previewCells.Add(new Vector2Int(x, y));
                }
            }

            DrawEditableGrid(width, height, previewCells, null, showCoordinates: true, readOnly: true);
        }

        private void DrawEditableGrid(int width, int height, List<Vector2Int> cells, Action onCellToggled,
            bool showCoordinates = false, bool readOnly = false)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var previousBackground = GUI.backgroundColor;
            if (showCoordinates)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(28f);
                for (var x = 0; x < width; x++)
                {
                    GUILayout.Label(x.ToString(), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(GridCellSize));
                }

                EditorGUILayout.EndHorizontal();
            }

            for (var y = height - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                if (showCoordinates)
                {
                    GUILayout.Label(y.ToString(), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(24f), GUILayout.Height(GridCellSize));
                }

                for (var x = 0; x < width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    var filled = cells.Contains(cell);
                    GUI.backgroundColor = filled ? FilledCellColor : EmptyCellColor;

                    using (new EditorGUI.DisabledScope(readOnly))
                    {
                        if (GUILayout.Button(filled ? "X" : string.Empty, GUILayout.Width(GridCellSize), GUILayout.Height(GridCellSize)))
                        {
                            if (filled)
                            {
                                cells.Remove(cell);
                            }
                            else
                            {
                                cells.Add(cell);
                            }

                            if (cells.Count == 0)
                            {
                                cells.Add(Vector2Int.zero);
                            }

                            NormalizeCellBuffer(cells);
                            onCellToggled?.Invoke();
                        }
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            GUI.backgroundColor = previousBackground;
        }

        private static void DrawShapeStats(string shapeKey, List<Vector2Int> cells)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Cell Count", (cells?.Count ?? 0).ToString());
                EditorGUILayout.LabelField("Shape Key", string.IsNullOrWhiteSpace(shapeKey) ? "none" : shapeKey);
            }

            EditorGUILayout.LabelField("Cells", FormatCellList(cells), EditorStyles.miniLabel);
        }

        private static string FormatCellList(List<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return "none";
            }

            const int maxVisibleCells = 10;
            var labels = new List<string>(Mathf.Min(maxVisibleCells, cells.Count));
            for (var i = 0; i < cells.Count && i < maxVisibleCells; i++)
            {
                labels.Add($"({cells[i].x},{cells[i].y})");
            }

            if (cells.Count > maxVisibleCells)
            {
                labels.Add($"+{cells.Count - maxVisibleCells} more");
            }

            return string.Join(", ", labels);
        }

        private void MarkDirty()
        {
            _isDirty = true;
        }

        private void LoadSelectedShapeToEditor()
        {
            _editCells.Clear();
            _isDirty = false;

            if (_selectedShapeIndex < 0 || _selectedShapeIndex >= _shapeAssets.Count)
            {
                return;
            }

            var shape = _shapeAssets[_selectedShapeIndex];
            if (!shape)
            {
                return;
            }

            var localCells = shape.GetLocalCells();
            if (localCells != null)
            {
                for (var i = 0; i < localCells.Length; i++)
                {
                    _editCells.Add(localCells[i]);
                }
            }

            NormalizeCellBuffer(_editCells);
            _editShapeKey = shape.ShapeKey;
            FitEditGridToCells();
        }

        private void FitEditGridToCells()
        {
            var maxX = 0;
            var maxY = 0;
            for (var i = 0; i < _editCells.Count; i++)
            {
                var cell = _editCells[i];
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y > maxY) maxY = cell.y;
            }

            _editGridWidth = Mathf.Clamp(maxX + 2, MinGridDimension, MaxGridDimension);
            _editGridHeight = Mathf.Clamp(maxY + 2, MinGridDimension, MaxGridDimension);
        }

        private void FillEditCellsWithRectangle(int width, int height)
        {
            _editCells.Clear();
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    _editCells.Add(new Vector2Int(x, y));
                }
            }

            NormalizeCellBuffer(_editCells);
            _isDirty = true;
            FitEditGridToCells();
        }

        private void SaveCurrentShapeIfDirty(bool force = false)
        {
            if (!force && !_isDirty)
            {
                return;
            }

            if (_selectedShapeIndex < 0 || _selectedShapeIndex >= _shapeAssets.Count)
            {
                return;
            }

            var shape = _shapeAssets[_selectedShapeIndex];
            if (!shape)
            {
                return;
            }

            var effectiveShapeKey = string.IsNullOrWhiteSpace(_editShapeKey) ? shape.name : _editShapeKey.Trim();
            Undo.RecordObject(shape, "Edit Shape Definition");
            shape.ApplyImportedData(effectiveShapeKey, _editCells);
            EditorUtility.SetDirty(shape);
            AssetDatabase.SaveAssets();
            BlockShapePrefabPipeline.SyncForShapeChange();
            _isDirty = false;
            RefreshShapeAssetCache();
            SelectShape(shape);
        }

        private void CreateShapeAsset()
        {
            var shapeKey = (_newShapeKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(shapeKey))
            {
                EditorUtility.DisplayDialog("Invalid Shape ID", "Shape ID cannot be empty.", "OK");
                return;
            }

            EnsureFolderExists(LevelContentPipelineTool.ShapeDefinitionFolder);

            var shapeCells = BuildNewShapeCellData();
            if (shapeCells.Count == 0)
            {
                EditorUtility.DisplayDialog("Invalid Shape", "Select at least one cell.", "OK");
                return;
            }

            var assetPath =
                AssetDatabase.GenerateUniqueAssetPath($"{LevelContentPipelineTool.ShapeDefinitionFolder}/{shapeKey}.asset");
            var shape = CreateInstance<BlockShapeDefinition>();
            shape.ApplyImportedData(shapeKey, shapeCells);
            AssetDatabase.CreateAsset(shape, assetPath);
            EditorUtility.SetDirty(shape);
            AssetDatabase.SaveAssets();
            BlockShapePrefabPipeline.SyncForShapeChange();

            RefreshShapeAssetCache();
            SelectShape(shape);

            LevelContentPipelineTool.SyncCollectionFromAssets();
            EditorGUIUtility.PingObject(shape);
        }

        private List<Vector2Int> BuildNewShapeCellData()
        {
            var cells = new List<Vector2Int>();
            if (_newShapeUseRectangle)
            {
                for (var y = 0; y < _newRectangleHeight; y++)
                {
                    for (var x = 0; x < _newRectangleWidth; x++)
                    {
                        cells.Add(new Vector2Int(x, y));
                    }
                }
            }
            else
            {
                cells.AddRange(_newCustomCells);
            }

            NormalizeCellBuffer(cells);
            return cells;
        }

        private void ResetCreateShapeDraft()
        {
            _newShapeKey = "Shape_Custom_1";
            _newShapeUseRectangle = true;
            _newRectangleWidth = 2;
            _newRectangleHeight = 2;
            _newCustomGridWidth = 4;
            _newCustomGridHeight = 4;
            _newCustomCells.Clear();
            _newCustomCells.Add(Vector2Int.zero);
        }

        private void RefreshShapeAssetCache()
        {
            _shapeAssets.Clear();
            var guids = AssetDatabase.FindAssets($"t:{nameof(BlockShapeDefinition)}",
                new[] { LevelContentPipelineTool.ShapeDefinitionFolder });
            Array.Sort(guids, StringComparer.Ordinal);

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var shape = AssetDatabase.LoadAssetAtPath<BlockShapeDefinition>(path);
                if (shape)
                {
                    _shapeAssets.Add(shape);
                }
            }

            _shapeAssets.Sort((left, right) => string.CompareOrdinal(left.ShapeKey, right.ShapeKey));
            _shapeOptions = new string[_shapeAssets.Count + 1];
            _shapeOptions[0] = "None";
            for (var i = 0; i < _shapeAssets.Count; i++)
            {
                _shapeOptions[i + 1] = _shapeAssets[i].ShapeKey;
            }

            if (_shapeAssets.Count == 0)
            {
                _selectedShapeIndex = -1;
                _editCells.Clear();
                return;
            }

            if (_selectedShapeIndex >= _shapeAssets.Count)
            {
                _selectedShapeIndex = _shapeAssets.Count - 1;
            }

            if (_selectedShapeIndex < 0)
            {
                _selectedShapeIndex = 0;
            }

            LoadSelectedShapeToEditor();
        }

        private void SelectShape(BlockShapeDefinition shape)
        {
            if (!shape)
            {
                return;
            }

            for (var i = 0; i < _shapeAssets.Count; i++)
            {
                if (_shapeAssets[i] == shape)
                {
                    _selectedShapeIndex = i;
                    LoadSelectedShapeToEditor();
                    return;
                }
            }
        }

        private static void ClampCellsToGrid(List<Vector2Int> cells, int width, int height)
        {
            if (cells == null)
            {
                return;
            }

            for (var i = cells.Count - 1; i >= 0; i--)
            {
                var cell = cells[i];
                if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height)
                {
                    cells.RemoveAt(i);
                }
            }

            if (cells.Count == 0)
            {
                cells.Add(Vector2Int.zero);
            }
        }

        private static void NormalizeCellBuffer(List<Vector2Int> cells)
        {
            if (cells == null)
            {
                return;
            }

            var unique = new HashSet<Vector2Int>(cells);
            cells.Clear();
            cells.AddRange(unique);
            cells.Sort((left, right) =>
            {
                var yCompare = left.y.CompareTo(right.y);
                return yCompare != 0 ? yCompare : left.x.CompareTo(right.x);
            });
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

        private void HandleProjectChanged()
        {
            RefreshShapeAssetCache();
            Repaint();
        }
    }
}
