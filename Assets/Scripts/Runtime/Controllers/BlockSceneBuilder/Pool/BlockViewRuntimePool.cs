using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Pool
{
    public sealed class BlockViewRuntimePool
    {
        private static readonly Renderer[] EmptyRendererArray = Array.Empty<Renderer>();
        private readonly Dictionary<string, List<BlockRootView>> _inactiveBlockRootsByKey =
            new(StringComparer.Ordinal);
        private readonly Dictionary<int, BlockRootView> _activeBlockRootById = new();

        public void Rebind(
            IReadOnlyDictionary<string, List<BlockPoolBindings>> blockBindingsByKey,
            Action<GameObject, bool> setActiveIfChanged)
        {
            _activeBlockRootById.Clear();
            _inactiveBlockRootsByKey.Clear();

            var pooledRootIds = new HashSet<int>();
            foreach (var pair in blockBindingsByKey)
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
                return null;
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
        }

        private void AddBlockViewsFromPool(
            string poolKey,
            IReadOnlyList<BlockPoolBindings> sourcePool,
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
                var rootBinding = sourcePool[i];
                if (!rootBinding || !rootBinding.RootObject)
                {
                    continue;
                }

                var rootObject = rootBinding.RootObject;
                var rootId = rootObject.GetInstanceID();
                if (!pooledRootIds.Add(rootId))
                {
                    continue;
                }

                var blockView = new BlockRootView(rootObject)
                {
                    PoolKey = poolKey
                };
                blockView.PlacementTransform = rootBinding.PlacementTransform;

                CacheBlockCellPool(blockView, rootBinding, setActiveIfChanged);

                GetOrCreateInactivePool(poolKey).Add(blockView);
                setActiveIfChanged?.Invoke(rootObject, false);
            }
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

        private static void CacheBlockCellPool(BlockRootView blockView, BlockPoolBindings rootBinding,
            Action<GameObject, bool> setActiveIfChanged)
        {
            blockView.Cells.Clear();
            blockView.CellRenderers.Clear();
            blockView.CellNestedRenderers.Clear();
            blockView.ConditionIndicatorObject = null;
            blockView.ConditionIndicatorText = null;
            blockView.PooledConditionIndicatorObject = null;
            blockView.PooledConditionIndicatorText = null;
            blockView.DragOutlineRenderer = null;
            blockView.PooledDragOutlineRenderer = null;
            blockView.PooledDoorExitBurstParticle = null;
            blockView.PooledDoorExitBurstRenderer = null;
            if (blockView.RootTransform == null || rootBinding == null)
            {
                return;
            }

            var cellBindings = rootBinding.CellBindings;
            var cellCount = cellBindings?.Count ?? 0;
            if (cellCount <= 0)
            {
                return;
            }

            for (var i = 0; i < cellCount; i++)
            {
                var cellBinding = cellBindings[i];
                var cellObject = cellBinding?.cellObject;
                if (!cellObject)
                {
                    continue;
                }

                blockView.Cells.Add(cellObject);
                var nestedRenderers = cellBinding.nestedRenderers;
                blockView.CellNestedRenderers.Add(nestedRenderers == null || nestedRenderers.Length == 0
                    ? EmptyRendererArray
                    : nestedRenderers);
                blockView.CellRenderers.Add(
                    cellBinding.primaryRenderer
                        ? cellBinding.primaryRenderer
                        : nestedRenderers != null && nestedRenderers.Length > 0
                            ? nestedRenderers[0]
                            : null);
                setActiveIfChanged?.Invoke(cellObject, false);
            }

            blockView.PooledConditionIndicatorText = rootBinding.ConditionIndicatorText;
            blockView.PooledConditionIndicatorObject = rootBinding.ConditionIndicatorText
                ? rootBinding.ConditionIndicatorText.gameObject
                : null;
            blockView.ConditionIndicatorText = blockView.PooledConditionIndicatorText;
            blockView.ConditionIndicatorObject = blockView.PooledConditionIndicatorObject;
            if (blockView.PooledConditionIndicatorObject)
            {
                setActiveIfChanged?.Invoke(blockView.PooledConditionIndicatorObject, false);
            }

            blockView.PooledDragOutlineRenderer = rootBinding.DragOutlineRenderer;
            blockView.DragOutlineRenderer = blockView.PooledDragOutlineRenderer;
            if (blockView.PooledDragOutlineRenderer && blockView.PooledDragOutlineRenderer.gameObject)
            {
                setActiveIfChanged?.Invoke(blockView.PooledDragOutlineRenderer.gameObject, false);
            }

            blockView.PooledDoorExitBurstParticle = rootBinding.DoorExitParticle;
            blockView.PooledDoorExitBurstRenderer = rootBinding.DoorExitParticleRenderer;
            if (blockView.PooledDoorExitBurstParticle && blockView.PooledDoorExitBurstParticle.gameObject)
            {
                setActiveIfChanged?.Invoke(blockView.PooledDoorExitBurstParticle.gameObject, false);
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
            blockView.PlacementTransform.localScale = Vector3.one;

            for (var i = 0; i < blockView.Cells.Count; i++)
            {
                var cellObject = blockView.Cells[i];
                if (cellObject)
                {
                    setActiveIfChanged?.Invoke(cellObject, false);
                }
            }

            if (blockView.ConditionIndicatorObject)
            {
                setActiveIfChanged?.Invoke(blockView.ConditionIndicatorObject, false);
            }

            if (blockView.DragOutlineRenderer && blockView.DragOutlineRenderer.gameObject)
            {
                setActiveIfChanged?.Invoke(blockView.DragOutlineRenderer.gameObject, false);
            }

            if (blockView.PooledDoorExitBurstParticle && blockView.PooledDoorExitBurstParticle.gameObject)
            {
                setActiveIfChanged?.Invoke(blockView.PooledDoorExitBurstParticle.gameObject, false);
            }

            setActiveIfChanged?.Invoke(blockView.RootObject, false);
            GetOrCreateInactivePool(blockView.PoolKey).Add(blockView);
        }
    }
}
