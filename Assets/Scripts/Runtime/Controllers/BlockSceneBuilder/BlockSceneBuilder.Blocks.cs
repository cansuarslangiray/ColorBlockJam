using System.Collections;
using Runtime.Data;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private void LateUpdate()
        {
            if (_blockTargetPositionById.Count == 0 || blockMoveSmoothingSpeed <= 0f)
            {
                return;
            }

            _reachedTargetIds.Clear();
            var interpolationFactor = 1f - Mathf.Exp(-blockMoveSmoothingSpeed * Time.deltaTime);

            foreach (var pair in _blockTargetPositionById)
            {
                if (!_activeBlockRootById.TryGetValue(pair.Key, out var blockView) || blockView == null || !blockView.RootObject.activeSelf)
                {
                    _reachedTargetIds.Add(pair.Key);
                    continue;
                }

                var targetPosition = pair.Value;
                var currentPosition = blockView.RootTransform.position;
                if ((currentPosition - targetPosition).sqrMagnitude <= 0.0001f)
                {
                    blockView.RootTransform.position = targetPosition;
                    _reachedTargetIds.Add(pair.Key);
                    continue;
                }

                blockView.RootTransform.position = Vector3.Lerp(currentPosition, targetPosition, interpolationFactor);
            }

            foreach (var blockId in _reachedTargetIds)
            {
                _blockTargetPositionById.Remove(blockId);
            }
        }

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
            _blockTargetPositionById.Clear();

            foreach (var blockView in _blockRootPool)
            {
                StopBlockMovement(blockView.BlockId);
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
                _blockTargetPositionById[i] = worldPosition;
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

        private void HandleBlockMoved(int blockId, Vector2Int newPosition)
        {
            if (!_activeBlockRootById.TryGetValue(blockId, out var blockView) || blockView == null)
            {
                return;
            }

            StopBlockExit(blockId);

            var targetPosition = ToWorldPosition(newPosition);
            if (blockMoveSmoothingSpeed <= 0f)
            {
                StopBlockMovement(blockId);
                blockView.RootTransform.position = targetPosition;
                return;
            }

            _blockTargetPositionById[blockId] = targetPosition;
        }

        private void HandleBlockCleared(int blockId, Vector2Int clearedPosition, Vector2Int exitDirection)
        {
            if (!_activeBlockRootById.TryGetValue(blockId, out var blockView) || blockView == null)
            {
                return;
            }

            StopBlockMovement(blockId);
            StopBlockExit(blockId);
            blockView.RootTransform.position = ToWorldPosition(clearedPosition);

            if (exitDirection == Vector2Int.zero)
            {
                SetActiveIfChanged(blockView.RootObject, false);
                _activeBlockRootById.Remove(blockId);
                return;
            }

            _blockExitRoutineById[blockId] = StartCoroutine(ExitBlockRoutine(blockId, blockView, exitDirection));
        }

        private IEnumerator ExitBlockRoutine(int blockId, BlockRootView blockView, Vector2Int exitDirection)
        {
            var blockTransform = blockView.RootTransform;
            var startPosition = blockTransform.position;
            var startScale = blockTransform.localScale;
            var distance = Mathf.Max(0.2f, doorExitTravelInCells) * CellSize;
            var targetPosition = startPosition + new Vector3(exitDirection.x, exitDirection.y, 0f) * distance;
            var duration = Mathf.Max(0.05f, doorExitDuration);
            var elapsed = 0f;
            var minScale = startScale * Mathf.Clamp01(doorExitMinScaleMultiplier);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var moveLerp = doorExitMoveCurve != null ? Mathf.Clamp01(doorExitMoveCurve.Evaluate(normalized)) : normalized;
                var scaleLerp = doorExitScaleCurve != null ? Mathf.Clamp01(doorExitScaleCurve.Evaluate(normalized)) : 1f - normalized;

                blockTransform.position = Vector3.LerpUnclamped(startPosition, targetPosition, moveLerp);
                blockTransform.localScale = Vector3.LerpUnclamped(minScale, startScale, scaleLerp);
                yield return null;
            }

            blockTransform.position = targetPosition;
            blockTransform.localScale = startScale;
            SetActiveIfChanged(blockView.RootObject, false);

            _activeBlockRootById.Remove(blockId);
            _blockExitRoutineById.Remove(blockId);
        }

        private void StopBlockMovement(int blockId)
        {
            _blockTargetPositionById.Remove(blockId);
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

        private void StopAllBlockRoutines()
        {
            foreach (var pair in _blockExitRoutineById)
            {
                if (pair.Value != null)
                {
                    StopCoroutine(pair.Value);
                }
            }

            _blockTargetPositionById.Clear();
            _blockExitRoutineById.Clear();
        }

        private Vector3 ToWorldPosition(Vector2Int gridPosition)
        {
            var cellSize = CellSize;
            var boardOrigin = BoardOrigin;
            var gridZ = Mathf.Abs((float)boardCellsZOffset);
            var blockZ = gridZ - Mathf.Max(0.01f, blockLayerForwardOffsetFromGrid);
            return new Vector3(boardOrigin.x + (gridPosition.x * cellSize), boardOrigin.y + (gridPosition.y * cellSize), blockZ);
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
