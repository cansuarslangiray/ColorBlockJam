using System;
using System.Collections.Generic;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class BlockViewRuntimePool
    {
        private readonly Dictionary<BlockShapeType, List<BlockRootView>> _inactiveBlockRootsByType = new();
        private readonly Dictionary<int, BlockRootView> _activeBlockRootById = new();

        public void Rebind(
            IReadOnlyDictionary<BlockShapeType, List<GameObject>> blockObjectsByType,
            Action<GameObject, bool> setActiveIfChanged)
        {
            _activeBlockRootById.Clear();
            _inactiveBlockRootsByType.Clear();

            if (blockObjectsByType == null)
            {
                return;
            }

            var pooledRootIds = new HashSet<int>();
            foreach (var pair in blockObjectsByType)
            {
                AddBlockViewsFromPool(pair.Key, pair.Value, pooledRootIds, setActiveIfChanged);
            }
        }

        public void ReleaseAllActive(
            Action<int> stopBlockMove,
            Action<int> stopBlockExit,
            Action<GameObject, bool> setActiveIfChanged)
        {
            foreach (var pair in _activeBlockRootById)
            {
                Release(
                    pair.Key,
                    pair.Value,
                    stopRoutines: true,
                    stopBlockMove,
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

        public BlockRootView Acquire(BlockShapeType blockType)
        {
            if (!_inactiveBlockRootsByType.TryGetValue(blockType, out var typePool) || typePool.Count == 0)
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
            Action<int> stopBlockMove,
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
                stopBlockMove,
                stopBlockExit,
                setActiveIfChanged);

            _activeBlockRootById.Remove(blockId);
        }

        public void EnsureBlockCells(
            BlockScenePoolManager poolManager,
            BlockRootView blockView,
            int requiredCellCount,
            Action<GameObject, bool> setActiveIfChanged)
        {
            if (poolManager == null || blockView == null || requiredCellCount <= blockView.Cells.Count)
            {
                return;
            }

            poolManager.EnsureBlockRootCellPoolSize(blockView.RootObject, requiredCellCount);
            CacheBlockCellPool(blockView, setActiveIfChanged);
        }

        private void AddBlockViewsFromPool(
            BlockShapeType blockType,
            IReadOnlyList<GameObject> sourcePool,
            HashSet<int> pooledRootIds,
            Action<GameObject, bool> setActiveIfChanged)
        {
            if (sourcePool == null)
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
                    BlockType = blockType
                };

                CacheBlockCellPool(blockView, setActiveIfChanged);
                GetOrCreateInactivePool(blockType).Add(blockView);
                setActiveIfChanged?.Invoke(rootObject, false);
            }
        }

        private List<BlockRootView> GetOrCreateInactivePool(BlockShapeType blockType)
        {
            if (_inactiveBlockRootsByType.TryGetValue(blockType, out var pool))
            {
                return pool;
            }

            pool = new List<BlockRootView>(16);
            _inactiveBlockRootsByType[blockType] = pool;
            return pool;
        }

        private static void CacheBlockCellPool(BlockRootView blockView, Action<GameObject, bool> setActiveIfChanged)
        {
            blockView.Cells.Clear();
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
                blockView.Cells.Add(cellObject);
                setActiveIfChanged?.Invoke(cellObject, false);
            }
        }

        private void Release(
            int blockId,
            BlockRootView blockView,
            bool stopRoutines,
            Action<int> stopBlockMove,
            Action<int> stopBlockExit,
            Action<GameObject, bool> setActiveIfChanged)
        {
            if (blockView == null)
            {
                return;
            }

            if (stopRoutines)
            {
                stopBlockMove?.Invoke(blockId);
                stopBlockExit?.Invoke(blockId);
            }

            blockView.RootTransform.localScale = Vector3.one;

            for (var i = 0; i < blockView.Cells.Count; i++)
            {
                var cellObject = blockView.Cells[i];
                if (cellObject)
                {
                    setActiveIfChanged?.Invoke(cellObject, false);
                }
            }

            setActiveIfChanged?.Invoke(blockView.RootObject, false);
            GetOrCreateInactivePool(blockView.BlockType).Add(blockView);
        }
    }
}
