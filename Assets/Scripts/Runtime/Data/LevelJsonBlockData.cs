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
            if (!string.IsNullOrWhiteSpace(shapeKey))
            {
                return shapeKey.Trim();
            }

            if (blockType != BlockShapeType.Unknown && blockType != BlockShapeType.Custom)
            {
                string keyFromType = BlockShapeTypeUtility.ToShapeKey(blockType);
                if (!string.IsNullOrWhiteSpace(keyFromType))
                {
                    return keyFromType;
                }
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
                return BlockShapeTypeUtility.FromShapeKey(currentShapeKey, fallbackCellCount);
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
            if (!string.IsNullOrWhiteSpace(currentShapeKey))
            {
                blockType = BlockShapeTypeUtility.FromShapeKey(currentShapeKey);
                shapeKey = currentShapeKey;
                return;
            }

            var resolvedType = ResolveBlockType();
            if (resolvedType == BlockShapeType.Unknown || resolvedType == BlockShapeType.Custom)
            {
                resolvedType = BlockShapeType.Shape1x1;
            }

            blockType = resolvedType;
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
