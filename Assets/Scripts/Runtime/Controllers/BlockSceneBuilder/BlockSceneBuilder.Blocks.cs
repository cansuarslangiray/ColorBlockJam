using System.Collections;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using Runtime.Managers;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private void ApplyBlockVisuals(LevelJsonData levelData, in LayoutMetrics layout)
        {
            ReleaseActiveBlockViewsToPool();

            var sourceBlockCount = GetSourceBlockCount(levelData);
            for (var i = 0; i < sourceBlockCount; i++)
            {
                if (!boardController.TryGetRuntimeBlock(i, out var runtimeBlock))
                {
                    continue;
                }

                var blockType = levelData.blocks[i].ResolveBlockType(runtimeBlock.LocalCells?.Length ?? 1);
                var blockView = AcquireBlockRoot(blockType);
                if (blockView == null)
                {
                    continue;
                }

                blockView.BlockType = blockType;
                ApplyBlockCells(blockView, runtimeBlock, layout);
                ApplyWorldTransform(blockView.RootTransform, ToWorldPosition(runtimeBlock.Position, layout), Vector3.one);

                _activeBlockRootById[i] = blockView;
                SetActiveIfChanged(blockView.RootObject, true);
            }
        }

        private void ReleaseActiveBlockViewsToPool()
        {
            foreach (var pair in _activeBlockRootById)
            {
                ReleaseActiveBlockView(pair.Key, pair.Value);
            }

            _activeBlockRootById.Clear();
        }

        private BlockRootView AcquireBlockRoot(BlockShapeType blockType)
        {
            if (!_inactiveBlockRootsByType.TryGetValue(blockType, out var typePool) || typePool.Count == 0)
            {
                return null;
            }

            var lastIndex = typePool.Count - 1;
            var blockView = typePool[lastIndex];
            typePool.RemoveAt(lastIndex);
            return blockView;
        }

        private void ReleaseActiveBlockView(int blockId, BlockRootView blockView, bool stopRoutines = true)
        {
            if (stopRoutines)
            {
                StopBlockMove(blockId);
                StopBlockExit(blockId);
            }

            blockView.RootTransform.localScale = Vector3.one;
            for (var i = 0; i < blockView.Cells.Count; i++)
            {
                var cellObject = blockView.Cells[i];
                if (cellObject)
                {
                    SetActiveIfChanged(cellObject, false);
                }
            }

            SetActiveIfChanged(blockView.RootObject, false);
            GetOrCreateInactivePool(blockView.BlockType).Add(blockView);
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
            if (!_activeBlockRootById.TryGetValue(blockId, out var blockView))
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
            if (!_activeBlockRootById.TryGetValue(blockId, out var blockView))
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
            yield return TweenBlockMove(blockView.RootTransform, targetWorldPosition, duration, MoveCurve);
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
                yield return TweenBlockMove(blockTransform, clearPosition, MoveDuration * distanceInCells, MoveCurve);
            }

            AudioManager.Instance.PlayBlockMatchSuccess();
            PlayDoorMatchFx(matchedDoor);

            var resolvedExitDirection = matchedDoor.ResolveExitDirection(boardController.GridDimensions, exitDirection);
            if (resolvedExitDirection == Vector2Int.zero)
            {
                FinalizeClearedBlock(blockId, blockView);
                yield break;
            }

            var startPosition = blockTransform.position;
            var startScale = blockTransform.localScale;
            var exitVector = new Vector3(resolvedExitDirection.x, resolvedExitDirection.y, 0f);

            var blockLocalCenter = blockView.LocalCenter;
            var doorWorldCenter = ResolveDoorWorldCenter(matchedDoor, layout);
            var doorWorldZ = layout.DoorZ;

            var doorCenterTargetPosition = new Vector3(
                doorWorldCenter.x - blockLocalCenter.x,
                doorWorldCenter.y - blockLocalCenter.y,
                Mathf.Max(startPosition.z, doorWorldZ + (layout.CellSize * 0.05f)));

            var passThroughDistance = ExitTravelInCells * layout.CellSize * 1.56f;
            var finalTargetPosition = doorCenterTargetPosition + (exitVector * passThroughDistance);
            finalTargetPosition.z = doorCenterTargetPosition.z + (layout.CellSize * 0.16f);

            var totalDuration = ExitDuration;
            var approachDuration = totalDuration * 0.58f;
            var passDuration = totalDuration - approachDuration;
            var invApproachDuration = 1f / approachDuration;
            var invPassDuration = 1f / passDuration;
            var exitMoveCurve = ExitMoveCurve;
            var exitScaleCurve = ExitScaleCurve;
            var minScale = startScale * ExitMinScaleMultiplier;
            var elapsed = 0f;

            while (elapsed < approachDuration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed * invApproachDuration);
                var pullT = normalized * normalized;
                blockTransform.position = Vector3.LerpUnclamped(startPosition, doorCenterTargetPosition, pullT);
                blockTransform.localScale = startScale;
                yield return null;
            }

            blockTransform.position = doorCenterTargetPosition;
            blockTransform.localScale = startScale;

            elapsed = 0f;

            while (elapsed < passDuration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed * invPassDuration);
                var moveT = EvaluateCurve01(exitMoveCurve, normalized, normalized);
                var shrinkNormalized = Mathf.Clamp01((normalized - 0.55f) / 0.45f);
                var scaleT = EvaluateCurve01(exitScaleCurve, shrinkNormalized, 1f - shrinkNormalized);

                blockTransform.position = Vector3.LerpUnclamped(doorCenterTargetPosition, finalTargetPosition, moveT);
                blockTransform.localScale = Vector3.LerpUnclamped(minScale, startScale, scaleT);
                yield return null;
            }

            blockTransform.position = finalTargetPosition;

            FinalizeClearedBlock(blockId, blockView);
        }

        private static Vector2 ResolveDoorWorldCenter(DoorOpeningData matchedDoor, in LayoutMetrics layout)
        {
            var centerX = (matchedDoor.MinCell.x + matchedDoor.MaxCell.x + 1) * 0.5f;
            var centerY = (matchedDoor.MinCell.y + matchedDoor.MaxCell.y + 1) * 0.5f;
            return new Vector2(
                layout.BoardOrigin.x + (centerX * layout.CellSize),
                layout.BoardOrigin.y + (centerY * layout.CellSize));
        }

        private IEnumerator TweenBlockMove(Transform blockTransform, Vector3 targetPosition, float duration,
            AnimationCurve easingCurve)
        {
            var startPosition = blockTransform.position;
            if ((startPosition - targetPosition).sqrMagnitude <= 0.0001f)
            {
                blockTransform.position = targetPosition;
                yield break;
            }

            var elapsed = 0f;
            var safeDuration = Mathf.Max(0.0001f, duration);
            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / safeDuration);
                var eased = EvaluateCurve01(easingCurve, normalized, normalized);
                blockTransform.position = Vector3.LerpUnclamped(startPosition, targetPosition, eased);
                yield return null;
            }

            blockTransform.position = targetPosition;
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

        private void FinalizeClearedBlock(int blockId, BlockRootView blockView)
        {
            ReleaseActiveBlockView(blockId, blockView, false);
            _activeBlockRootById.Remove(blockId);
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
            if (requiredCellCount <= blockView.Cells.Count)
            {
                return;
            }

            if (!_sharedBlockCellTemplate)
            {
                CacheBlockCellTemplate(blockView);
                CacheSharedBlockCellTemplateFromPools();
            }

            poolManager.EnsureBlockRootCellPoolSize(blockView.RootObject, requiredCellCount, _sharedBlockCellTemplate);
            CacheBlockCellPool(blockView);

            if (!_sharedBlockCellTemplate && blockView.Cells.Count > 0)
            {
                _sharedBlockCellTemplate = blockView.Cells[0];
            }
        }

        private void CacheSharedBlockCellTemplateFromPools()
        {
            foreach (var pair in _inactiveBlockRootsByType)
            {
                var pool = pair.Value;
                for (var i = 0; i < pool.Count; i++)
                {
                    var blockView = pool[i];
                    if (blockView == null || blockView.Cells.Count == 0)
                    {
                        continue;
                    }

                    var candidate = blockView.Cells[0];
                    if (candidate)
                    {
                        _sharedBlockCellTemplate = candidate;
                        return;
                    }
                }
            }

            foreach (var pair in _gridCellPoolByCell)
            {
                var candidate = pair.Value;
                if (candidate)
                {
                    _sharedBlockCellTemplate = candidate;
                    return;
                }
            }
        }
    }
}
