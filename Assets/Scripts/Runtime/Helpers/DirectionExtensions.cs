using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Helpers
{
    public static class DirectionExtensions
    {
        public static Vector2Int ToVector(this Direction direction)
        {
            return direction switch
            {
                Direction.Up => Vector2Int.up,
                Direction.Down => Vector2Int.down,
                Direction.Left => Vector2Int.left,
                Direction.Right => Vector2Int.right,
                _ => Vector2Int.zero
            };
        }

        public static bool IsVertical(this Direction direction)
        {
            return direction is Direction.Up or Direction.Down;
        }

        public static bool IsHorizontal(this Direction direction)
        {
            return direction is Direction.Left or Direction.Right;
        }

        public static Vector2Int ResolveExitDirection(this DoorOpeningData opening, Vector2Int gridDimensions,
            Vector2Int fallbackDirection = default)
        {
            if (gridDimensions.x > 0 && gridDimensions.y > 0)
            {
                var maxX = gridDimensions.x - 1;
                var maxY = gridDimensions.y - 1;

                if (opening.MinCell.x <= 0)
                {
                    return Vector2Int.left;
                }

                if (opening.MaxCell.x >= maxX)
                {
                    return Vector2Int.right;
                }

                if (opening.MinCell.y <= 0)
                {
                    return Vector2Int.down;
                }

                if (opening.MaxCell.y >= maxY)
                {
                    return Vector2Int.up;
                }
            }

            return fallbackDirection != Vector2Int.zero ? fallbackDirection : opening.EdgeDirection.ToVector();
        }
    }

}
