using System;
using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers
{
    public class BoardController : MonoBehaviour
    {
        [SerializeField] private bool slideUntilBlocked = true;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector2 boardOrigin;

        public event Action LevelCompleted;
        public event Action<int, Vector2Int> BlockMoved;
        public event Action<int, Vector2Int, Vector2Int> BlockCleared;

        public LevelData CurrentLevel { get; private set; }
        public bool IsInitialized { get; private set; }
        public float CellSize => cellSize;
        public Vector2 BoardOrigin => boardOrigin;

        private readonly Dictionary<int, RuntimeBlockState> _runtimeBlocks = new();
        private readonly DoorOpeningMap _doorOpeningMap = new();
        private readonly BoardOccupancyMap _occupancyMap = new();

        public void Setup(LevelData levelData)
        {
            CurrentLevel = levelData;
            IsInitialized = false;

            _runtimeBlocks.Clear();
            _doorOpeningMap.Clear();

            _occupancyMap.Configure(levelData.gridDimensions.x, levelData.gridDimensions.y, levelData.blockedCells);

            _doorOpeningMap.Build(levelData.doors, levelData.gridDimensions);

            for (var i = 0; i < levelData.blocks.Count; i++)
            {
                var blockData = levelData.blocks[i];
                var blockLocalCells = blockData.GetLocalCells();

                var runtimeBlock = new RuntimeBlockState(
                    i,
                    blockData.position,
                    blockLocalCells,
                    blockData.movementConstraint,
                    blockData.colorType);

                if (!_occupancyMap.CanPlace(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells))
                {
                    Debug.LogError($"Invalid block placement in level {levelData.levelNumber}, block index: {i}");
                    continue;
                }

                _runtimeBlocks.Add(runtimeBlock.Id, runtimeBlock);
                _occupancyMap.FillBlock(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells);
            }

            IsInitialized = true;
            CheckLevelCompletion();
        }


        public bool TryWorldToCell(Vector2 worldPosition, out Vector2Int cell)
        {
            cell = default;
            if (!IsInitialized || cellSize <= 0f)
            {
                return false;
            }

            var cellX = Mathf.FloorToInt((worldPosition.x - boardOrigin.x) / cellSize);
            var cellY = Mathf.FloorToInt((worldPosition.y - boardOrigin.y) / cellSize);
            if (!_occupancyMap.IsInside(cellX, cellY))
            {
                return false;
            }

            cell = new Vector2Int(cellX, cellY);
            return true;
        }

        public bool TryGetBlockAtCell(Vector2Int cell, out int blockId)
        {
            blockId = -1;
            if (!IsInitialized)
            {
                return false;
            }

            return _occupancyMap.TryGetBlockAt(cell.x, cell.y, out blockId);
        }

        public bool TryMoveBlock(int blockId, Direction direction)
        {
            if (!IsInitialized || !_runtimeBlocks.TryGetValue(blockId, out var block))
            {
                return false;
            }

            if (!CanMoveInDirection(block.MovementConstraint, direction))
            {
                return false;
            }

            _occupancyMap.ClearBlock(blockId, block.Position, block.LocalCells);
            var targetPosition = block.Position;
            var clearingOpening = default(DoorOpeningData);
            var isCleared = false;
            var stepCount = 0;

            if (slideUntilBlocked)
            {
                stepCount = GetSlideTravelSteps(
                    block,
                    direction,
                    out targetPosition,
                    out isCleared,
                    out clearingOpening);
            }
            else if (CanMoveByOneStep(block, direction))
            {
                targetPosition += direction.ToVector();
                stepCount = 1;
                isCleared = TryGetClearingOpening(block, targetPosition, out clearingOpening);
            }

            if (stepCount <= 0)
            {
                _occupancyMap.FillBlock(blockId, block.Position, block.LocalCells);
                return false;
            }

            block.Position = targetPosition;

            if (isCleared)
            {
                _runtimeBlocks.Remove(blockId);
                var hasExitDirection = TryGetExitDirection(direction, clearingOpening.edgeSide, out var exitDirection);
                BlockCleared?.Invoke(blockId, block.Position, hasExitDirection ? exitDirection : Vector2Int.zero);
            }
            else
            {
                _runtimeBlocks[blockId] = block;
                _occupancyMap.FillBlock(blockId, block.Position, block.LocalCells);

                BlockMoved?.Invoke(blockId, block.Position);
            }

            CheckLevelCompletion();
            return true;
        }

        private static bool CanMoveInDirection(BlockMovementConstraint movementConstraint, Direction inputDirection)
        {
            return movementConstraint switch
            {
                BlockMovementConstraint.HorizontalOnly => inputDirection.IsHorizontal(),
                BlockMovementConstraint.VerticalOnly => inputDirection.IsVertical(),
                _ => true
            };
        }

        private int GetSlideTravelSteps(
            RuntimeBlockState block,
            Direction direction,
            out Vector2Int targetPosition,
            out bool isCleared,
            out DoorOpeningData clearingOpening)
        {
            var delta = direction.ToVector();
            var currentPosition = block.Position;
            var steps = 0;

            while (_occupancyMap.CanPlace(block.Id, currentPosition + delta, block.LocalCells))
            {
                currentPosition += delta;
                steps++;

                if (TryGetClearingOpening(block, currentPosition, out var opening))
                {
                    targetPosition = currentPosition;
                    isCleared = true;
                    clearingOpening = opening;
                    return steps;
                }
            }

            targetPosition = currentPosition;
            isCleared = false;
            clearingOpening = default;
            return steps;
        }

        private bool CanMoveByOneStep(RuntimeBlockState block, Direction direction)
        {
            var nextPosition = block.Position + direction.ToVector();
            return _occupancyMap.CanPlace(block.Id, nextPosition, block.LocalCells);
        }

        private bool TryGetClearingOpening(
            RuntimeBlockState block,
            Vector2Int blockPosition,
            out DoorOpeningData openingData)
        {
            if (block.LocalCells == null || block.LocalCells.Length == 0)
            {
                openingData = default;
                return false;
            }

            for (var i = 0; i < block.LocalCells.Length; i++)
            {
                var worldCell = blockPosition + block.LocalCells[i];
                if (!_doorOpeningMap.TryGetOpening(worldCell, out var opening))
                {
                    continue;
                }

                if (opening.colorType != block.ColorType)
                {
                    continue;
                }

                if (!CanBlockPassOpening(block, blockPosition, opening))
                {
                    continue;
                }

                openingData = opening;
                return true;
            }

            openingData = default;
            return false;
        }

        private static bool CanBlockPassOpening(RuntimeBlockState block, Vector2Int blockPosition,
            DoorOpeningData opening)
        {
            if (block.LocalCells == null || block.LocalCells.Length == 0)
            {
                return false;
            }

            var verticalAxis = opening.edgeSide.IsVertical();
            var minAxis = int.MaxValue;
            var maxAxis = int.MinValue;

            foreach (var cell in block.LocalCells)
            {
                var worldCell = blockPosition + cell;
                var axisValue = verticalAxis ? worldCell.y : worldCell.x;
                if (axisValue < minAxis)
                {
                    minAxis = axisValue;
                }

                if (axisValue > maxAxis)
                {
                    maxAxis = axisValue;
                }
            }

            if (minAxis == int.MaxValue)
            {
                return false;
            }

            var blockWidthOnDoorAxis = (maxAxis - minAxis) + 1;
            return blockWidthOnDoorAxis <= opening.OpeningWidth;
        }

        private void CheckLevelCompletion()
        {
            if (_runtimeBlocks.Count == 0)
            {
                LevelCompleted?.Invoke();
            }
        }

        private static bool TryGetExitDirection(Direction moveDirection, EdgeSide edgeSide, out Vector2Int direction)
        {
            var moveVector = moveDirection.ToVector();
            if (moveVector != Vector2Int.zero && edgeSide.IsToward(moveVector))
            {
                direction = moveVector;
                return true;
            }

            return edgeSide.TryGetNormal(out direction);
        }
    }
}
