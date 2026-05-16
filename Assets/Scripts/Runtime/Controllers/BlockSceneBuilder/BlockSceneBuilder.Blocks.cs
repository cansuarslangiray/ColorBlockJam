using System.Collections;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private void EnsureBlockPool(int requiredBlockCount)
        {
            var blockParent = BlocksRoot;
            requiredBlockCount = Mathf.Max(0, requiredBlockCount);

            while (_blockRootPool.Count < requiredBlockCount)
            {
                var blockId = _blockRootPool.Count;
                var rootName = GetRuntimeName(blockRootNamePrefix, blockId);
                var rootObject = string.IsNullOrWhiteSpace(rootName) ? new GameObject() : new GameObject(rootName);
                rootObject.transform.SetParent(blockParent, false);
                rootObject.transform.localRotation = Quaternion.identity;
                _blockRootPool.Add(new BlockRootView(blockId, rootObject));
            }
        }

        private void ApplyBlockVisuals(LevelData levelData)
        {
            _activeBlockRootById.Clear();

            foreach (var blockView in _blockRootPool)
            {
                StopBlockMove(blockView.BlockId);
                StopBlockExit(blockView.BlockId);
                SetActiveIfChanged(blockView.RootObject, false);
            }

            var sourceBlockCount = GetSourceBlockCount(levelData);
            for (var i = 0; i < sourceBlockCount; i++)
            {
                if (!boardController.TryGetRuntimeBlock(i, out var runtimeBlock))
                {
                    continue;
                }

                var blockView = _blockRootPool[i];
                ApplyBlockCells(blockView, runtimeBlock);

                var worldPosition = ToWorldPosition(runtimeBlock.Position);
                if (blockView.RootTransform.position != worldPosition)
                {
                    blockView.RootTransform.position = worldPosition;
                }

                if (blockView.RootTransform.localScale != Vector3.one)
                {
                    blockView.RootTransform.localScale = Vector3.one;
                }

                _activeBlockRootById[i] = blockView;
                SetActiveIfChanged(blockView.RootObject, true);
            }
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
            if (!_activeBlockRootById.TryGetValue(blockId, out var blockView) || blockView == null)
            {
                return;
            }

            StopBlockMove(blockId);
            var distanceInCells = Mathf.Abs(toPosition.x - fromPosition.x) + Mathf.Abs(toPosition.y - fromPosition.y);
            _blockMoveRoutineById[blockId] =
                StartCoroutine(MoveBlockRoutine(blockId, blockView, toPosition, distanceInCells));
        }

        private void HandleBlockCleared(int blockId, Vector2Int clearedPosition, Vector2Int exitDirection, DoorOpeningData matchedDoor)
        {
            if (!_activeBlockRootById.TryGetValue(blockId, out var blockView) || blockView == null)
            {
                return;
            }

            StopBlockMove(blockId);
            StopBlockExit(blockId);
            _blockExitRoutineById[blockId] = StartCoroutine(ClearAndExitRoutine(blockId, blockView, clearedPosition, exitDirection, matchedDoor));
        }

        private IEnumerator MoveBlockRoutine(int blockId, BlockRootView blockView, Vector2Int targetGridPosition,
            int distanceInCells)
        {
            var targetWorldPosition = ToWorldPosition(targetGridPosition);
            var duration = MoveDuration * Mathf.Max(1, distanceInCells);
            yield return TweenBlockMove(blockView.RootTransform, targetWorldPosition, duration, MoveCurve);
            _blockMoveRoutineById.Remove(blockId);
        }

        private IEnumerator ClearAndExitRoutine(int blockId, BlockRootView blockView, Vector2Int clearedPosition, Vector2Int exitDirection, DoorOpeningData matchedDoor)
        {
            var blockTransform = blockView.RootTransform;
            var targetGridPosition = ToWorldPosition(clearedPosition);
            if ((blockTransform.position - targetGridPosition).sqrMagnitude > 0.0001f)
            {
                var travelDistance = Vector3.Distance(blockTransform.position, targetGridPosition);
                var distanceInCells = Mathf.Max(1f, travelDistance / Mathf.Max(0.01f, CellSize));
                yield return TweenBlockMove(blockTransform, targetGridPosition, MoveDuration * distanceInCells,
                    MoveCurve);
            }

            PlayDoorMatchFx(matchedDoor);

            var resolvedExitDirection = ResolveExitDirectionForDoor(matchedDoor, exitDirection);
            if (resolvedExitDirection == Vector2Int.zero)
            {
                blockTransform.localScale = Vector3.one;
                SetActiveIfChanged(blockView.RootObject, false);
                _activeBlockRootById.Remove(blockId);
                _blockExitRoutineById.Remove(blockId);
                yield break;
            }

            var startPosition = blockTransform.position;
            var startScale = blockTransform.localScale;
            var exitVector = new Vector3(resolvedExitDirection.x, resolvedExitDirection.y, 0f);
            var pullDistance = Mathf.Max(0.1f, Mathf.Min(0.55f, ExitTravelInCells * 0.32f) * CellSize);
            var passDistance = Mathf.Max(0.2f, ExitTravelInCells * CellSize);

            var blockLocalCenter = ResolveBlockLocalCenter(blockView);
            var doorWorldCenter = ResolveDoorWorldCenter(matchedDoor);
            var doorWorldZ = ResolveDoorWorldZ();

            var pullTargetPosition = new Vector3(
                doorWorldCenter.x - blockLocalCenter.x,
                doorWorldCenter.y - blockLocalCenter.y,
                Mathf.Max(startPosition.z, doorWorldZ + (CellSize * 0.05f)));
            pullTargetPosition += exitVector * pullDistance;

            var finalTargetPosition = pullTargetPosition + (exitVector * passDistance);
            finalTargetPosition.z = pullTargetPosition.z + (CellSize * 0.22f);

            var totalDuration = ExitDuration;
            var pullDuration = Mathf.Clamp(totalDuration * 0.42f, 0.05f, totalDuration);
            var passDuration = Mathf.Max(0.05f, totalDuration - pullDuration);
            var suctionScale = startScale * 0.84f;
            var minScale = startScale * ExitMinScaleMultiplier;
            var elapsed = 0f;

            while (elapsed < pullDuration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / pullDuration);
                var pullT = normalized * normalized;
                blockTransform.position = Vector3.LerpUnclamped(startPosition, pullTargetPosition, pullT);
                blockTransform.localScale = Vector3.LerpUnclamped(startScale, suctionScale, normalized);
                yield return null;
            }

            blockTransform.position = pullTargetPosition;
            blockTransform.localScale = suctionScale;

            elapsed = 0f;

            while (elapsed < passDuration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / passDuration);
                var moveT = ExitMoveCurve != null ? Mathf.Clamp01(ExitMoveCurve.Evaluate(normalized)) : normalized;
                var scaleT = ExitScaleCurve != null
                    ? Mathf.Clamp01(ExitScaleCurve.Evaluate(normalized))
                    : 1f - normalized;

                blockTransform.position = Vector3.LerpUnclamped(pullTargetPosition, finalTargetPosition, moveT);
                blockTransform.localScale = Vector3.LerpUnclamped(minScale, suctionScale, scaleT);
                yield return null;
            }

            blockTransform.position = finalTargetPosition;
            blockTransform.localScale = Vector3.one;
            SetActiveIfChanged(blockView.RootObject, false);

            _activeBlockRootById.Remove(blockId);
            _blockExitRoutineById.Remove(blockId);
        }

        private Vector2 ResolveDoorWorldCenter(DoorOpeningData matchedDoor)
        {
            var cellSize = CellSize;
            var boardOrigin = BoardOrigin;
            var centerX = (matchedDoor.MinCell.x + matchedDoor.MaxCell.x + 1) * 0.5f;
            var centerY = (matchedDoor.MinCell.y + matchedDoor.MaxCell.y + 1) * 0.5f;
            return new Vector2(boardOrigin.x + (centerX * cellSize), boardOrigin.y + (centerY * cellSize));
        }

        private float ResolveDoorWorldZ()
        {
            var cellSize = CellSize;
            var frameDepth = Mathf.Max(0.01f, edgeFrameDepthInCells * cellSize);
            var borderZ = Mathf.Abs((float)boardCellsZOffset) - 0.01f;
            var doorDepth = frameDepth * 1.08f;
            return borderZ - Mathf.Max(0.005f, doorDepthBiasFromFrame);
        }

        private static Vector2 ResolveBlockLocalCenter(BlockRootView blockView)
        {
            if (blockView == null || blockView.Cells.Count == 0)
            {
                return Vector2.zero;
            }

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var hasActiveCell = false;

            for (var i = 0; i < blockView.Cells.Count; i++)
            {
                var cellVisual = blockView.Cells[i];
                if (cellVisual == null || !cellVisual.GameObject || !cellVisual.GameObject.activeSelf)
                {
                    continue;
                }

                var localPos = cellVisual.Transform.localPosition;
                if (localPos.x < min.x) min.x = localPos.x;
                if (localPos.x > max.x) max.x = localPos.x;
                if (localPos.y < min.y) min.y = localPos.y;
                if (localPos.y > max.y) max.y = localPos.y;
                hasActiveCell = true;
            }

            if (!hasActiveCell)
            {
                return Vector2.zero;
            }

            return (min + max) * 0.5f;
        }

        private Vector2Int ResolveExitDirectionForDoor(DoorOpeningData matchedDoor, Vector2Int fallbackDirection)
        {
            if (boardController)
            {
                var gridSize = boardController.GridDimensions;
                if (gridSize.x > 0 && gridSize.y > 0)
                {
                    var maxX = gridSize.x - 1;
                    var maxY = gridSize.y - 1;

                    if (matchedDoor.MinCell.x <= 0)
                    {
                        return Vector2Int.left;
                    }

                    if (matchedDoor.MaxCell.x >= maxX)
                    {
                        return Vector2Int.right;
                    }

                    if (matchedDoor.MinCell.y <= 0)
                    {
                        return Vector2Int.down;
                    }

                    if (matchedDoor.MaxCell.y >= maxY)
                    {
                        return Vector2Int.up;
                    }
                }
            }

            if (fallbackDirection != Vector2Int.zero)
            {
                return fallbackDirection;
            }

            return matchedDoor.EdgeDirection.ToVector();
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
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var eased = easingCurve != null ? easingCurve.Evaluate(normalized) : normalized;
                blockTransform.position = Vector3.LerpUnclamped(startPosition, targetPosition, eased);
                yield return null;
            }

            blockTransform.position = targetPosition;
        }

        private IEnumerator PlayLandingSquash(Transform blockTransform)
        {
            if (LandingAmount <= 0f)
            {
                blockTransform.localScale = Vector3.one;
                yield break;
            }

            var startScale = Vector3.one;
            var squashScale = new Vector3(1f + LandingAmount, 1f - LandingAmount, 1f);
            var elapsed = 0f;

            while (elapsed < LandingDuration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / LandingDuration);
                var curveT = LandingCurve != null ? Mathf.Clamp01(LandingCurve.Evaluate(normalized)) : normalized;
                var wave = Mathf.Sin(curveT * Mathf.PI);
                blockTransform.localScale = Vector3.LerpUnclamped(startScale, squashScale, wave);
                yield return null;
            }

            blockTransform.localScale = Vector3.one;
        }

        private void StopBlockExit(int blockId)
        {
            if (!_blockExitRoutineById.TryGetValue(blockId, out var routine) || routine == null)
            {
                return;
            }

            StopCoroutine(routine);
            _blockExitRoutineById.Remove(blockId);
        }

        private void StopBlockMove(int blockId)
        {
            if (!_blockMoveRoutineById.TryGetValue(blockId, out var routine) || routine == null)
            {
                return;
            }

            StopCoroutine(routine);
            _blockMoveRoutineById.Remove(blockId);
        }

        private void StopAllBlockRoutines()
        {
            foreach (var pair in _blockMoveRoutineById)
            {
                if (pair.Value != null)
                {
                    StopCoroutine(pair.Value);
                }
            }

            _blockMoveRoutineById.Clear();

            foreach (var pair in _blockExitRoutineById)
            {
                if (pair.Value != null)
                {
                    StopCoroutine(pair.Value);
                }
            }

            _blockExitRoutineById.Clear();
            StopAllDoorMatchFx();
        }

        private Vector3 ToWorldPosition(Vector2Int gridPosition)
        {
            var cellSize = CellSize;
            var boardOrigin = BoardOrigin;
            var gridZ = Mathf.Abs((float)boardCellsZOffset);
            var blockZ = gridZ - Mathf.Max(0.01f, blockLayerForwardOffsetFromGrid);
            return new Vector3(boardOrigin.x + (gridPosition.x * cellSize), boardOrigin.y + (gridPosition.y * cellSize),
                blockZ);
        }

        private void ApplyBlockCells(BlockRootView blockView, RuntimeBlockState blockState)
        {
            var localCells = blockState.LocalCells ?? System.Array.Empty<Vector2Int>();
            var localCellCount = localCells.Length;
            var cellSize = CellSize;
            var scaledCellSize = Mathf.Max(0.01f, cellSize * blockCellVisualScale);
            var targetScale = Vector3.one * scaledCellSize;

            var resolvedMaterial = GetBlockMaterial(blockState.ColorType);

            EnsureBlockCells(blockView, localCellCount);
            for (var i = 0; i < blockView.Cells.Count; i++)
            {
                var cellVisual = blockView.Cells[i];
                var isActive = i < localCellCount;
                SetActiveIfChanged(cellVisual.GameObject, isActive);
                if (!isActive)
                {
                    continue;
                }

                var localCell = localCells[i];
                var localPosition = new Vector3((localCell.x + 0.5f) * cellSize, (localCell.y + 0.5f) * cellSize, 0f);
                ApplyLocalTransform(cellVisual.Transform, localPosition, targetScale);

                if (cellVisual.Renderer && cellVisual.Renderer.sharedMaterial != resolvedMaterial)
                {
                    cellVisual.Renderer.sharedMaterial = resolvedMaterial;
                }
            }
        }

        private void EnsureBlockCells(BlockRootView blockView, int requiredCellCount)
        {
            if (requiredCellCount <= 0)
            {
                return;
            }

            while (blockView.Cells.Count < requiredCellCount)
            {
                var cellIndex = blockView.Cells.Count;
                var cellVisual = CreateBlockCellObject(blockView.RootTransform);
                RenameIfConfigured(cellVisual.GameObject, GetRuntimeName(blockCellNamePrefix, cellIndex));
                blockView.Cells.Add(cellVisual);
            }
        }
    }
}