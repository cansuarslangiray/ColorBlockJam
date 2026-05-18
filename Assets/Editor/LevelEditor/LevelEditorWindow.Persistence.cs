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

        private void CreateNewLevelJson(int levelNumber)
        {
            levelNumber = Math.Max(1, levelNumber);
            _newLevelNumber = levelNumber;

            if (!AssetDatabase.IsValidFolder(LevelJsonFolder))
            {
                EditorUtility.DisplayDialog(
                    "Level Folder Missing",
                    $"Level JSON klasoru bulunamadi: {LevelJsonFolder}",
                    "Tamam");
                return;
            }

            string levelKey = BuildLevelKey(levelNumber);
            string path = BuildLevelJsonPath(levelNumber);
            if (File.Exists(path) || AssetDatabase.LoadAssetAtPath<TextAsset>(path) != null)
            {
                EditorUtility.DisplayDialog(
                    "Level Already Exists",
                    $"{levelKey}.json zaten olusturulmus. Lutfen farkli bir level numarasi gir.",
                    "Tamam");
                return;
            }

            _activeLevel = new LevelJsonData
            {
                levelKey = levelKey,
                levelNumber = levelNumber
            };
            _activeLevel.Sanitize();
            MarkGridLookupCacheDirty();

            _activeLevelJsonPath = path;
            WriteActiveLevelToJson();
            AssetDatabase.Refresh();
            MarkProjectJsonCacheDirty();

            LoadLevelFromJsonPath(_activeLevelJsonPath);
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
            _newLevelNumber = Math.Max(1, _activeLevel.levelNumber);
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
            AssetDatabase.ImportAsset(_activeLevelJsonPath);
            if (_activeLevelJson == null)
            {
                _activeLevelJson = AssetDatabase.LoadAssetAtPath<TextAsset>(_activeLevelJsonPath);
            }
        }

        private void EnsureShapeRegistryLoaded(bool forceReload = false)
        {
            if (_shapeRegistry != null && !_shapeRegistryCacheDirty && !forceReload)
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
            RebuildShapeOptionCache();
            _shapeRegistryCacheDirty = false;
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

            if (AssetDatabase.IsValidFolder(LevelJsonFolder))
            {
                string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { LevelJsonFolder });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        _projectJsonPaths.Add(path);
                    }
                }
            }

            SortLevelJsonPaths(_projectJsonPaths);

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

        private void RefreshProjectLevelCollections()
        {
            string[] collectionGuids = AssetDatabase.FindAssets("t:LevelCollection");
            if (collectionGuids == null || collectionGuids.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Level Collection Bulunamadi",
                    "Projede LevelCollection asset'i bulunamadı.",
                    "Tamam");
                return;
            }

            int refreshedCollectionCount = 0;
            int totalValidationIssueCount = 0;
            var summaryLines = new List<string>(collectionGuids.Length + 4);

            for (int i = 0; i < collectionGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(collectionGuids[i]);
                LevelCollection levelCollection = AssetDatabase.LoadAssetAtPath<LevelCollection>(path);
                if (levelCollection == null)
                {
                    continue;
                }

                LevelCollection.LevelCollectionRefreshReport report = levelCollection.RefreshEditorAssetLists(logResult: false);
                refreshedCollectionCount++;
                totalValidationIssueCount += report.ValidationIssueCount;
                summaryLines.Add(
                    $"- {levelCollection.name}: Levels={report.LevelCount}, Shapes={report.ShapeCount}, Issues={report.ValidationIssueCount}");

                if (report.HasValidationIssues)
                {
                    const int maxIssuePreview = 3;
                    int visibleIssueCount = Mathf.Min(maxIssuePreview, report.ValidationIssueCount);
                    for (int issueIndex = 0; issueIndex < visibleIssueCount; issueIndex++)
                    {
                        summaryLines.Add($"  * {report.ValidationIssues[issueIndex]}");
                    }

                    if (report.ValidationIssueCount > visibleIssueCount)
                    {
                        summaryLines.Add($"  * ... +{report.ValidationIssueCount - visibleIssueCount} more issue(s)");
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            MarkProjectJsonCacheDirty();
            MarkShapeRegistryCacheDirty();
            EnsureProjectJsonCache();
            EnsureShapeRegistryLoaded(true);

            if (_activeLevelJson != null)
            {
                LoadLevelFromJsonAsset(_activeLevelJson);
            }

            string title = totalValidationIssueCount == 0
                ? "Level Collection Refreshed"
                : "Level Collection Refreshed (Validation Warnings)";
            string detail =
                $"Guncellenen koleksiyon: {refreshedCollectionCount}\nToplam validation warning: {totalValidationIssueCount}";
            if (summaryLines.Count > 0)
            {
                detail += $"\n\n{string.Join("\n", summaryLines)}";
            }

            Debug.Log($"[LevelEditor] {title}\n{detail}");
            EditorUtility.DisplayDialog(title, detail, "Tamam");
        }

        private static string BuildLevelKey(int levelNumber)
        {
            return $"Level{Math.Max(1, levelNumber)}";
        }

        private static string BuildLevelJsonPath(int levelNumber)
        {
            return $"{LevelJsonFolder}/{BuildLevelKey(levelNumber)}.json";
        }

        private static void SortLevelJsonPaths(List<string> levelJsonPaths)
        {
            if (levelJsonPaths == null || levelJsonPaths.Count <= 1)
            {
                return;
            }

            levelJsonPaths.Sort((left, right) =>
            {
                int leftOrder = ResolveLevelOrderFromPath(left);
                int rightOrder = ResolveLevelOrderFromPath(right);

                int orderCompare = leftOrder.CompareTo(rightOrder);
                if (orderCompare != 0)
                {
                    return orderCompare;
                }

                return string.CompareOrdinal(left, right);
            });
        }

        private static int ResolveLevelOrderFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return int.MaxValue;
            }

            string fileName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return int.MaxValue;
            }

            int parsed = 0;
            bool hasDigit = false;
            for (int i = 0; i < fileName.Length; i++)
            {
                char c = fileName[i];
                if (c < '0' || c > '9')
                {
                    continue;
                }

                hasDigit = true;
                parsed = (parsed * 10) + (c - '0');
            }

            if (!hasDigit)
            {
                return int.MaxValue;
            }

            return Mathf.Max(1, parsed);
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

        private void RebuildShapeOptionCache()
        {
            _shapeOptionIndexByKey.Clear();

            if (_shapeRegistry == null || _shapeRegistry.Shapes.Count == 0)
            {
                _shapeOptionLabels = Array.Empty<string>();
                return;
            }

            IReadOnlyList<BlockShapeJsonData> shapes = _shapeRegistry.Shapes;
            _shapeOptionLabels = new string[shapes.Count];

            for (int i = 0; i < shapes.Count; i++)
            {
                BlockShapeJsonData shape = shapes[i];
                string key = shape != null ? shape.ShapeKey : string.Empty;
                _shapeOptionLabels[i] = string.IsNullOrWhiteSpace(key) ? $"Shape_{i}" : key;

                if (!string.IsNullOrWhiteSpace(key) && !_shapeOptionIndexByKey.ContainsKey(key))
                {
                    _shapeOptionIndexByKey.Add(key, i);
                }
            }
        }

    }
}
