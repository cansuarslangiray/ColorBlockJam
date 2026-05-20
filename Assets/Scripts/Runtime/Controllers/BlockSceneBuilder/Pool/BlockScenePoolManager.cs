using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Pool
{
    [DisallowMultipleComponent]
    public sealed partial class BlockScenePoolManager : MonoBehaviour
    {
        public const int TargetBoardPoolWidth = 25;
        public const int TargetBoardPoolHeight = 25;
        public const int TargetDoorPoolCount = 16;
        public const int TargetBlockPoolCountPerShape = 15;

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
