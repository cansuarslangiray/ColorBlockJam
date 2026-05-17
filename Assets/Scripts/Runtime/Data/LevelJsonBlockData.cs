using System;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Data
{
    [Serializable]
    public struct LevelJsonBlockData
    {
        private static readonly Vector2Int[] DefaultCells = { Vector2Int.zero };

        public Vector2Int position;
        public string shapeKey;
        public BlockMovementConstraint movementConstraint;
        public BlockColor colorType;

        public Vector2Int[] GetLocalCells(BlockShapeRegistry shapeRegistry)
        {
            var shape = ResolveShape(shapeRegistry);
            if (shape == null)
            {
                return DefaultCells;
            }

            var localCells = shape.GetLocalCells();
            if (localCells == null || localCells.Length == 0)
            {
                return DefaultCells;
            }

            return localCells;
        }

        public Vector2Int GetSize(BlockShapeRegistry shapeRegistry)
        {
            var shape = ResolveShape(shapeRegistry);
            return shape == null ? Vector2Int.one : shape.Size;
        }

        private BlockShapeJsonData ResolveShape(BlockShapeRegistry shapeRegistry)
        {
            if (shapeRegistry == null || string.IsNullOrWhiteSpace(shapeKey))
            {
                return null;
            }

            return shapeRegistry.TryResolveShape(shapeKey.Trim(), out var shape) ? shape : null;
        }
    }
}
