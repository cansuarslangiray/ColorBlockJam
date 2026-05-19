using System.Collections.Generic;
using UnityEditor;
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
        [HideInInspector] [SerializeField] private List<GameObject> doorObjects = new(16);

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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            RefreshPools(validateAuthoringTargets: false);
        }
#endif

        public void RefreshPools(bool validateAuthoringTargets = true)
        {
            SanitizePoolList(gridCellObjects);
            SanitizePoolList(blockedCellObjects);
            SanitizePoolList(borderObjects);
            SanitizePoolList(doorBindings);
#if UNITY_EDITOR
            MigrateLegacyDoorObjects();
#endif
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
            WarnIfScenePoolIsShort("grid cell", gridCellObjects.Count, requiredCount);
        }

        public void EnsureBlockedCellPoolSize(int requiredCount)
        {
            requiredCount = Mathf.Max(0, requiredCount);
            blockedCellObjects ??= new List<GameObject>(requiredCount);
            SanitizePoolList(blockedCellObjects);
            WarnIfScenePoolIsShort("blocked cell", blockedCellObjects.Count, requiredCount);
        }

        public void EnsureDoorPoolSize(int requiredCount)
        {
            doorBindings ??= new List<DoorPoolBindings>();
            SanitizePoolList(doorBindings);
            WarnIfScenePoolIsShort("door", doorBindings.Count, requiredCount);
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
                        poolEntry.blockBindings?.Count ?? 0, requiredCount);
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

                poolEntry.blockBindings ??= new List<BlockPoolBindings>(TargetBlockPoolCountPerShape);
#if UNITY_EDITOR
                MigrateLegacyBlockObjects(poolEntry);
#endif
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
        private void MigrateLegacyDoorObjects()
        {
            if (doorObjects == null || doorObjects.Count == 0)
            {
                return;
            }

            doorBindings ??= new List<DoorPoolBindings>(doorObjects.Count);
            for (var i = 0; i < doorObjects.Count; i++)
            {
                var doorObject = doorObjects[i];
                if (!doorObject)
                {
                    continue;
                }

                if (!doorObject.TryGetComponent<DoorPoolBindings>(out var doorBinding))
                {
                    doorBinding = Undo.AddComponent<DoorPoolBindings>(doorObject);
                }

                if (doorBinding && !doorBindings.Contains(doorBinding))
                {
                    doorBindings.Add(doorBinding);
                }
            }
        }

        private static void MigrateLegacyBlockObjects(BlockTypePoolEntry poolEntry)
        {
            if (poolEntry == null || poolEntry.blockObjects == null || poolEntry.blockObjects.Count == 0)
            {
                return;
            }

            poolEntry.blockBindings ??= new List<BlockPoolBindings>(poolEntry.blockObjects.Count);
            for (var i = 0; i < poolEntry.blockObjects.Count; i++)
            {
                var blockObject = poolEntry.blockObjects[i];
                if (!blockObject)
                {
                    continue;
                }

                if (!blockObject.TryGetComponent<BlockPoolBindings>(out var blockBinding))
                {
                    blockBinding = Undo.AddComponent<BlockPoolBindings>(blockObject);
                }

                if (blockBinding && !poolEntry.blockBindings.Contains(blockBinding))
                {
                    poolEntry.blockBindings.Add(blockBinding);
                }
            }
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
