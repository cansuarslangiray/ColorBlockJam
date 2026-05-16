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
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector2 boardOrigin;

        public event Action LevelCompleted;
        public event Action<int, Vector2Int> BlockMoved;
        public event Action<int, Vector2Int, Vector2Int> BlockCleared;

        private LevelData CurrentLevel { get; set; }
        public bool IsInitialized { get; private set; }
        private int RuntimeBlockCount { get; set; }
        public Vector2Int GridDimensions => CurrentLevel ? CurrentLevel.gridDimensions : Vector2Int.zero;
        public float CellSize => cellSize;
        public Vector2 BoardOrigin => boardOrigin;

        private readonly Dictionary<int, RuntimeBlockState> _runtimeBlocks = new();
        private readonly List<DoorOpeningData> _doorOpenings = new();
        private readonly BoardOccupancyMap _occupancyMap = new();

        public void Setup(LevelData levelData)
        {
            IsInitialized = false;
            _runtimeBlocks.Clear();
            _doorOpenings.Clear();
            RuntimeBlockCount = 0;

            if (!levelData)
            {
                CurrentLevel = null;
                return;
            }

            CurrentLevel = levelData;

            if (levelData.blocks == null)
            {
                _occupancyMap.Configure(levelData.gridDimensions.x, levelData.gridDimensions.y);
                IsInitialized = true;
                return;
            }

            _occupancyMap.Configure(levelData.gridDimensions.x, levelData.gridDimensions.y);

            if (levelData.blockedCells != null && levelData.blockedCells.Count > 0)
            {
                _occupancyMap.MarkBlockedCells(levelData.blockedCells);
            }

            var openings = levelData.GetDoorOpenings();
            foreach (var openingData in openings)
            {
                _doorOpenings.Add(openingData);
            }

            for (var i = 0; i < levelData.blocks.Count; i++)
            {
                var blockData = levelData.blocks[i];
                var blockLocalCells = blockData.GetLocalCells();

                var runtimeBlock = new RuntimeBlockState(i, blockData.position, blockLocalCells,
                    blockData.movementConstraint, blockData.colorType);

                if (!_occupancyMap.CanPlace(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells))
                {
                    continue;
                }

                _runtimeBlocks.Add(runtimeBlock.Id, runtimeBlock);
                _occupancyMap.FillBlock(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells);
            }

            IsInitialized = true;
            RuntimeBlockCount = _runtimeBlocks.Count;
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
            return IsInitialized && _occupancyMap.TryGetBlockAt(cell.x, cell.y, out blockId);
        }

        public bool TryGetBlockMovementConstraint(int blockId, out BlockMovementConstraint movementConstraint)
        {
            movementConstraint = default;
            if (!TryGetRuntimeBlock(blockId, out var block))
            {
                return false;
            }

            movementConstraint = block.MovementConstraint;
            return true;
        }

        public bool TryMoveBlockByStep(int blockId, Direction direction)
        {
            if (!TryGetRuntimeBlock(blockId, out var block))
            {
                return false;
            }

            if (!CanMoveInDirection(block.MovementConstraint, direction))
            {
                return false;
            }

            _occupancyMap.ClearBlock(blockId, block.Position, block.LocalCells);

            var targetPosition = block.Position + direction.ToVector();
            var canClear = TryGetDoorExit(block, targetPosition, direction, out var exitDoor);

            if (!canClear && !CanMoveByOneStep(block, direction))
            {
                _occupancyMap.FillBlock(blockId, block.Position, block.LocalCells);
                return false;
            }

            block.Position = targetPosition;

            if (canClear)
            {
                _runtimeBlocks.Remove(blockId);
                BlockCleared?.Invoke(blockId, block.Position, direction.ToVector());
                SetRuntimeBlockCount(_runtimeBlocks.Count);
            }
            else
            {
                _runtimeBlocks[blockId] = block;
                _occupancyMap.FillBlock(blockId, block.Position, block.LocalCells);
                BlockMoved?.Invoke(blockId, block.Position);
            }

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

        private bool CanMoveByOneStep(RuntimeBlockState block, Direction direction)
        {
            var nextPosition = block.Position + direction.ToVector();
            return _occupancyMap.CanPlace(block.Id, nextPosition, block.LocalCells);
        }

        private bool TryGetDoorExit(RuntimeBlockState block, Vector2Int blockPosition, Direction moveDirection,
            out DoorOpeningData doorExit)
        {
            doorExit = default;

            var localCells = block.LocalCells;
            if (localCells == null || localCells.Length == 0 || _doorOpenings.Count == 0)
            {
                return false;
            }

            GetBlockBounds(localCells, blockPosition, out var minX, out var maxX, out var minY, out var maxY);

            foreach (var opening in _doorOpenings)
            {
                if (opening.ColorType != block.ColorType || opening.EdgeDirection != moveDirection)
                {
                    continue;
                }

                if (!IsBlockTouchingDoorEdge(opening, moveDirection, minX, maxX, minY, maxY))
                {
                    continue;
                }

                if (!IsBlockInsideDoorSpan(opening, moveDirection, minX, maxX, minY, maxY))
                {
                    continue;
                }

                if (!FitsInsideDoor(opening, moveDirection, minX, maxX, minY, maxY))
                {
                    continue;
                }

                doorExit = opening;
                return true;
            }

            return false;
        }

        private static bool FitsInsideDoor(DoorOpeningData opening, Direction moveDirection, int minX, int maxX,
            int minY, int maxY)
        {
            var widthOnDoorAxis = moveDirection.IsHorizontal() ? (maxY - minY) + 1 : (maxX - minX) + 1;
            return widthOnDoorAxis <= opening.OpeningWidth;
        }

        private static void GetBlockBounds(Vector2Int[] localCells, Vector2Int blockPosition, out int minX,
            out int maxX, out int minY, out int maxY)
        {
            minX = int.MaxValue;
            maxX = int.MinValue;
            minY = int.MaxValue;
            maxY = int.MinValue;

            foreach (var cell in localCells)
            {
                var worldCell = blockPosition + cell;
                if (worldCell.x < minX) minX = worldCell.x;
                if (worldCell.x > maxX) maxX = worldCell.x;
                if (worldCell.y < minY) minY = worldCell.y;
                if (worldCell.y > maxY) maxY = worldCell.y;
            }
        }

        private static bool IsBlockTouchingDoorEdge(DoorOpeningData opening, Direction moveDirection, int minX,
            int maxX, int minY, int maxY)
        {
            return moveDirection switch
            {
                Direction.Left => minX == opening.MinCell.x,
                Direction.Right => maxX == opening.MaxCell.x,
                Direction.Down => minY == opening.MinCell.y,
                Direction.Up => maxY == opening.MaxCell.y,
                _ => false
            };
        }

        private static bool IsBlockInsideDoorSpan(DoorOpeningData opening, Direction moveDirection, int minX, int maxX,
            int minY, int maxY)
        {
            if (moveDirection.IsHorizontal())
            {
                return minY >= opening.MinCell.y && maxY <= opening.MaxCell.y;
            }

            return minX >= opening.MinCell.x && maxX <= opening.MaxCell.x;
        }

        public bool TryGetRuntimeBlock(int blockId, out RuntimeBlockState block)
        {
            block = default;
            return IsInitialized && _runtimeBlocks.TryGetValue(blockId, out block);
        }

        private void SetRuntimeBlockCount(int nextCount)
        {
            var previousCount = RuntimeBlockCount;
            nextCount = Mathf.Max(0, nextCount);
            if (RuntimeBlockCount == nextCount)
                return;

            RuntimeBlockCount = nextCount;
            if (nextCount == 0 && previousCount != nextCount)
            {
                LevelCompleted?.Invoke();
            }
        }
    }
}
