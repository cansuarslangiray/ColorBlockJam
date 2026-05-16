using System.Collections;
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

        private void HandleBlockCleared(int blockId, Vector2Int clearedPosition, Vector2Int exitDirection)
        {
            if (!_activeBlockRootById.TryGetValue(blockId, out var blockView) || blockView == null)
            {
                return;
            }

            StopBlockMove(blockId);
            StopBlockExit(blockId);
            _blockExitRoutineById[blockId] =
                StartCoroutine(ClearAndExitRoutine(blockId, blockView, clearedPosition, exitDirection));
        }

        private IEnumerator MoveBlockRoutine(int blockId, BlockRootView blockView, Vector2Int targetGridPosition,
            int distanceInCells)
        {
            var targetWorldPosition = ToWorldPosition(targetGridPosition);
            var duration = MoveDuration * Mathf.Max(1, distanceInCells);
            yield return TweenBlockMove(blockView.RootTransform, targetWorldPosition, duration, MoveCurve);
            _blockMoveRoutineById.Remove(blockId);
        }

        private IEnumerator ClearAndExitRoutine(int blockId, BlockRootView blockView, Vector2Int clearedPosition,
            Vector2Int exitDirection)
        {
            var blockTransform = blockView.RootTransform;
            var targetGridPosition = ToWorldPosition(clearedPosition);
            if ((blockTransform.position - targetGridPosition).sqrMagnitude > 0.0001f)
            {
                yield return TweenBlockMove(blockTransform, targetGridPosition, MoveDuration, MoveCurve);
            }

            yield return PlayLandingSquash(blockTransform);

            if (exitDirection == Vector2Int.zero)
            {
                blockTransform.localScale = Vector3.one;
                SetActiveIfChanged(blockView.RootObject, false);
                _activeBlockRootById.Remove(blockId);
                _blockExitRoutineById.Remove(blockId);
                yield break;
            }

            var startPosition = blockTransform.position;
            var startScale = blockTransform.localScale;
            var distance = ExitTravelInCells * CellSize;
            var targetPosition = startPosition + new Vector3(exitDirection.x, exitDirection.y, 0f) * distance;
            var duration = ExitDuration;
            var elapsed = 0f;
            var minScale = startScale * ExitMinScaleMultiplier;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var moveT = ExitMoveCurve != null ? Mathf.Clamp01(ExitMoveCurve.Evaluate(normalized)) : normalized;
                var scaleT = ExitScaleCurve != null
                    ? Mathf.Clamp01(ExitScaleCurve.Evaluate(normalized))
                    : 1f - normalized;

                blockTransform.position = Vector3.LerpUnclamped(startPosition, targetPosition, moveT);
                blockTransform.localScale = Vector3.LerpUnclamped(minScale, startScale, scaleT);
                yield return null;
            }

            blockTransform.position = targetPosition;
            blockTransform.localScale = Vector3.one;
            SetActiveIfChanged(blockView.RootObject, false);

            _activeBlockRootById.Remove(blockId);
            _blockExitRoutineById.Remove(blockId);
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