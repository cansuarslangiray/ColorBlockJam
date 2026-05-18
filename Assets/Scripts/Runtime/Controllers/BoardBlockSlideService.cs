using System.Collections.Generic;
using Runtime.Core;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers
{
    internal sealed class BoardBlockSlideService
    {
        private readonly Dictionary<int, RuntimeBlockState> _runtimeBlocks;
        private readonly IReadOnlyList<DoorOpeningData> _doorOpenings;
        private readonly BoardOccupancyMap _occupancyMap;

        public BoardBlockSlideService(Dictionary<int, RuntimeBlockState> runtimeBlocks,
            IReadOnlyList<DoorOpeningData> doorOpenings, BoardOccupancyMap occupancyMap)
        {
            _runtimeBlocks = runtimeBlocks;
            _doorOpenings = doorOpenings;
            _occupancyMap = occupancyMap;
        }

        public bool TrySlide(int blockId, Direction direction, int maxCellsToMove, out BoardBlockSlideResult slideResult)
        {
            slideResult = default;
            if (!_runtimeBlocks.TryGetValue(blockId, out var block) ||
                !IsDirectionAllowed(block.MovementConstraint, direction))
            {
                return false;
            }

            var startPosition = block.Position;
            var currentPosition = startPosition;
            var directionVector = direction.ToVector();
            var requestedCells = Mathf.Max(1, maxCellsToMove);
            var movedCellCount = 0;
            var hasMoved = false;
            var reachedDoor = false;
            var matchedDoor = default(DoorOpeningData);
            var currentlyOverlappingDoor = _occupancyMap.IsDoorOverlapping(block, currentPosition);

            while (requestedCells > 0)
            {
                requestedCells--;
                var nextPosition = currentPosition + directionVector;
                var canExitThroughDoor = DoorExitEvaluator.TryResolveDoorExit(block, nextPosition, direction, _doorOpenings,
                    out var resolvedDoor);
                var nextOverlapsDoor = _occupancyMap.IsDoorOverlapping(block, nextPosition);

                if (canExitThroughDoor && nextOverlapsDoor && !currentlyOverlappingDoor)
                {
                    matchedDoor = resolvedDoor;
                    reachedDoor = true;
                    hasMoved = true;
                    break;
                }

                if (!canExitThroughDoor && nextOverlapsDoor && !currentlyOverlappingDoor)
                {
                    break;
                }

                if (!_occupancyMap.CanPlace(block.Id, nextPosition, block.LocalCells))
                {
                    break;
                }

                currentPosition = nextPosition;
                currentlyOverlappingDoor = nextOverlapsDoor;
                hasMoved = true;
                movedCellCount++;

                if (!canExitThroughDoor)
                {
                    continue;
                }

                matchedDoor = resolvedDoor;
                reachedDoor = true;
                break;
            }

            if (hasMoved && !reachedDoor &&
                DoorExitEvaluator.TryResolveDoorPullExit(block, currentPosition, _doorOpenings, out var pulledDoor))
            {
                matchedDoor = pulledDoor;
                reachedDoor = true;
            }

            if (hasMoved && !reachedDoor)
            {
                var frontCellPosition = currentPosition + directionVector;
                if (DoorExitEvaluator.TryResolveDoorExit(block, frontCellPosition, direction, _doorOpenings,
                        out var frontDoor) &&
                    !_occupancyMap.IsDoorOverlapping(block, currentPosition))
                {
                    matchedDoor = frontDoor;
                    reachedDoor = true;
                }
            }

            if (!hasMoved && !reachedDoor &&
                TryResolveStationaryDoorPull(block, currentPosition, out var stationaryDoor))
            {
                matchedDoor = stationaryDoor;
                reachedDoor = true;
                hasMoved = true;
            }

            if (!hasMoved)
            {
                return false;
            }

            _occupancyMap.ClearBlock(blockId, startPosition, block.LocalCells);
            block.Position = currentPosition;

            if (reachedDoor)
            {
                _runtimeBlocks.Remove(blockId);
            }
            else
            {
                _runtimeBlocks[blockId] = block;
                _occupancyMap.FillBlock(blockId, currentPosition, block.LocalCells);
            }

            slideResult = new BoardBlockSlideResult(blockId, startPosition, currentPosition, movedCellCount, reachedDoor,
                matchedDoor);
            return true;
        }

        private bool TryResolveStationaryDoorPull(RuntimeBlockState block, Vector2Int blockPosition,
            out DoorOpeningData doorExit)
        {
            if (DoorExitEvaluator.TryResolveDoorPullExit(block, blockPosition, _doorOpenings, out doorExit))
            {
                return true;
            }

            if (_occupancyMap.IsDoorOverlapping(block, blockPosition))
            {
                return false;
            }

            return DoorExitEvaluator.TryResolveDoorPullFromFrontCell(block, blockPosition, _doorOpenings, out doorExit);
        }

        private static bool IsDirectionAllowed(BlockMovementConstraint movementConstraint, Direction direction)
        {
            switch (movementConstraint)
            {
                case BlockMovementConstraint.HorizontalOnly:
                    return direction is Direction.Left or Direction.Right;
                case BlockMovementConstraint.VerticalOnly:
                    return direction is Direction.Up or Direction.Down;
                default:
                    return true;
            }
        }
    }
}
