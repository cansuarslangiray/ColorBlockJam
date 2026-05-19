using System;
using Runtime.Domain.Enums;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Data
{
    [Serializable]
    public struct LevelBlockEntry
    {
        private const string DefaultShapeKey = "Shape_1x1";
        private static readonly Vector2Int[] FallbackLocalCells = { Vector2Int.zero };

        public Vector2Int position;
        public string shapeKey;
        public BlockShapeDefinition shapeDefinition;
        public BlockFeature blockFeatures;
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

            return string.Empty;
        }

        public string ResolvePoolKey()
        {
            var resolvedShapeKey = ResolveShapeKey();
            return string.IsNullOrWhiteSpace(resolvedShapeKey) ? DefaultShapeKey : resolvedShapeKey;
        }

        public void Normalize()
        {
            shapeKey = string.IsNullOrWhiteSpace(shapeKey) ? string.Empty : shapeKey.Trim();
            blockFeatures = blockFeatures.Sanitize();

            if (shapeDefinition != null)
            {
                shapeDefinition.Sanitize();
                shapeKey = shapeDefinition.ShapeKey;
                return;
            }

            if (string.IsNullOrWhiteSpace(shapeKey))
            {
                shapeKey = DefaultShapeKey;
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

            return shapeCatalog.ResolveShape(ResolvePoolKey());
        }
    }
}
