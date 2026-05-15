using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Core
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
    }
}
