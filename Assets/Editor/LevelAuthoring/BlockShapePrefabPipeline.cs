using System;
using System.Collections.Generic;
using Editor.DataPipeline;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editor.LevelAuthoring
{
    public static class BlockShapePrefabPipeline
    {
        private const string BlockPrefabRootFolder = "Assets/Art/GeneratedBlocks/Prefabs/Blocks";
        private const string DefaultBlockRootPrefabPath = "Assets/Art/GeneratedBlocks/Prefabs/DefaultBlockRoot.prefab";
        private const string DoorExitParticlePrefabPath = "Assets/Art/GeneratedBlocks/Prefabs/FX/PS_DoorExitBurst.prefab";
        private const string OutlineMaterialFolder = "Assets/Art/GeneratedBlocks/Materials";
        private const string OutlineMaterialPath = "Assets/Art/GeneratedBlocks/Materials/MAT_DragHighlightWhite.mat";

        private const string GridCellPrefabPath = "Assets/Art/GeneratedBlocks/Prefabs/GridCell.prefab";
        private const string BlockedCellPrefabPath = "Assets/Art/GeneratedBlocks/Prefabs/BlockedCell.prefab";
        private const string BorderPrefabPath = "Assets/Art/GeneratedBlocks/Prefabs/Frame.prefab";
        private const string BackdropPrefabPath = "Assets/Art/GeneratedBlocks/Prefabs/BoardBackdrop.prefab";
        private const string DoorPrefabPath = "Assets/Art/GeneratedBlocks/Prefabs/DoorFill.prefab";

        private const string PoolManagerPrefabPath = "Assets/Art/GeneratedBlocks/Prefabs/GameRoot/Manager/BlockScenePoolManager.prefab";
        private const string ManagersPrefabPath = "Assets/Art/GeneratedBlocks/Prefabs/GameRoot/Manager/Managers.prefab";
        private const string BlockSceneBuilderPrefabPath = "Assets/Art/GeneratedBlocks/Prefabs/GameRoot/BlockSceneBuilder.prefab";
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";

        private const int ConditionIndicatorFontSize = 24;
        private const float ConditionIndicatorFrontLocalZ = -0.287f;
        private const float OutlineVerticalOffset = 0.03f;
        private const float OutlineZOffset = -0.01f;
        private const float OutlineThickness = 0.095f;
        private static readonly Quaternion GeneratedCellLocalRotation = Quaternion.Euler(0f, 180f, 0f);
        private static readonly Color OutlineColor = new(1f, 1f, 1f, 0.92f);

        [MenuItem("Tools/Color Block Jam/Data/Sync Shape Prefabs + Pool Prefabs")]
        public static void SyncShapePrefabsAndPoolPrefabs()
        {
            SyncInternal(rebuildMainScene: false, logSummary: true);
        }

        [MenuItem("Tools/Color Block Jam/Data/Sync Shape Prefabs + Pool Prefabs + Main Scene")]
        public static void SyncShapePrefabsAndPoolPrefabsAndScene()
        {
            SyncInternal(rebuildMainScene: true, logSummary: true);
        }

        public static void SyncForShapeChange()
        {
            SyncInternal(rebuildMainScene: true, logSummary: false);
        }

        private static void SyncInternal(bool rebuildMainScene, bool logSummary)
        {
            EnsureFolderExists(BlockPrefabRootFolder);
            EnsureFolderExists(LevelContentPipelineTool.ShapeDefinitionFolder);

            var shapes = LoadShapeDefinitions();
            var levels = LoadLevelDefinitions();
            var shapePrefabByKey = GenerateShapePrefabs(shapes);
            var requiredBlockPoolCountByShape = ResolveRequiredBlockPoolCountsByShape(levels);
            CleanupStaleShapePrefabFolders(shapePrefabByKey.Keys);
            if (shapePrefabByKey.Count == 0)
            {
                return;
            }

            var gridCellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GridCellPrefabPath);
            var blockedCellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockedCellPrefabPath);
            var borderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BorderPrefabPath);
            var backdropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackdropPrefabPath);
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);

            if (!gridCellPrefab || !blockedCellPrefab || !borderPrefab || !backdropPrefab || !doorPrefab)
            {
                Debug.LogError(
                    "BlockShapePrefabPipeline could not load all base pool prefabs (GridCell, BlockedCell, Frame, BoardBackdrop, DoorFill).");
                return;
            }

            EnsureBlockedCellPrefabMatchesBorderMaterial(blockedCellPrefab, borderPrefab);

            RebuildPoolManagerPrefab(PoolManagerPrefabPath, shapePrefabByKey, requiredBlockPoolCountByShape,
                gridCellPrefab, blockedCellPrefab, borderPrefab, backdropPrefab, doorPrefab);
            RebuildPoolManagerPrefab(ManagersPrefabPath, shapePrefabByKey, requiredBlockPoolCountByShape,
                gridCellPrefab, blockedCellPrefab, borderPrefab, backdropPrefab, doorPrefab);

            if (rebuildMainScene)
            {
                RebuildMainScenePool(shapePrefabByKey, requiredBlockPoolCountByShape, gridCellPrefab, blockedCellPrefab,
                    borderPrefab, backdropPrefab, doorPrefab);
            }

            SanitizeBlockSceneBuilderPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (logSummary)
            {
                Debug.Log(
                    $"[BlockShapePrefabPipeline] Sync complete. Shapes={shapePrefabByKey.Count}, SceneRebuilt={rebuildMainScene}.");
            }
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

        private static List<LevelDefinition> LoadLevelDefinitions()
        {
            var result = new List<LevelDefinition>();
            var guids = AssetDatabase.FindAssets($"t:{nameof(LevelDefinition)}",
                new[] { LevelContentPipelineTool.LevelDefinitionFolder });
            Array.Sort(guids, StringComparer.Ordinal);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (level)
                {
                    result.Add(level);
                }
            }

            return result;
        }

        private static Dictionary<string, int> ResolveRequiredBlockPoolCountsByShape(IReadOnlyList<LevelDefinition> levels)
        {
            var requiredMaxByShape = new Dictionary<string, int>(StringComparer.Ordinal);
            if (levels == null || levels.Count == 0)
            {
                return requiredMaxByShape;
            }

            var levelCountsByShape = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var levelIndex = 0; levelIndex < levels.Count; levelIndex++)
            {
                var level = levels[levelIndex];
                if (!level)
                {
                    continue;
                }

                var blocks = level.blocks;
                if (blocks == null || blocks.Count == 0)
                {
                    continue;
                }

                levelCountsByShape.Clear();
                for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
                {
                    var shapeKey = blocks[blockIndex].ResolvePoolKey();
                    if (string.IsNullOrWhiteSpace(shapeKey))
                    {
                        continue;
                    }

                    levelCountsByShape.TryGetValue(shapeKey, out var existingCount);
                    levelCountsByShape[shapeKey] = existingCount + 1;
                }

                foreach (var pair in levelCountsByShape)
                {
                    if (requiredMaxByShape.TryGetValue(pair.Key, out var existingMax))
                    {
                        requiredMaxByShape[pair.Key] = Mathf.Max(existingMax, pair.Value);
                    }
                    else
                    {
                        requiredMaxByShape[pair.Key] = pair.Value;
                    }
                }
            }

            return requiredMaxByShape;
        }

        private static Dictionary<string, GameObject> GenerateShapePrefabs(IReadOnlyList<BlockShapeDefinition> shapes)
        {
            var output = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            if (shapes == null || shapes.Count == 0)
            {
                return output;
            }

            var defaultRootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultBlockRootPrefabPath);
            if (!defaultRootPrefab)
            {
                Debug.LogError($"BlockShapePrefabPipeline could not load default root prefab: {DefaultBlockRootPrefabPath}");
                return output;
            }

            var outlineMaterial = EnsureOutlineMaterial();
            var doorExitParticlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorExitParticlePrefabPath);

            for (var i = 0; i < shapes.Count; i++)
            {
                var shape = shapes[i];
                if (!shape)
                {
                    continue;
                }

                shape.Sanitize();
                var shapeKey = shape.ShapeKey;
                if (string.IsNullOrWhiteSpace(shapeKey))
                {
                    continue;
                }

                var localCells = shape.GetLocalCells();
                if (localCells == null || localCells.Length == 0)
                {
                    Debug.LogError(
                        $"BlockShapePrefabPipeline skipped '{shapeKey}' because it has no local cells. Fix shape data in '{LevelContentPipelineTool.ShapeDefinitionFolder}'.");
                    continue;
                }

                var prefab = BuildShapePrefab(defaultRootPrefab, doorExitParticlePrefab, outlineMaterial, shapeKey,
                    localCells);
                if (prefab)
                {
                    output[shapeKey] = prefab;
                }
            }

            return output;
        }

        private static Material EnsureOutlineMaterial()
        {
            var outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (!outlineMaterial)
            {
                EnsureFolderExists(OutlineMaterialFolder);
                var createdShader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
                if (!createdShader)
                {
                    Debug.LogError(
                        "BlockShapePrefabPipeline could not find a shader for MAT_DragHighlightWhite.");
                    return null;
                }

                outlineMaterial = new Material(createdShader)
                {
                    name = "MAT_DragHighlightWhite"
                };
                AssetDatabase.CreateAsset(outlineMaterial, OutlineMaterialPath);
            }

            var targetShader = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default") ?? outlineMaterial.shader;
            if (targetShader && outlineMaterial.shader != targetShader)
            {
                outlineMaterial.shader = targetShader;
            }

            if (outlineMaterial.HasProperty("_Color"))
            {
                outlineMaterial.SetColor("_Color", Color.white);
            }

            if (outlineMaterial.HasProperty("_BaseColor"))
            {
                outlineMaterial.SetColor("_BaseColor", Color.white);
            }

            EditorUtility.SetDirty(outlineMaterial);
            return outlineMaterial;
        }

        private static void CleanupStaleShapePrefabFolders(IEnumerable<string> activeShapeKeys)
        {
            if (!AssetDatabase.IsValidFolder(BlockPrefabRootFolder))
            {
                return;
            }

            var activeKeys = new HashSet<string>(StringComparer.Ordinal);
            if (activeShapeKeys != null)
            {
                foreach (var key in activeShapeKeys)
                {
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        activeKeys.Add(key.Trim());
                    }
                }
            }

            var existingShapeFolders = AssetDatabase.GetSubFolders(BlockPrefabRootFolder);
            for (var i = 0; i < existingShapeFolders.Length; i++)
            {
                var folderPath = existingShapeFolders[i];
                var separatorIndex = folderPath.LastIndexOf('/');
                var folderName = separatorIndex >= 0
                    ? folderPath.Substring(separatorIndex + 1)
                    : folderPath;
                if (activeKeys.Contains(folderName))
                {
                    continue;
                }

                AssetDatabase.DeleteAsset(folderPath);
            }
        }

        private static GameObject BuildShapePrefab(GameObject defaultRootPrefab, GameObject doorExitParticlePrefab,
            Material outlineMaterial, string shapeKey, IReadOnlyList<Vector2Int> localCells)
        {
            var folderPath = $"{BlockPrefabRootFolder}/{shapeKey}";
            EnsureFolderExists(folderPath);
            var prefabPath = $"{folderPath}/Block_{shapeKey}.prefab";

            var root = UnityEngine.Object.Instantiate(defaultRootPrefab);
            if (!root)
            {
                return null;
            }

            root.name = $"Block_{shapeKey}";
            try
            {
                var templateCell = FindFirstBlockCell(root.transform);
                if (!templateCell)
                {
                    Debug.LogError(
                        $"BlockShapePrefabPipeline could not find a 'BlockCell_*' template under '{DefaultBlockRootPrefabPath}'.");
                    return null;
                }

                var templateCellInstance = UnityEngine.Object.Instantiate(templateCell);
                templateCellInstance.name = "__CellTemplate";
                var templateLocalPosition = templateCellInstance.transform.localPosition;
                var templateLocalScale = templateCellInstance.transform.localScale;

                RemoveChildrenByPrefix(root.transform, "BlockCell_");
                DestroyChildIfExists(root.transform, "ConditionIndicator");
                DestroyChildIfExists(root.transform, "BlockDragOutline");
                DestroyChildIfExists(root.transform, "DoorExitParticle");

                for (var i = 0; i < localCells.Count; i++)
                {
                    var cell = localCells[i];
                    var cellObject = UnityEngine.Object.Instantiate(templateCellInstance, root.transform);
                    cellObject.name = $"BlockCell_{i}";
                    cellObject.SetActive(true);

                    var cellTransform = cellObject.transform;
                    cellTransform.localPosition =
                        new Vector3(cell.x + 0.5f, cell.y + 0.5f, templateLocalPosition.z);
                    cellTransform.localRotation = GeneratedCellLocalRotation;
                    cellTransform.localScale = templateLocalScale;
                }

                UnityEngine.Object.DestroyImmediate(templateCellInstance);
                ConfigureConditionIndicator(root.transform, localCells);
                ConfigureDragOutline(root.transform, localCells, outlineMaterial);
                ConfigureDoorExitParticle(root.transform, localCells, doorExitParticlePrefab);

                if (!root.TryGetComponent<BlockPoolBindings>(out var bindings))
                {
                    bindings = root.AddComponent<BlockPoolBindings>();
                }

                bindings.EditorRebuildBindingsFromHierarchy();
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return savedPrefab ? savedPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject FindFirstBlockCell(Transform root)
        {
            if (!root)
            {
                return null;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child && child.name.StartsWith("BlockCell_", StringComparison.Ordinal))
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static void RemoveChildrenByPrefix(Transform parent, string prefix)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child && child.name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void DestroyChildIfExists(Transform parent, string childName)
        {
            if (!parent)
            {
                return;
            }

            var child = parent.Find(childName);
            if (child)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void ConfigureConditionIndicator(Transform root, IReadOnlyList<Vector2Int> localCells)
        {
            var indicatorObject = new GameObject("ConditionIndicator");
            indicatorObject.SetActive(false);
            indicatorObject.transform.SetParent(root, false);

            var indicatorText = indicatorObject.AddComponent<TextMesh>();
            indicatorText.text = "<-->";
            indicatorText.anchor = TextAnchor.MiddleCenter;
            indicatorText.alignment = TextAlignment.Center;
            indicatorText.fontSize = ConditionIndicatorFontSize;
            indicatorText.characterSize = 0.16f;
            indicatorText.color = Color.white;

            ResolveCellBounds(localCells, out var minX, out var minY, out var maxX, out var maxY);
            var anchorX = (minX + maxX + 1f) * 0.5f;
            var anchorY = (minY + maxY + 1f) * 0.5f;
            indicatorObject.transform.localPosition = new Vector3(anchorX, anchorY, ConditionIndicatorFrontLocalZ);
            indicatorObject.transform.localRotation = Quaternion.identity;
            indicatorObject.transform.localScale = Vector3.one;
        }

        private static void ConfigureDragOutline(Transform root, IReadOnlyList<Vector2Int> localCells,
            Material outlineMaterial)
        {
            var outlineObject = new GameObject("BlockDragOutline");
            outlineObject.SetActive(false);
            outlineObject.transform.SetParent(root, false);
            outlineObject.transform.localPosition = Vector3.zero;
            outlineObject.transform.localRotation = Quaternion.identity;
            outlineObject.transform.localScale = Vector3.one;

            var lineRenderer = outlineObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.widthMultiplier = OutlineThickness;
            lineRenderer.startColor = OutlineColor;
            lineRenderer.endColor = OutlineColor;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.numCapVertices = 2;
            lineRenderer.sortingOrder = 12000;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            lineRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            if (outlineMaterial)
            {
                lineRenderer.sharedMaterial = outlineMaterial;
            }

            var outlineLoop = BuildOutlineLoop(localCells);
            if (outlineLoop == null || outlineLoop.Count < 4)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            lineRenderer.positionCount = outlineLoop.Count;
            for (var i = 0; i < outlineLoop.Count; i++)
            {
                var point = outlineLoop[i];
                lineRenderer.SetPosition(i, new Vector3(point.x, point.y + OutlineVerticalOffset, OutlineZOffset));
            }
        }

        private static void ConfigureDoorExitParticle(Transform root, IReadOnlyList<Vector2Int> localCells,
            GameObject doorExitParticlePrefab)
        {
            if (!doorExitParticlePrefab)
            {
                return;
            }

            var particleObject = (GameObject)PrefabUtility.InstantiatePrefab(doorExitParticlePrefab, root);
            if (!particleObject)
            {
                particleObject = UnityEngine.Object.Instantiate(doorExitParticlePrefab, root);
            }

            particleObject.name = "DoorExitParticle";
            particleObject.SetActive(false);

            ResolveCellBounds(localCells, out var minX, out var minY, out var maxX, out var maxY);
            var center = new Vector3((minX + maxX + 1f) * 0.5f, (minY + maxY + 1f) * 0.5f, 0f);
            particleObject.transform.localPosition = center;
            particleObject.transform.localScale = Vector3.one;

            if (!particleObject.TryGetComponent<ParticleSystem>(out var particle))
            {
                return;
            }

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var particleMain = particle.main;
            particleMain.playOnAwake = false;
            particleMain.useUnscaledTime = true;

            var widthInCells = (maxX - minX) + 1f;
            var heightInCells = (maxY - minY) + 1f;
            var maxSpan = Mathf.Max(widthInCells, heightInCells);
            var shape = particle.shape;
            shape.enabled = true;
            shape.radius = Mathf.Clamp(maxSpan * 0.16f, 0.12f, 0.55f);
            shape.length = Mathf.Clamp(maxSpan * 0.22f, 0.2f, 0.62f);
        }

        private static List<Vector2Int> BuildOutlineLoop(IReadOnlyList<Vector2Int> localCells)
        {
            if (localCells == null || localCells.Count == 0)
            {
                return null;
            }

            var boundaryByEdge = new Dictionary<UndirectedGridEdgeKey, DirectedGridEdge>();
            for (var i = 0; i < localCells.Count; i++)
            {
                var cell = localCells[i];
                var bottomLeft = new Vector2Int(cell.x, cell.y);
                var bottomRight = new Vector2Int(cell.x + 1, cell.y);
                var topRight = new Vector2Int(cell.x + 1, cell.y + 1);
                var topLeft = new Vector2Int(cell.x, cell.y + 1);

                ToggleBoundaryEdge(boundaryByEdge, bottomLeft, bottomRight);
                ToggleBoundaryEdge(boundaryByEdge, bottomRight, topRight);
                ToggleBoundaryEdge(boundaryByEdge, topRight, topLeft);
                ToggleBoundaryEdge(boundaryByEdge, topLeft, bottomLeft);
            }

            if (boundaryByEdge.Count < 4)
            {
                return null;
            }

            var outgoing = new Dictionary<Vector2Int, Vector2Int>();
            foreach (var edge in boundaryByEdge.Values)
            {
                if (outgoing.ContainsKey(edge.Start))
                {
                    return null;
                }

                outgoing.Add(edge.Start, edge.End);
            }

            if (outgoing.Count < 4)
            {
                return null;
            }

            var hasStart = false;
            var startVertex = Vector2Int.zero;
            foreach (var vertex in outgoing.Keys)
            {
                if (!hasStart || IsLexicographicallyBefore(vertex, startVertex))
                {
                    startVertex = vertex;
                    hasStart = true;
                }
            }

            if (!hasStart)
            {
                return null;
            }

            var loop = new List<Vector2Int>(outgoing.Count);
            var current = startVertex;
            for (var guard = 0; guard <= outgoing.Count; guard++)
            {
                loop.Add(current);
                if (!outgoing.TryGetValue(current, out var next))
                {
                    return null;
                }

                if (next == startVertex)
                {
                    break;
                }

                current = next;
            }

            return loop.Count >= 4 ? loop : null;
        }

        private static void ToggleBoundaryEdge(IDictionary<UndirectedGridEdgeKey, DirectedGridEdge> edgesByKey,
            Vector2Int start, Vector2Int end)
        {
            var key = new UndirectedGridEdgeKey(start, end);
            if (edgesByKey.Remove(key))
            {
                return;
            }

            edgesByKey.Add(key, new DirectedGridEdge(start, end));
        }

        private static bool IsLexicographicallyBefore(Vector2Int left, Vector2Int right)
        {
            if (left.y != right.y)
            {
                return left.y < right.y;
            }

            return left.x < right.x;
        }

        private static void ResolveCellBounds(IReadOnlyList<Vector2Int> localCells, out int minX, out int minY,
            out int maxX, out int maxY)
        {
            minX = int.MaxValue;
            minY = int.MaxValue;
            maxX = int.MinValue;
            maxY = int.MinValue;

            if (localCells == null || localCells.Count == 0)
            {
                minX = minY = maxX = maxY = 0;
                return;
            }

            for (var i = 0; i < localCells.Count; i++)
            {
                var cell = localCells[i];
                if (cell.x < minX) minX = cell.x;
                if (cell.y < minY) minY = cell.y;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y > maxY) maxY = cell.y;
            }
        }

        private static void RebuildPoolManagerPrefab(string prefabPath,
            IReadOnlyDictionary<string, GameObject> shapePrefabsByKey,
            IReadOnlyDictionary<string, int> requiredBlockPoolCountByShape,
            GameObject gridCellPrefab,
            GameObject blockedCellPrefab,
            GameObject borderPrefab,
            GameObject backdropPrefab,
            GameObject doorPrefab)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
            {
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var poolManager = root.GetComponentInChildren<BlockScenePoolManager>(true);
                if (!poolManager)
                {
                    return;
                }

                poolManager.EditorRebuildAuthoringPools(shapePrefabsByKey, gridCellPrefab, blockedCellPrefab,
                    borderPrefab, backdropPrefab, doorPrefab, requiredBlockPoolCountByShape);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RebuildMainScenePool(IReadOnlyDictionary<string, GameObject> shapePrefabsByKey,
            IReadOnlyDictionary<string, int> requiredBlockPoolCountByShape,
            GameObject gridCellPrefab,
            GameObject blockedCellPrefab,
            GameObject borderPrefab,
            GameObject backdropPrefab,
            GameObject doorPrefab)
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath))
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            var activeScenePath = activeScene.path;
            var openedScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            try
            {
                var managers = UnityEngine.Object.FindObjectsOfType<BlockScenePoolManager>(true);
                for (var i = 0; i < managers.Length; i++)
                {
                    var manager = managers[i];
                    if (!manager)
                    {
                        continue;
                    }

                    manager.EditorRebuildAuthoringPools(shapePrefabsByKey, gridCellPrefab, blockedCellPrefab,
                        borderPrefab, backdropPrefab, doorPrefab, requiredBlockPoolCountByShape);
                    EditorUtility.SetDirty(manager.gameObject);
                }

                EditorSceneManager.MarkSceneDirty(openedScene);
                EditorSceneManager.SaveScene(openedScene);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(activeScenePath) && !string.Equals(activeScenePath, MainScenePath,
                        StringComparison.Ordinal))
                {
                    EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
                }
            }
        }

        private static void SanitizeBlockSceneBuilderPrefab()
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(BlockSceneBuilderPrefabPath))
            {
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(BlockSceneBuilderPrefabPath);
            try
            {
                var staleFxRoot = root.transform.Find("DoorExitBurstParticles");
                if (staleFxRoot)
                {
                    UnityEngine.Object.DestroyImmediate(staleFxRoot.gameObject);
                }

                PrefabUtility.SaveAsPrefabAsset(root, BlockSceneBuilderPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureBlockedCellPrefabMatchesBorderMaterial(GameObject blockedCellPrefab,
            GameObject borderPrefab)
        {
            if (!blockedCellPrefab || !borderPrefab)
            {
                return;
            }

            var borderRenderer = borderPrefab.GetComponentInChildren<Renderer>(true);
            var borderMaterial = borderRenderer ? borderRenderer.sharedMaterial : null;
            if (!borderMaterial)
            {
                return;
            }

            var blockedRoot = PrefabUtility.LoadPrefabContents(BlockedCellPrefabPath);
            try
            {
                var blockedRenderers = blockedRoot.GetComponentsInChildren<Renderer>(true);
                var hasChanges = false;
                for (var i = 0; i < blockedRenderers.Length; i++)
                {
                    var renderer = blockedRenderers[i];
                    if (!renderer)
                    {
                        continue;
                    }

                    var materials = renderer.sharedMaterials;
                    var rendererChanged = false;
                    for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    {
                        if (materials[materialIndex] == borderMaterial)
                        {
                            continue;
                        }

                        materials[materialIndex] = borderMaterial;
                        rendererChanged = true;
                    }

                    if (!rendererChanged)
                    {
                        continue;
                    }

                    renderer.sharedMaterials = materials;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    PrefabUtility.SaveAsPrefabAsset(blockedRoot, BlockedCellPrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(blockedRoot);
            }
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

        private readonly struct DirectedGridEdge
        {
            public DirectedGridEdge(Vector2Int start, Vector2Int end)
            {
                Start = start;
                End = end;
            }

            public Vector2Int Start { get; }
            public Vector2Int End { get; }
        }

        private readonly struct UndirectedGridEdgeKey : IEquatable<UndirectedGridEdgeKey>
        {
            private readonly Vector2Int _from;
            private readonly Vector2Int _to;

            public UndirectedGridEdgeKey(Vector2Int first, Vector2Int second)
            {
                if (first.x < second.x || first.x == second.x && first.y <= second.y)
                {
                    _from = first;
                    _to = second;
                }
                else
                {
                    _from = second;
                    _to = first;
                }
            }

            public bool Equals(UndirectedGridEdgeKey other)
            {
                return _from == other._from && _to == other._to;
            }

            public override bool Equals(object obj)
            {
                return obj is UndirectedGridEdgeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_from.GetHashCode() * 397) ^ _to.GetHashCode();
                }
            }
        }
    }
}
