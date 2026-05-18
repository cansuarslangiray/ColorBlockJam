using System;
using System.Collections.Generic;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class BlockViewRuntimePool
    {
        private const string PlacementAnchorPrefix = "__BlockPlacementAnchor_";
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

                rootObject.TryGetComponent(out Animator animator);
                blockView.Animator = animator;
                blockView.PlacementTransform = EnsurePlacementAnchor(rootObject);

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
            GetOrCreateInactivePool(blockView.BlockType).Add(blockView);
        }

        private static Transform EnsurePlacementAnchor(GameObject rootObject)
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

            var anchorObject = new GameObject(PlacementAnchorPrefix + rootObject.GetInstanceID());
            var anchorTransform = anchorObject.transform;
            anchorTransform.SetParent(existingParent, false);
            anchorTransform.SetPositionAndRotation(rootTransform.position, rootTransform.rotation);
            anchorTransform.localScale = rootTransform.localScale;

            rootTransform.SetParent(anchorTransform, true);
            rootTransform.localPosition = Vector3.zero;
            rootTransform.localRotation = Quaternion.identity;
            rootTransform.localScale = Vector3.one;

            return anchorTransform;
        }
    }
}
