using System;
using Runtime.Controllers.BlockSceneBuilder.Board;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
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
                if (!boardController.TryGetRuntimeBlock(i, out var runtimeBlock) || runtimeBlock == null)
                {
                    continue;
                }

                var poolKey = string.IsNullOrWhiteSpace(runtimeBlock.PoolKey)
                    ? sourceBlocks[i].ResolvePoolKey()
                    : runtimeBlock.PoolKey;

                var blockView = blockViewPool.Acquire(poolKey);
                if (blockView == null)
                {
                    continue;
                }

                blockView.PoolKey = poolKey;
                ApplyRuntimeBlockVisualState(i, blockView, runtimeBlock, request);

                blockViewPool.MarkActive(i, blockView);
                request.SetActiveIfChanged(blockView.RootObject, true);
            }
        }

        public void ApplyRuntimeBlockVisualState(int blockId, BlockRootView blockView, RuntimeBlockState blockState,
            in BlockVisualBuildRequest request)
        {
            if (blockView == null || blockState == null)
            {
                return;
            }

            ApplyBlockCells(blockId, blockView, blockState, request);

            var placementTransform = blockView.PlacementTransform
                ? blockView.PlacementTransform
                : blockView.RootTransform;
            request.ApplyWorldPosition(placementTransform, ToWorldPosition(blockState.Position, request.Layout));
            request.SetOutlineDragActive(blockView, false);
        }

        private static Vector3 ToWorldPosition(Vector2Int gridPosition, in LayoutMetrics layout)
        {
            return new Vector3(layout.BoardOrigin.x + (gridPosition.x * layout.CellSize),
                layout.BoardOrigin.y + (gridPosition.y * layout.CellSize), layout.BlockZ);
        }

        private static void ApplyBlockCells(int blockId, BlockRootView blockView, RuntimeBlockState blockState,
            in BlockVisualBuildRequest request)
        {
            var localCells = ResolveVisualLocalCells(blockView, blockState.RenderableLocalCells);
            var cellSize = request.Layout.CellSize;
            var useLockedAppearance = request.IsBlockLocked?.Invoke(blockId) == true;

            var representativeColorType = ResolveRepresentativeColorType(blockState, localCells);
            var representativeMaterial = request.ResolveMaterial(representativeColorType);
            blockView.HasCachedBlockColor = TryResolvePrimaryMaterialColor(representativeMaterial, out var cachedBlockColor);
            blockView.CachedBlockColor = cachedBlockColor;
            blockView.LocalCenter = ResolveLocalCenter(localCells, cellSize);

            var cells = blockView.Cells;
            var pooledCellCount = cells.Count;
            var activeCellCount = Mathf.Min(localCells.Length, pooledCellCount);
            blockView.ActiveCellCount = activeCellCount;
            var useNestedDualMaterial = ResolveNestedDualMaterials(blockState, request, out var nestedPrimaryMaterial,
                out var nestedInnerMaterial);

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

                var resolvedMaterial = ResolveCellMaterial(blockState, request, localCell);
                var resolvedPrimaryMaterial = useNestedDualMaterial && !useLockedAppearance
                    ? nestedPrimaryMaterial
                    : resolvedMaterial;
                var resolvedNestedMaterial = useNestedDualMaterial && !useLockedAppearance
                    ? nestedInnerMaterial
                    : resolvedMaterial;
                ApplyCellAppearance(blockView, i, resolvedPrimaryMaterial, resolvedNestedMaterial, useLockedAppearance);
            }

            for (var i = activeCellCount; i < pooledCellCount; i++)
            {
                request.SetActiveIfChanged(cells[i], false);
            }

            blockView.IsUsingLockedAppearance = useLockedAppearance;
            if (!blockView.HasCachedBlockColor &&
                TryResolveBlockColorFromRenderers(blockView, activeCellCount, out var cachedRendererColor))
            {
                blockView.HasCachedBlockColor = true;
                blockView.CachedBlockColor = cachedRendererColor;
            }
        }

        private static Material ResolveCellMaterial(RuntimeBlockState blockState, in BlockVisualBuildRequest request,
            Vector2Int localCell)
        {
            if (blockState.TryResolveVisibleColor(localCell, out var colorType))
            {
                return request.ResolveMaterial(colorType);
            }

            return request.ResolveMaterial(blockState.ColorType);
        }

        private static bool ResolveNestedDualMaterials(RuntimeBlockState blockState, in BlockVisualBuildRequest request,
            out Material outerMaterial, out Material innerMaterial)
        {
            outerMaterial = null;
            innerMaterial = null;

            if (blockState == null || !blockState.BlockFeatures.HasFeature(BlockFeature.NestedShape))
            {
                return false;
            }

            if (!blockState.TryResolveRemainingLayerColor(ShapeLayerRole.Outer, out var outerColorType) ||
                !blockState.TryResolveRemainingLayerColor(ShapeLayerRole.Inner, out var innerColorType))
            {
                return false;
            }

            outerMaterial = request.ResolveMaterial(outerColorType);
            innerMaterial = request.ResolveMaterial(innerColorType);
            return outerMaterial != null && innerMaterial != null;
        }

        private static void ApplyCellAppearance(BlockRootView blockView, int cellIndex, Material primaryMaterial,
            Material nestedMaterial, bool useLockedAppearance)
        {
            if (blockView == null || cellIndex < 0)
            {
                return;
            }

            if (cellIndex < blockView.CellRenderers.Count)
            {
                var primaryRenderer = blockView.CellRenderers[cellIndex];
                var targetPrimaryMaterial = ResolvePrimaryMaterial(blockView, cellIndex, primaryMaterial,
                    useLockedAppearance);
                if (primaryRenderer && primaryRenderer.sharedMaterial != targetPrimaryMaterial)
                {
                    primaryRenderer.sharedMaterial = targetPrimaryMaterial;
                }
            }

            if (cellIndex >= blockView.CellNestedRenderers.Count)
            {
                return;
            }

            var nestedRenderers = blockView.CellNestedRenderers[cellIndex];
            if (nestedRenderers == null)
            {
                return;
            }

            for (var nestedIndex = 0; nestedIndex < nestedRenderers.Length; nestedIndex++)
            {
                var nestedRenderer = nestedRenderers[nestedIndex];
                var targetNestedMaterial = ResolveNestedMaterial(blockView, cellIndex, nestedIndex, nestedMaterial,
                    useLockedAppearance);
                if (nestedRenderer && nestedRenderer.sharedMaterial != targetNestedMaterial)
                {
                    nestedRenderer.sharedMaterial = targetNestedMaterial;
                }
            }
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

        private static BlockColor ResolveRepresentativeColorType(RuntimeBlockState blockState, Vector2Int[] localCells)
        {
            if (blockState == null)
            {
                return BlockColor.Red;
            }

            if (localCells != null)
            {
                for (var i = 0; i < localCells.Length; i++)
                {
                    if (blockState.TryResolveVisibleColor(localCells[i], out var colorType))
                    {
                        return colorType;
                    }
                }
            }

            return blockState.ColorType;
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
            if (runtimeLocalCells != null && runtimeLocalCells.Length > 0)
            {
                return runtimeLocalCells;
            }

            if (blockView?.ShapeLocalCells != null && blockView.ShapeLocalCells.Length > 0)
            {
                return blockView.ShapeLocalCells;
            }

            return Array.Empty<Vector2Int>();
        }
    }
}
