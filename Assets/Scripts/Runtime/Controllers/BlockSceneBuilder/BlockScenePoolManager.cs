using System.Collections.Generic;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    [DisallowMultipleComponent]
    public sealed class BlockScenePoolManager : MonoBehaviour
    {
        [Header("Shared Root")] [SerializeField]
        private Transform poolRoot;

        [Header("Grid Cell Pool")] [SerializeField]
        private GameObject gridCellPrefab;

        [SerializeField] private List<GameObject> gridCellObjects = new(64);

        [Header("Board Frame")] [SerializeField]
        private List<GameObject> borderObjects = new(4);

        [SerializeField] private GameObject backdropObject;

        [Header("Door Pool")] [SerializeField]
        private GameObject doorPrefab;

        [SerializeField] private List<GameObject> doorObjects = new(16);

        [Header("Block Pools")] [SerializeField]
        private GameObject defaultBlockRootPrefab;

        [SerializeField] private List<BlockTypePoolEntry> blockTypePools = new(8);

        [Header("Fixed Pool Sizes")] [SerializeField, Min(0)]
        private int minimumGridCellPoolCount = 56;

        [SerializeField, Min(0)] private int minimumDoorPoolCount = 8;
        [SerializeField, Min(0)] private int minimumBlockRootPoolCountPerType = 8;

        private readonly Dictionary<BlockShapeType, List<GameObject>> _blockObjectsByType = new();

        public IReadOnlyList<GameObject> GridCellObjects => gridCellObjects;
        public IReadOnlyList<GameObject> BorderObjects => borderObjects;
        public IReadOnlyList<GameObject> DoorObjects => doorObjects;
        public IReadOnlyDictionary<BlockShapeType, List<GameObject>> BlockObjectsByType => _blockObjectsByType;
        public GameObject BackdropObject => backdropObject;

        public void RefreshPools()
        {
            SanitizePoolList(gridCellObjects);
            SanitizePoolList(borderObjects);
            SanitizePoolList(doorObjects);
            SanitizeBlockTypePools();
            EnsureGridCellPoolSize(minimumGridCellPoolCount);
            EnsureDoorPoolSize(minimumDoorPoolCount);
            EnsureMinimumBlockPoolSizePerType();
            RebuildBlockTypeLookup();
        }

        public void EnsureGridCellPoolSize(int requiredCount)
        {
            gridCellObjects ??= new List<GameObject>();
            requiredCount = Mathf.Max(requiredCount, minimumGridCellPoolCount);
            EnsurePoolSize(
                gridCellObjects,
                gridCellPrefab,
                "GridCell",
                requiredCount,
                useCubeFallback: true);
        }

        public void EnsureDoorPoolSize(int requiredCount)
        {
            doorObjects ??= new List<GameObject>();
            requiredCount = Mathf.Max(requiredCount, minimumDoorPoolCount);
            EnsurePoolSize(
                doorObjects,
                doorPrefab,
                "Door",
                requiredCount,
                useCubeFallback: true);
        }

        public void EnsureBlockPoolSizes(IReadOnlyDictionary<BlockShapeType, int> requiredCountByType)
        {
            SanitizeBlockTypePools();
            if (requiredCountByType != null)
            {
                foreach (var pair in requiredCountByType)
                {
                    var poolEntry = GetOrCreateBlockPoolEntry(pair.Key);
                    var requiredCount = Mathf.Max(pair.Value, minimumBlockRootPoolCountPerType);
                    EnsurePoolSize(
                        poolEntry.blockObjects,
                        ResolveBlockTemplate(poolEntry),
                        "BlockRoot_" + pair.Key,
                        requiredCount,
                        useCubeFallback: false);
                }
            }

            EnsureMinimumBlockPoolSizePerType();

            RebuildBlockTypeLookup();
        }

        public void EnsureBlockRootCellPoolSize(GameObject blockRootObject, int requiredCellCount,
            GameObject cellTemplate = null)
        {
            if (!blockRootObject || requiredCellCount <= 0)
            {
                return;
            }

            var blockRootTransform = blockRootObject.transform;
            while (blockRootTransform.childCount < requiredCellCount)
            {
                var resolvedTemplate = ResolveBlockCellTemplate(cellTemplate, blockRootObject);
                var cellIndex = blockRootTransform.childCount;
                var createdCell = CreateBlockCellObject(resolvedTemplate, blockRootTransform, cellIndex);
                if (!createdCell)
                {
                    break;
                }
            }
        }

        private void EnsurePoolSize(
            List<GameObject> serializedPool,
            GameObject explicitPrefab,
            string objectNamePrefix,
            int requiredCount,
            bool useCubeFallback)
        {
            if (serializedPool == null)
            {
                return;
            }

            SanitizePoolList(serializedPool);

            requiredCount = Mathf.Max(0, requiredCount);
            var targetCount = Mathf.Max(serializedPool.Count, requiredCount);

            while (serializedPool.Count < targetCount)
            {
                var template = ResolveTemplate(explicitPrefab, serializedPool);
                var created = CreatePoolObject(template, ResolvePoolRoot(), objectNamePrefix, serializedPool.Count,
                    useCubeFallback);
                if (!created)
                {
                    break;
                }

                serializedPool.Add(created);
            }
        }

        private void SanitizeBlockTypePools()
        {
            blockTypePools ??= new List<BlockTypePoolEntry>();

            for (var i = blockTypePools.Count - 1; i >= 0; i--)
            {
                var poolEntry = blockTypePools[i];
                if (poolEntry == null)
                {
                    blockTypePools.RemoveAt(i);
                    continue;
                }

                poolEntry.blockObjects ??= new List<GameObject>(16);
                SanitizePoolList(poolEntry.blockObjects);
            }
        }

        private void EnsureMinimumBlockPoolSizePerType()
        {
            if (minimumBlockRootPoolCountPerType <= 0 || blockTypePools == null || blockTypePools.Count == 0)
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

                EnsurePoolSize(
                    poolEntry.blockObjects,
                    ResolveBlockTemplate(poolEntry),
                    "BlockRoot_" + poolEntry.blockType,
                    minimumBlockRootPoolCountPerType,
                    useCubeFallback: false);
            }
        }

        private BlockTypePoolEntry GetOrCreateBlockPoolEntry(BlockShapeType blockType)
        {
            for (var i = 0; i < blockTypePools.Count; i++)
            {
                var poolEntry = blockTypePools[i];
                if (poolEntry != null && poolEntry.blockType == blockType)
                {
                    return poolEntry;
                }
            }

            var createdPool = new BlockTypePoolEntry
            {
                blockType = blockType,
                blockPrefab = defaultBlockRootPrefab,
                blockObjects = new List<GameObject>(16)
            };

            blockTypePools.Add(createdPool);
            return createdPool;
        }

        private void RebuildBlockTypeLookup()
        {
            _blockObjectsByType.Clear();
            for (var i = 0; i < blockTypePools.Count; i++)
            {
                var poolEntry = blockTypePools[i];
                if (poolEntry == null)
                {
                    continue;
                }

                poolEntry.blockObjects ??= new List<GameObject>(16);
                _blockObjectsByType[poolEntry.blockType] = poolEntry.blockObjects;
            }
        }

        private GameObject ResolveBlockTemplate(BlockTypePoolEntry poolEntry)
        {
            if (poolEntry != null && poolEntry.blockPrefab)
            {
                return poolEntry.blockPrefab;
            }

            if (defaultBlockRootPrefab)
            {
                return defaultBlockRootPrefab;
            }

            return poolEntry != null ? ResolveFirstAlive(poolEntry.blockObjects) : null;
        }

        private static void SanitizePoolList(List<GameObject> pool)
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

        private static GameObject ResolveTemplate(GameObject explicitPrefab, IReadOnlyList<GameObject> serializedPool)
        {
            if (explicitPrefab)
            {
                return explicitPrefab;
            }

            return ResolveFirstAlive(serializedPool);
        }

        private static GameObject ResolveFirstAlive(IReadOnlyList<GameObject> pool)
        {
            if (pool == null)
            {
                return null;
            }

            for (var i = 0; i < pool.Count; i++)
            {
                if (pool[i])
                {
                    return pool[i];
                }
            }

            return null;
        }

        private GameObject ResolveBlockCellTemplate(GameObject explicitTemplate, GameObject blockRootObject)
        {
            if (explicitTemplate)
            {
                return explicitTemplate;
            }

            if (blockRootObject)
            {
                var rootTransform = blockRootObject.transform;
                if (rootTransform && rootTransform.childCount > 0)
                {
                    var firstChild = rootTransform.GetChild(0);
                    if (firstChild)
                    {
                        return firstChild.gameObject;
                    }
                }
            }

            if (gridCellPrefab)
            {
                return gridCellPrefab;
            }

            return ResolveFirstAlive(gridCellObjects);
        }

        private static GameObject CreateBlockCellObject(GameObject template, Transform parent, int index)
        {
            GameObject createdCell;
            if (template)
            {
                createdCell = Instantiate(template, parent);
            }
            else
            {
                createdCell = new GameObject();
                if (parent)
                {
                    createdCell.transform.SetParent(parent, false);
                }
            }

            createdCell.name = "BlockCell_" + index;
            createdCell.SetActive(false);
            return createdCell;
        }

        private static GameObject CreatePoolObject(
            GameObject template,
            Transform parent,
            string objectNamePrefix,
            int index,
            bool useCubeFallback)
        {
            GameObject created;
            if (template)
            {
                created = Instantiate(template, parent);
            }
            else if (useCubeFallback)
            {
                created = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (created.TryGetComponent<Collider>(out var collider))
                {
                    Destroy(collider);
                }

                if (parent)
                {
                    created.transform.SetParent(parent, false);
                }
            }
            else
            {
                created = new GameObject();
                if (parent)
                {
                    created.transform.SetParent(parent, false);
                }
            }

            created.name = string.IsNullOrWhiteSpace(objectNamePrefix)
                ? "Pooled_" + index
                : objectNamePrefix + "_" + index;
            created.SetActive(false);
            return created;
        }

        private Transform ResolvePoolRoot()
        {
            return poolRoot ? poolRoot : transform;
        }
    }
}
