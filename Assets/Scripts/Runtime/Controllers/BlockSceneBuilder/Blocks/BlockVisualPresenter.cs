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
                request.ApplyWorldPosition(placementTransform, ToWorldPosition(runtimeBlock.Position, request.Layout));
                request.SetOutlineDragActive(blockView, false);

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
            var localCells = ResolveVisualLocalCells(blockView, blockState.LocalCells);
            var cellSize = request.Layout.CellSize;
            var resolvedMaterial = request.ResolveMaterial(blockState.ColorType);
            var useLockedAppearance = request.IsBlockLocked?.Invoke(blockState.Id) == true;
            blockView.HasCachedBlockColor = TryResolvePrimaryMaterialColor(resolvedMaterial, out var cachedBlockColor);
            blockView.CachedBlockColor = cachedBlockColor;

            blockView.LocalCenter = ResolveLocalCenter(localCells, cellSize);

            var cells = blockView.Cells;
            var pooledCellCount = cells.Count;
            var activeCellCount = Mathf.Min(localCells.Length, pooledCellCount);
            blockView.ActiveCellCount = activeCellCount;

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

            }

            for (var i = activeCellCount; i < pooledCellCount; i++)
            {
                request.SetActiveIfChanged(cells[i], false);
            }

            ApplyBlockAppearance(blockView, resolvedMaterial, useLockedAppearance, activeCellCount);
            if (!blockView.HasCachedBlockColor &&
                TryResolveBlockColorFromRenderers(blockView, activeCellCount, out var cachedRendererColor))
            {
                blockView.HasCachedBlockColor = true;
                blockView.CachedBlockColor = cachedRendererColor;
            }
        }

        public void ApplyBlockAppearance(BlockRootView blockView, RuntimeBlockState blockState, Material resolvedMaterial,
            bool useLockedAppearance)
        {
            if (blockView == null)
            {
                return;
            }

            var localCells = ResolveVisualLocalCells(blockView, blockState.LocalCells);
            var activeCellCount = Mathf.Min(localCells.Length, blockView.Cells.Count);
            blockView.ActiveCellCount = activeCellCount;
            ApplyBlockAppearance(blockView, resolvedMaterial, useLockedAppearance, activeCellCount);
        }

        private static void ApplyBlockAppearance(BlockRootView blockView, Material resolvedMaterial,
            bool useLockedAppearance, int activeCellCount)
        {
            if (blockView == null)
            {
                return;
            }

            var cellRenderers = blockView.CellRenderers;
            var nestedRenderers = blockView.CellNestedRenderers;
            var rendererCount = Mathf.Min(activeCellCount, cellRenderers.Count);
            for (var i = 0; i < rendererCount; i++)
            {
                if (!useLockedAppearance && !resolvedMaterial)
                {
                    continue;
                }

                var cellRenderer = cellRenderers[i];
                var targetMaterial = ResolvePrimaryMaterial(blockView, i, resolvedMaterial, useLockedAppearance);
                if (cellRenderer && cellRenderer.sharedMaterial != targetMaterial)
                {
                    cellRenderer.sharedMaterial = targetMaterial;
                }
            }

            var nestedCount = Mathf.Min(activeCellCount, nestedRenderers.Count);
            for (var i = 0; i < nestedCount; i++)
            {
                var nestedRendererSet = nestedRenderers[i];
                if (nestedRendererSet == null)
                {
                    continue;
                }

                for (var nestedIndex = 0; nestedIndex < nestedRendererSet.Length; nestedIndex++)
                {
                    if (!useLockedAppearance && !resolvedMaterial)
                    {
                        continue;
                    }

                    var nestedRenderer = nestedRendererSet[nestedIndex];
                    var targetMaterial = ResolveNestedMaterial(blockView, i, nestedIndex, resolvedMaterial,
                        useLockedAppearance);
                    if (nestedRenderer && nestedRenderer.sharedMaterial != targetMaterial)
                    {
                        nestedRenderer.sharedMaterial = targetMaterial;
                    }
                }
            }

            blockView.IsUsingLockedAppearance = useLockedAppearance;
        }

        private static Material ResolvePrimaryMaterial(BlockRootView blockView, int cellIndex, Material blockMaterial,
            bool useLockedAppearance)
        {
            if (!useLockedAppearance)
            {
                return blockMaterial;
            }

            if (cellIndex < 0 || cellIndex >= blockView.CellDefaultMaterials.Count)
            {
                return null;
            }

            return blockView.CellDefaultMaterials[cellIndex];
        }

        private static Material ResolveNestedMaterial(BlockRootView blockView, int cellIndex, int nestedIndex,
            Material blockMaterial, bool useLockedAppearance)
        {
            if (!useLockedAppearance)
            {
                return blockMaterial;
            }

            if (cellIndex < 0 || cellIndex >= blockView.CellNestedDefaultMaterials.Count)
            {
                return null;
            }

            var nestedDefaults = blockView.CellNestedDefaultMaterials[cellIndex];
            if (nestedDefaults == null || nestedIndex < 0 || nestedIndex >= nestedDefaults.Length)
            {
                return null;
            }

            return nestedDefaults[nestedIndex];
        }

        private static bool TryResolvePrimaryMaterialColor(Material sourceMaterial, out Color color)
        {
            color = default;
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

        private static bool TryResolveBlockColorFromRenderers(BlockRootView blockView, int activeCellCount, out Color color)
        {
            color = default;
            if (blockView == null)
            {
                return false;
            }

            var renderers = blockView.CellRenderers;
            var rendererCount = Mathf.Min(activeCellCount, renderers.Count);
            for (var i = 0; i < rendererCount; i++)
            {
                var renderer = renderers[i];
                var material = renderer ? renderer.sharedMaterial : null;
                if (!material)
                {
                    continue;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    color = material.GetColor("_BaseColor");
                    return true;
                }

                if (material.HasProperty("_Color"))
                {
                    color = material.GetColor("_Color");
                    return true;
                }
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

        private static Vector2Int[] ResolveVisualLocalCells(BlockRootView blockView, Vector2Int[] runtimeLocalCells)
        {
            if (blockView?.ShapeLocalCells != null && blockView.ShapeLocalCells.Length > 0)
            {
                return blockView.ShapeLocalCells;
            }

            return runtimeLocalCells ?? Array.Empty<Vector2Int>();
        }
    }
}
