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
                ApplyWorldTransform(blockView.RootTransform, ToWorldPosition(runtimeBlock.Position, layout),
                    Vector3.one);

                _blockViewPool.MarkActive(i, blockView);
                SetActiveIfChanged(blockView.RootObject, true);
            }
        }

        private void ReleaseActiveBlockViewsToPool()
        {
            _blockViewPool.ReleaseAllActive(StopBlockMove, StopBlockExit, SetActiveIfChanged);
        }

        private BlockRootView AcquireBlockRoot(BlockShapeType blockType)
        {
            return _blockViewPool.Acquire(blockType);
        }

        private void ReleaseActiveBlockView(int blockId, bool stopRoutines = true)
        {
            _blockViewPool.ReleaseAndRemove(blockId, stopRoutines, StopBlockMove, StopBlockExit, SetActiveIfChanged);
        }

        private void SubscribeBoardEvents()
        {
            boardController.BlockMoved -= HandleBlockMoved;
            boardController.BlockMoved += HandleBlockMoved;
            boardController.BlockCleared -= HandleBlockCleared;
            boardController.BlockCleared += HandleBlockCleared;
        }

        private void UnsubscribeBoardEvents()
        {
            boardController.BlockMoved -= HandleBlockMoved;
            boardController.BlockCleared -= HandleBlockCleared;
        }

        private void HandleBlockMoved(int blockId, Vector2Int fromPosition, Vector2Int toPosition)
        {
            if (!_blockViewPool.TryGetActive(blockId, out var blockView))
            {
                return;
            }

            StopBlockMove(blockId);
            var distanceInCells = Mathf.Abs(toPosition.x - fromPosition.x) + Mathf.Abs(toPosition.y - fromPosition.y);
            if (distanceInCells <= 0)
            {
                return;
            }

            _blockMoveRoutineById[blockId] =
                StartCoroutine(MoveBlockRoutine(blockId, blockView, toPosition, distanceInCells));
        }

        private void HandleBlockCleared(int blockId, Vector2Int clearedPosition, Vector2Int exitDirection,
            DoorOpeningData matchedDoor)
        {
            if (!_blockViewPool.TryGetActive(blockId, out var blockView))
            {
                return;
            }

            StopBlockMove(blockId);
            StopBlockExit(blockId);
            _blockExitRoutineById[blockId] =
                StartCoroutine(ClearAndExitRoutine(blockId, blockView, clearedPosition, exitDirection, matchedDoor));
        }

        private IEnumerator MoveBlockRoutine(int blockId, BlockRootView blockView, Vector2Int targetGridPosition,
            int distanceInCells)
        {
            var targetWorldPosition = ToWorldPosition(targetGridPosition, GetCurrentLayout());
            var duration = MoveDuration * Mathf.Max(1, distanceInCells);
            yield return BlockMotionTween.TweenMove(blockView.RootTransform, targetWorldPosition, duration, MoveCurve);
            _blockMoveRoutineById.Remove(blockId);
        }

        private IEnumerator ClearAndExitRoutine(int blockId, BlockRootView blockView, Vector2Int clearedPosition,
            Vector2Int exitDirection, DoorOpeningData matchedDoor)
        {
            var layout = GetCurrentLayout();
            var blockTransform = blockView.RootTransform;
            var clearPosition = ToWorldPosition(clearedPosition, layout);

            if ((blockTransform.position - clearPosition).sqrMagnitude > 0.0001f)
            {
                var travelDistance = Vector3.Distance(blockTransform.position, clearPosition);
                var distanceInCells = Mathf.Max(1f, travelDistance / layout.CellSize);
                yield return BlockMotionTween.TweenMove(blockTransform, clearPosition, MoveDuration * distanceInCells,
                    MoveCurve);
            }

            audioManager.PlayBlockMatchSuccess();
            PlayDoorMatchFx(matchedDoor);

            var resolvedExitDirection = matchedDoor.ResolveExitDirection(boardController.GridDimensions, exitDirection);
            if (resolvedExitDirection == Vector2Int.zero)
            {
                FinalizeClearedBlock(blockId);
                yield break;
            }

            yield return BlockMotionTween.TweenExitThroughDoor(blockTransform, resolvedExitDirection,
                blockView.LocalCenter, ResolveDoorWorldCenter(matchedDoor, boardController.GridDimensions, layout),
                layout.DoorZ, layout.CellSize,
                ExitDuration, ExitTravelInCells, ExitMoveCurve, ExitScaleCurve, ExitMinScaleMultiplier);

            FinalizeClearedBlock(blockId);
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

        private void StopBlockExit(int blockId)
        {
            if (_blockExitRoutineById.TryGetValue(blockId, out var routine))
            {
                StopCoroutine(routine);
                _blockExitRoutineById.Remove(blockId);
            }
        }

        private void StopBlockMove(int blockId)
        {
            if (_blockMoveRoutineById.TryGetValue(blockId, out var routine))
            {
                StopCoroutine(routine);
                _blockMoveRoutineById.Remove(blockId);
            }
        }

        private void StopAllBlockRoutines()
        {
            foreach (var routine in _blockMoveRoutineById.Values)
            {
                StopCoroutine(routine);
            }

            _blockMoveRoutineById.Clear();

            foreach (var routine in _blockExitRoutineById.Values)
            {
                StopCoroutine(routine);
            }

            _blockExitRoutineById.Clear();
            StopAllDoorMatchFx();
        }

        private void FinalizeClearedBlock(int blockId)
        {
            ReleaseActiveBlockView(blockId, stopRoutines: false);
            _blockExitRoutineById.Remove(blockId);
        }

        private static Vector3 ToWorldPosition(Vector2Int gridPosition, in LayoutMetrics layout)
        {
            return new Vector3(
                layout.BoardOrigin.x + (gridPosition.x * layout.CellSize),
                layout.BoardOrigin.y + (gridPosition.y * layout.CellSize),
                layout.BlockZ);
        }

        private void ApplyBlockCells(BlockRootView blockView, RuntimeBlockState blockState, in LayoutMetrics layout)
        {
            var localCells = blockState.LocalCells ?? System.Array.Empty<Vector2Int>();
            var cellSize = layout.CellSize;
            var targetScale = Vector3.one * Mathf.Max(0.01f, cellSize * blockCellVisualScale);
            var resolvedMaterial = GetMaterial(blockState.ColorType);

            EnsureBlockCells(blockView, localCells.Length);
            blockView.LocalCenter = ResolveLocalCenter(localCells, cellSize);

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
                ApplySharedMaterial(cellObject, resolvedMaterial);
            }
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

        private void EnsureBlockCells(BlockRootView blockView, int requiredCellCount)
        {
            _blockViewPool.EnsureBlockCells(poolManager, blockView, requiredCellCount, _gridCellPoolByCell,
                SetActiveIfChanged);
        }
    }
}