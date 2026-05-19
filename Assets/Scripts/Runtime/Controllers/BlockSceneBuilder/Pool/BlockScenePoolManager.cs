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
        public const int TargetBlockPoolCountPerShape = 15;
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

        private static void SanitizePoolList<T>(List<T> pool) where T : Object
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
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            if (poolRoot)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(poolRoot);
            }
            EditorUtility.SetDirty(this);
        }

        public void EditorEnsureShapePools(
            IReadOnlyDictionary<string, GameObject> shapePrefabsByKey,
            IReadOnlyDictionary<string, int> requiredBlockPoolCountByKey = null)
        {
            if (shapePrefabsByKey == null || shapePrefabsByKey.Count == 0)
            {
                Debug.LogWarning("EditorEnsureShapePools skipped: no shape prefab mapping found.", this);
                return;
            }

            poolRoot = ResolveOrCreatePoolRootWithoutTouchingOtherSections();
            var blockRoot = FindDescendantByName(poolRoot, "BlockPools");
            if (!blockRoot)
            {
                blockRoot = ResolveOrCreateChildByName(poolRoot, "BlockPools");
            }

            blockTypePools ??= new List<BlockTypePoolEntry>();

            var poolEntryByKey = new Dictionary<string, BlockTypePoolEntry>(System.StringComparer.Ordinal);
            for (var i = blockTypePools.Count - 1; i >= 0; i--)
            {
                var poolEntry = blockTypePools[i];
                if (poolEntry == null)
                {
                    blockTypePools.RemoveAt(i);
                    continue;
                }

                var poolKey = poolEntry.ResolvePoolKey();
                if (string.IsNullOrWhiteSpace(poolKey))
                {
                    blockTypePools.RemoveAt(i);
                    continue;
                }

                if (poolEntryByKey.ContainsKey(poolKey))
                {
                    continue;
                }

                poolEntry.shapeKey = poolKey;
                poolEntryByKey[poolKey] = poolEntry;
            }

            var sortedKeys = new List<string>(shapePrefabsByKey.Keys);
            sortedKeys.Sort(string.CompareOrdinal);

            for (var i = 0; i < sortedKeys.Count; i++)
            {
                var shapeKey = sortedKeys[i];
                if (string.IsNullOrWhiteSpace(shapeKey) ||
                    !shapePrefabsByKey.TryGetValue(shapeKey, out var shapePrefab) ||
                    !shapePrefab)
                {
                    continue;
                }

                if (!poolEntryByKey.TryGetValue(shapeKey, out var poolEntry))
                {
                    poolEntry = new BlockTypePoolEntry
                    {
                        shapeKey = shapeKey,
                        blockBindings = new List<BlockPoolBindings>(TargetBlockPoolCountPerShape)
                    };
                    blockTypePools.Add(poolEntry);
                    poolEntryByKey[shapeKey] = poolEntry;
                }

                poolEntry.blockBindings ??= new List<BlockPoolBindings>(TargetBlockPoolCountPerShape);
                for (var bindingIndex = poolEntry.blockBindings.Count - 1; bindingIndex >= 0; bindingIndex--)
                {
                    if (!poolEntry.blockBindings[bindingIndex])
                    {
                        poolEntry.blockBindings.RemoveAt(bindingIndex);
                    }
                }

                var targetBlockPoolCount = ResolveTargetBlockPoolCount(shapeKey, requiredBlockPoolCountByKey);
                if (poolEntry.blockBindings.Count >= targetBlockPoolCount)
                {
                    continue;
                }

                var shapePoolRoot = FindDescendantByName(blockRoot, $"BlockPool_{shapeKey}");
                if (!shapePoolRoot)
                {
                    shapePoolRoot = ResolveOrCreateChildByName(blockRoot, $"BlockPool_{shapeKey}");
                }

                for (var poolIndex = poolEntry.blockBindings.Count; poolIndex < targetBlockPoolCount; poolIndex++)
                {
                    var blockObject =
                        InstantiatePoolPrefab(shapePrefab, shapePoolRoot, $"Block_{shapeKey}_{poolIndex:000}");
                    if (!blockObject.TryGetComponent<BlockPoolBindings>(out var blockBinding))
                    {
                        blockBinding = blockObject.AddComponent<BlockPoolBindings>();
                    }

                    blockBinding.EditorRebuildBindingsFromHierarchy();
                    poolEntry.blockBindings.Add(blockBinding);
                }
            }

            SanitizeBlockTypePools();
            RebuildBlockTypeLookup();
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            if (poolRoot)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(poolRoot);
            }

            EditorUtility.SetDirty(this);
        }

        public void EditorRebindAllPoolsFromHierarchy()
        {
            poolRoot = ResolveOrCreatePoolRoot();
            if (!poolRoot)
            {
                return;
            }

            var gridRoot = FindChildByName(poolRoot, "GridCellPool");
            if (gridRoot)
            {
                gridCellObjects = CollectImmediateChildObjects(gridRoot);
            }

            var blockedRoot = FindChildByName(poolRoot, "BlockedCellPool");
            if (blockedRoot)
            {
                blockedCellObjects = CollectImmediateChildObjects(blockedRoot);
            }

            var borderRoot = FindChildByName(poolRoot, "BorderPool");
            if (borderRoot)
            {
                borderObjects = CollectImmediateChildObjects(borderRoot);
            }

            var doorRoot = FindChildByName(poolRoot, "DoorPool");
            if (doorRoot)
            {
                doorBindings = CollectImmediateChildComponents<DoorPoolBindings>(doorRoot);
            }

            var backdropRoot = FindChildByName(poolRoot, "Backdrop");
            if (backdropRoot && backdropRoot.childCount > 0)
            {
                var backdropChild = backdropRoot.GetChild(0);
                if (backdropChild)
                {
                    backdropObject = backdropChild.gameObject;
                }
            }

            var blockRoot = FindChildByName(poolRoot, "BlockPools");
            if (blockRoot)
            {
                var rebuiltEntries = new List<BlockTypePoolEntry>(blockRoot.childCount);
                for (var i = 0; i < blockRoot.childCount; i++)
                {
                    var section = blockRoot.GetChild(i);
                    if (!section)
                    {
                        continue;
                    }

                    var shapeKey = ResolveShapeKeyFromPoolSectionName(section.name);
                    if (string.IsNullOrWhiteSpace(shapeKey))
                    {
                        continue;
                    }

                    var blockBindings = CollectImmediateChildComponents<BlockPoolBindings>(section);
                    rebuiltEntries.Add(new BlockTypePoolEntry
                    {
                        shapeKey = shapeKey,
                        blockBindings = blockBindings
                    });
                }

                rebuiltEntries.Sort((left, right) => string.CompareOrdinal(left?.shapeKey, right?.shapeKey));
                blockTypePools = rebuiltEntries;
            }

            RefreshPools(validateAuthoringTargets: false);
            EditorUtility.SetDirty(this);
        }

        public void EditorRepairPoolReferencesFromHierarchy()
        {
            var resolvedPoolRoot = FindBestPoolRoot();
            if (!resolvedPoolRoot)
            {
                return;
            }

            poolRoot = resolvedPoolRoot;

            TryRebindChildObjects(resolvedPoolRoot, "GridCellPool", "GridCell_", ref gridCellObjects);
            TryRebindChildObjects(resolvedPoolRoot, "BlockedCellPool", "BlockedCell_", ref blockedCellObjects);
            TryRebindChildObjects(resolvedPoolRoot, "BorderPool", "Border_", ref borderObjects);
            TryRebindChildComponents(resolvedPoolRoot, "DoorPool", ref doorBindings);

            var backdropRoot = FindDescendantByName(resolvedPoolRoot, "Backdrop");
            if (backdropRoot && backdropRoot.childCount > 0)
            {
                var backdropChild = backdropRoot.GetChild(0);
                if (backdropChild)
                {
                    backdropObject = backdropChild.gameObject;
                }
            }

            var blockRoot = FindDescendantByName(resolvedPoolRoot, "BlockPools");
            if (blockRoot)
            {
                var rebuiltEntries = new List<BlockTypePoolEntry>(blockRoot.childCount);
                for (var i = 0; i < blockRoot.childCount; i++)
                {
                    var section = blockRoot.GetChild(i);
                    if (!section)
                    {
                        continue;
                    }

                    var shapeKey = ResolveShapeKeyFromPoolSectionName(section.name);
                    if (string.IsNullOrWhiteSpace(shapeKey))
                    {
                        continue;
                    }

                    var bindings = CollectDescendantComponents(section, new List<BlockPoolBindings>());
                    if (bindings.Count == 0)
                    {
                        continue;
                    }

                    rebuiltEntries.Add(new BlockTypePoolEntry
                    {
                        shapeKey = shapeKey,
                        blockBindings = bindings
                    });
                }

                if (rebuiltEntries.Count > 0)
                {
                    rebuiltEntries.Sort((left, right) => string.CompareOrdinal(left?.shapeKey, right?.shapeKey));
                    blockTypePools = rebuiltEntries;
                }
            }

            SanitizePoolList(gridCellObjects);
            SanitizePoolList(blockedCellObjects);
            SanitizePoolList(borderObjects);
            SanitizePoolList(doorBindings);
            SanitizeBlockTypePools();
            RebuildBlockTypeLookup();

            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            if (poolRoot)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(poolRoot);
            }

            EditorUtility.SetDirty(this);
        }

        private Transform FindBestPoolRoot()
        {
            var candidates = new List<Transform>(4);
            CollectDescendantsByExactName(transform, "PoolRoot", candidates);
            if (candidates.Count == 0)
            {
                return null;
            }

            Transform best = null;
            var bestScore = int.MinValue;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate)
                {
                    continue;
                }

                var score = CountDescendants(candidate);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                best = candidate;
            }

            return best;
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

        private Transform ResolveOrCreatePoolRootWithoutTouchingOtherSections()
        {
            if (poolRoot && poolRoot.parent)
            {
                return poolRoot;
            }

            var foundRoot = FindDescendantByName(transform, "PoolRoot");
            if (foundRoot)
            {
                return foundRoot;
            }

            return ResolveOrCreatePoolRoot();
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

        private static Transform ResolveOrCreateChildByName(Transform parent, string childName)
        {
            if (!parent || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            var childCount = parent.childCount;
            for (var i = 0; i < childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child && string.Equals(child.name, childName, System.StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return CreatePoolSectionRoot(childName, parent);
        }

        private static void TryRebindChildObjects(Transform root, string childRootName, string entryNamePrefix,
            ref List<GameObject> targetList)
        {
            var childRoot = FindDescendantByName(root, childRootName);
            if (!childRoot)
            {
                return;
            }

            var rebound = CollectDescendantObjectsByNamePrefix(childRoot, entryNamePrefix);
            if (rebound.Count == 0)
            {
                return;
            }

            targetList = rebound;
        }

        private static void TryRebindChildComponents<T>(Transform root, string childRootName, ref List<T> targetList)
            where T : Component
        {
            var childRoot = FindDescendantByName(root, childRootName);
            if (!childRoot)
            {
                return;
            }

            var rebound = CollectDescendantComponents(childRoot, new List<T>());
            if (rebound.Count == 0)
            {
                return;
            }

            targetList = rebound;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (!root || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (string.Equals(root.name, name, System.StringComparison.Ordinal))
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                var match = FindDescendantByName(child, name);
                if (match)
                {
                    return match;
                }
            }

            return null;
        }

        private static void CollectDescendantsByExactName(Transform root, string name, List<Transform> collector)
        {
            if (!root || string.IsNullOrWhiteSpace(name) || collector == null)
            {
                return;
            }

            if (string.Equals(root.name, name, System.StringComparison.Ordinal))
            {
                collector.Add(root);
            }

            for (var i = 0; i < root.childCount; i++)
            {
                CollectDescendantsByExactName(root.GetChild(i), name, collector);
            }
        }

        private static int CountDescendants(Transform root)
        {
            if (!root)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < root.childCount; i++)
            {
                count += 1 + CountDescendants(root.GetChild(i));
            }

            return count;
        }

        private static List<GameObject> CollectDescendantObjectsByNamePrefix(Transform root, string namePrefix)
        {
            var result = new List<GameObject>();
            if (!root || string.IsNullOrWhiteSpace(namePrefix))
            {
                return result;
            }

            CollectDescendantObjectsByNamePrefix(root, namePrefix, result);
            result.Sort((left, right) => CompareByNameAndIndex(left ? left.name : string.Empty, right ? right.name : string.Empty));
            return result;
        }

        private static void CollectDescendantObjectsByNamePrefix(Transform root, string namePrefix,
            List<GameObject> collector)
        {
            if (!root || collector == null)
            {
                return;
            }

            if (root != null && root.gameObject &&
                root.name.StartsWith(namePrefix, System.StringComparison.Ordinal))
            {
                collector.Add(root.gameObject);
            }

            for (var i = 0; i < root.childCount; i++)
            {
                CollectDescendantObjectsByNamePrefix(root.GetChild(i), namePrefix, collector);
            }
        }

        private static List<T> CollectDescendantComponents<T>(Transform root, List<T> collector) where T : Component
        {
            collector ??= new List<T>();
            if (!root)
            {
                return collector;
            }

            var component = root.GetComponent<T>();
            if (component)
            {
                collector.Add(component);
            }

            for (var i = 0; i < root.childCount; i++)
            {
                CollectDescendantComponents(root.GetChild(i), collector);
            }

            collector.Sort((left, right) =>
                CompareByNameAndIndex(left ? left.gameObject.name : string.Empty, right ? right.gameObject.name : string.Empty));
            return collector;
        }

        private static Transform FindChildByName(Transform parent, string childName)
        {
            if (!parent || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child && string.Equals(child.name, childName, System.StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static List<GameObject> CollectImmediateChildObjects(Transform parent)
        {
            var result = new List<GameObject>(parent ? parent.childCount : 0);
            if (!parent)
            {
                return result;
            }

            var orderedChildren = new List<Transform>(parent.childCount);
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child)
                {
                    orderedChildren.Add(child);
                }
            }

            orderedChildren.Sort((left, right) => CompareByNameAndIndex(left?.name, right?.name));
            for (var i = 0; i < orderedChildren.Count; i++)
            {
                var child = orderedChildren[i];
                if (child)
                {
                    result.Add(child.gameObject);
                }
            }

            return result;
        }

        private static List<T> CollectImmediateChildComponents<T>(Transform parent) where T : Component
        {
            var result = new List<T>(parent ? parent.childCount : 0);
            if (!parent)
            {
                return result;
            }

            var orderedChildren = new List<Transform>(parent.childCount);
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child)
                {
                    orderedChildren.Add(child);
                }
            }

            orderedChildren.Sort((left, right) => CompareByNameAndIndex(left?.name, right?.name));
            for (var i = 0; i < orderedChildren.Count; i++)
            {
                var child = orderedChildren[i];
                if (!child)
                {
                    continue;
                }

                if (child.TryGetComponent<T>(out var component) && component)
                {
                    result.Add(component);
                }
            }

            return result;
        }

        private static int CompareByNameAndIndex(string leftName, string rightName)
        {
            var leftIndex = ParseTrailingIndex(leftName);
            var rightIndex = ParseTrailingIndex(rightName);

            if (leftIndex == int.MaxValue || rightIndex == int.MaxValue)
            {
                return string.CompareOrdinal(leftName, rightName);
            }

            var numericCompare = leftIndex.CompareTo(rightIndex);
            return numericCompare != 0 ? numericCompare : string.CompareOrdinal(leftName, rightName);
        }

        private static int ParseTrailingIndex(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return int.MaxValue;
            }

            var cursor = name.Length - 1;
            while (cursor >= 0 && char.IsDigit(name[cursor]))
            {
                cursor--;
            }

            var digitStart = cursor + 1;
            if (digitStart >= name.Length)
            {
                return int.MaxValue;
            }

            var numberText = name.Substring(digitStart);
            return int.TryParse(numberText, out var parsedValue) ? parsedValue : int.MaxValue;
        }

        private static string ResolveShapeKeyFromPoolSectionName(string sectionName)
        {
            const string prefix = "BlockPool_";
            if (string.IsNullOrWhiteSpace(sectionName) ||
                !sectionName.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return sectionName.Substring(prefix.Length).Trim();
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
