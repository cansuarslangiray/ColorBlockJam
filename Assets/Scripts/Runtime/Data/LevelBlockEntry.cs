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

        public Vector2Int position;
        public string shapeKey;
        public BlockShapeDefinition shapeDefinition;
        public BlockFeature blockFeatures;
        public BlockColor colorType;
        public int maxMovesBeforeExit;
        public int minClearedBlocksBeforeExit;

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
            maxMovesBeforeExit = ResolveMaxMovesBeforeExitLimit();
            minClearedBlocksBeforeExit = ResolveMinClearedBlocksBeforeExitRequirement();

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
            if (shapeDefinition != null)
            {
                return shapeDefinition.GetLocalCells();
            }

            var resolvedShape = shapeCatalog.ResolveShape(ResolvePoolKey());
            return resolvedShape != null ? resolvedShape.GetLocalCells() : Array.Empty<Vector2Int>();
        }

        public int ResolveMaxMovesBeforeExitLimit()
        {
            if (!blockFeatures.HasFeature(BlockFeature.MaxMovesBeforeExit))
            {
                return 0;
            }

            return Mathf.Max(1, maxMovesBeforeExit);
        }

        public int ResolveMinClearedBlocksBeforeExitRequirement()
        {
            if (!blockFeatures.HasFeature(BlockFeature.MinClearedBlocksBeforeExit))
            {
                return 0;
            }

            return Mathf.Max(1, minClearedBlocksBeforeExit);
        }
    }
}
