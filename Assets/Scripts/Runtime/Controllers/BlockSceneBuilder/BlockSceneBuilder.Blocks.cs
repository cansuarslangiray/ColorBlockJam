using System.Collections;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private void ApplyBlockVisuals(LevelJsonData levelData, in LayoutMetrics layout)
        {
            ReleaseActiveBlockViewsToPool();

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

                var blockType = sourceBlocks[i].ResolveBlockType(runtimeBlock.LocalCells?.Length ?? 1);
                var blockView = AcquireBlockRoot(blockType);
                if (blockView == null)
                {
                    continue;
                }

                blockView.BlockType = blockType;
                ApplyBlockCells(blockView, runtimeBlock, layout);
                ApplyWorldTransform(blockView.PlacementTransform, ToWorldPosition(runtimeBlock.Position, layout),
                    blockRootScale);
                SetDragHighlightActive(blockView, false);

                _blockViewPool.MarkActive(i, blockView);
                SetActiveIfChanged(blockView.RootObject, true);
                ResetBlockAnimatorState(blockView.Animator);
            }

            RefreshAllConditionIndicators();
        }

        private void ReleaseActiveBlockViewsToPool()
        {
            _blockViewPool.ReleaseAllActive(StopBlockExit, SetActiveIfChanged, ResetBlockTransientFx);
        }

        private BlockRootView AcquireBlockRoot(BlockShapeType blockType)
        {
            return _blockViewPool.Acquire(blockType);
        }

        private void ReleaseActiveBlockView(int blockId, bool stopRoutines = true)
        {
            if (_blockViewPool.TryGetActive(blockId, out var blockView))
            {
                ResetBlockTransientFx(blockView);
            }

            _blockViewPool.ReleaseAndRemove(blockId, stopRoutines, StopBlockExit, SetActiveIfChanged);
        }

        private void SubscribeBoardEvents()
        {
            boardController.BlockMoved -= HandleBlockMoved;
            boardController.BlockMoved += HandleBlockMoved;
            boardController.BlockCleared -= HandleBlockCleared;
            boardController.BlockCleared += HandleBlockCleared;
            boardController.BlockDragHighlightChanged -= HandleBlockDragHighlightChanged;
            boardController.BlockDragHighlightChanged += HandleBlockDragHighlightChanged;
        }

        private void UnsubscribeBoardEvents()
        {
            boardController.BlockMoved -= HandleBlockMoved;
            boardController.BlockCleared -= HandleBlockCleared;
            boardController.BlockDragHighlightChanged -= HandleBlockDragHighlightChanged;
        }

        private void HandleBlockMoved(int blockId, Vector2Int fromPosition, Vector2Int toPosition)
        {
            if (!_blockViewPool.TryGetActive(blockId, out var blockView))
            {
                return;
            }

            if (fromPosition == toPosition)
            {
                return;
            }

            SyncBlockToGridPosition(blockView, toPosition);
            RefreshAllConditionIndicators();
        }

        private void HandleBlockCleared(int blockId, Vector2Int clearedPosition, Vector2Int exitDirection,
            DoorOpeningData matchedDoor)
        {
            if (!_blockViewPool.TryGetActive(blockId, out var blockView))
            {
                return;
            }

            StopBlockExit(blockId);
            ResetBlockTransientFx(blockView);
            _blockExitRoutineById[blockId] =
                StartCoroutine(ClearAndExitRoutine(blockId, blockView, clearedPosition, exitDirection, matchedDoor));

            RefreshAllConditionIndicators();
        }

        private IEnumerator ClearAndExitRoutine(int blockId, BlockRootView blockView, Vector2Int clearedPosition,
            Vector2Int exitDirection, DoorOpeningData matchedDoor)
        {
            SyncBlockToGridPosition(blockView, clearedPosition);

            audioManager?.PlayBlockMatchSuccess();
            PlayDoorMatchFx(matchedDoor);

            var resolvedExitDirection = matchedDoor.ResolveExitDirection(boardController.GridDimensions, exitDirection);
            if (resolvedExitDirection == Vector2Int.zero)
            {
                PlayBlockExitDisintegrateFx(blockView);
                FinalizeClearedBlock(blockId);
                yield break;
            }

            yield return AnimateBlockDoorExitSequence(blockView, matchedDoor, resolvedExitDirection);
            FinalizeClearedBlock(blockId);
        }

        private void SyncBlockToGridPosition(BlockRootView blockView, Vector2Int targetGridPosition)
        {
            if (blockView?.RootTransform == null)
            {
                return;
            }

            var placementTransform = blockView.PlacementTransform ? blockView.PlacementTransform : blockView.RootTransform;
            placementTransform.position = ToWorldPosition(targetGridPosition, GetCurrentLayout());
        }

        private void StopBlockExit(int blockId)
        {
            if (_blockExitRoutineById.TryGetValue(blockId, out var routine))
            {
                StopCoroutine(routine);
                _blockExitRoutineById.Remove(blockId);
            }

            if (_blockViewPool.TryGetActive(blockId, out var blockView))
            {
                ResetBlockTransientFx(blockView);
            }
        }

        private void StopAllBlockRoutines()
        {
            if (_blockExitRoutineById.Count > 0)
            {
                foreach (var pair in _blockExitRoutineById)
                {
                    StopCoroutine(pair.Value);
                }
            }

            _blockExitRoutineById.Clear();
            _blockViewPool.ForEachActive((_, blockView) => ResetBlockTransientFx(blockView));
            StopAllDoorMatchFx();
        }

        private void FinalizeClearedBlock(int blockId)
        {
            ReleaseActiveBlockView(blockId, stopRoutines: false);
            _blockExitRoutineById.Remove(blockId);
        }

        private void ResetBlockTransientFx(BlockRootView blockView)
        {
            ApplyDoorPassThroughScale(
                blockView?.DoorPassThroughCellTransformsBuffer,
                blockView?.DoorPassThroughInitialScalesBuffer,
                1f);
            SetDragHighlightActive(blockView, false);
            StopDoorExitBurstParticle(blockView);
            ClearDoorPassThroughVisualOverrides(blockView?.DoorPassThroughCellRendererBuffer);
        }

        private static Vector3 ToWorldPosition(Vector2Int gridPosition, in LayoutMetrics layout)
        {
            return new Vector3(
                layout.BoardOrigin.x + (gridPosition.x * layout.CellSize),
                layout.BoardOrigin.y + (gridPosition.y * layout.CellSize),
                layout.BlockZ);
        }

        private static Vector2 ResolveDoorWorldCenter(DoorOpeningData matchedDoor, Vector2Int gridDimensions,
            in LayoutMetrics layout)
        {
            var mappedMinX = MapLogicalToVisualCellIndex(matchedDoor.MinCell.x, gridDimensions.x);
            var mappedMaxX = MapLogicalToVisualCellIndex(matchedDoor.MaxCell.x, gridDimensions.x);
            var mappedMinY = MapLogicalToVisualCellIndex(matchedDoor.MinCell.y, gridDimensions.y);
            var mappedMaxY = MapLogicalToVisualCellIndex(matchedDoor.MaxCell.y, gridDimensions.y);
            var centerX = (mappedMinX + mappedMaxX + 1) * 0.5f;
            var centerY = (mappedMinY + mappedMaxY + 1) * 0.5f;
            return new Vector2(
                layout.BoardOrigin.x + (centerX * layout.CellSize),
                layout.BoardOrigin.y + (centerY * layout.CellSize));
        }

        private void ApplyBlockCells(BlockRootView blockView, RuntimeBlockState blockState, in LayoutMetrics layout)
        {
            var localCells = blockState.LocalCells ?? System.Array.Empty<Vector2Int>();
            var cellSize = layout.CellSize;
            var targetScale = Vector3.one * Mathf.Max(0.01f, cellSize * blockCellVisualScale);
            var resolvedMaterial = GetMaterial(blockState.ColorType);
            blockView.HasCachedBlockColor = TryResolvePrimaryMaterialColor(resolvedMaterial, out var cachedBlockColor);
            blockView.CachedBlockColor = cachedBlockColor;

            EnsureBlockCells(blockView, localCells.Length);
            blockView.LocalCenter = ResolveLocalCenter(localCells, cellSize);
            blockView.ConditionIndicatorLocalAnchor = ResolveConditionIndicatorLocalAnchor(localCells, cellSize);

            var hasLocalBounds = false;
            var localBoundsMin = Vector3.zero;
            var localBoundsMax = Vector3.zero;
            var localHalfExtents = targetScale * 0.5f;

            for (var i = 0; i < blockView.Cells.Count; i++)
            {
                var cellObject = blockView.Cells[i];
                var isActive = i < localCells.Length;
                SetActiveIfChanged(cellObject, isActive);
                if (!isActive)
                {
                    continue;
                }

                var localCell = localCells[i];
                var localPosition = new Vector3((localCell.x + 0.5f) * cellSize, (localCell.y + 0.5f) * cellSize, 0f);
                ApplyLocalTransform(cellObject.transform, localPosition, targetScale);

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

                if (i < blockView.CellRenderers.Count)
                {
                    var cellRenderer = blockView.CellRenderers[i];
                    if (cellRenderer && resolvedMaterial && cellRenderer.sharedMaterial != resolvedMaterial)
                    {
                        cellRenderer.sharedMaterial = resolvedMaterial;
                    }
                }
            }

            blockView.HasCachedLocalBounds = hasLocalBounds;
            blockView.CachedLocalBoundsMin = localBoundsMin;
            blockView.CachedLocalBoundsMax = localBoundsMax;
            CacheBlockOutlineGridLoop(blockView, localCells);
            RefreshDragHighlightBounds(blockView);
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

        private Vector3 ResolveConditionIndicatorLocalAnchor(Vector2Int[] localCells, float cellSize)
        {
            if (localCells == null || localCells.Length == 0)
            {
                return new Vector3(cellSize * 0.5f, cellSize * (1f + indicatorHeightOffsetInCells), indicatorLocalZOffset);
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

        private void EnsureBlockCells(BlockRootView blockView, int requiredCellCount)
        {
            _blockViewPool.EnsureBlockCells(poolManager, blockView, requiredCellCount, SetActiveIfChanged);
        }
    }
}
