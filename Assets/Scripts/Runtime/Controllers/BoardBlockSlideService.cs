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
            if (!_runtimeBlocks.TryGetValue(blockId, out var block))
            {
                return false;
            }

            if (!block.BlockFeatures.IsDirectionAllowed(direction))
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
            _occupancyMap.ClearBlock(blockId, startPosition, block.LocalCells);

            while (requestedCells > 0)
            {
                requestedCells--;
                var nextPosition = currentPosition + directionVector;
                var canExitThroughDoor = TryResolveDoorExit(block, nextPosition, direction, out var resolvedDoor);
                var nextOverlapsDoor = _occupancyMap.IsDoorOverlapping(block, nextPosition);

                if (nextOverlapsDoor && !currentlyOverlappingDoor)
                {
                    if (!canExitThroughDoor || !_occupancyMap.CanOccupy(nextPosition, block.LocalCells))
                    {
                        break;
                    }

                    matchedDoor = resolvedDoor;
                    reachedDoor = true;
                    hasMoved = true;
                    break;
                }

                if (!_occupancyMap.CanOccupy(nextPosition, block.LocalCells))
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
                TryResolveDoorPullExit(block, currentPosition, out var pulledDoor))
            {
                matchedDoor = pulledDoor;
                reachedDoor = true;
            }

            if (hasMoved && !reachedDoor)
            {
                var frontCellPosition = currentPosition + directionVector;
                if (TryResolveDoorExit(block, frontCellPosition, direction, out var frontDoor) &&
                    _occupancyMap.CanOccupy(frontCellPosition, block.LocalCells) &&
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
                _occupancyMap.FillBlock(blockId, startPosition, block.LocalCells);
                return false;
            }

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
            if (TryResolveDoorPullExit(block, blockPosition, out doorExit))
            {
                return true;
            }

            if (_occupancyMap.IsDoorOverlapping(block, blockPosition))
            {
                return false;
            }

            return TryResolveDoorPullFromFrontCell(block, blockPosition, out doorExit);
        }

        private bool TryResolveDoorExit(RuntimeBlockState block, Vector2Int blockPosition,
            Direction moveDirection,
            out DoorOpeningData doorExit)
        {
            doorExit = default;
            if (!DoorExitEvaluator.TryResolveDoorExit(block, blockPosition, moveDirection, _doorOpenings,
                    out var resolvedDoor))
            {
                return false;
            }

            doorExit = resolvedDoor;
            return true;
        }

        private bool TryResolveDoorPullExit(RuntimeBlockState block, Vector2Int blockPosition,
            out DoorOpeningData doorExit)
        {
            doorExit = default;
            if (!DoorExitEvaluator.TryResolveDoorPullExit(block, blockPosition, _doorOpenings, out var resolvedDoor))
            {
                return false;
            }

            doorExit = resolvedDoor;
            return true;
        }

        private bool TryResolveDoorPullFromFrontCell(RuntimeBlockState block, Vector2Int blockPosition,
            out DoorOpeningData doorExit)
        {
            doorExit = default;
            if (!DoorExitEvaluator.TryResolveDoorPullFromFrontCell(block, blockPosition, _doorOpenings,
                    out var resolvedDoor))
            {
                return false;
            }

            var frontCellPosition = blockPosition + resolvedDoor.EdgeDirection.ToVector();
            if (!_occupancyMap.CanOccupy(frontCellPosition, block.LocalCells))
            {
                return false;
            }

            doorExit = resolvedDoor;
            return true;
        }
    }
}
