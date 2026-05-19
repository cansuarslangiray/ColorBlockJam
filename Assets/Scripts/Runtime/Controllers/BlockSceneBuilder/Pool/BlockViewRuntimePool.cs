using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Pool
{
    public sealed class BlockViewRuntimePool
    {
        private static readonly Renderer[] EmptyRendererArray = Array.Empty<Renderer>();
        private static readonly Material[] EmptyMaterialArray = Array.Empty<Material>();
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
                blockView.ShapeKey = rootBinding.ShapeKey;
                blockView.ShapeLocalCells = ResolveShapeLocalCells(rootBinding.ShapeLocalCells);

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
            blockView.CellDefaultMaterials.Clear();
            blockView.CellNestedDefaultMaterials.Clear();
            blockView.ActiveCellCount = 0;
            blockView.IsUsingLockedAppearance = false;
            blockView.ConditionIndicatorObject = null;
            blockView.ConditionIndicatorText = null;
            blockView.OutlineRenderer = null;
            blockView.HasCachedOutlineActiveColor = false;
            blockView.CachedOutlineActiveColor = default;
            blockView.HasAppliedOutlineColor = false;
            blockView.AppliedOutlineColor = default;
            blockView.DoorExitBurstParticle = null;
            blockView.DoorExitBurstParticles = Array.Empty<ParticleSystem>();
            blockView.DoorExitBurstRenderers = Array.Empty<Renderer>();
            if (blockView.RootTransform == null || rootBinding == null)
            {
                return;
            }

            var cellBindings = rootBinding.CellBindings;
            var cellCount = cellBindings?.Count ?? 0;

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
                var resolvedNestedRenderers = nestedRenderers == null || nestedRenderers.Length == 0
                    ? EmptyRendererArray
                    : nestedRenderers;
                var resolvedPrimaryRenderer = cellBinding.primaryRenderer
                    ? cellBinding.primaryRenderer
                    : resolvedNestedRenderers.Length > 0
                        ? resolvedNestedRenderers[0]
                        : null;
                blockView.CellNestedRenderers.Add(resolvedNestedRenderers);
                blockView.CellRenderers.Add(resolvedPrimaryRenderer);
                blockView.CellDefaultMaterials.Add(resolvedPrimaryRenderer ? resolvedPrimaryRenderer.sharedMaterial : null);
                blockView.CellNestedDefaultMaterials.Add(ResolveNestedDefaultMaterials(resolvedNestedRenderers));
                setActiveIfChanged?.Invoke(cellObject, false);
            }

            blockView.ConditionIndicatorText = rootBinding.ConditionIndicatorText;
            blockView.ConditionIndicatorObject = blockView.ConditionIndicatorText
                ? blockView.ConditionIndicatorText.gameObject
                : null;
            blockView.ConditionIndicatorDefaultLocalRotation = blockView.ConditionIndicatorObject
                ? blockView.ConditionIndicatorObject.transform.localRotation
                : Quaternion.identity;
            if (blockView.ConditionIndicatorObject)
            {
                setActiveIfChanged?.Invoke(blockView.ConditionIndicatorObject, false);
            }

            blockView.OutlineRenderer = rootBinding.OutlineRenderer;
            if (blockView.OutlineRenderer && blockView.OutlineRenderer.gameObject)
            {
                setActiveIfChanged?.Invoke(blockView.OutlineRenderer.gameObject, true);
            }

            blockView.DoorExitBurstParticle = rootBinding.DoorExitParticle;
            blockView.DoorExitBurstParticles = ResolveDoorExitBurstParticles(blockView.DoorExitBurstParticle);
            blockView.DoorExitBurstRenderers = ResolveDoorExitBurstRenderers(blockView.DoorExitBurstParticle);
            if (blockView.DoorExitBurstParticle && blockView.DoorExitBurstParticle.gameObject)
            {
                setActiveIfChanged?.Invoke(blockView.DoorExitBurstParticle.gameObject, false);
            }
        }

        private static Vector2Int[] ResolveShapeLocalCells(IReadOnlyList<Vector2Int> sourceCells)
        {
            if (sourceCells == null || sourceCells.Count == 0)
            {
                return Array.Empty<Vector2Int>();
            }

            var result = new Vector2Int[sourceCells.Count];
            for (var i = 0; i < sourceCells.Count; i++)
            {
                result[i] = sourceCells[i];
            }

            return result;
        }

        private static ParticleSystem[] ResolveDoorExitBurstParticles(ParticleSystem doorExitBurstRoot)
        {
            if (!doorExitBurstRoot)
            {
                return Array.Empty<ParticleSystem>();
            }

            return doorExitBurstRoot.GetComponentsInChildren<ParticleSystem>(true);
        }

        private static Renderer[] ResolveDoorExitBurstRenderers(ParticleSystem doorExitBurstRoot)
        {
            if (doorExitBurstRoot)
            {
                return doorExitBurstRoot.GetComponentsInChildren<Renderer>(true);
            }
            
            return Array.Empty<Renderer>();
        }

        private static Material[] ResolveNestedDefaultMaterials(Renderer[] nestedRenderers)
        {
            if (nestedRenderers == null || nestedRenderers.Length == 0)
            {
                return EmptyMaterialArray;
            }

            var defaultMaterials = new Material[nestedRenderers.Length];
            for (var i = 0; i < nestedRenderers.Length; i++)
            {
                var renderer = nestedRenderers[i];
                defaultMaterials[i] = renderer ? renderer.sharedMaterial : null;
            }

            return defaultMaterials;
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

            RestoreDefaultCellMaterials(blockView);

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

            if (blockView.OutlineRenderer && blockView.OutlineRenderer.enabled)
            {
                blockView.OutlineRenderer.enabled = false;
            }

            if (blockView.OutlineRenderer && blockView.OutlineRenderer.gameObject)
            {
                setActiveIfChanged?.Invoke(blockView.OutlineRenderer.gameObject, false);
            }

            if (blockView.DoorExitBurstParticle && blockView.DoorExitBurstParticle.gameObject)
            {
                setActiveIfChanged?.Invoke(blockView.DoorExitBurstParticle.gameObject, false);
            }

            setActiveIfChanged?.Invoke(blockView.RootObject, false);
            GetOrCreateInactivePool(blockView.PoolKey).Add(blockView);
        }

        private static void RestoreDefaultCellMaterials(BlockRootView blockView)
        {
            if (blockView == null)
            {
                return;
            }

            var primaryCount = Mathf.Min(blockView.CellRenderers.Count, blockView.CellDefaultMaterials.Count);
            for (var i = 0; i < primaryCount; i++)
            {
                var renderer = blockView.CellRenderers[i];
                if (!renderer)
                {
                    continue;
                }

                var defaultMaterial = blockView.CellDefaultMaterials[i];
                if (renderer.sharedMaterial != defaultMaterial)
                {
                    renderer.sharedMaterial = defaultMaterial;
                }

                renderer.SetPropertyBlock(null);
            }

            var nestedCount = Mathf.Min(blockView.CellNestedRenderers.Count, blockView.CellNestedDefaultMaterials.Count);
            for (var i = 0; i < nestedCount; i++)
            {
                var nestedRenderers = blockView.CellNestedRenderers[i];
                if (nestedRenderers == null)
                {
                    continue;
                }

                var nestedDefaultMaterials = blockView.CellNestedDefaultMaterials[i];
                for (var nestedIndex = 0; nestedIndex < nestedRenderers.Length; nestedIndex++)
                {
                    var renderer = nestedRenderers[nestedIndex];
                    if (!renderer)
                    {
                        continue;
                    }

                    var defaultMaterial =
                        nestedDefaultMaterials != null && nestedIndex < nestedDefaultMaterials.Length
                            ? nestedDefaultMaterials[nestedIndex]
                            : null;
                    if (renderer.sharedMaterial != defaultMaterial)
                    {
                        renderer.sharedMaterial = defaultMaterial;
                    }

                    renderer.SetPropertyBlock(null);
                }
            }

            blockView.IsUsingLockedAppearance = false;
        }
    }
}
