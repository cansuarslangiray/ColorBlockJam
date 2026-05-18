using System;
using Runtime.Domain.Enums;
using Runtime.Helpers;
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
        public BlockFeature blockFeatures;
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
            if (!string.IsNullOrWhiteSpace(shapeKey))
            {
                string currentShapeKey = shapeKey.Trim();
                BlockShapeType typeFromShapeKey = BlockShapeTypeUtility.FromShapeKey(currentShapeKey, fallbackCellCount);

                if (blockType == BlockShapeType.Unknown)
                {
                    return typeFromShapeKey;
                }

                string shapeKeyFromBlockType = BlockShapeTypeUtility.ToShapeKey(blockType);
                if (string.IsNullOrWhiteSpace(shapeKeyFromBlockType) ||
                    !string.Equals(shapeKeyFromBlockType, currentShapeKey, StringComparison.Ordinal))
                {
                    return typeFromShapeKey;
                }

                return blockType;
            }

            if (blockType != BlockShapeType.Unknown)
            {
                return blockType;
            }

            return fallbackCellCount <= 1 ? BlockShapeType.Shape1x1 : BlockShapeType.Custom;
        }

        public void NormalizeBlockType()
        {
            var currentShapeKey = string.IsNullOrWhiteSpace(shapeKey) ? string.Empty : shapeKey.Trim();
            var resolvedType = ResolveBlockType();
            blockType = resolvedType;

            if (resolvedType == BlockShapeType.Custom)
            {
                shapeKey = currentShapeKey;
                return;
            }

            var resolvedShapeKey = BlockShapeTypeUtility.ToShapeKey(resolvedType);
            shapeKey = string.IsNullOrWhiteSpace(resolvedShapeKey) ? currentShapeKey : resolvedShapeKey;
        }

        public void NormalizeMovementConstraint()
        {
            if (!Enum.IsDefined(typeof(BlockMovementConstraint), movementConstraint))
            {
                movementConstraint = BlockMovementConstraint.Default;
            }

            blockFeatures = blockFeatures.Sanitize();
            movementConstraint = blockFeatures.ResolveMovementConstraint(BlockMovementConstraint.Default);
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
