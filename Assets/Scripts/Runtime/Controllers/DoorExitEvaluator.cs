using System.Collections.Generic;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers
{
    internal static class DoorExitEvaluator
    {
        public static bool TryResolveDoorExit(RuntimeBlockState block, Vector2Int blockPosition, Direction moveDirection,
            IReadOnlyList<DoorOpeningData> doorOpenings, out DoorOpeningData doorExit)
        {
            return TryResolveDoorExitInternal(block, blockPosition, doorOpenings, true, moveDirection, out doorExit);
        }

        public static bool TryResolveDoorPullExit(RuntimeBlockState block, Vector2Int blockPosition,
            IReadOnlyList<DoorOpeningData> doorOpenings, out DoorOpeningData doorExit)
        {
            return TryResolveDoorExitInternal(block, blockPosition, doorOpenings, false, default, out doorExit);
        }

        public static bool TryResolveDoorPullFromFrontCell(RuntimeBlockState block, Vector2Int blockPosition,
            IReadOnlyList<DoorOpeningData> doorOpenings, out DoorOpeningData doorExit)
        {
            doorExit = default;

            var localCells = block.LocalCells;
            if (localCells == null || localCells.Length == 0 || doorOpenings == null || doorOpenings.Count == 0)
            {
                return false;
            }

            GetBlockBounds(localCells, blockPosition, out var minX, out var maxX, out var minY, out var maxY);

            for (var i = 0; i < doorOpenings.Count; i++)
            {
                var opening = doorOpenings[i];
                if (opening.ColorType != block.ColorType)
                {
                    continue;
                }

                var approachOffset = opening.EdgeDirection.ToVector();
                var shiftedMinX = minX + approachOffset.x;
                var shiftedMaxX = maxX + approachOffset.x;
                var shiftedMinY = minY + approachOffset.y;
                var shiftedMaxY = maxY + approachOffset.y;

                if (!IsBlockTouchingDoorEdge(opening, opening.EdgeDirection, shiftedMinX, shiftedMaxX, shiftedMinY,
                        shiftedMaxY))
                {
                    continue;
                }

                if (!IsBlockInsideDoorSpan(opening, opening.EdgeDirection, shiftedMinX, shiftedMaxX, shiftedMinY,
                        shiftedMaxY))
                {
                    continue;
                }

                if (!FitsInsideDoor(opening, opening.EdgeDirection, shiftedMinX, shiftedMaxX, shiftedMinY, shiftedMaxY))
                {
                    continue;
                }

                doorExit = opening;
                return true;
            }

            return false;
        }

        private static bool TryResolveDoorExitInternal(RuntimeBlockState block, Vector2Int blockPosition,
            IReadOnlyList<DoorOpeningData> doorOpenings, bool requireMoveDirectionMatch, Direction moveDirection,
            out DoorOpeningData doorExit)
        {
            doorExit = default;

            var localCells = block.LocalCells;
            if (localCells == null || localCells.Length == 0 || doorOpenings == null || doorOpenings.Count == 0)
            {
                return false;
            }

            GetBlockBounds(localCells, blockPosition, out var minX, out var maxX, out var minY, out var maxY);

            for (var i = 0; i < doorOpenings.Count; i++)
            {
                var opening = doorOpenings[i];
                var edgeDirection = opening.EdgeDirection;
                if (opening.ColorType != block.ColorType)
                {
                    continue;
                }

                if (requireMoveDirectionMatch && edgeDirection != moveDirection)
                {
                    continue;
                }

                if (!IsBlockTouchingDoorEdge(opening, edgeDirection, minX, maxX, minY, maxY))
                {
                    continue;
                }

                if (!IsBlockInsideDoorSpan(opening, edgeDirection, minX, maxX, minY, maxY))
                {
                    continue;
                }

                if (!FitsInsideDoor(opening, edgeDirection, minX, maxX, minY, maxY))
                {
                    continue;
                }

                doorExit = opening;
                return true;
            }

            return false;
        }

        private static bool FitsInsideDoor(DoorOpeningData opening, Direction edgeDirection, int minX, int maxX,
            int minY, int maxY)
        {
            var widthOnDoorAxis = edgeDirection.IsHorizontal() ? (maxY - minY) + 1 : (maxX - minX) + 1;
            return widthOnDoorAxis <= opening.OpeningWidth;
        }

        private static void GetBlockBounds(Vector2Int[] localCells, Vector2Int blockPosition, out int minX,
            out int maxX, out int minY, out int maxY)
        {
            minX = int.MaxValue;
            maxX = int.MinValue;
            minY = int.MaxValue;
            maxY = int.MinValue;

            for (var i = 0; i < localCells.Length; i++)
            {
                var worldCell = blockPosition + localCells[i];
                if (worldCell.x < minX) minX = worldCell.x;
                if (worldCell.x > maxX) maxX = worldCell.x;
                if (worldCell.y < minY) minY = worldCell.y;
                if (worldCell.y > maxY) maxY = worldCell.y;
            }
        }

        private static bool IsBlockTouchingDoorEdge(DoorOpeningData opening, Direction edgeDirection, int minX,
            int maxX, int minY, int maxY)
        {
            return edgeDirection switch
            {
                Direction.Left => minX == opening.MinCell.x,
                Direction.Right => maxX == opening.MaxCell.x,
                Direction.Down => minY == opening.MinCell.y,
                Direction.Up => maxY == opening.MaxCell.y,
                _ => false
            };
        }

        private static bool IsBlockInsideDoorSpan(DoorOpeningData opening, Direction edgeDirection, int minX,
            int maxX, int minY, int maxY)
        {
            if (edgeDirection.IsHorizontal())
            {
                return minY >= opening.MinCell.y && maxY <= opening.MaxCell.y;
            }

            return minX >= opening.MinCell.x && maxX <= opening.MaxCell.x;
        }
    }
}
