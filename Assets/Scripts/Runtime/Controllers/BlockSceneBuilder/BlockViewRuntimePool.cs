using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class BlockViewRuntimePool
    {
        private const string PlacementAnchorPrefix = "__BlockPlacementAnchor_";
        private readonly Dictionary<string, List<BlockRootView>> _inactiveBlockRootsByKey =
            new(StringComparer.Ordinal);
        private readonly Dictionary<int, BlockRootView> _activeBlockRootById = new();
        private readonly Dictionary<string, BlockRootView> _templateBlockViewByKey =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _runtimeOverflowCountByKey =
            new(StringComparer.Ordinal);
        private readonly List<GameObject> _runtimeOverflowRoots = new();
        private Action<GameObject, bool> _setActiveIfChanged;

        public void Rebind(
            IReadOnlyDictionary<string, List<GameObject>> blockObjectsByKey,
            Action<GameObject, bool> setActiveIfChanged)
        {
            CleanupRuntimeOverflowRoots();
            _activeBlockRootById.Clear();
            _inactiveBlockRootsByKey.Clear();
            _templateBlockViewByKey.Clear();
            _runtimeOverflowCountByKey.Clear();
            _setActiveIfChanged = setActiveIfChanged;

            if (blockObjectsByKey == null)
            {
                return;
            }

            var pooledRootIds = new HashSet<int>();
            foreach (var pair in blockObjectsByKey)
            {
                AddBlockViewsFromPool(pair.Key, pair.Value, pooledRootIds, setActiveIfChanged);
            }
        }

        public void ReleaseAllActive(
            Action<int> stopBlockExit,
            Action<GameObject, bool> setActiveIfChanged,
            Action<BlockRootView> beforeRelease = null)
        {
            foreach (var pair in _activeBlockRootById)
            {
                beforeRelease?.Invoke(pair.Value);
                Release(
                    pair.Key,
                    pair.Value,
                    stopRoutines: true,
                    stopBlockExit,
                    setActiveIfChanged);
            }

            _activeBlockRootById.Clear();
        }

        public bool TryGetActive(int blockId, out BlockRootView blockView)
        {
            return _activeBlockRootById.TryGetValue(blockId, out blockView);
        }

        public void MarkActive(int blockId, BlockRootView blockView)
        {
            if (blockView == null)
            {
                return;
            }

            _activeBlockRootById[blockId] = blockView;
        }

        public void ForEachActive(Action<int, BlockRootView> visitor)
        {
            if (visitor == null || _activeBlockRootById.Count == 0)
            {
                return;
            }

            foreach (var pair in _activeBlockRootById)
            {
                visitor(pair.Key, pair.Value);
            }
        }

        public BlockRootView Acquire(string poolKey)
        {
            if (string.IsNullOrWhiteSpace(poolKey))
            {
                return null;
            }

            if (!_inactiveBlockRootsByKey.TryGetValue(poolKey, out var typePool) || typePool.Count == 0)
            {
                return CreateRuntimeOverflowView(poolKey);
            }

            var lastIndex = typePool.Count - 1;
            var blockView = typePool[lastIndex];
            typePool.RemoveAt(lastIndex);
            return blockView;
        }

        public void ReleaseAndRemove(
            int blockId,
            bool stopRoutines,
            Action<int> stopBlockExit,
            Action<GameObject, bool> setActiveIfChanged)
        {
            if (!_activeBlockRootById.TryGetValue(blockId, out var blockView))
            {
                return;
            }

            Release(
                blockId,
                blockView,
                stopRoutines,
                stopBlockExit,
                setActiveIfChanged);

            _activeBlockRootById.Remove(blockId);
        }

        public void EnsureBlockCells(BlockRootView blockView, int requiredCellCount)
        {
            if (blockView == null || requiredCellCount <= blockView.Cells.Count)
            {
                return;
            }

            if (!blockView.HasLoggedMissingBlockCells)
            {
                Debug.LogWarning(
                    $"Block pool object '{blockView.RootObject.name}' has {blockView.Cells.Count} cells but level needs {requiredCellCount}. " +
                    "Runtime cell generation is disabled. Add missing BlockCell_* children in prefab/scene pool.",
                    blockView.RootObject);
                blockView.HasLoggedMissingBlockCells = true;
            }
        }

        private void AddBlockViewsFromPool(
            string poolKey,
            IReadOnlyList<GameObject> sourcePool,
            HashSet<int> pooledRootIds,
            Action<GameObject, bool> setActiveIfChanged)
        {
            if (string.IsNullOrWhiteSpace(poolKey) || sourcePool == null)
            {
                return;
            }

            var sourceCount = sourcePool.Count;
            for (var i = 0; i < sourceCount; i++)
            {
                var rootObject = sourcePool[i];
                if (!rootObject)
                {
                    continue;
                }

                var rootId = rootObject.GetInstanceID();
                if (!pooledRootIds.Add(rootId))
                {
                    continue;
                }

                var blockView = new BlockRootView(rootObject)
                {
                    PoolKey = poolKey
                };

                rootObject.TryGetComponent(out Animator animator);
                blockView.Animator = animator;
                blockView.PlacementTransform = ResolvePlacementTransform(rootObject);

                CacheBlockCellPool(blockView, setActiveIfChanged);
                if (!_templateBlockViewByKey.ContainsKey(poolKey))
                {
                    _templateBlockViewByKey[poolKey] = blockView;
                }

                GetOrCreateInactivePool(poolKey).Add(blockView);
                setActiveIfChanged?.Invoke(rootObject, false);
            }
        }

        private BlockRootView CreateRuntimeOverflowView(string poolKey)
        {
            if (!_templateBlockViewByKey.TryGetValue(poolKey, out var templateView) ||
                templateView?.RootObject == null)
            {
                return null;
            }

            var templateRoot = templateView.RootObject;
            if (!_runtimeOverflowCountByKey.TryGetValue(poolKey, out var overflowCount))
            {
                overflowCount = 0;
            }

            overflowCount++;
            _runtimeOverflowCountByKey[poolKey] = overflowCount;

            var overflowSuffix = $"{overflowCount:000}";
            GameObject clonedRoot;
            if (templateView.PlacementTransform &&
                templateView.PlacementTransform != templateView.RootTransform &&
                templateView.PlacementTransform.name.StartsWith(PlacementAnchorPrefix, StringComparison.Ordinal))
            {
                var templateAnchor = templateView.PlacementTransform;
                var anchorClone = UnityEngine.Object.Instantiate(
                    templateAnchor.gameObject,
                    templateAnchor.parent);
                if (!anchorClone || anchorClone.transform.childCount <= 0)
                {
                    if (anchorClone)
                    {
                        UnityEngine.Object.Destroy(anchorClone);
                    }

                    return null;
                }

                anchorClone.name = $"{templateAnchor.name}_Overflow_{overflowSuffix}";
                anchorClone.SetActive(templateAnchor.gameObject.activeSelf);
                clonedRoot = anchorClone.transform.GetChild(0).gameObject;
                _runtimeOverflowRoots.Add(anchorClone);
            }
            else
            {
                var templateTransform = templateRoot.transform;
                var parent = templateTransform.parent;
                clonedRoot = UnityEngine.Object.Instantiate(templateRoot, parent);
                if (!clonedRoot)
                {
                    return null;
                }

                _runtimeOverflowRoots.Add(clonedRoot);
            }

            clonedRoot.name = $"{templateRoot.name}_Overflow_{overflowSuffix}";

            var blockView = new BlockRootView(clonedRoot)
            {
                PoolKey = poolKey
            };

            clonedRoot.TryGetComponent(out Animator animator);
            blockView.Animator = animator;
            blockView.PlacementTransform = ResolvePlacementTransform(clonedRoot);
            _setActiveIfChanged?.Invoke(clonedRoot, false);
            CacheBlockCellPool(blockView, _setActiveIfChanged);
            return blockView;
        }

        private void CleanupRuntimeOverflowRoots()
        {
            for (var i = 0; i < _runtimeOverflowRoots.Count; i++)
            {
                var overflowRoot = _runtimeOverflowRoots[i];
                if (overflowRoot)
                {
                    UnityEngine.Object.Destroy(overflowRoot);
                }
            }

            _runtimeOverflowRoots.Clear();
        }

        private List<BlockRootView> GetOrCreateInactivePool(string poolKey)
        {
            if (_inactiveBlockRootsByKey.TryGetValue(poolKey, out var pool))
            {
                return pool;
            }

            pool = new List<BlockRootView>(16);
            _inactiveBlockRootsByKey[poolKey] = pool;
            return pool;
        }

        private static void CacheBlockCellPool(BlockRootView blockView, Action<GameObject, bool> setActiveIfChanged)
        {
            blockView.Cells.Clear();
            blockView.CellRenderers.Clear();
            if (blockView.RootTransform == null)
            {
                return;
            }

            var childCount = blockView.RootTransform.childCount;
            if (childCount <= 0)
            {
                return;
            }

            for (var i = 0; i < childCount; i++)
            {
                var child = blockView.RootTransform.GetChild(i);
                if (!child)
                {
                    continue;
                }

                var cellObject = child.gameObject;
                if (!cellObject.name.StartsWith("BlockCell_", StringComparison.Ordinal))
                {
                    continue;
                }

                blockView.Cells.Add(cellObject);
                blockView.CellRenderers.Add(cellObject.TryGetComponent<Renderer>(out var renderer) ? renderer : null);
                setActiveIfChanged?.Invoke(cellObject, false);
            }
        }

        private void Release(
            int blockId,
            BlockRootView blockView,
            bool stopRoutines,
            Action<int> stopBlockExit,
            Action<GameObject, bool> setActiveIfChanged)
        {
            if (blockView == null)
            {
                return;
            }

            if (stopRoutines)
            {
                stopBlockExit?.Invoke(blockId);
            }

            blockView.RootTransform.localScale = Vector3.one;
            if (blockView.PlacementTransform)
            {
                blockView.PlacementTransform.localScale = Vector3.one;
            }

            for (var i = 0; i < blockView.Cells.Count; i++)
            {
                var cellObject = blockView.Cells[i];
                if (cellObject)
                {
                    setActiveIfChanged?.Invoke(cellObject, false);
                }
            }

            setActiveIfChanged?.Invoke(blockView.RootObject, false);
            GetOrCreateInactivePool(blockView.PoolKey).Add(blockView);
        }

        private static Transform ResolvePlacementTransform(GameObject rootObject)
        {
            if (!rootObject)
            {
                return null;
            }

            var rootTransform = rootObject.transform;
            var existingParent = rootTransform.parent;
            if (existingParent &&
                existingParent.name.StartsWith(PlacementAnchorPrefix, StringComparison.Ordinal) &&
                existingParent.childCount == 1 &&
                existingParent.GetChild(0) == rootTransform)
            {
                return existingParent;
            }

            return rootTransform;
        }
    }
}
