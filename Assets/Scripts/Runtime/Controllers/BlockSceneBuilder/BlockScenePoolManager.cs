using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    [DisallowMultipleComponent]
    public sealed class BlockScenePoolManager : MonoBehaviour
    {
        public const int TargetBoardPoolWidth = 25;
        public const int TargetBoardPoolHeight = 25;
        public const int TargetDoorPoolCount = 16;
        public const int TargetBlockPoolCountPerShape = 5;

        [Header("Shared Root")] [SerializeField]
        private Transform poolRoot;

        [Header("Grid Cell Pool")] [SerializeField]
        private List<GameObject> gridCellObjects = new(625);

        [Header("Board Frame")] [SerializeField]
        private List<GameObject> borderObjects = new(4);

        [SerializeField] private GameObject backdropObject;

        [Header("Door Pool")] [SerializeField]
        private List<GameObject> doorObjects = new(16);

        [Header("Block Pools")] [SerializeField]
        private List<BlockTypePoolEntry> blockTypePools = new(20);

        private readonly Dictionary<string, List<GameObject>> _blockObjectsByKey =
            new(System.StringComparer.Ordinal);

        public IReadOnlyList<GameObject> GridCellObjects => gridCellObjects;
        public IReadOnlyList<GameObject> BorderObjects => borderObjects;
        public IReadOnlyList<GameObject> DoorObjects => doorObjects;
        public IReadOnlyDictionary<string, List<GameObject>> BlockObjectsByKey => _blockObjectsByKey;
        public GameObject BackdropObject => backdropObject;
        public int RequiredBoardGridCellCount => TargetBoardPoolWidth * TargetBoardPoolHeight;

        public void RefreshPools(bool validateAuthoringTargets = true)
        {
            SanitizePoolList(gridCellObjects);
            SanitizePoolList(borderObjects);
            SanitizePoolList(doorObjects);
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
            WarnIfScenePoolIsShort("grid cell", gridCellObjects.Count, requiredCount);
        }

        public void EnsureDoorPoolSize(int requiredCount)
        {
            doorObjects ??= new List<GameObject>();
            SanitizePoolList(doorObjects);
            WarnIfScenePoolIsShort("door", doorObjects.Count, requiredCount);
        }

        public void EnsureBlockPoolSizes(IReadOnlyDictionary<string, int> requiredCountByKey,
            IReadOnlyDictionary<string, int> requiredCellCountByKey = null)
        {
            SanitizeBlockTypePools();
            if (requiredCountByKey != null)
            {
                foreach (var pair in requiredCountByKey)
                {
                    var requiredCount = Mathf.Max(0, pair.Value);
                    if (!TryGetBlockPoolEntry(pair.Key, out var poolEntry))
                    {
                        Debug.LogWarning(
                            $"BlockScenePoolManager missing block pool entry for shape '{pair.Key}'. " +
                            $"Add a pool entry with at least {TargetBlockPoolCountPerShape} objects.",
                            this);
                        continue;
                    }

                    WarnIfScenePoolIsShort($"block root ({poolEntry.ResolvePoolKey()})",
                        poolEntry.blockObjects?.Count ?? 0, requiredCount);
                }
            }

            WarnIfBlockPoolIsBelowAuthoringMinimum();

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

                poolEntry.blockObjects ??= new List<GameObject>(TargetBlockPoolCountPerShape);
                SanitizePoolList(poolEntry.blockObjects);
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
                    poolEntry.blockObjects?.Count ?? 0, TargetBlockPoolCountPerShape);
            }
        }

        private void RebuildBlockTypeLookup()
        {
            _blockObjectsByKey.Clear();
            for (var i = 0; i < blockTypePools.Count; i++)
            {
                var poolEntry = blockTypePools[i];
                if (poolEntry == null)
                {
                    continue;
                }

                poolEntry.blockObjects ??= new List<GameObject>(TargetBlockPoolCountPerShape);
                var poolKey = poolEntry.ResolvePoolKey();
                if (_blockObjectsByKey.ContainsKey(poolKey))
                {
                    continue;
                }

                _blockObjectsByKey[poolKey] = poolEntry.blockObjects;
            }
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

        private void ValidateAuthoringTargets()
        {
            WarnIfScenePoolIsShort("grid cell", gridCellObjects?.Count ?? 0, RequiredBoardGridCellCount);
            WarnIfScenePoolIsShort("door", doorObjects?.Count ?? 0, TargetDoorPoolCount);
            WarnIfScenePoolIsShort("border", borderObjects?.Count ?? 0, 4);
            WarnIfBlockPoolIsBelowAuthoringMinimum();
        }

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
