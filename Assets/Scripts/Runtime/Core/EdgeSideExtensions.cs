using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Core
{
    public static class EdgeSideExtensions
    {
        public static bool IsVertical(this EdgeSide edgeSide)
        {
            return edgeSide is EdgeSide.Left or EdgeSide.Right;
        }

        public static bool IsHorizontal(this EdgeSide edgeSide)
        {
            return edgeSide is EdgeSide.Bottom or EdgeSide.Top;
        }

        public static bool IsToward(this EdgeSide edgeSide, Vector2Int moveVector)
        {
            return edgeSide switch
            {
                EdgeSide.Left => moveVector.x < 0,
                EdgeSide.Right => moveVector.x > 0,
                EdgeSide.Bottom => moveVector.y < 0,
                EdgeSide.Top => moveVector.y > 0,
                _ => false
            };
        }

        public static bool TryGetNormal(this EdgeSide edgeSide, out Vector2Int normal)
        {
            normal = edgeSide switch
            {
                EdgeSide.Left => Vector2Int.left,
                EdgeSide.Right => Vector2Int.right,
                EdgeSide.Bottom => Vector2Int.down,
                EdgeSide.Top => Vector2Int.up,
                _ => Vector2Int.zero
            };
            return normal != Vector2Int.zero;
        }
    }
}
