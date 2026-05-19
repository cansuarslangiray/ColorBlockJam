using System;
using System.Collections.Generic;
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
                request.ApplyWorldPosition(placementTransform, ToWorldPosition(runtimeBlock.Position, request.Layout));
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
            var resolvedMaterial = request.ResolveMaterial(blockState.ColorType);
            blockView.HasCachedBlockColor = TryResolvePrimaryMaterialColor(resolvedMaterial, out var cachedBlockColor);
            blockView.CachedBlockColor = cachedBlockColor;

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

            ApplyConditionIndicatorPlacement(blockView, localCells, cellSize);
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

        private static void ApplyConditionIndicatorPlacement(BlockRootView blockView, Vector2Int[] localCells,
            float cellSize)
        {
            if (blockView?.ConditionIndicatorObject == null)
            {
                return;
            }

            var indicatorTransform = blockView.ConditionIndicatorObject.transform;
            var existingLocalPosition = indicatorTransform.localPosition;
            var denseAnchor = ResolveDenseIndicatorAnchor(localCells, cellSize);
            indicatorTransform.localPosition =
                new Vector3(denseAnchor.x, denseAnchor.y, existingLocalPosition.z);
        }

        private static Vector2 ResolveDenseIndicatorAnchor(Vector2Int[] localCells, float cellSize)
        {
            if (localCells == null || localCells.Length == 0)
            {
                return Vector2.zero;
            }

            var denseCell = ResolveDensestLocalCell(localCells);
            return new Vector2((denseCell.x + 0.5f) * cellSize, (denseCell.y + 0.5f) * cellSize);
        }

        private static Vector2Int ResolveDensestLocalCell(Vector2Int[] localCells)
        {
            if (localCells == null || localCells.Length == 0)
            {
                return Vector2Int.zero;
            }

            var occupiedCells = new HashSet<Vector2Int>(localCells);
            var weightedCenter = Vector2.zero;
            for (var i = 0; i < localCells.Length; i++)
            {
                var localCell = localCells[i];
                weightedCenter += new Vector2(localCell.x + 0.5f, localCell.y + 0.5f);
            }

            weightedCenter /= localCells.Length;

            var bestCell = localCells[0];
            var bestDensityScore = int.MinValue;
            var bestDistanceToCenter = float.PositiveInfinity;

            for (var i = 0; i < localCells.Length; i++)
            {
                var localCell = localCells[i];
                var densityScore = ResolveDensityScore(localCell, occupiedCells);
                var localCellCenter = new Vector2(localCell.x + 0.5f, localCell.y + 0.5f);
                var distanceToCenter = (localCellCenter - weightedCenter).sqrMagnitude;

                if (densityScore > bestDensityScore ||
                    (densityScore == bestDensityScore && distanceToCenter < bestDistanceToCenter) ||
                    (densityScore == bestDensityScore &&
                     Mathf.Approximately(distanceToCenter, bestDistanceToCenter) &&
                     IsDeterministicallyPreferred(localCell, bestCell)))
                {
                    bestCell = localCell;
                    bestDensityScore = densityScore;
                    bestDistanceToCenter = distanceToCenter;
                }
            }

            return bestCell;
        }

        private static int ResolveDensityScore(Vector2Int origin, HashSet<Vector2Int> occupiedCells)
        {
            var score = 0;
            for (var deltaY = -1; deltaY <= 1; deltaY++)
            {
                for (var deltaX = -1; deltaX <= 1; deltaX++)
                {
                    var sampleCell = new Vector2Int(origin.x + deltaX, origin.y + deltaY);
                    if (!occupiedCells.Contains(sampleCell))
                    {
                        continue;
                    }

                    if (deltaX == 0 && deltaY == 0)
                    {
                        score += 6;
                        continue;
                    }

                    score += deltaX == 0 || deltaY == 0 ? 3 : 1;
                }
            }

            return score;
        }

        private static bool IsDeterministicallyPreferred(Vector2Int candidate, Vector2Int currentBest)
        {
            if (candidate.y != currentBest.y)
            {
                return candidate.y < currentBest.y;
            }

            return candidate.x < currentBest.x;
        }

    }
}
