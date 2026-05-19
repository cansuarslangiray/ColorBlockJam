using System;
using Runtime.Controllers.BlockSceneBuilder.Board;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Blocks
{
    public sealed class BlockVisualPresenter
    {
        public void ApplyLevelBlockVisuals(in BlockVisualBuildRequest request)
        {
            var levelData = request.LevelData;
            var boardController = request.BoardController;
            var blockViewPool = request.BlockViewPool;

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
                request.ApplyWorldTransform(placementTransform,
                    ToWorldPosition(runtimeBlock.Position, request.Layout), request.BlockRootScale);
                request.SetDragHighlightActive(blockView, false);

                blockViewPool.MarkActive(i, blockView);
                request.SetActiveIfChanged(blockView.RootObject, true);
            }
        }

        private static Vector3 ToWorldPosition(Vector2Int gridPosition, in LayoutMetrics layout)
        {
            return new Vector3(layout.BoardOrigin.x + (gridPosition.x * layout.CellSize),
                layout.BoardOrigin.y + (gridPosition.y * layout.CellSize), layout.BlockZ);
        }

        private static void ApplyBlockCells(BlockRootView blockView, RuntimeBlockState blockState,
            in BlockVisualBuildRequest request)
        {
            var localCells = blockState.LocalCells ?? Array.Empty<Vector2Int>();
            var cellSize = request.Layout.CellSize;
            var targetScale = Vector3.one * Mathf.Max(0.01f, cellSize * request.BlockCellVisualScale);
            var resolvedMaterial = request.ResolveMaterial(blockState.ColorType);
            blockView.HasCachedBlockColor = TryResolvePrimaryMaterialColor(resolvedMaterial, out var cachedBlockColor);
            blockView.CachedBlockColor = cachedBlockColor;

            request.EnsureBlockCells(blockView, localCells.Length);
            blockView.LocalCenter = ResolveLocalCenter(localCells, cellSize);

            var cells = blockView.Cells;
            var cellRenderers = blockView.CellRenderers;
            var cellNestedRenderers = blockView.CellNestedRenderers;
            var pooledCellCount = cells.Count;
            var activeCellCount = Mathf.Min(localCells.Length, pooledCellCount);

            for (var i = 0; i < activeCellCount; i++)
            {
                var cellObject = cells[i];
                request.SetActiveIfChanged(cellObject, true);

                var localCell = localCells[i];
                var localPosition = new Vector3((localCell.x + 0.5f) * cellSize, (localCell.y + 0.5f) * cellSize, 0f);
                if (cellObject)
                {
                    cellObject.transform.localPosition = localPosition;
                    cellObject.transform.localScale = targetScale;
                }

                if (i < cellRenderers.Count)
                {
                    var cellRenderer = cellRenderers[i];
                    if (cellRenderer && resolvedMaterial && cellRenderer.sharedMaterial != resolvedMaterial)
                    {
                        cellRenderer.sharedMaterial = resolvedMaterial;
                    }
                }

                if (resolvedMaterial && i < cellNestedRenderers.Count)
                {
                    var nestedRenderers = cellNestedRenderers[i];
                    if (nestedRenderers == null)
                    {
                        continue;
                    }

                    for (var nestedIndex = 0; nestedIndex < nestedRenderers.Length; nestedIndex++)
                    {
                        var nestedRenderer = nestedRenderers[nestedIndex];
                        if (nestedRenderer && nestedRenderer.sharedMaterial != resolvedMaterial)
                        {
                            nestedRenderer.sharedMaterial = resolvedMaterial;
                        }
                    }
                }
            }

            for (var i = activeCellCount; i < pooledCellCount; i++)
            {
                request.SetActiveIfChanged(cells[i], false);
            }
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

    }
}
