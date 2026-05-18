using System;
using System.Collections.Generic;
using System.IO;
using Runtime.Data;
using Runtime.Domain.Enums;
using UnityEditor;
using UnityEngine;

namespace Editor.AssetTools
{
    public class BlockShapeAssetCreatorWindow : EditorWindow
    {
        private const string ShapeJsonFolder = "Assets/Data/BlockShapes";
        private const string LevelJsonFolder = "Assets/Data/LevelsJson";

        private string _shapeName = "Shape_1x1";
        private int _width = 1;
        private int _height = 1;
        private bool _useCustomShape;
        private int _customGridWidth = 4;
        private int _customGridHeight = 4;
        private readonly HashSet<Vector2Int> _selectedCustomCells = new HashSet<Vector2Int> { Vector2Int.zero };
        private readonly List<string> _shapeJsonPaths = new List<string>(128);
        private readonly Dictionary<string, int> _shapeJsonIndexByPath = new Dictionary<string, int>(128, StringComparer.Ordinal);
        private string[] _shapeJsonOptions = { "New Shape" };
        private int _selectedShapeOptionIndex;
        private string _selectedShapeJsonPath = string.Empty;

        [MenuItem("Tools/Color Block Jam/Block Shape JSON Creator")]
        private static void OpenWindow()
        {
            BlockShapeAssetCreatorWindow window = GetWindow<BlockShapeAssetCreatorWindow>();
            window.titleContent = new GUIContent("Shape JSON");
            window.minSize = new Vector2(400f, 200f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshShapeCatalog(forceAssetDatabaseRefresh: false);
            ResetShapeEditorState(CreateSuggestedShapeName());
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Block Shape JSON Creator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawShapeSelectionToolbar();
            EditorGUILayout.Space(4f);

            _shapeName = EditorGUILayout.TextField("Shape Name", _shapeName);
            _useCustomShape = EditorGUILayout.Toggle("Use Custom Shape", _useCustomShape);

            if (_useCustomShape)
            {
                DrawCustomShapeEditor();
            }
            else
            {
                _width = Mathf.Max(1, EditorGUILayout.IntField("Width", _width));
                _height = Mathf.Max(1, EditorGUILayout.IntField("Height", _height));
            }

            EditorGUILayout.Space();
            DrawSaveButtons();

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox("Shape dosyalari otomatik olarak shape klasorune kaydedilir.", MessageType.Info);
        }

        private void DrawShapeSelectionToolbar()
        {
            _selectedShapeOptionIndex = Mathf.Clamp(_selectedShapeOptionIndex, 0, _shapeJsonOptions.Length - 1);
            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUILayout.Popup("Shape Library", _selectedShapeOptionIndex, _shapeJsonOptions);
            if (EditorGUI.EndChangeCheck())
            {
                HandleShapeSelectionChanged(nextIndex);
            }

            if (GUILayout.Button("Refresh", GUILayout.Width(72f)))
            {
                RefreshCatalogAndRestoreSelection(forceAssetDatabaseRefresh: true);
            }

            if (GUILayout.Button("New", GUILayout.Width(56f)))
            {
                _selectedShapeOptionIndex = 0;
                _selectedShapeJsonPath = string.Empty;
                ResetShapeEditorState(CreateSuggestedShapeName());
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSaveButtons()
        {
            if (_selectedShapeOptionIndex > 0 && !string.IsNullOrWhiteSpace(_selectedShapeJsonPath))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Update Selected Shape JSON", GUILayout.Height(32f)))
                {
                    SaveShapeJson(updateSelected: true);
                }

                if (GUILayout.Button("Create New Shape JSON", GUILayout.Height(32f)))
                {
                    SaveShapeJson(updateSelected: false);
                }

                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Rename Selected Shape (File + Level Refs)", GUILayout.Height(28f)))
                {
                    RenameSelectedShape();
                }

                return;
            }

            if (GUILayout.Button("Create Shape JSON", GUILayout.Height(32f)))
            {
                SaveShapeJson(updateSelected: false);
            }
        }

        private void DrawCustomShapeEditor()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Custom Shape Grid", EditorStyles.boldLabel);

            int nextGridWidth = Mathf.Max(1, EditorGUILayout.IntField("Grid Width", _customGridWidth));
            int nextGridHeight = Mathf.Max(1, EditorGUILayout.IntField("Grid Height", _customGridHeight));
            if (nextGridWidth != _customGridWidth || nextGridHeight != _customGridHeight)
            {
                _customGridWidth = nextGridWidth;
                _customGridHeight = nextGridHeight;
                ClampCustomSelectionToGrid();
            }

            if (GUILayout.Button("Reset To Single Cell", GUILayout.Height(22f)))
            {
                _selectedCustomCells.Clear();
                _selectedCustomCells.Add(Vector2Int.zero);
            }

            if (GUILayout.Button("Fill Rectangle", GUILayout.Height(22f)))
            {
                _selectedCustomCells.Clear();
                for (int y = 0; y < _customGridHeight; y++)
                {
                    for (int x = 0; x < _customGridWidth; x++)
                    {
                        _selectedCustomCells.Add(new Vector2Int(x, y));
                    }
                }
            }

            EditorGUILayout.Space(4f);
            DrawCustomGridButtons();

            if (_selectedCustomCells.Count == 0)
            {
                EditorGUILayout.HelpBox("En az bir hücre seçmelisin.", MessageType.Warning);
            }
        }

        private void DrawCustomGridButtons()
        {
            for (int y = _customGridHeight - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < _customGridWidth; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    bool selected = _selectedCustomCells.Contains(cell);

                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = selected ? new Color(0.2f, 0.75f, 0.3f) : Color.white;

                    if (GUILayout.Button(selected ? "■" : " ", GUILayout.Width(26f), GUILayout.Height(24f)))
                    {
                        ToggleCustomCell(cell);
                    }

                    GUI.backgroundColor = previous;
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void ToggleCustomCell(Vector2Int cell)
        {
            if (_selectedCustomCells.Contains(cell))
            {
                _selectedCustomCells.Remove(cell);
            }
            else
            {
                _selectedCustomCells.Add(cell);
            }
        }

        private void SaveShapeJson(bool updateSelected)
        {
            if (string.IsNullOrWhiteSpace(_shapeName))
            {
                EditorUtility.DisplayDialog("Invalid Name", "Shape adı boş olamaz.", "OK");
                return;
            }

            if (_useCustomShape && _selectedCustomCells.Count == 0)
            {
                EditorUtility.DisplayDialog("Invalid Shape", "Custom shape için en az bir hücre seçmelisin.", "OK");
                return;
            }

            EnsureFolder(ShapeJsonFolder);
            var shape = BuildShapeFromEditorState();

            string targetPath;
            if (updateSelected && !string.IsNullOrWhiteSpace(_selectedShapeJsonPath))
            {
                targetPath = _selectedShapeJsonPath;
            }
            else
            {
                targetPath = $"{ShapeJsonFolder}/{shape.ShapeKey}.json";
                if (File.Exists(targetPath) || AssetDatabase.LoadAssetAtPath<TextAsset>(targetPath) != null)
                {
                    bool overwrite = EditorUtility.DisplayDialog(
                        "Shape Already Exists",
                        $"{shape.ShapeKey}.json zaten var. Uzerine yazilsin mi?",
                        "Overwrite",
                        "Cancel");
                    if (!overwrite)
                    {
                        return;
                    }
                }
            }

            if (updateSelected)
            {
                string selectedFileShapeKey = Path.GetFileNameWithoutExtension(targetPath);
                shape.shapeKey = selectedFileShapeKey;
                shape.Sanitize();
                _shapeName = selectedFileShapeKey;
            }

            string json = BlockShapeJsonSerialization.Serialize(shape, true);
            File.WriteAllText(targetPath, json);
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            RefreshShapeCatalog(forceAssetDatabaseRefresh: false);
            TrySelectShapeByPath(targetPath);

            TextAsset jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(targetPath);
            if (jsonAsset != null)
            {
                EditorGUIUtility.PingObject(jsonAsset);
            }
        }

        private void RenameSelectedShape()
        {
            if (_selectedShapeOptionIndex <= 0 || string.IsNullOrWhiteSpace(_selectedShapeJsonPath))
            {
                EditorUtility.DisplayDialog("No Selection", "Yeniden adlandirmak icin once bir shape sec.", "OK");
                return;
            }

            string oldPath = _selectedShapeJsonPath;
            string oldShapeKey = Path.GetFileNameWithoutExtension(oldPath);
            string newShapeKey = string.IsNullOrWhiteSpace(_shapeName) ? string.Empty : _shapeName.Trim();

            if (string.IsNullOrWhiteSpace(newShapeKey))
            {
                EditorUtility.DisplayDialog("Invalid Name", "Yeni shape adı boş olamaz.", "OK");
                return;
            }

            if (newShapeKey.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                EditorUtility.DisplayDialog("Invalid Name", "Shape adı geçersiz karakter içeriyor.", "OK");
                return;
            }

            if (string.Equals(oldShapeKey, newShapeKey, StringComparison.Ordinal))
            {
                SaveShapeJson(updateSelected: true);
                return;
            }

            string newPath = $"{ShapeJsonFolder}/{newShapeKey}.json";
            if (File.Exists(newPath) || AssetDatabase.LoadAssetAtPath<TextAsset>(newPath) != null)
            {
                EditorUtility.DisplayDialog("Name Conflict", $"{newShapeKey}.json zaten var. Farkli bir isim sec.", "OK");
                return;
            }

            string moveError = AssetDatabase.MoveAsset(oldPath, newPath);
            if (!string.IsNullOrWhiteSpace(moveError))
            {
                EditorUtility.DisplayDialog("Rename Failed", moveError, "OK");
                return;
            }

            var shape = BuildShapeFromEditorState();
            shape.shapeKey = newShapeKey;
            shape.Sanitize();

            string json = BlockShapeJsonSerialization.Serialize(shape, true);
            File.WriteAllText(newPath, json);
            AssetDatabase.ImportAsset(newPath, ImportAssetOptions.ForceUpdate);

            int updatedLevelCount = RenameShapeReferencesInLevelJsons(oldShapeKey, newShapeKey);
            AssetDatabase.Refresh();

            _selectedShapeJsonPath = newPath;
            _shapeName = newShapeKey;
            RefreshShapeCatalog(forceAssetDatabaseRefresh: false);
            TrySelectShapeByPath(newPath);

            EditorUtility.DisplayDialog(
                "Shape Renamed",
                $"'{oldShapeKey}' -> '{newShapeKey}'\nGuncellenen level json sayisi: {updatedLevelCount}",
                "OK");
        }

        private BlockShapeJsonData BuildShapeFromEditorState()
        {
            List<Vector2Int> normalizedCustomCells = _useCustomShape
                ? GetNormalizedCustomCells()
                : new List<Vector2Int> { Vector2Int.zero };
            int shapeWidth = _width;
            int shapeHeight = _height;
            if (_useCustomShape)
            {
                Vector2Int customSize = ComputeCustomShapeSize(normalizedCustomCells);
                shapeWidth = customSize.x;
                shapeHeight = customSize.y;
            }

            var shape = new BlockShapeJsonData
            {
                shapeKey = _shapeName.Trim(),
                width = shapeWidth,
                height = shapeHeight,
                useCustomShape = _useCustomShape,
                customCells = normalizedCustomCells
            };
            shape.Sanitize();
            return shape;
        }

        private void HandleShapeSelectionChanged(int nextIndex)
        {
            nextIndex = Mathf.Clamp(nextIndex, 0, _shapeJsonOptions.Length - 1);
            _selectedShapeOptionIndex = nextIndex;

            if (nextIndex == 0)
            {
                _selectedShapeJsonPath = string.Empty;
                ResetShapeEditorState(CreateSuggestedShapeName());
                return;
            }

            string selectedPath = _shapeJsonPaths[nextIndex - 1];
            LoadShapeFromJsonPath(selectedPath);
        }

        private void RefreshShapeCatalog(bool forceAssetDatabaseRefresh)
        {
            if (forceAssetDatabaseRefresh)
            {
                AssetDatabase.Refresh();
            }

            EnsureFolder(ShapeJsonFolder);

            _shapeJsonPaths.Clear();
            _shapeJsonIndexByPath.Clear();
            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { ShapeJsonFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _shapeJsonPaths.Add(path);
            }

            _shapeJsonPaths.Sort(StringComparer.Ordinal);

            _shapeJsonOptions = new string[_shapeJsonPaths.Count + 1];
            _shapeJsonOptions[0] = "New Shape";
            for (int i = 0; i < _shapeJsonPaths.Count; i++)
            {
                string path = _shapeJsonPaths[i];
                string fallback = Path.GetFileNameWithoutExtension(path);
                _shapeJsonOptions[i + 1] = fallback;
                _shapeJsonIndexByPath[path] = i + 1;
            }
        }

        private void RefreshCatalogAndRestoreSelection(bool forceAssetDatabaseRefresh)
        {
            string previousPath = _selectedShapeJsonPath;
            int previousOptionIndex = _selectedShapeOptionIndex;
            bool hadSelectedShape = previousOptionIndex > 0 && !string.IsNullOrWhiteSpace(previousPath);

            RefreshShapeCatalog(forceAssetDatabaseRefresh);

            if (hadSelectedShape && TrySelectShapeByPath(previousPath))
            {
                return;
            }

            if (_shapeJsonPaths.Count == 0)
            {
                _selectedShapeOptionIndex = 0;
                _selectedShapeJsonPath = string.Empty;
                ResetShapeEditorState(CreateSuggestedShapeName());
                return;
            }

            if (!hadSelectedShape)
            {
                _selectedShapeOptionIndex = 0;
                _selectedShapeJsonPath = string.Empty;
                return;
            }

            int fallbackOptionIndex = Mathf.Clamp(previousOptionIndex, 1, _shapeJsonPaths.Count);
            _selectedShapeOptionIndex = fallbackOptionIndex;
            LoadShapeFromJsonPath(_shapeJsonPaths[fallbackOptionIndex - 1]);
        }

        private bool TrySelectShapeByPath(string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                return false;
            }

            if (!_shapeJsonIndexByPath.TryGetValue(jsonPath, out int optionIndex))
            {
                return false;
            }

            _selectedShapeOptionIndex = optionIndex;
            LoadShapeFromJsonPath(_shapeJsonPaths[optionIndex - 1]);
            return true;
        }

        private void LoadShapeFromJsonPath(string jsonPath)
        {
            TextAsset jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            string fallbackShapeKey = Path.GetFileNameWithoutExtension(jsonPath);
            BlockShapeJsonData shape = jsonAsset != null
                ? BlockShapeJsonSerialization.Deserialize(jsonAsset.text, fallbackShapeKey)
                : new BlockShapeJsonData { shapeKey = fallbackShapeKey };

            shape.Sanitize();
            _selectedShapeJsonPath = jsonPath;
            _shapeName = fallbackShapeKey;
            _width = Mathf.Max(1, shape.width);
            _height = Mathf.Max(1, shape.height);
            _useCustomShape = shape.useCustomShape;

            _selectedCustomCells.Clear();
            List<Vector2Int> cells = shape.customCells != null
                ? shape.customCells
                : new List<Vector2Int> { Vector2Int.zero };
            for (int i = 0; i < cells.Count; i++)
            {
                _selectedCustomCells.Add(cells[i]);
            }

            if (_selectedCustomCells.Count == 0)
            {
                _selectedCustomCells.Add(Vector2Int.zero);
            }

            RefreshCustomGridBoundsFromSelection();
        }

        private int RenameShapeReferencesInLevelJsons(string oldShapeKey, string newShapeKey)
        {
            if (string.IsNullOrWhiteSpace(oldShapeKey) ||
                string.IsNullOrWhiteSpace(newShapeKey) ||
                string.Equals(oldShapeKey, newShapeKey, StringComparison.Ordinal))
            {
                return 0;
            }

            if (!AssetDatabase.IsValidFolder(LevelJsonFolder))
            {
                return 0;
            }

            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { LevelJsonFolder });
            int updatedFileCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TextAsset levelJson = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (levelJson == null || string.IsNullOrWhiteSpace(levelJson.text))
                {
                    continue;
                }

                LevelJsonData levelData;
                try
                {
                    levelData = JsonUtility.FromJson<LevelJsonData>(levelJson.text);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ShapeRename] '{path}' parse edilemedi: {ex.Message}");
                    continue;
                }

                if (levelData == null)
                {
                    continue;
                }

                bool changed = false;

                if (levelData.availableShapeKeys != null)
                {
                    for (int keyIndex = 0; keyIndex < levelData.availableShapeKeys.Count; keyIndex++)
                    {
                        string key = levelData.availableShapeKeys[keyIndex];
                        if (!string.Equals(key, oldShapeKey, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        levelData.availableShapeKeys[keyIndex] = newShapeKey;
                        changed = true;
                    }
                }

                if (levelData.blocks != null)
                {
                    for (int blockIndex = 0; blockIndex < levelData.blocks.Count; blockIndex++)
                    {
                        LevelJsonBlockData block = levelData.blocks[blockIndex];
                        if (!string.Equals(block.shapeKey, oldShapeKey, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        block.shapeKey = newShapeKey;
                        block.blockType = BlockShapeTypeUtility.FromShapeKey(newShapeKey);
                        levelData.blocks[blockIndex] = block;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    continue;
                }

                string updatedJson = LevelJsonSerialization.Serialize(levelData, true);
                File.WriteAllText(path, updatedJson);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                updatedFileCount++;
            }

            return updatedFileCount;
        }

        private void ResetShapeEditorState(string nextShapeName)
        {
            _shapeName = string.IsNullOrWhiteSpace(nextShapeName) ? "Shape_New" : nextShapeName;
            _width = 1;
            _height = 1;
            _useCustomShape = false;
            _customGridWidth = 4;
            _customGridHeight = 4;
            _selectedCustomCells.Clear();
            _selectedCustomCells.Add(Vector2Int.zero);
        }

        private string CreateSuggestedShapeName()
        {
            string baseName = "Shape_New";
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _shapeJsonPaths.Count; i++)
            {
                usedNames.Add(Path.GetFileNameWithoutExtension(_shapeJsonPaths[i]));
            }

            if (!usedNames.Contains(baseName))
            {
                return baseName;
            }

            int suffix = 1;
            while (usedNames.Contains(baseName + "_" + suffix))
            {
                suffix++;
            }

            return baseName + "_" + suffix;
        }

        private void RefreshCustomGridBoundsFromSelection()
        {
            int maxX = 0;
            int maxY = 0;
            foreach (Vector2Int cell in _selectedCustomCells)
            {
                if (cell.x > maxX)
                {
                    maxX = cell.x;
                }

                if (cell.y > maxY)
                {
                    maxY = cell.y;
                }
            }

            _customGridWidth = Mathf.Max(1, maxX + 1);
            _customGridHeight = Mathf.Max(1, maxY + 1);
        }

        private void ClampCustomSelectionToGrid()
        {
            var clamped = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in _selectedCustomCells)
            {
                if (cell.x < 0 || cell.y < 0 || cell.x >= _customGridWidth || cell.y >= _customGridHeight)
                {
                    continue;
                }

                clamped.Add(cell);
            }

            _selectedCustomCells.Clear();
            foreach (Vector2Int cell in clamped)
            {
                _selectedCustomCells.Add(cell);
            }

            if (_selectedCustomCells.Count == 0)
            {
                _selectedCustomCells.Add(Vector2Int.zero);
            }
        }

        private static Vector2Int ComputeCustomShapeSize(List<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return Vector2Int.one;
            }

            int maxX = 0;
            int maxY = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];
                if (cell.x > maxX)
                {
                    maxX = cell.x;
                }

                if (cell.y > maxY)
                {
                    maxY = cell.y;
                }
            }

            return new Vector2Int(maxX + 1, maxY + 1);
        }

        private List<Vector2Int> GetNormalizedCustomCells()
        {
            if (_selectedCustomCells.Count == 0)
            {
                return new List<Vector2Int> { Vector2Int.zero };
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            foreach (Vector2Int cell in _selectedCustomCells)
            {
                if (cell.x < minX)
                {
                    minX = cell.x;
                }

                if (cell.y < minY)
                {
                    minY = cell.y;
                }
            }

            List<Vector2Int> normalizedCells = new List<Vector2Int>(_selectedCustomCells.Count);
            foreach (Vector2Int cell in _selectedCustomCells)
            {
                normalizedCells.Add(new Vector2Int(cell.x - minX, cell.y - minY));
            }

            normalizedCells.Sort((a, b) =>
            {
                int yCompare = a.y.CompareTo(b.y);
                return yCompare != 0 ? yCompare : a.x.CompareTo(b.x);
            });

            return normalizedCells;
        }

        private void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] split = folderPath.Split('/');
            string current = split[0];

            for (int i = 1; i < split.Length; i++)
            {
                string next = current + "/" + split[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, split[i]);
                }

                current = next;
            }
        }
    }
}
