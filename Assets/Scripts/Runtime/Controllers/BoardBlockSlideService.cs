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
        private static readonly Vector2Int[] FloodNeighbors =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        private readonly Dictionary<int, RuntimeBlockState> _runtimeBlocks;
        private readonly IReadOnlyList<DoorOpeningData> _doorOpenings;
        private readonly BoardOccupancyMap _occupancyMap;
        private readonly Dictionary<int, CollisionProfileCacheEntry> _collisionProfileCacheByBlockId = new();

        public BoardBlockSlideService(Dictionary<int, RuntimeBlockState> runtimeBlocks,
            IReadOnlyList<DoorOpeningData> doorOpenings, BoardOccupancyMap occupancyMap)
        {
            _runtimeBlocks = runtimeBlocks;
            _doorOpenings = doorOpenings;
            _occupancyMap = occupancyMap;
        }

        public bool TrySlide(int blockId, Direction direction, int maxCellsToMove,
            out BoardBlockSlideResult slideResult)
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
            var currentlyOverlappingDoor = _occupancyMap.IsDoorOverlapping(block.ActiveExitLocalCells, currentPosition);
            _occupancyMap.ClearBlock(blockId, startPosition, block.RenderableLocalCells);
            var collisionProfile = GetOrCreateCollisionProfile(blockId, block);

            while (requestedCells > 0)
            {
                requestedCells--;
                var nextPosition = currentPosition + directionVector;
                var canExitThroughDoor = TryResolveDoorExit(block, nextPosition, direction, out var resolvedDoor);
                var nextOverlapsDoor = _occupancyMap.IsDoorOverlapping(block.ActiveExitLocalCells, nextPosition);

                if (nextOverlapsDoor && !currentlyOverlappingDoor)
                {
                    if (!canExitThroughDoor ||
                        !_occupancyMap.CanOccupy(nextPosition, block.RenderableLocalCells) ||
                        !IsExitSweepClear(collisionProfile, nextPosition, direction))
                    {
                        break;
                    }

                    matchedDoor = resolvedDoor;
                    reachedDoor = true;
                    hasMoved = true;
                    break;
                }

                if (!_occupancyMap.CanOccupy(nextPosition, block.RenderableLocalCells))
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

                if (!IsExitSweepClear(collisionProfile, currentPosition, direction))
                {
                    continue;
                }

                matchedDoor = resolvedDoor;
                reachedDoor = true;
                break;
            }

            if (hasMoved && !reachedDoor &&
                TryResolveDoorPullExit(block, currentPosition, out var pulledDoor) &&
                IsExitSweepClear(collisionProfile, currentPosition, pulledDoor.EdgeDirection))
            {
                matchedDoor = pulledDoor;
                reachedDoor = true;
            }

            if (hasMoved && !reachedDoor)
            {
                var frontCellPosition = currentPosition + directionVector;
                if (TryResolveDoorExit(block, frontCellPosition, direction, out var frontDoor) &&
                    _occupancyMap.CanOccupy(frontCellPosition, block.RenderableLocalCells) &&
                    IsExitSweepClear(collisionProfile, frontCellPosition, direction) &&
                    !_occupancyMap.IsDoorOverlapping(block.ActiveExitLocalCells, currentPosition))
                {
                    matchedDoor = frontDoor;
                    reachedDoor = true;
                }
            }

            if (!hasMoved && !reachedDoor &&
                TryResolveStationaryDoorPull(block, collisionProfile, currentPosition, out var stationaryDoor))
            {
                matchedDoor = stationaryDoor;
                reachedDoor = true;
                hasMoved = true;
            }

            if (!hasMoved)
            {
                _occupancyMap.FillBlock(blockId, startPosition, block.RenderableLocalCells);
                return false;
            }

            block.Position = currentPosition;
            var blockRemovedFromBoard = false;
            var layerExitedWithRemainingBlock = false;

            if (reachedDoor)
            {
                if (block.ExitActiveLayer())
                {
                    _runtimeBlocks[blockId] = block;
                    _occupancyMap.FillBlock(blockId, currentPosition, block.RenderableLocalCells);
                    layerExitedWithRemainingBlock = true;
                }
                else
                {
                    _runtimeBlocks.Remove(blockId);
                    _collisionProfileCacheByBlockId.Remove(blockId);
                    blockRemovedFromBoard = true;
                }
            }
            else
            {
                _runtimeBlocks[blockId] = block;
                _occupancyMap.FillBlock(blockId, currentPosition, block.RenderableLocalCells);
            }

            slideResult = new BoardBlockSlideResult(blockId, startPosition, currentPosition, movedCellCount,
                reachedDoor,
                blockRemovedFromBoard, layerExitedWithRemainingBlock, matchedDoor);
            return true;
        }

        private bool TryResolveStationaryDoorPull(RuntimeBlockState block, CollisionProfile collisionProfile,
            Vector2Int blockPosition,
            out DoorOpeningData doorExit)
        {
            if (TryResolveDoorPullExit(block, blockPosition, out doorExit))
            {
                return IsExitSweepClear(collisionProfile, blockPosition, doorExit.EdgeDirection);
            }

            if (_occupancyMap.IsDoorOverlapping(block.ActiveExitLocalCells, blockPosition))
            {
                return false;
            }

            return TryResolveDoorPullFromFrontCell(block, collisionProfile, blockPosition, out doorExit);
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

        private bool TryResolveDoorPullFromFrontCell(RuntimeBlockState block, CollisionProfile collisionProfile,
            Vector2Int blockPosition,
            out DoorOpeningData doorExit)
        {
            doorExit = default;
            if (!DoorExitEvaluator.TryResolveDoorPullFromFrontCell(block, blockPosition, _doorOpenings,
                    out var resolvedDoor))
            {
                return false;
            }

            var frontCellPosition = blockPosition + resolvedDoor.EdgeDirection.ToVector();
            if (!_occupancyMap.CanOccupy(frontCellPosition, block.RenderableLocalCells))
            {
                return false;
            }

            if (!IsExitSweepClear(collisionProfile, frontCellPosition, resolvedDoor.EdgeDirection))
            {
                return false;
            }

            doorExit = resolvedDoor;
            return true;
        }

        private bool IsExitSweepClear(CollisionProfile collisionProfile,
            Vector2Int startAnchorPosition, Direction exitDirection)
        {
            if (!collisionProfile.IsValid)
            {
                return false;
            }

            var directionVector = exitDirection.ToVector();
            if (directionVector == Vector2Int.zero)
            {
                return false;
            }

            var collisionCells = collisionProfile.Cells;
            var maxSteps = ResolveMaxSweepSteps(collisionProfile, startAnchorPosition, exitDirection);
            var anchorPosition = startAnchorPosition;

            for (var step = 0; step < maxSteps; step++)
            {
                var hasAnyCellInsideBoard = false;
                for (var i = 0; i < collisionCells.Length; i++)
                {
                    var worldCell = anchorPosition + collisionCells[i];
                    if (!_occupancyMap.IsInside(worldCell.x, worldCell.y))
                    {
                        continue;
                    }

                    hasAnyCellInsideBoard = true;
                    if (_occupancyMap.TryGetBlockAt(worldCell.x, worldCell.y, out _))
                    {
                        return false;
                    }
                }

                if (!hasAnyCellInsideBoard)
                {
                    return true;
                }

                anchorPosition += directionVector;
            }

            return false;
        }

        private int ResolveMaxSweepSteps(CollisionProfile collisionProfile, Vector2Int startAnchorPosition,
            Direction exitDirection)
        {
            var boardWidth = Mathf.Max(1, _occupancyMap.Width);
            var boardHeight = Mathf.Max(1, _occupancyMap.Height);

            return exitDirection switch
            {
                Direction.Right => Mathf.Max(2, (boardWidth - (startAnchorPosition.x + collisionProfile.MinX)) + 2),
                Direction.Left => Mathf.Max(2, (startAnchorPosition.x + collisionProfile.MaxX) + 2),
                Direction.Up => Mathf.Max(2, (boardHeight - (startAnchorPosition.y + collisionProfile.MinY)) + 2),
                Direction.Down => Mathf.Max(2, (startAnchorPosition.y + collisionProfile.MaxY) + 2),
                _ => 2
            };
        }

        private CollisionProfile GetOrCreateCollisionProfile(int blockId, RuntimeBlockState block)
        {
            var localCells = block.RenderableLocalCells;
            if (_collisionProfileCacheByBlockId.TryGetValue(blockId, out var cached) &&
                ReferenceEquals(cached.LocalCells, localCells))
            {
                return cached.Profile;
            }

            var rebuiltProfile = BuildCollisionProfile(localCells);
            _collisionProfileCacheByBlockId[blockId] = new CollisionProfileCacheEntry
            {
                LocalCells = localCells,
                Profile = rebuiltProfile
            };
            return rebuiltProfile;
        }

        private static CollisionProfile BuildCollisionProfile(Vector2Int[] localCells)
        {
            if (localCells == null || localCells.Length <= 0)
            {
                return default;
            }

            var occupiedLocalCells = new HashSet<Vector2Int>(localCells);
            var collisionCells = new List<Vector2Int>(occupiedLocalCells.Count);
            collisionCells.AddRange(occupiedLocalCells);
            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var minY = int.MaxValue;
            var maxY = int.MinValue;

            for (var i = 0; i < localCells.Length; i++)
            {
                var cell = localCells[i];
                if (cell.x < minX) minX = cell.x;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y < minY) minY = cell.y;
                if (cell.y > maxY) maxY = cell.y;
            }

            var floodMinX = minX - 1;
            var floodMaxX = maxX + 1;
            var floodMinY = minY - 1;
            var floodMaxY = maxY + 1;
            var reachableEmptyCells = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();

            var start = new Vector2Int(floodMinX, floodMinY);
            reachableEmptyCells.Add(start);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (var i = 0; i < FloodNeighbors.Length; i++)
                {
                    var next = current + FloodNeighbors[i];
                    if (next.x < floodMinX || next.x > floodMaxX || next.y < floodMinY || next.y > floodMaxY)
                    {
                        continue;
                    }

                    if (occupiedLocalCells.Contains(next) || !reachableEmptyCells.Add(next))
                    {
                        continue;
                    }

                    queue.Enqueue(next);
                }
            }

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (occupiedLocalCells.Contains(candidate) || reachableEmptyCells.Contains(candidate))
                    {
                        continue;
                    }

                    collisionCells.Add(candidate);
                }
            }

            return new CollisionProfile(collisionCells.ToArray(), minX, maxX, minY, maxY);
        }
    }
}
