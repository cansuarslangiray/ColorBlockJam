using System;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class BlockVisualPresenter
    {
        public struct BuildRequest
        {
            public LevelDefinition LevelData;
            public BoardController BoardController;
            public BlockViewRuntimePool BlockViewPool;
            public LayoutMetrics Layout;
            public float BlockCellVisualScale;
            public Vector3 BlockRootScale;
            public float IndicatorHeightOffsetInCells;
            public float IndicatorLocalZOffset;
            public Func<BlockColor, Material> ResolveMaterial;
            public Action<BlockRootView, int> EnsureBlockCells;
            public Action<GameObject, bool> SetActiveIfChanged;
            public Action<Transform, Vector3, Vector3> ApplyWorldTransform;
            public Action<BlockRootView, bool> SetDragHighlightActive;
            public Action<BlockRootView, Vector2Int[]> CacheBlockOutlineGridLoop;
            public Action<BlockRootView> RefreshDragHighlightBounds;
            public Action<Animator> ResetBlockAnimatorState;
        }

        public void ApplyLevelBlockVisuals(in BuildRequest request)
        {
            var levelData = request.LevelData;
            var boardController = request.BoardController;
            var blockViewPool = request.BlockViewPool;
            if (levelData == null || boardController == null || blockViewPool == null)
            {
                return;
            }

            var sourceBlocks = levelData.blocks;
            if (sourceBlocks == null || sourceBlocks.Count == 0)
            {
                return;
            }

            for (var i = 0; i < sourceBlocks.Count; i++)
            {
                if (!boardController.TryGetRuntimeBlock(i, out var runtimeBlock))
                {
                    continue;
                }

                var poolKey = sourceBlocks[i].ResolvePoolKey();

                var blockView = blockViewPool.Acquire(poolKey);
                if (blockView == null)
                {
                    continue;
                }

                blockView.PoolKey = poolKey;
                ApplyBlockCells(blockView, runtimeBlock, request);

                var placementTransform = blockView.PlacementTransform
                    ? blockView.PlacementTransform
                    : blockView.RootTransform;
                request.ApplyWorldTransform?.Invoke(placementTransform,
                    ToWorldPosition(runtimeBlock.Position, request.Layout), request.BlockRootScale);
                request.SetDragHighlightActive?.Invoke(blockView, false);

                blockViewPool.MarkActive(i, blockView);
                request.SetActiveIfChanged?.Invoke(blockView.RootObject, true);
                request.ResetBlockAnimatorState?.Invoke(blockView.Animator);
            }
        }

        private static Vector3 ToWorldPosition(Vector2Int gridPosition, in LayoutMetrics layout)
        {
            return new Vector3(layout.BoardOrigin.x + (gridPosition.x * layout.CellSize),
                layout.BoardOrigin.y + (gridPosition.y * layout.CellSize), layout.BlockZ);
        }

        private static void ApplyBlockCells(BlockRootView blockView, RuntimeBlockState blockState,
            in BuildRequest request)
        {
            var localCells = blockState.LocalCells ?? Array.Empty<Vector2Int>();
            var cellSize = request.Layout.CellSize;
            var targetScale = Vector3.one * Mathf.Max(0.01f, cellSize * request.BlockCellVisualScale);
            var resolvedMaterial = request.ResolveMaterial?.Invoke(blockState.ColorType);
            blockView.HasCachedBlockColor = TryResolvePrimaryMaterialColor(resolvedMaterial, out var cachedBlockColor);
            blockView.CachedBlockColor = cachedBlockColor;

            request.EnsureBlockCells?.Invoke(blockView, localCells.Length);
            blockView.LocalCenter = ResolveLocalCenter(localCells, cellSize);
            blockView.ConditionIndicatorLocalAnchor =
                ResolveConditionIndicatorLocalAnchor(localCells, cellSize, request.IndicatorHeightOffsetInCells,
                    request.IndicatorLocalZOffset);

            var cells = blockView.Cells;
            var cellRenderers = blockView.CellRenderers;
            var pooledCellCount = cells.Count;
            var activeCellCount = Mathf.Min(localCells.Length, pooledCellCount);
            var hasLocalBounds = false;
            var localBoundsMin = Vector3.zero;
            var localBoundsMax = Vector3.zero;
            var localHalfExtents = targetScale * 0.5f;

            for (var i = 0; i < activeCellCount; i++)
            {
                var cellObject = cells[i];
                request.SetActiveIfChanged?.Invoke(cellObject, true);

                var localCell = localCells[i];
                var localPosition = new Vector3((localCell.x + 0.5f) * cellSize, (localCell.y + 0.5f) * cellSize, 0f);
                if (cellObject)
                {
                    cellObject.transform.localPosition = localPosition;
                    cellObject.transform.localRotation = Quaternion.identity;
                    cellObject.transform.localScale = targetScale;
                }

                var cellMin = localPosition - localHalfExtents;
                var cellMax = localPosition + localHalfExtents;
                if (!hasLocalBounds)
                {
                    localBoundsMin = cellMin;
                    localBoundsMax = cellMax;
                    hasLocalBounds = true;
                }
                else
                {
                    localBoundsMin = Vector3.Min(localBoundsMin, cellMin);
                    localBoundsMax = Vector3.Max(localBoundsMax, cellMax);
                }

                if (i < cellRenderers.Count)
                {
                    var cellRenderer = cellRenderers[i];
                    if (cellRenderer && resolvedMaterial && cellRenderer.sharedMaterial != resolvedMaterial)
                    {
                        cellRenderer.sharedMaterial = resolvedMaterial;
                    }
                }
            }

            for (var i = activeCellCount; i < pooledCellCount; i++)
            {
                request.SetActiveIfChanged?.Invoke(cells[i], false);
            }

            blockView.HasCachedLocalBounds = hasLocalBounds;
            blockView.CachedLocalBoundsMin = localBoundsMin;
            blockView.CachedLocalBoundsMax = localBoundsMax;
            request.CacheBlockOutlineGridLoop?.Invoke(blockView, localCells);
            request.RefreshDragHighlightBounds?.Invoke(blockView);
        }

        private static bool TryResolvePrimaryMaterialColor(Material sourceMaterial, out Color color)
        {
            color = Color.white;
            if (!sourceMaterial)
            {
                return false;
            }

            if (sourceMaterial.HasProperty("_BaseColor"))
            {
                color = sourceMaterial.GetColor("_BaseColor");
                return true;
            }

            if (sourceMaterial.HasProperty("_Color"))
            {
                color = sourceMaterial.GetColor("_Color");
                return true;
            }

            return false;
        }

        private static Vector2 ResolveLocalCenter(Vector2Int[] localCells, float cellSize)
        {
            if (localCells == null || localCells.Length == 0)
            {
                return Vector2.zero;
            }

            var minX = int.MaxValue;
            var minY = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;

            for (var i = 0; i < localCells.Length; i++)
            {
                var cell = localCells[i];
                if (cell.x < minX) minX = cell.x;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y < minY) minY = cell.y;
                if (cell.y > maxY) maxY = cell.y;
            }

            return new Vector2(
                ((minX + maxX + 1) * 0.5f) * cellSize,
                ((minY + maxY + 1) * 0.5f) * cellSize);
        }

        private static Vector3 ResolveConditionIndicatorLocalAnchor(Vector2Int[] localCells, float cellSize, float indicatorHeightOffsetInCells, float indicatorLocalZOffset)
        {
            if (localCells == null || localCells.Length == 0)
            {
                return new Vector3(
                    cellSize * 0.5f,
                    cellSize * (1f + indicatorHeightOffsetInCells),
                    indicatorLocalZOffset);
            }

            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var maxY = int.MinValue;

            for (var i = 0; i < localCells.Length; i++)
            {
                var cell = localCells[i];
                if (cell.x < minX) minX = cell.x;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y > maxY) maxY = cell.y;
            }

            var anchorX = ((minX + maxX + 1) * 0.5f) * cellSize;
            var anchorY = (maxY + 1f + indicatorHeightOffsetInCells) * cellSize;
            return new Vector3(anchorX, anchorY, indicatorLocalZOffset);
        }
    }
}
