using System.Collections.Generic;
using Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace Editor.AssetTools
{
    public class BlockShapeAssetCreatorWindow : EditorWindow
    {
        private string _shapeName = "Shape_1x1";
        private int _width = 1;
        private int _height = 1;
        private string _outputFolder = "Assets/Data/BlockShapes";
        private bool _useCustomShape;
        private int _customGridWidth = 4;
        private int _customGridHeight = 4;
        private readonly HashSet<Vector2Int> _selectedCustomCells = new HashSet<Vector2Int> { Vector2Int.zero };

        [MenuItem("Tools/Color Block Jam/Block Shape Creator")]
        private static void OpenWindow()
        {
            BlockShapeAssetCreatorWindow window = GetWindow<BlockShapeAssetCreatorWindow>();
            window.titleContent = new GUIContent("Shape Creator");
            window.minSize = new Vector2(400f, 200f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Block Shape Asset Creator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _shapeName = EditorGUILayout.TextField("Shape Name", _shapeName);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
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
            if (GUILayout.Button("Create Shape Asset", GUILayout.Height(32f)))
            {
                CreateShapeAsset();
            }
        }

        private void DrawCustomShapeEditor()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Custom Shape Grid", EditorStyles.boldLabel);

            _customGridWidth = Mathf.Max(1, EditorGUILayout.IntField("Grid Width", _customGridWidth));
            _customGridHeight = Mathf.Max(1, EditorGUILayout.IntField("Grid Height", _customGridHeight));

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

        private void CreateShapeAsset()
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

            EnsureFolder(_outputFolder);
            string assetPath = _outputFolder + "/" + _shapeName + ".asset";

            BlockShapeData shape = CreateInstance<BlockShapeData>();
            shape.width = _width;
            shape.height = _height;
            shape.useCustomShape = _useCustomShape;
            shape.customCells = GetNormalizedCustomCells();

            AssetDatabase.CreateAsset(shape, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(shape);
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
