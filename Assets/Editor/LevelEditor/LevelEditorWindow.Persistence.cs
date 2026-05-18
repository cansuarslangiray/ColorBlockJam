using System;
using System.Collections.Generic;
using System.IO;
using Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace Editor.LevelEditor
{
    public partial class LevelEditorWindow
    {
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
            MarkProjectJsonCacheDirty();

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

        private void LoadLevelFromJsonPath(string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
            {
                LoadLevelFromJsonAsset(null);
                return;
            }

            TextAsset jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            LoadLevelFromJsonAsset(jsonAsset);
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

        private void EnsureProjectJsonCache()
        {
            if (!_projectJsonCacheDirty)
            {
                return;
            }

            _projectJsonCacheDirty = false;
            _projectJsonPaths.Clear();
            _projectJsonIndexByPath.Clear();

            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    _projectJsonPaths.Add(path);
                }
            }

            _projectJsonPaths.Sort(StringComparer.Ordinal);

            _projectJsonOptions = new string[_projectJsonPaths.Count + 1];
            _projectJsonOptions[0] = "None";
            for (int i = 0; i < _projectJsonPaths.Count; i++)
            {
                string path = _projectJsonPaths[i];
                int optionIndex = i + 1;
                _projectJsonOptions[optionIndex] = path;
                _projectJsonIndexByPath[path] = optionIndex;
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
