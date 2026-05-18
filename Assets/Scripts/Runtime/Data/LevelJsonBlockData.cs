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
        public BlockShapeType blockType;
        public BlockMovementConstraint movementConstraint;
        public BlockColor colorType;

        public string ResolveShapeKey(int fallbackCellCount = 1)
        {
            var resolvedType = ResolveBlockType(fallbackCellCount);
            var resolvedShapeKey = BlockShapeTypeUtility.ToShapeKey(resolvedType);
            if (!string.IsNullOrWhiteSpace(resolvedShapeKey))
            {
                return resolvedShapeKey;
            }

            if (!string.IsNullOrWhiteSpace(shapeKey))
            {
                return shapeKey.Trim();
            }

            return BlockShapeTypeUtility.ToShapeKey(fallbackCellCount <= 1
                ? BlockShapeType.Shape1x1
                : BlockShapeType.Custom);
        }

        public BlockShapeType ResolveBlockType(int fallbackCellCount = 1)
        {
            if (blockType != BlockShapeType.Unknown)
            {
                return blockType;
            }

            if (!string.IsNullOrWhiteSpace(shapeKey))
            {
                return BlockShapeTypeUtility.FromShapeKey(shapeKey, fallbackCellCount);
            }

            return fallbackCellCount <= 1 ? BlockShapeType.Shape1x1 : BlockShapeType.Custom;
        }

        public void NormalizeBlockType()
        {
            var resolvedType = ResolveBlockType();
            blockType = resolvedType;

            var resolvedShapeKey = BlockShapeTypeUtility.ToShapeKey(resolvedType);
            shapeKey = string.IsNullOrWhiteSpace(resolvedShapeKey) ? string.Empty : resolvedShapeKey;
        }

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
            if (shapeRegistry == null)
            {
                return null;
            }

            var resolvedShapeKey = ResolveShapeKey();
            if (string.IsNullOrWhiteSpace(resolvedShapeKey))
            {
                return null;
            }

            return shapeRegistry.TryResolveShape(resolvedShapeKey, out var shape) ? shape : null;
        }
    }
}
