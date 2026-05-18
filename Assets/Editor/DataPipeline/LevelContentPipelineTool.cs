using System;
using System.Collections.Generic;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEditor;
using UnityEngine;

namespace Editor.DataPipeline
{
    public static class LevelContentPipelineTool
    {
        public const string ShapeDefinitionFolder = "Assets/Data/BlockShapeDefinitions";
        public const string LevelDefinitionFolder = "Assets/Data/LevelDefinitions";
        public const string ShapeCatalogAssetPath = "Assets/Data/BlockShapeCatalog.asset";
        public const string LevelCollectionAssetPath = "Assets/Data/LevelCollection.asset";

        [MenuItem("Tools/Color Block Jam/Data/Sync Collection From Assets")]
        public static void SyncCollectionFromAssets()
        {
            SyncCollectionFromAssetsInternal(saveAssets: true, logSummary: true);
        }

        [MenuItem("Tools/Color Block Jam/Data/Create Empty Level Asset")]
        public static void CreateEmptyLevelAsset()
        {
            EnsureFolderExists(LevelDefinitionFolder);

            var levels = LoadLevelDefinitions();
            var templateLevel = ResolveTemplateLevel(levels);
            var nextLevelNumber = ResolveNextLevelNumber(levels);
            var levelDefinition = ScriptableObject.CreateInstance<LevelDefinition>();
            InitializeLevelDefinition(levelDefinition, templateLevel, nextLevelNumber);
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{LevelDefinitionFolder}/Level{nextLevelNumber}.asset");
            AssetDatabase.CreateAsset(levelDefinition, assetPath);
            EditorUtility.SetDirty(levelDefinition);

            SyncCollectionFromAssetsInternal(saveAssets: true, logSummary: false);
            Debug.Log(
                $"[LevelContentPipelineTool] Empty level created: {assetPath} (LevelNumber={nextLevelNumber})");
        }

        private static void SyncCollectionFromAssetsInternal(bool saveAssets, bool logSummary)
        {
            EnsureFolderExists(ShapeDefinitionFolder);
            EnsureFolderExists(LevelDefinitionFolder);

            var shapes = LoadShapeDefinitions();
            var levels = LoadLevelDefinitions();

            var shapeCatalog = GetOrCreateAsset<BlockShapeCatalog>(ShapeCatalogAssetPath);
            shapeCatalog.SetShapes(shapes);
            EditorUtility.SetDirty(shapeCatalog);

            var levelCollection = GetOrCreateAsset<LevelCollection>(LevelCollectionAssetPath);
            levelCollection.SetRuntimeSources(shapeCatalog, levels);
            EditorUtility.SetDirty(levelCollection);

            if (saveAssets)
            {
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.Refresh();

            if (logSummary)
            {
                Debug.Log(
                    $"[LevelContentPipelineTool] Sync complete. Shapes={shapes.Count}, Levels={levels.Count}");
            }
        }

        private static List<BlockShapeDefinition> LoadShapeDefinitions()
        {
            return LoadAssetsInFolder<BlockShapeDefinition>(
                ShapeDefinitionFolder,
                (left, right) => string.CompareOrdinal(left.ShapeKey, right.ShapeKey));
        }

        private static List<LevelDefinition> LoadLevelDefinitions()
        {
            return LoadAssetsInFolder<LevelDefinition>(
                LevelDefinitionFolder,
                (left, right) =>
                {
                    var levelCompare = left.levelNumber.CompareTo(right.levelNumber);
                    return levelCompare != 0
                        ? levelCompare
                        : string.CompareOrdinal(left.levelKey, right.levelKey);
                });
        }

        private static List<T> LoadAssetsInFolder<T>(string folderPath, Comparison<T> sortComparison)
            where T : UnityEngine.Object
        {
            var assets = new List<T>();
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                return assets;
            }

            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folderPath });
            Array.Sort(guids, StringComparer.Ordinal);

            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            if (sortComparison != null && assets.Count > 1)
            {
                assets.Sort(sortComparison);
            }

            return assets;
        }

        private static T GetOrCreateAsset<T>(string assetPath) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static void InitializeLevelDefinition(LevelDefinition levelDefinition, LevelDefinition templateLevel,
            int levelNumber)
        {
            levelDefinition.levelKey = $"Level{levelNumber}";
            levelDefinition.levelNumber = Mathf.Max(1, levelNumber);
            levelDefinition.timeLimit = templateLevel != null ? templateLevel.timeLimit : 60f;
            levelDefinition.gridDimensions = templateLevel != null ? templateLevel.gridDimensions : new Vector2Int(6, 8);
            levelDefinition.blockedCells = new List<Vector2Int>();
            levelDefinition.doors = new List<DoorData>();
            levelDefinition.blocks = new List<LevelBlockEntry>();

            if (templateLevel != null && templateLevel.availableColors != null && templateLevel.availableColors.Count > 0)
            {
                levelDefinition.availableColors = new List<BlockColor>(templateLevel.availableColors);
            }

            levelDefinition.Sanitize();
        }

        private static LevelDefinition ResolveTemplateLevel(IReadOnlyList<LevelDefinition> levels)
        {
            if (levels == null || levels.Count == 0)
            {
                return null;
            }

            for (var i = levels.Count - 1; i >= 0; i--)
            {
                if (levels[i] != null)
                {
                    return levels[i];
                }
            }

            return null;
        }

        private static int ResolveNextLevelNumber(IReadOnlyList<LevelDefinition> levels)
        {
            var maxLevelNumber = 0;
            if (levels != null)
            {
                for (var i = 0; i < levels.Count; i++)
                {
                    var level = levels[i];
                    if (level == null)
                    {
                        continue;
                    }

                    maxLevelNumber = Mathf.Max(maxLevelNumber, Mathf.Max(1, level.levelNumber));
                }
            }

            return Mathf.Max(1, maxLevelNumber + 1);
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
    }
}
