using System.Collections;
using System.Collections.Generic;
using Runtime.Controllers.BlockSceneBuilder.Animations;
using Runtime.Controllers.BlockSceneBuilder.Blocks;
using Runtime.Controllers.BlockSceneBuilder.Board;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Data;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private const float BlockMoveArrivalEpsilon = 0.0001f;

        private void ApplyBlockVisuals(LevelDefinition levelData, in LayoutMetrics layout)
        {
            ReleaseActiveBlockViewsToPool();
            var blockVisualRequest = new BlockVisualBuildRequest
            {
                LevelData = levelData,
                BoardController = boardController,
                BlockViewPool = _blockViewPool,
                Layout = layout,
                ResolveMaterial = GetMaterial,
                IsBlockLocked = boardController.IsBlockLocked,
                SetActiveIfChanged = SetActiveIfChanged,
                ApplyWorldPosition = ApplyWorldPosition,
                SetOutlineDragActive = ApplyOutlineDragState
            };
            _blockVisualPresenter.ApplyLevelBlockVisuals(blockVisualRequest);

            RefreshConditionDrivenVisuals();
        }

        private void ReleaseActiveBlockViewsToPool()
        {
            StopAllBlockConditionUnlockTransitions(clearVisualOverrides: true);
            _blockViewPool.ReleaseAllActive(StopBlockExit, SetActiveIfChanged,
                ResetBlockTransientFx);
        }

        private void ReleaseActiveBlockView(int blockId, bool stopRoutines = true)
        {
            StopBlockMove(blockId, snapToTarget: false);
            StopBlockConditionUnlockTransition(blockId, clearVisualOverrides: true);
            if (_blockViewPool.TryGetActive(blockId, out var blockView))
            {
                ResetBlockTransientFx(blockView);
            }

            _blockViewPool.ReleaseAndRemove(blockId, stopRoutines, StopBlockExit,
                SetActiveIfChanged);
        }

        private void SubscribeBoardEvents()
        {
            boardController.BlockMoved -= HandleBlockMoved;
            boardController.BlockMoved += HandleBlockMoved;
            boardController.BlockCleared -= HandleBlockCleared;
            boardController.BlockCleared += HandleBlockCleared;
            boardController.BlockLayerExited -= HandleBlockLayerExited;
            boardController.BlockLayerExited += HandleBlockLayerExited;
            boardController.BlockOutlineDragStateChanged -= HandleBlockOutlineDragStateChanged;
            boardController.BlockOutlineDragStateChanged += HandleBlockOutlineDragStateChanged;
            boardController.ConditionStatesChanged -= HandleConditionStatesChanged;
            boardController.ConditionStatesChanged += HandleConditionStatesChanged;
        }

        private void UnsubscribeBoardEvents()
        {
            boardController.BlockMoved -= HandleBlockMoved;
            boardController.BlockCleared -= HandleBlockCleared;
            boardController.BlockLayerExited -= HandleBlockLayerExited;
            boardController.BlockOutlineDragStateChanged -= HandleBlockOutlineDragStateChanged;
            boardController.ConditionStatesChanged -= HandleConditionStatesChanged;
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

            QueueBlockMove(blockId, blockView, fromPosition, toPosition);
        }

        private void HandleBlockCleared(int blockId, Vector2Int clearedPosition, Vector2Int exitDirection,
            DoorOpeningData matchedDoor)
        {
            if (!_blockViewPool.TryGetActive(blockId, out var blockView))
            {
                return;
            }

            StopBlockExit(blockId);
            ApplyOutlineDragState(blockView, false);
            _blockExitRoutineById[blockId] =
                StartCoroutine(ClearAndExitRoutine(blockId, blockView, clearedPosition, exitDirection, matchedDoor));
        }

        private void HandleBlockLayerExited(int blockId, Vector2Int blockPosition)
        {
            if (!_blockViewPool.TryGetActive(blockId, out var blockView))
            {
                return;
            }

            StopBlockExit(blockId);
            ApplyOutlineDragState(blockView, false);
            SyncBlockToGridPosition(blockView, blockPosition);

            if (boardController.TryGetRuntimeBlock(blockId, out var runtimeBlock))
            {
                ApplyRuntimeBlockVisualState(blockId, blockView, runtimeBlock);
            }

            RefreshConditionDrivenVisuals();
        }

        private void HandleConditionStatesChanged()
        {
            RefreshConditionDrivenVisuals();
        }

        private IEnumerator ClearAndExitRoutine(int blockId, BlockRootView blockView, Vector2Int clearedPosition,
            Vector2Int exitDirection, DoorOpeningData matchedDoor)
        {
            ApplyOutlineDragState(blockView, false);
            SyncBlockToGridPosition(blockView, clearedPosition);

            var resolvedExitDirection = matchedDoor.ResolveExitDirection(boardController.GridDimensions, exitDirection);
            var exitFxRequest = new BlockExitSequenceRequest
            {
                BlockView = blockView,
                MatchedDoor = matchedDoor,
                ResolvedExitDirection = resolvedExitDirection,
                PlayBlockMatchSuccessSfx = audioManager.PlayBlockMatchSuccess,
                PlayDoorMatchFx = PlayDoorMatchFx,
                AnimateBlockDoorExitSequence = AnimateBlockDoorExitSequence,
                PlayBlockExitDisintegrateFx = PlayBlockExitDisintegrateFx
            };
            yield return _blockExitFxController.PlayClearAndExitSequence(exitFxRequest);
            yield return CleanupDoorExitBurstAfterDelay(blockView, DoorExitBurstCleanupDelay);
            FinalizeClearedBlock(blockId);
        }

        private void SyncBlockToGridPosition(BlockRootView blockView, Vector2Int targetGridPosition)
        {
            if (!TryResolvePlacementTransform(blockView, out var placementTransform))
            {
                return;
            }

            placementTransform.position = ToWorldPosition(targetGridPosition, GetCurrentLayout());
        }

        private void QueueBlockMove(int blockId, BlockRootView blockView, Vector2Int fromGridPosition,
            Vector2Int toGridPosition)
        {
            if (!TryResolvePlacementTransform(blockView, out _))
            {
                return;
            }

            if (!TryResolveLinearMoveSegment(fromGridPosition, toGridPosition, out var stepDirection, out var stepCount))
            {
                return;
            }

            if (!_blockMoveWaypointQueueById.TryGetValue(blockId, out var waypointQueue))
            {
                waypointQueue = new Queue<Vector3>(stepCount);
                _blockMoveWaypointQueueById[blockId] = waypointQueue;
            }

            var layout = GetCurrentLayout();
            for (var stepIndex = 1; stepIndex <= stepCount; stepIndex++)
            {
                var stepGridPosition = fromGridPosition + (stepDirection * stepIndex);
                var stepWorldPosition = ToWorldPosition(stepGridPosition, layout);
                waypointQueue.Enqueue(stepWorldPosition);
                _blockMoveTargetWorldById[blockId] = stepWorldPosition;
            }

            if (_blockMoveRoutineById.ContainsKey(blockId))
            {
                return;
            }

            _blockMoveRoutineById[blockId] = StartCoroutine(AnimateBlockMoveRoutine(blockId, blockView));
        }

        private IEnumerator AnimateBlockMoveRoutine(int blockId, BlockRootView blockView)
        {
            if (!TryResolvePlacementTransform(blockView, out var placementTransform))
            {
                ClearBlockMoveRuntimeState(blockId);
                yield break;
            }

            while (placementTransform &&
                   _blockMoveWaypointQueueById.TryGetValue(blockId, out var waypointQueue) &&
                   waypointQueue.Count > 0)
            {
                var moveSpeedMultiplier = ResolveMoveSpeedMultiplierForQueueDepth(waypointQueue.Count);
                var remainingFrameTravel =
                    ResolveBlockMoveSpeedWorldUnitsPerSecond() * moveSpeedMultiplier * Time.deltaTime;
                while (remainingFrameTravel > 0f && waypointQueue.Count > 0)
                {
                    var currentPosition = placementTransform.position;
                    var targetWorldPosition = waypointQueue.Peek();
                    var remainingDelta = targetWorldPosition - currentPosition;
                    var remainingDistance = remainingDelta.magnitude;
                    if (remainingDistance <= BlockMoveArrivalEpsilon)
                    {
                        placementTransform.position = targetWorldPosition;
                        waypointQueue.Dequeue();
                        continue;
                    }

                    if (remainingFrameTravel >= remainingDistance)
                    {
                        placementTransform.position = targetWorldPosition;
                        waypointQueue.Dequeue();
                        remainingFrameTravel -= remainingDistance;
                        continue;
                    }

                    placementTransform.position =
                        currentPosition + ((remainingDelta / remainingDistance) * remainingFrameTravel);
                    remainingFrameTravel = 0f;
                }

                if (waypointQueue.Count == 0)
                {
                    break;
                }

                yield return null;
            }

            ClearBlockMoveRuntimeState(blockId);
        }

        private float ResolveBlockMoveSpeedWorldUnitsPerSecond()
        {
            return Mathf.Max(0.01f, blockMoveSpeedInCellsPerSecond * CellSize);
        }

        private static float ResolveMoveSpeedMultiplierForQueueDepth(int queueDepth)
        {
            if (queueDepth <= 1)
            {
                return 1f;
            }

            return Mathf.Min(2.5f, 1f + ((queueDepth - 1) * 0.35f));
        }

        private static bool TryResolvePlacementTransform(BlockRootView blockView, out Transform placementTransform)
        {
            placementTransform = null;
            if (blockView == null)
            {
                return false;
            }

            placementTransform = blockView.PlacementTransform;
            return true;
        }

        private void StopBlockMove(int blockId, bool snapToTarget)
        {
            if (snapToTarget &&
                _blockMoveTargetWorldById.TryGetValue(blockId, out var targetWorldPosition) &&
                _blockViewPool.TryGetActive(blockId, out var blockView) &&
                TryResolvePlacementTransform(blockView, out var placementTransform))
            {
                placementTransform.position = targetWorldPosition;
            }

            if (_blockMoveRoutineById.TryGetValue(blockId, out var routine))
            {
                StopCoroutine(routine);
            }

            ClearBlockMoveRuntimeState(blockId);
        }

        private void StopAllBlockMoveRoutines(bool snapToTargets = false)
        {
            if (snapToTargets && _blockMoveTargetWorldById.Count > 0)
            {
                foreach (var pair in _blockMoveTargetWorldById)
                {
                    if (!_blockViewPool.TryGetActive(pair.Key, out var blockView) ||
                        !TryResolvePlacementTransform(blockView, out var placementTransform))
                    {
                        continue;
                    }

                    placementTransform.position = pair.Value;
                }
            }

            if (_blockMoveRoutineById.Count > 0)
            {
                foreach (var pair in _blockMoveRoutineById)
                {
                    StopCoroutine(pair.Value);
                }
            }

            _blockMoveRoutineById.Clear();
            _blockMoveWaypointQueueById.Clear();
            _blockMoveTargetWorldById.Clear();
        }

        private void ClearBlockMoveRuntimeState(int blockId)
        {
            _blockMoveRoutineById.Remove(blockId);
            _blockMoveWaypointQueueById.Remove(blockId);
            _blockMoveTargetWorldById.Remove(blockId);
        }

        private static bool TryResolveLinearMoveSegment(Vector2Int fromGridPosition, Vector2Int toGridPosition,
            out Vector2Int stepDirection, out int stepCount)
        {
            stepDirection = Vector2Int.zero;
            stepCount = 0;

            var delta = toGridPosition - fromGridPosition;
            if (delta == Vector2Int.zero || (delta.x != 0 && delta.y != 0))
            {
                return false;
            }

            if (delta.x != 0)
            {
                stepDirection = delta.x > 0 ? Vector2Int.right : Vector2Int.left;
                stepCount = Mathf.Abs(delta.x);
                return stepCount > 0;
            }

            stepDirection = delta.y > 0 ? Vector2Int.up : Vector2Int.down;
            stepCount = Mathf.Abs(delta.y);
            return stepCount > 0;
        }

        private void StopBlockExit(int blockId)
        {
            StopBlockMove(blockId, snapToTarget: false);
            StopBlockConditionUnlockTransition(blockId, clearVisualOverrides: true);
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
            StopAllBlockMoveRoutines(snapToTargets: true);
            StopAllBlockConditionUnlockTransitions(clearVisualOverrides: true);
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
            StopBlockMove(blockId, snapToTarget: false);
            if (_blockViewPool.TryGetActive(blockId, out var blockView))
            {
                ResetBlockTransientFx(blockView);
            }

            ReleaseActiveBlockView(blockId, stopRoutines: false);
            _blockExitRoutineById.Remove(blockId);
        }

        private void ResetBlockTransientFx(BlockRootView blockView)
        {
            SetBlockCellsActive(blockView, true);
            ApplyOutlineDragState(blockView, false);
            StopDoorExitBurstParticle(blockView);
        }

        private void RefreshConditionDrivenVisuals()
        {
            RefreshBlockConditionAppearances();
            RefreshAllConditionIndicators();
        }

        private void RefreshBlockConditionAppearances()
        {
            _blockViewPool.ForEachActive((blockId, blockView) =>
            {
                if (blockView == null || !boardController.TryGetRuntimeBlock(blockId, out var runtimeBlock))
                {
                    return;
                }

                var isLocked = boardController.IsBlockLocked(blockId);
                if (blockView.IsUsingLockedAppearance == isLocked)
                {
                    if (isLocked)
                    {
                        StopBlockConditionUnlockTransition(blockId, clearVisualOverrides: true);
                        ClearBlockColorOverrides(blockView);
                        ApplyOutlineDragState(blockView, false, forceWhite: true);
                    }

                    return;
                }

                var wasLocked = blockView.IsUsingLockedAppearance;
                StopBlockConditionUnlockTransition(blockId, clearVisualOverrides: true);
                ApplyRuntimeBlockVisualState(blockId, blockView, runtimeBlock);
                if (isLocked)
                {
                    ClearBlockColorOverrides(blockView);
                    ApplyOutlineDragState(blockView, false, forceWhite: true);
                    return;
                }

                if (wasLocked)
                {
                    StartBlockConditionUnlockTransition(blockId, blockView, runtimeBlock);
                    return;
                }

                ClearBlockColorOverrides(blockView);
                ApplyOutlineDragState(blockView, false);
            });
        }

        private void ApplyRuntimeBlockVisualState(int blockId, BlockRootView blockView, RuntimeBlockState runtimeBlock)
        {
            var blockVisualRequest = new BlockVisualBuildRequest
            {
                LevelData = null,
                BoardController = boardController,
                BlockViewPool = _blockViewPool,
                Layout = GetCurrentLayout(),
                ResolveMaterial = GetMaterial,
                IsBlockLocked = boardController.IsBlockLocked,
                SetActiveIfChanged = SetActiveIfChanged,
                ApplyWorldPosition = ApplyWorldPosition,
                SetOutlineDragActive = ApplyOutlineDragState
            };
            _blockVisualPresenter.ApplyRuntimeBlockVisualState(blockId, blockView, runtimeBlock, blockVisualRequest);
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
            var mappedMinX =
                MapLogicalToVisualCellIndex(matchedDoor.MinCell.x, gridDimensions.x);
            var mappedMaxX =
                MapLogicalToVisualCellIndex(matchedDoor.MaxCell.x, gridDimensions.x);
            var mappedMinY =
                MapLogicalToVisualCellIndex(matchedDoor.MinCell.y, gridDimensions.y);
            var mappedMaxY =
                MapLogicalToVisualCellIndex(matchedDoor.MaxCell.y, gridDimensions.y);
            var centerX = (mappedMinX + mappedMaxX + 1) * 0.5f;
            var centerY = (mappedMinY + mappedMaxY + 1) * 0.5f;
            return new Vector2(
                layout.BoardOrigin.x + (centerX * layout.CellSize),
                layout.BoardOrigin.y + (centerY * layout.CellSize));
        }
    }
}
