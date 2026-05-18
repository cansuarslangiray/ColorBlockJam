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
            var blockVisualRequest = new BlockVisualPresenter.BuildRequest
            {
                LevelData = levelData,
                BoardController = boardController,
                BlockViewPool = _blockViewPool,
                Layout = layout,
                BlockCellVisualScale = blockCellVisualScale,
                BlockRootScale = blockRootScale,
                IndicatorHeightOffsetInCells = indicatorHeightOffsetInCells,
                IndicatorLocalZOffset = indicatorLocalZOffset,
                ResolveMaterial = GetMaterial,
                EnsureBlockCells = EnsureBlockCells,
                SetActiveIfChanged = SetActiveIfChanged,
                ApplyWorldTransform = ApplyWorldTransform,
                SetDragHighlightActive = SetDragHighlightActive,
                CacheBlockOutlineGridLoop = CacheBlockOutlineGridLoop,
                RefreshDragHighlightBounds = RefreshDragHighlightBounds,
                ResetBlockAnimatorState = ResetBlockAnimatorState
            };
            _blockVisualPresenter.ApplyLevelBlockVisuals(blockVisualRequest);

            RefreshAllConditionIndicators();
        }

        private void ReleaseActiveBlockViewsToPool()
        {
            _blockViewPool.ReleaseAllActive(StopBlockExit, SetActiveIfChanged, ResetBlockTransientFx);
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
            if (boardController == null)
            {
                return;
            }

            boardController.BlockMoved -= HandleBlockMoved;
            boardController.BlockMoved += HandleBlockMoved;
            boardController.BlockCleared -= HandleBlockCleared;
            boardController.BlockCleared += HandleBlockCleared;
            boardController.BlockDragHighlightChanged -= HandleBlockDragHighlightChanged;
            boardController.BlockDragHighlightChanged += HandleBlockDragHighlightChanged;
        }

        private void UnsubscribeBoardEvents()
        {
            if (boardController == null)
            {
                return;
            }

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
            SetDragHighlightActive(blockView, false);
            _blockExitRoutineById[blockId] =
                StartCoroutine(ClearAndExitRoutine(blockId, blockView, clearedPosition, exitDirection, matchedDoor));

            RefreshAllConditionIndicators();
        }

        private IEnumerator ClearAndExitRoutine(int blockId, BlockRootView blockView, Vector2Int clearedPosition,
            Vector2Int exitDirection, DoorOpeningData matchedDoor)
        {
            SetDragHighlightActive(blockView, false);
            SyncBlockToGridPosition(blockView, clearedPosition);

            var resolvedExitDirection = matchedDoor.ResolveExitDirection(boardController.GridDimensions, exitDirection);
            var exitFxRequest = new BlockExitFxController.ExitSequenceRequest
            {
                BlockView = blockView,
                MatchedDoor = matchedDoor,
                ResolvedExitDirection = resolvedExitDirection,
                PlayBlockMatchSuccessSfx = () => audioManager?.PlayBlockMatchSuccess(),
                PlayDoorMatchFx = PlayDoorMatchFx,
                AnimateBlockDoorExitSequence = AnimateBlockDoorExitSequence,
                PlayBlockExitDisintegrateFx = (view, direction) => PlayBlockExitDisintegrateFx(view, direction, false)
            };
            yield return _blockExitFxController.PlayClearAndExitSequence(exitFxRequest);
            yield return CleanupDoorExitBurstAfterDelay(blockView, DoorExitBurstCleanupDelay);
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
            if (_blockViewPool.TryGetActive(blockId, out var blockView))
            {
                ResetBlockTransientFx(blockView);
            }

            ReleaseActiveBlockView(blockId, stopRoutines: false);
            _blockExitRoutineById.Remove(blockId);
        }

        private void ResetBlockTransientFx(BlockRootView blockView)
        {
            ApplyDoorPassThroughScale(
                blockView?.DoorPassThroughCellTransformsBuffer,
                blockView?.DoorPassThroughInitialScalesBuffer,
                1f);
            ApplyDoorPassThroughPositions(blockView?.DoorPassThroughCellTransformsBuffer, blockView?.DoorPassThroughInitialPositionsBuffer);
            ApplyDoorPassThroughRotations(blockView?.DoorPassThroughCellTransformsBuffer, blockView?.DoorPassThroughInitialRotationsBuffer, blockView?.DoorPassThroughScatterRotationBuffer, blockView?.DoorPassThroughScatterDelayBuffer,
                0f);
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

        private void EnsureBlockCells(BlockRootView blockView, int requiredCellCount)
        {
            _blockViewPool.EnsureBlockCells(poolManager, blockView, requiredCellCount, SetActiveIfChanged);
        }
    }
}
