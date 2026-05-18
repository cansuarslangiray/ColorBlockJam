using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Runtime.Data
{
    [CreateAssetMenu(fileName = "LevelCollection", menuName = "ColorBlockJam/LevelCollection")]
    public class LevelCollection : ScriptableObject
    {
        private const string DefaultLevelJsonFolder = "Assets/Data/LevelsJson";
        private const string DefaultShapeJsonFolder = "Assets/Data/BlockShapes";

        [Header("Json Source")]
        [SerializeField] private List<TextAsset> levelJsonFiles = new();
        [SerializeField] private List<TextAsset> blockShapeJsonFiles = new();
        [Header("Editor Sync")]
        [SerializeField] private bool autoRefreshOnValidate = true;

        [System.NonSerialized] private List<LevelJsonData> _runtimeLevels;
        [System.NonSerialized] private BlockShapeRegistry _runtimeShapeRegistry;
        [System.NonSerialized] private bool _isRuntimeCacheReady;

        public BlockShapeRegistry RuntimeShapeRegistry
        {
            get
            {
                EnsureRuntimeCache();
                return _runtimeShapeRegistry;
            }
        }

        public int Count
        {
            get
            {
                EnsureRuntimeCache();
                return _runtimeLevels.Count;
            }
        }

        public bool TryGetLevelAt(int index, out LevelJsonData levelData)
        {
            EnsureRuntimeCache();

            if ((uint)index >= (uint)_runtimeLevels.Count)
            {
                levelData = null;
                return false;
            }

            levelData = _runtimeLevels[index];
            return true;
        }

        private void OnEnable()
        {
            InvalidateRuntimeCache();
        }

        private void OnValidate()
        {
            InvalidateRuntimeCache();
#if UNITY_EDITOR
            if (autoRefreshOnValidate && !Application.isPlaying)
            {
                RefreshEditorAssetLists(logResult: false);
            }
#endif
        }

        private void InvalidateRuntimeCache()
        {
            _isRuntimeCacheReady = false;
            _runtimeLevels = null;
            _runtimeShapeRegistry = null;
        }

        private void EnsureRuntimeCache()
        {
            if (_isRuntimeCacheReady && _runtimeLevels != null)
            {
                return;
            }

            _runtimeLevels = new List<LevelJsonData>();
            _isRuntimeCacheReady = true;
            _runtimeShapeRegistry = BlockShapeRegistry.FromJsonAssets(blockShapeJsonFiles);

            if (levelJsonFiles == null || levelJsonFiles.Count == 0)
            {
                return;
            }

            foreach (var levelJson in levelJsonFiles)
            {
                if (!levelJson)
                {
                    continue;
                }

                var levelData = LevelJsonSerialization.Deserialize(levelJson.text, levelJson.name);
                if (levelData == null)
                {
                    continue;
                }

                _runtimeLevels.Add(levelData);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Refresh Level Collection")]
        public void RefreshEditorAssetListsFromContextMenu()
        {
            RefreshEditorAssetLists(logResult: true);
        }

        public LevelCollectionRefreshReport RefreshEditorAssetLists(bool logResult = false)
        {
            List<TextAsset> refreshedLevelJsonFiles = LoadJsonAssets(DefaultLevelJsonFolder);
            List<TextAsset> refreshedShapeJsonFiles = LoadJsonAssets(DefaultShapeJsonFolder);

            SortLevelJsonAssets(refreshedLevelJsonFiles);
            SortAssetsByPath(refreshedShapeJsonFiles);

            bool hasLevelChanges = !HasSameAssetOrder(levelJsonFiles, refreshedLevelJsonFiles);
            bool hasShapeChanges = !HasSameAssetOrder(blockShapeJsonFiles, refreshedShapeJsonFiles);

            if (hasLevelChanges)
            {
                levelJsonFiles = refreshedLevelJsonFiles;
            }

            if (hasShapeChanges)
            {
                blockShapeJsonFiles = refreshedShapeJsonFiles;
            }

            InvalidateRuntimeCache();

            if (hasLevelChanges || hasShapeChanges)
            {
                EditorUtility.SetDirty(this);
            }

            LevelCollectionRefreshReport report = BuildRefreshReport(levelJsonFiles, blockShapeJsonFiles);
            if (logResult)
            {
                Debug.Log(report.ToConsoleMessage(name), this);
            }

            return report;
        }

        private static List<TextAsset> LoadJsonAssets(string folderPath)
        {
            var result = new List<TextAsset>();
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                return result;
            }

            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { folderPath });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TextAsset jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (jsonAsset != null)
                {
                    result.Add(jsonAsset);
                }
            }

            return result;
        }

        private static void SortLevelJsonAssets(List<TextAsset> assets)
        {
            if (assets == null || assets.Count <= 1)
            {
                return;
            }

            assets.Sort((left, right) =>
            {
                int leftOrder = ResolveLevelOrder(left);
                int rightOrder = ResolveLevelOrder(right);

                int orderCompare = leftOrder.CompareTo(rightOrder);
                if (orderCompare != 0)
                {
                    return orderCompare;
                }

                string leftPath = left != null ? AssetDatabase.GetAssetPath(left) : string.Empty;
                string rightPath = right != null ? AssetDatabase.GetAssetPath(right) : string.Empty;
                return string.CompareOrdinal(leftPath, rightPath);
            });
        }

        private static int ResolveLevelOrder(TextAsset levelAsset)
        {
            if (levelAsset == null)
            {
                return int.MaxValue;
            }

            LevelJsonData levelData = LevelJsonSerialization.Deserialize(levelAsset.text, levelAsset.name);
            if (levelData != null)
            {
                return Mathf.Max(1, levelData.levelNumber);
            }

            return ParseNumberFromLabel(levelAsset.name);
        }

        private static void SortAssetsByPath(List<TextAsset> assets)
        {
            if (assets == null || assets.Count <= 1)
            {
                return;
            }

            assets.Sort((left, right) =>
            {
                string leftPath = left != null ? AssetDatabase.GetAssetPath(left) : string.Empty;
                string rightPath = right != null ? AssetDatabase.GetAssetPath(right) : string.Empty;
                return string.CompareOrdinal(leftPath, rightPath);
            });
        }

        private static bool HasSameAssetOrder(IReadOnlyList<TextAsset> left, IReadOnlyList<TextAsset> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static int ParseNumberFromLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return int.MaxValue;
            }

            int parsed = 0;
            bool hasDigits = false;
            for (int i = 0; i < label.Length; i++)
            {
                char c = label[i];
                if (c < '0' || c > '9')
                {
                    continue;
                }

                hasDigits = true;
                parsed = (parsed * 10) + (c - '0');
            }

            if (!hasDigits)
            {
                return int.MaxValue;
            }

            return Mathf.Max(1, parsed);
        }

        private static LevelCollectionRefreshReport BuildRefreshReport(
            IReadOnlyList<TextAsset> levels,
            IReadOnlyList<TextAsset> shapes)
        {
            var issues = new List<string>(32);
            var resolvedLevels = new List<LevelJsonData>(levels?.Count ?? 0);
            var sourceNames = new List<string>(levels?.Count ?? 0);
            var resolvedShapeAssets = new List<TextAsset>(shapes?.Count ?? 0);

            if (shapes != null)
            {
                for (int i = 0; i < shapes.Count; i++)
                {
                    if (shapes[i] != null)
                    {
                        resolvedShapeAssets.Add(shapes[i]);
                    }
                }
            }

            BlockShapeRegistry shapeRegistry = BlockShapeRegistry.FromJsonAssets(resolvedShapeAssets);

            if (levels != null)
            {
                for (int i = 0; i < levels.Count; i++)
                {
                    TextAsset levelAsset = levels[i];
                    string sourceName = levelAsset != null ? levelAsset.name : $"Index_{i}";
                    LevelJsonData levelData = levelAsset != null
                        ? LevelJsonSerialization.Deserialize(levelAsset.text, sourceName)
                        : null;

                    resolvedLevels.Add(levelData);
                    sourceNames.Add(sourceName);

                    LevelJsonValidator.ValidateLevel(levelData, shapeRegistry, issues, sourceName);
                }
            }

            LevelJsonValidator.ValidateCollection(resolvedLevels, issues, sourceNames);

            return new LevelCollectionRefreshReport(
                levels?.Count ?? 0,
                shapes?.Count ?? 0,
                issues);
        }

        public readonly struct LevelCollectionRefreshReport
        {
            public readonly int LevelCount;
            public readonly int ShapeCount;
            public readonly IReadOnlyList<string> ValidationIssues;

            public int ValidationIssueCount => ValidationIssues?.Count ?? 0;
            public bool HasValidationIssues => ValidationIssueCount > 0;

            public LevelCollectionRefreshReport(int levelCount, int shapeCount, IReadOnlyList<string> validationIssues)
            {
                LevelCount = levelCount;
                ShapeCount = shapeCount;
                ValidationIssues = validationIssues ?? Array.Empty<string>();
            }

            public string ToConsoleMessage(string collectionName)
            {
                string safeName = string.IsNullOrWhiteSpace(collectionName) ? "LevelCollection" : collectionName;
                string header =
                    $"[{safeName}] Refresh complete. Levels={LevelCount}, Shapes={ShapeCount}, ValidationIssues={ValidationIssueCount}";

                if (!HasValidationIssues)
                {
                    return header;
                }

                const int maxVisibleIssues = 10;
                int visibleCount = Mathf.Min(maxVisibleIssues, ValidationIssueCount);
                var messageLines = new List<string>(visibleCount + 2) { header };
                for (int i = 0; i < visibleCount; i++)
                {
                    messageLines.Add($"- {ValidationIssues[i]}");
                }

                if (ValidationIssueCount > visibleCount)
                {
                    messageLines.Add($"... +{ValidationIssueCount - visibleCount} more issue(s)");
                }

                return string.Join("\n", messageLines);
            }
        }
#endif
    }
}
