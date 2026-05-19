using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Pool
{
    [DisallowMultipleComponent]
    public sealed class BlockScenePoolManager : MonoBehaviour
    {
        public const int TargetBoardPoolWidth = 25;
        public const int TargetBoardPoolHeight = 25;
        public const int TargetDoorPoolCount = 16;
        public const int TargetBlockPoolCountPerShape = 5;
        private const string BlockPlacementAnchorPrefix = "__BlockPlacementAnchor_";
        private const string DoorPlacementAnchorPrefix = "__DoorPlacementAnchor_";

        [Header("Shared Root")] [SerializeField]
        private Transform poolRoot;

        [Header("Grid Cell Pool")] [SerializeField]
        private List<GameObject> gridCellObjects = new(625);

        [Header("Blocked Cell Pool")] [SerializeField]
        private List<GameObject> blockedCellObjects = new(64);

        [Header("Board Frame")] [SerializeField]
        private List<GameObject> borderObjects = new(4);

        [SerializeField] private GameObject backdropObject;

        [Header("Door Pool")] [SerializeField]
        private List<DoorPoolBindings> doorBindings = new(16);

        [Header("Block Pools")] [SerializeField]
        private List<BlockTypePoolEntry> blockTypePools = new(20);

        private readonly Dictionary<string, List<BlockPoolBindings>> _blockBindingsByKey =
            new(System.StringComparer.Ordinal);

        public IReadOnlyList<GameObject> GridCellObjects => gridCellObjects;
        public IReadOnlyList<GameObject> BlockedCellObjects => blockedCellObjects;
        public IReadOnlyList<GameObject> BorderObjects => borderObjects;
        public IReadOnlyList<DoorPoolBindings> DoorBindings => doorBindings;
        public IReadOnlyDictionary<string, List<BlockPoolBindings>> BlockBindingsByKey => _blockBindingsByKey;
        public GameObject BackdropObject => backdropObject;
        public int RequiredBoardGridCellCount => TargetBoardPoolWidth * TargetBoardPoolHeight;

        public void RefreshPools(bool validateAuthoringTargets = true)
        {
            SanitizePoolList(gridCellObjects);
            SanitizePoolList(blockedCellObjects);
            SanitizePoolList(borderObjects);
            SanitizePoolList(doorBindings);
            SanitizeBlockTypePools();
            RebuildBlockTypeLookup();

            if (validateAuthoringTargets)
            {
                ValidateAuthoringTargets();
            }
        }

        public void EnsureGridCellPoolSize(int requiredCount)
        {
            gridCellObjects ??= new List<GameObject>();
            SanitizePoolList(gridCellObjects);
        }

        public void EnsureBlockedCellPoolSize(int requiredCount)
        {
            requiredCount = Mathf.Max(0, requiredCount);
            blockedCellObjects ??= new List<GameObject>(requiredCount);
            SanitizePoolList(blockedCellObjects);
        }

        public void EnsureDoorPoolSize(int requiredCount)
        {
            doorBindings ??= new List<DoorPoolBindings>();
            SanitizePoolList(doorBindings);
        }

        public void EnsureBlockPoolSizes(IReadOnlyDictionary<string, int> requiredCountByKey)
        {
            _ = requiredCountByKey;
            SanitizeBlockTypePools();
            RebuildBlockTypeLookup();
        }

        private void SanitizeBlockTypePools()
        {
            blockTypePools ??= new List<BlockTypePoolEntry>();
            var seenKeys = new HashSet<string>(System.StringComparer.Ordinal);

            for (var i = blockTypePools.Count - 1; i >= 0; i--)
            {
                var poolEntry = blockTypePools[i];
                if (poolEntry == null)
                {
                    blockTypePools.RemoveAt(i);
                    continue;
                }

                poolEntry.shapeKey = string.IsNullOrWhiteSpace(poolEntry.shapeKey) ? string.Empty : poolEntry.shapeKey.Trim();
                if (string.IsNullOrWhiteSpace(poolEntry.shapeKey) || !seenKeys.Add(poolEntry.shapeKey))
                {
                    blockTypePools.RemoveAt(i);
                    continue;
                }

                poolEntry.blockBindings ??= new List<BlockPoolBindings>(TargetBlockPoolCountPerShape);
                SanitizePoolList(poolEntry.blockBindings);
            }
        }

        private bool TryGetBlockPoolEntry(string poolKey, out BlockTypePoolEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(poolKey) || blockTypePools == null)
            {
                return false;
            }

            var normalizedPoolKey = poolKey.Trim();

            for (var i = 0; i < blockTypePools.Count; i++)
            {
                var poolEntry = blockTypePools[i];
                if (poolEntry != null &&
                    string.Equals(poolEntry.ResolvePoolKey(), normalizedPoolKey, System.StringComparison.Ordinal))
                {
                    entry = poolEntry;
                    return true;
                }
            }

            return false;
        }

        private void WarnIfBlockPoolIsBelowAuthoringMinimum()
        {
            if (blockTypePools == null || blockTypePools.Count == 0)
            {
                return;
            }

            for (var i = 0; i < blockTypePools.Count; i++)
            {
                var poolEntry = blockTypePools[i];
                if (poolEntry == null)
                {
                    continue;
                }

                WarnIfScenePoolIsShort($"block root ({poolEntry.ResolvePoolKey()})",
                    poolEntry.blockBindings?.Count ?? 0, TargetBlockPoolCountPerShape);
            }
        }

        private void RebuildBlockTypeLookup()
        {
            _blockBindingsByKey.Clear();
            for (var i = 0; i < blockTypePools.Count; i++)
            {
                var poolEntry = blockTypePools[i];
                if (poolEntry == null)
                {
                    continue;
                }

                poolEntry.blockBindings ??= new List<BlockPoolBindings>(TargetBlockPoolCountPerShape);
                var poolKey = poolEntry.ResolvePoolKey();
                if (_blockBindingsByKey.ContainsKey(poolKey))
                {
                    continue;
                }

                _blockBindingsByKey[poolKey] = poolEntry.blockBindings;
            }
        }

        private static void SanitizePoolList<T>(List<T> pool) where T : UnityEngine.Object
        {
            if (pool == null)
            {
                return;
            }

            for (var i = pool.Count - 1; i >= 0; i--)
            {
                if (!pool[i])
                {
                    pool.RemoveAt(i);
                }
            }
        }

        private void ValidateAuthoringTargets()
        {
            WarnIfScenePoolIsShort("grid cell", gridCellObjects?.Count ?? 0, RequiredBoardGridCellCount);
            WarnIfScenePoolIsShort("door", doorBindings?.Count ?? 0, TargetDoorPoolCount);
            WarnIfScenePoolIsShort("border", borderObjects?.Count ?? 0, 4);
            WarnIfBlockPoolIsBelowAuthoringMinimum();
        }

#if UNITY_EDITOR
        public void EditorRebuildAuthoringPools(
            IReadOnlyDictionary<string, GameObject> shapePrefabsByKey,
            GameObject gridCellPrefab,
            GameObject blockedCellPrefab,
            GameObject borderPrefab,
            GameObject backdropPrefab,
            GameObject doorPrefab,
            IReadOnlyDictionary<string, int> requiredBlockPoolCountByKey = null,
            int blockedPoolCount = 64)
        {
            if (!gridCellPrefab || !blockedCellPrefab || !borderPrefab || !backdropPrefab || !doorPrefab)
            {
                Debug.LogError(
                    "BlockScenePoolManager.EditorRebuildAuthoringPools requires all base prefabs (grid, blocked, border, backdrop, door).",
                    this);
                return;
            }

            if (shapePrefabsByKey == null || shapePrefabsByKey.Count == 0)
            {
                Debug.LogError(
                    "BlockScenePoolManager.EditorRebuildAuthoringPools requires at least one shape prefab.",
                    this);
                return;
            }

            Undo.RegisterCompleteObjectUndo(gameObject, "Rebuild Block Scene Pools");
            Undo.RegisterCompleteObjectUndo(this, "Rebuild Block Scene Pools");
            RemoveLegacyPlacementAnchorsByPrefix(transform, BlockPlacementAnchorPrefix);
            RemoveLegacyPlacementAnchorsByPrefix(transform, DoorPlacementAnchorPrefix);

            poolRoot = ResolveOrCreatePoolRoot();

            for (var i = poolRoot.childCount - 1; i >= 0; i--)
            {
                var child = poolRoot.GetChild(i);
                if (child)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            var gridRoot = CreatePoolSectionRoot("GridCellPool", poolRoot);
            var blockedRoot = CreatePoolSectionRoot("BlockedCellPool", poolRoot);
            var borderRoot = CreatePoolSectionRoot("BorderPool", poolRoot);
            var doorRoot = CreatePoolSectionRoot("DoorPool", poolRoot);
            var blockRoot = CreatePoolSectionRoot("BlockPools", poolRoot);
            var backdropRoot = CreatePoolSectionRoot("Backdrop", poolRoot);

            gridCellObjects = new List<GameObject>(RequiredBoardGridCellCount);
            for (var i = 0; i < RequiredBoardGridCellCount; i++)
            {
                var pooledGrid = InstantiatePoolPrefab(gridCellPrefab, gridRoot, $"GridCell_{i:000}");
                gridCellObjects.Add(pooledGrid);
            }

            blockedPoolCount = Mathf.Max(0, blockedPoolCount);
            blockedCellObjects = new List<GameObject>(blockedPoolCount);
            for (var i = 0; i < blockedPoolCount; i++)
            {
                var blockedCell = InstantiatePoolPrefab(blockedCellPrefab, blockedRoot, $"BlockedCell_{i:000}");
                blockedCellObjects.Add(blockedCell);
            }

            borderObjects = new List<GameObject>(4);
            for (var i = 0; i < 4; i++)
            {
                var border = InstantiatePoolPrefab(borderPrefab, borderRoot, $"Border_{i:000}");
                borderObjects.Add(border);
            }

            backdropObject = InstantiatePoolPrefab(backdropPrefab, backdropRoot, "BoardBackdrop");

            doorBindings = new List<DoorPoolBindings>(TargetDoorPoolCount);
            for (var i = 0; i < TargetDoorPoolCount; i++)
            {
                var doorObject = InstantiatePoolPrefab(doorPrefab, doorRoot, $"Door_{i:000}");
                if (!doorObject.TryGetComponent<DoorPoolBindings>(out var doorBinding))
                {
                    doorBinding = Undo.AddComponent<DoorPoolBindings>(doorObject);
                }

                doorBinding.EditorRebuildBindingsFromHierarchy();
                doorBindings.Add(doorBinding);
            }

            var sortedShapeKeys = new List<string>(shapePrefabsByKey.Keys);
            sortedShapeKeys.Sort(string.CompareOrdinal);
            blockTypePools = new List<BlockTypePoolEntry>(sortedShapeKeys.Count);

            for (var shapeIndex = 0; shapeIndex < sortedShapeKeys.Count; shapeIndex++)
            {
                var shapeKey = sortedShapeKeys[shapeIndex];
                var shapePrefab = shapePrefabsByKey[shapeKey];
                if (!shapePrefab)
                {
                    continue;
                }

                var shapePoolRoot = CreatePoolSectionRoot($"BlockPool_{shapeKey}", blockRoot);
                var targetBlockPoolCount = ResolveTargetBlockPoolCount(shapeKey, requiredBlockPoolCountByKey);
                var poolEntry = new BlockTypePoolEntry
                {
                    shapeKey = shapeKey,
                    blockBindings = new List<BlockPoolBindings>(targetBlockPoolCount)
                };

                for (var poolIndex = 0; poolIndex < targetBlockPoolCount; poolIndex++)
                {
                    var blockObject = InstantiatePoolPrefab(shapePrefab, shapePoolRoot, $"Block_{shapeKey}_{poolIndex:000}");
                    if (!blockObject.TryGetComponent<BlockPoolBindings>(out var blockBinding))
                    {
                        blockBinding = Undo.AddComponent<BlockPoolBindings>(blockObject);
                    }

                    blockBinding.EditorRebuildBindingsFromHierarchy();
                    poolEntry.blockBindings.Add(blockBinding);
                }

                blockTypePools.Add(poolEntry);
            }

            RefreshPools(validateAuthoringTargets: false);
            EditorUtility.SetDirty(this);
        }

        private static int ResolveTargetBlockPoolCount(string shapeKey,
            IReadOnlyDictionary<string, int> requiredBlockPoolCountByKey)
        {
            var targetCount = TargetBlockPoolCountPerShape;
            if (requiredBlockPoolCountByKey == null || string.IsNullOrWhiteSpace(shapeKey))
            {
                return targetCount;
            }

            if (!requiredBlockPoolCountByKey.TryGetValue(shapeKey, out var requiredCount))
            {
                return targetCount;
            }

            return Mathf.Max(targetCount, requiredCount);
        }

        private Transform ResolveOrCreatePoolRoot()
        {
            Transform resolvedRoot = null;

            if (poolRoot && poolRoot.parent == transform)
            {
                resolvedRoot = poolRoot;
            }

            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (!child || !string.Equals(child.name, "PoolRoot", System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (!resolvedRoot)
                {
                    resolvedRoot = child;
                    continue;
                }

                Undo.DestroyObjectImmediate(child.gameObject);
            }

            if (!resolvedRoot)
            {
                var rootObject = new GameObject("PoolRoot");
                rootObject.transform.SetParent(transform, false);
                resolvedRoot = rootObject.transform;
            }

            return resolvedRoot;
        }

        private static void RemoveLegacyPlacementAnchorsByPrefix(Transform root, string prefix)
        {
            if (!root || string.IsNullOrEmpty(prefix))
            {
                return;
            }

            var staleAnchors = new List<Transform>();
            CollectTransformsByNamePrefix(root, prefix, staleAnchors);
            for (var i = 0; i < staleAnchors.Count; i++)
            {
                var staleAnchor = staleAnchors[i];
                if (staleAnchor)
                {
                    Undo.DestroyObjectImmediate(staleAnchor.gameObject);
                }
            }
        }

        private static void CollectTransformsByNamePrefix(Transform root, string prefix, List<Transform> collector)
        {
            if (!root || collector == null)
            {
                return;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (!child)
                {
                    continue;
                }

                if (child.name.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    collector.Add(child);
                    continue;
                }

                CollectTransformsByNamePrefix(child, prefix, collector);
            }
        }

        private static Transform CreatePoolSectionRoot(string sectionName, Transform parent)
        {
            var sectionObject = new GameObject(sectionName);
            sectionObject.transform.SetParent(parent, false);
            return sectionObject.transform;
        }

        private static GameObject InstantiatePoolPrefab(GameObject prefab, Transform parent, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (!instance)
            {
                instance = Object.Instantiate(prefab, parent);
            }

            instance.name = name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(false);
            return instance;
        }
#endif

        private void WarnIfScenePoolIsShort(string poolName, int availableCount, int requiredCount)
        {
            requiredCount = Mathf.Max(0, requiredCount);
            if (availableCount >= requiredCount)
            {
                return;
            }

            Debug.LogWarning(
                $"BlockScenePoolManager has {availableCount} {poolName} objects assigned, but this level needs {requiredCount}. " +
                "Add the missing authored pool objects to the scene or prefab and assign them on BlockScenePoolManager.",
                this);
        }
    }
}
