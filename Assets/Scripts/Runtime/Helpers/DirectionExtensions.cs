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

    public static class BlockColorPalette
    {
        public static Color GetColor(BlockColor color)
        {
            return color switch
            {
                BlockColor.Red => new Color(0.9f, 0.25f, 0.25f),
                BlockColor.Blue => new Color(0.2f, 0.45f, 0.95f),
                BlockColor.Green => new Color(0.2f, 0.78f, 0.35f),
                BlockColor.Yellow => new Color(0.95f, 0.82f, 0.2f),
                BlockColor.Purple => new Color(0.62f, 0.32f, 0.88f),
                BlockColor.Orange => new Color(0.98f, 0.56f, 0.18f),
                BlockColor.Cyan => new Color(0.15f, 0.82f, 0.9f),
                BlockColor.Pink => new Color(0.98f, 0.42f, 0.67f),
                BlockColor.Mint => new Color(0.42f, 0.92f, 0.72f),
                BlockColor.Indigo => new Color(0.34f, 0.34f, 0.9f),
                BlockColor.Coral => new Color(0.96f, 0.5f, 0.44f),
                BlockColor.Lime => new Color(0.72f, 0.9f, 0.24f),
                _ => Color.white
            };
        }
    }
}
