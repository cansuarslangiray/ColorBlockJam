using System;
using System.Collections.Generic;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Editor.DataPipeline
{
    public static class BlockScenePoolSyncTool
    {
        private const string BlockPrefabRootFolder = "Assets/Art/GeneratedBlocks/Prefabs/Blocks";
        private const string BlockPrefabTemplatePath =
            "Assets/Art/GeneratedBlocks/Prefabs/Blocks/Shape_1x1/Block_Shape_1x1.prefab";
        private const string DefaultBlockRootPrefabPath =
            "Assets/Art/GeneratedBlocks/Prefabs/DefaultBlockRoot.prefab";
        private const string BlockScenePoolManagerPrefabPath =
            "Assets/Art/GeneratedBlocks/Prefabs/GameRoot/Manager/BlockScenePoolManager.prefab";
        private const string ManagersPrefabPath =
            "Assets/Art/GeneratedBlocks/Prefabs/GameRoot/Manager/Managers.prefab";
        private const string DefaultBlockMaterialPath =
            "Assets/Art/GeneratedBlocks/Materials/MAT_Block_DefaultWhite.mat";
        private const string DefaultBlockMaterialTemplatePath =
            "Assets/Art/GeneratedBlocks/Materials/MAT_GridCellRim.mat";
        private const string BlockCellNamePrefix = "BlockCell_";
        private const string ConditionIndicatorObjectName = "ConditionIndicator";
        private const float ConditionIndicatorCharacterSize = 0.22f;
        private const int ConditionIndicatorFontSize = 42;

        [MenuItem("Tools/Color Block Jam/Pools/Sync Missing Shape Pools")]
        public static void SyncMissingShapePools()
        {
            var shapes = LoadShapeDefinitions();
            if (shapes.Count == 0)
            {
                Debug.LogWarning("[BlockScenePoolSyncTool] No shape definitions found.");
                return;
            }

            var templatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockPrefabTemplatePath);
            if (!templatePrefab)
            {
                Debug.LogError(
                    $"[BlockScenePoolSyncTool] Missing template prefab at path: {BlockPrefabTemplatePath}");
                return;
            }

            var defaultBlockMaterial = LoadOrCreateDefaultBlockMaterial();
            if (!defaultBlockMaterial)
            {
                Debug.LogError(
                    $"[BlockScenePoolSyncTool] Failed to resolve default block material at: {DefaultBlockMaterialPath}");
                return;
            }

            SyncDefaultBlockTemplatePrefab(defaultBlockMaterial);

            var shapePrefabsByKey = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            var createdPrefabCount = 0;
            var updatedPrefabCount = 0;

            for (var i = 0; i < shapes.Count; i++)
            {
                var shape = shapes[i];
                if (!shape)
                {
                    continue;
                }

                var shapeKey = shape.ShapeKey;
                if (string.IsNullOrWhiteSpace(shapeKey))
                {
                    continue;
                }

                var shapePrefabPath = ResolveShapePrefabPath(shapeKey);
                var created = false;
                var shapePrefab =
                    EnsureShapePrefab(shape, shapePrefabPath, templatePrefab, defaultBlockMaterial, out created);
                if (!shapePrefab)
                {
                    continue;
                }

                if (created)
                {
                    createdPrefabCount++;
                }
                else
                {
                    updatedPrefabCount++;
                }

                shapePrefabsByKey[shapeKey] = shapePrefab;
            }

            if (shapePrefabsByKey.Count == 0)
            {
                Debug.LogError("[BlockScenePoolSyncTool] No valid shape prefabs resolved.");
                return;
            }

            var sceneManagerCount = SyncSceneManagers(shapePrefabsByKey);
            var prefabManagerUpdated = SyncManagerPrefabAsset(shapePrefabsByKey);
            var managersPrefabUpdated = SyncManagersPrefabAsset(shapePrefabsByKey);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[BlockScenePoolSyncTool] Done. Shapes={shapePrefabsByKey.Count}, " +
                $"CreatedPrefabs={createdPrefabCount}, UpdatedPrefabs={updatedPrefabCount}, " +
                $"SceneManagers={sceneManagerCount}, PrefabManagerUpdated={prefabManagerUpdated}, " +
                $"ManagersPrefabUpdated={managersPrefabUpdated}.");
        }

        private static List<BlockShapeDefinition> LoadShapeDefinitions()
        {
            var result = new List<BlockShapeDefinition>();
            var guids = AssetDatabase.FindAssets($"t:{nameof(BlockShapeDefinition)}",
                new[] { LevelContentPipelineTool.ShapeDefinitionFolder });
            Array.Sort(guids, StringComparer.Ordinal);

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var shape = AssetDatabase.LoadAssetAtPath<BlockShapeDefinition>(path);
                if (shape)
                {
                    result.Add(shape);
                }
            }

            result.Sort((left, right) => string.CompareOrdinal(left.ShapeKey, right.ShapeKey));
            return result;
        }

        private static GameObject EnsureShapePrefab(BlockShapeDefinition shape, string prefabPath,
            GameObject templatePrefab, Material defaultBlockMaterial, out bool created)
        {
            created = false;
            var localCells = shape.GetLocalCells();
            var requiredCellCount = Mathf.Max(1, localCells?.Length ?? 0);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
            {
                var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    prefabRoot.name = $"Block_{shape.ShapeKey}";
                    EnsureCellCount(prefabRoot.transform, requiredCellCount);
                    ApplyDefaultBlockVisualSetup(prefabRoot, defaultBlockMaterial);
                    EnsureBindings(prefabRoot);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }

                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }

            EnsureFolderExistsForAsset(prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(templatePrefab);
            if (!instance)
            {
                instance = UnityEngine.Object.Instantiate(templatePrefab);
            }

            try
            {
                instance.name = $"Block_{shape.ShapeKey}";
                EnsureCellCount(instance.transform, requiredCellCount);
                ApplyDefaultBlockVisualSetup(instance, defaultBlockMaterial);
                EnsureBindings(instance);
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                created = savedPrefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static Material LoadOrCreateDefaultBlockMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(DefaultBlockMaterialPath);
            if (existing)
            {
                ApplyDefaultBlockMaterialColor(existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            EnsureFolderExistsForAsset(DefaultBlockMaterialPath);
            var templateMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultBlockMaterialTemplatePath);
            Material createdMaterial;
            if (templateMaterial)
            {
                createdMaterial = new Material(templateMaterial);
            }
            else
            {
                var shader = Shader.Find("Standard");
                if (!shader)
                {
                    return null;
                }

                createdMaterial = new Material(shader);
            }

            createdMaterial.name = "MAT_Block_DefaultWhite";
            ApplyDefaultBlockMaterialColor(createdMaterial);
            AssetDatabase.CreateAsset(createdMaterial, DefaultBlockMaterialPath);
            return AssetDatabase.LoadAssetAtPath<Material>(DefaultBlockMaterialPath);
        }

        private static void ApplyDefaultBlockMaterialColor(Material material)
        {
            if (!material)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }
        }

        private static void SyncDefaultBlockTemplatePrefab(Material defaultBlockMaterial)
        {
            SyncDefaultBlockTemplatePrefabAtPath(BlockPrefabTemplatePath, defaultBlockMaterial);
            SyncDefaultBlockTemplatePrefabAtPath(DefaultBlockRootPrefabPath, defaultBlockMaterial);
        }

        private static void SyncDefaultBlockTemplatePrefabAtPath(string prefabPath, Material defaultBlockMaterial)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab)
            {
                return;
            }

            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ApplyDefaultBlockVisualSetup(prefabRoot, defaultBlockMaterial);
                EnsureBindings(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static int SyncSceneManagers(IReadOnlyDictionary<string, GameObject> shapePrefabsByKey)
        {
            var managers = UnityEngine.Object.FindObjectsOfType<BlockScenePoolManager>(true);
            var syncedCount = 0;
            for (var i = 0; i < managers.Length; i++)
            {
                var manager = managers[i];
                if (!manager)
                {
                    continue;
                }

                manager.EditorEnsureShapePools(shapePrefabsByKey);
                EditorUtility.SetDirty(manager);
                if (manager.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                }
                syncedCount++;
            }

            return syncedCount;
        }

        private static bool SyncManagerPrefabAsset(IReadOnlyDictionary<string, GameObject> shapePrefabsByKey)
        {
            var managerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockScenePoolManagerPrefabPath);
            if (!managerPrefab)
            {
                Debug.LogWarning(
                    $"[BlockScenePoolSyncTool] Manager prefab not found at: {BlockScenePoolManagerPrefabPath}");
                return false;
            }

            var prefabRoot = PrefabUtility.LoadPrefabContents(BlockScenePoolManagerPrefabPath);
            try
            {
                var manager = prefabRoot.GetComponent<BlockScenePoolManager>();
                if (!manager)
                {
                    manager = prefabRoot.GetComponentInChildren<BlockScenePoolManager>(true);
                }

                if (!manager)
                {
                    Debug.LogWarning(
                        $"[BlockScenePoolSyncTool] BlockScenePoolManager component missing in prefab: {BlockScenePoolManagerPrefabPath}");
                    return false;
                }

                manager.EditorEnsureShapePools(shapePrefabsByKey);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, BlockScenePoolManagerPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool SyncManagersPrefabAsset(IReadOnlyDictionary<string, GameObject> shapePrefabsByKey)
        {
            var managersPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagersPrefabPath);
            if (!managersPrefab)
            {
                Debug.LogWarning(
                    $"[BlockScenePoolSyncTool] Managers prefab not found at: {ManagersPrefabPath}");
                return false;
            }

            var prefabRoot = PrefabUtility.LoadPrefabContents(ManagersPrefabPath);
            try
            {
                var managers = prefabRoot.GetComponentsInChildren<BlockScenePoolManager>(true);
                if (managers == null || managers.Length == 0)
                {
                    Debug.LogWarning(
                        $"[BlockScenePoolSyncTool] No BlockScenePoolManager found in prefab: {ManagersPrefabPath}");
                    return false;
                }

                for (var i = 0; i < managers.Length; i++)
                {
                    var manager = managers[i];
                    if (!manager)
                    {
                        continue;
                    }

                    manager.EditorEnsureShapePools(shapePrefabsByKey);
                    if (manager.gameObject.scene.IsValid())
                    {
                        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, ManagersPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static string ResolveShapePrefabPath(string shapeKey)
        {
            return $"{BlockPrefabRootFolder}/{shapeKey}/Block_{shapeKey}.prefab";
        }

        private static void EnsureCellCount(Transform root, int requiredCellCount)
        {
            requiredCellCount = Mathf.Max(1, requiredCellCount);
            var cells = CollectBlockCells(root);
            if (cells.Count == 0)
            {
                return;
            }

            var templateCell = cells[0];
            while (cells.Count < requiredCellCount)
            {
                var newCell = UnityEngine.Object.Instantiate(templateCell.gameObject, root, false);
                cells.Add(newCell.transform);
            }

            for (var i = cells.Count - 1; i >= requiredCellCount; i--)
            {
                var cell = cells[i];
                if (cell)
                {
                    UnityEngine.Object.DestroyImmediate(cell.gameObject);
                }
            }

            cells = CollectBlockCells(root);
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (!cell)
                {
                    continue;
                }

                cell.name = $"{BlockCellNamePrefix}{i}";
            }
        }

        private static List<Transform> CollectBlockCells(Transform root)
        {
            var cells = new List<Transform>();
            if (!root)
            {
                return cells;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child && child.name.StartsWith(BlockCellNamePrefix, StringComparison.Ordinal))
                {
                    cells.Add(child);
                }
            }

            cells.Sort((left, right) => ParseCellIndex(left.name).CompareTo(ParseCellIndex(right.name)));
            return cells;
        }

        private static int ParseCellIndex(string cellName)
        {
            if (string.IsNullOrWhiteSpace(cellName) ||
                !cellName.StartsWith(BlockCellNamePrefix, StringComparison.Ordinal))
            {
                return int.MaxValue;
            }

            var suffix = cellName.Substring(BlockCellNamePrefix.Length);
            return int.TryParse(suffix, out var parsedValue) ? parsedValue : int.MaxValue;
        }

        private static void ApplyDefaultBlockVisualSetup(GameObject rootObject, Material defaultBlockMaterial)
        {
            if (!rootObject)
            {
                return;
            }

            var rootTransform = rootObject.transform;
            ApplyDefaultCellMaterial(rootTransform, defaultBlockMaterial);
            ApplyConditionIndicatorTextStyle(rootTransform);
        }

        private static void ApplyDefaultCellMaterial(Transform root, Material defaultBlockMaterial)
        {
            if (!root || !defaultBlockMaterial)
            {
                return;
            }

            var cells = CollectBlockCells(root);
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (!cell)
                {
                    continue;
                }

                var renderers = cell.GetComponentsInChildren<Renderer>(true);
                for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    var renderer = renderers[rendererIndex];
                    if (!renderer || renderer is LineRenderer || renderer is ParticleSystemRenderer)
                    {
                        continue;
                    }

                    var sharedMaterials = renderer.sharedMaterials;
                    if (sharedMaterials == null || sharedMaterials.Length == 0)
                    {
                        if (renderer.sharedMaterial != defaultBlockMaterial)
                        {
                            renderer.sharedMaterial = defaultBlockMaterial;
                        }

                        continue;
                    }

                    var changed = false;
                    for (var materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                    {
                        if (sharedMaterials[materialIndex] == defaultBlockMaterial)
                        {
                            continue;
                        }

                        sharedMaterials[materialIndex] = defaultBlockMaterial;
                        changed = true;
                    }

                    if (changed)
                    {
                        renderer.sharedMaterials = sharedMaterials;
                    }
                }
            }
        }

        private static void ApplyConditionIndicatorTextStyle(Transform root)
        {
            if (!root)
            {
                return;
            }

            var textMeshes = root.GetComponentsInChildren<TextMesh>(true);
            for (var i = 0; i < textMeshes.Length; i++)
            {
                var textMesh = textMeshes[i];
                if (!textMesh || !textMesh.gameObject ||
                    !string.Equals(textMesh.gameObject.name, ConditionIndicatorObjectName, StringComparison.Ordinal))
                {
                    continue;
                }

                textMesh.text = "0";
                textMesh.characterSize = ConditionIndicatorCharacterSize;
                textMesh.fontSize = ConditionIndicatorFontSize;
                textMesh.fontStyle = FontStyle.Bold;
                textMesh.alignment = TextAlignment.Center;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.lineSpacing = 1f;
                textMesh.richText = false;
                textMesh.color = Color.black;
            }
        }

        private static void EnsureBindings(GameObject rootObject)
        {
            if (!rootObject)
            {
                return;
            }

            var bindings = rootObject.GetComponent<BlockPoolBindings>();
            if (!bindings)
            {
                bindings = rootObject.AddComponent<BlockPoolBindings>();
            }

            bindings.EditorRebuildBindingsFromHierarchy();
            EditorUtility.SetDirty(bindings);
        }

        private static void EnsureFolderExistsForAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            var folderPath = System.IO.Path.GetDirectoryName(assetPath);
            if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var folders = folderPath.Replace("\\", "/").Split('/');
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
