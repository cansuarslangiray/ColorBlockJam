using System;
using Runtime.Domain.Enums;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Data
{
    [Serializable]
    public struct LevelBlockEntry
    {
        private static readonly Vector2Int[] FallbackLocalCells = { Vector2Int.zero };

        public Vector2Int position;
        public string shapeKey;
        public BlockShapeType blockType;
        public BlockShapeDefinition shapeDefinition;
        public BlockFeature blockFeatures;
        public BlockMovementConstraint movementConstraint;
        public BlockColor colorType;

        public string ResolveShapeKey()
        {
            if (shapeDefinition != null && !string.IsNullOrWhiteSpace(shapeDefinition.ShapeKey))
            {
                return shapeDefinition.ShapeKey;
            }

            if (!string.IsNullOrWhiteSpace(shapeKey))
            {
                return shapeKey.Trim();
            }

            if (blockType != BlockShapeType.Unknown)
            {
                var resolved = BlockShapeTypeUtility.ToShapeKey(blockType);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }
            }

            return string.Empty;
        }

        public BlockShapeType ResolveBlockType(int fallbackCellCount = 1)
        {
            if (shapeDefinition != null && shapeDefinition.BlockType != BlockShapeType.Unknown)
            {
                return shapeDefinition.BlockType;
            }

            if (!string.IsNullOrWhiteSpace(shapeKey))
            {
                return BlockShapeTypeUtility.FromShapeKey(shapeKey.Trim(), fallbackCellCount);
            }

            if (blockType != BlockShapeType.Unknown)
            {
                return blockType;
            }

            return fallbackCellCount <= 1 ? BlockShapeType.Shape1x1 : BlockShapeType.Custom;
        }

        public void Normalize()
        {
            shapeKey = string.IsNullOrWhiteSpace(shapeKey) ? string.Empty : shapeKey.Trim();
            blockFeatures = blockFeatures.Sanitize();
            movementConstraint = blockFeatures.ResolveMovementConstraint(
                Enum.IsDefined(typeof(BlockMovementConstraint), movementConstraint)
                    ? movementConstraint
                    : BlockMovementConstraint.Default);

            if (shapeDefinition != null)
            {
                shapeDefinition.Sanitize();
                if (string.IsNullOrWhiteSpace(shapeKey))
                {
                    shapeKey = shapeDefinition.ShapeKey;
                }

                blockType = shapeDefinition.BlockType;
                return;
            }

            if (!string.IsNullOrWhiteSpace(shapeKey))
            {
                blockType = BlockShapeTypeUtility.FromShapeKey(shapeKey, 1);
                return;
            }

            if (blockType == BlockShapeType.Unknown)
            {
                blockType = BlockShapeType.Shape1x1;
            }

            var key = BlockShapeTypeUtility.ToShapeKey(blockType);
            if (!string.IsNullOrWhiteSpace(key))
            {
                shapeKey = key;
            }
        }

        public Vector2Int[] GetLocalCells(BlockShapeCatalog shapeCatalog)
        {
            var shape = ResolveShape(shapeCatalog);
            if (shape == null)
            {
                return FallbackLocalCells;
            }

            var localCells = shape.GetLocalCells();
            return localCells == null || localCells.Length == 0 ? FallbackLocalCells : localCells;
        }

        private BlockShapeDefinition ResolveShape(BlockShapeCatalog shapeCatalog)
        {
            if (shapeDefinition != null)
            {
                return shapeDefinition;
            }

            if (shapeCatalog == null)
            {
                return null;
            }

            return shapeCatalog.ResolveShape(ResolveShapeKey(), blockType);
        }
    }
}
