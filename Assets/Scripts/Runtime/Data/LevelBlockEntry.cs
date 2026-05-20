using System;
using System.Collections.Generic;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Data
{
    [Serializable]
    public struct LevelBlockEntry
    {
        private const string DefaultShapeKey = "Shape_1x1";
        private const string PrimaryLayerId = "PrimaryShape";
        private const string OuterLayerId = "OuterShape";
        private const string InnerLayerId = "InnerShape";

        public Vector2Int position;
        public string shapeKey;
        public BlockShapeDefinition shapeDefinition;
        public string innerShapeKey;
        public BlockShapeDefinition innerShapeDefinition;
        public BlockColor innerColorType;
        public NestedShapeExitOrder nestedExitOrder;
        public NestedShapeContainmentRule nestedContainmentRule;
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

        public string ResolveInnerShapeKey()
        {
            if (blockFeatures.HasFeature(BlockFeature.NestedShape))
            {
                var resolvedOuterShapeKey = ResolveShapeKey();
                return string.IsNullOrWhiteSpace(resolvedOuterShapeKey) ? DefaultShapeKey : resolvedOuterShapeKey;
            }

            if (innerShapeDefinition != null && !string.IsNullOrWhiteSpace(innerShapeDefinition.ShapeKey))
            {
                return innerShapeDefinition.ShapeKey;
            }

            return string.IsNullOrWhiteSpace(innerShapeKey) ? string.Empty : innerShapeKey.Trim();
        }

        public void Normalize()
        {
            shapeKey = string.IsNullOrWhiteSpace(shapeKey) ? string.Empty : shapeKey.Trim();
            innerShapeKey = string.IsNullOrWhiteSpace(innerShapeKey) ? string.Empty : innerShapeKey.Trim();
            blockFeatures = blockFeatures.Sanitize();
            nestedExitOrder = SanitizeNestedExitOrder(nestedExitOrder);
            nestedContainmentRule = SanitizeNestedContainmentRule(nestedContainmentRule);
            maxMovesBeforeExit = ResolveMaxMovesBeforeExitLimit();
            minClearedBlocksBeforeExit = ResolveMinClearedBlocksBeforeExitRequirement();

            if (shapeDefinition != null)
            {
                shapeDefinition.Sanitize();
                shapeKey = shapeDefinition.ShapeKey;
            }
            else if (string.IsNullOrWhiteSpace(shapeKey))
            {
                shapeKey = DefaultShapeKey;
            }

            if (innerShapeDefinition != null)
            {
                innerShapeDefinition.Sanitize();
                innerShapeKey = innerShapeDefinition.ShapeKey;
            }

            if (!blockFeatures.HasFeature(BlockFeature.NestedShape))
            {
                return;
            }

            innerShapeDefinition = shapeDefinition;
            innerShapeKey = ResolveShapeKey();
            nestedExitOrder = SanitizeNestedExitOrder(nestedExitOrder);
            nestedContainmentRule = SanitizeNestedContainmentRule(nestedContainmentRule);
        }

        public bool TryResolveLayers(BlockShapeCatalog shapeCatalog, List<RuntimeBlockLayerState> result,
            out string validationError)
        {
            result ??= new List<RuntimeBlockLayerState>(2);
            result.Clear();
            validationError = string.Empty;

            if (blockFeatures.HasFeature(BlockFeature.NestedShape))
            {
                return TryResolveNestedLayers(shapeCatalog, result, out validationError);
            }

            var resolvedShape = ResolveShape(shapeDefinition, ResolveShapeKey(), shapeCatalog);

            if (resolvedShape == null)
            {
                validationError = "Shape definition could not be resolved.";
                return false;
            }

            var localCells = resolvedShape.GetLocalCells();
            if (localCells == null || localCells.Length == 0)
            {
                validationError = "Shape local cells are empty.";
                return false;
            }

            result.Add(new RuntimeBlockLayerState(PrimaryLayerId, ShapeLayerRole.Primary, localCells, colorType, 0));
            return true;
        }

        public Vector2Int[] GetLocalCells(BlockShapeCatalog shapeCatalog)
        {
            var layers = new List<RuntimeBlockLayerState>(2);
            if (!TryResolveLayers(shapeCatalog, layers, out _))
            {
                return Array.Empty<Vector2Int>();
            }

            return BuildOccupancyCells(layers);
        }

        public bool TryResolveCellColor(BlockShapeCatalog shapeCatalog, Vector2Int localCell, out BlockColor resolvedColor)
        {
            resolvedColor = colorType;
            var layers = new List<RuntimeBlockLayerState>(2);
            if (!TryResolveLayers(shapeCatalog, layers, out _) || layers.Count == 0)
            {
                return false;
            }

            var found = false;
            var highestRenderPriority = int.MinValue;
            var highestOrder = int.MinValue;
            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (!layer.ContainsLocalCell(localCell))
                {
                    continue;
                }

                var renderPriority = ResolveLayerRenderPriority(layer.LayerRole);
                if (!found ||
                    renderPriority > highestRenderPriority ||
                    (renderPriority == highestRenderPriority && layer.ExitOrder >= highestOrder))
                {
                    resolvedColor = layer.ColorType;
                    highestRenderPriority = renderPriority;
                    highestOrder = layer.ExitOrder;
                }

                found = true;
            }

            return found;
        }

        public bool UsesColor(BlockShapeCatalog shapeCatalog, BlockColor color)
        {
            var layers = new List<RuntimeBlockLayerState>(2);
            if (!TryResolveLayers(shapeCatalog, layers, out _))
            {
                return colorType == color;
            }

            for (var i = 0; i < layers.Count; i++)
            {
                if (layers[i].ColorType == color)
                {
                    return true;
                }
            }

            return false;
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

        private bool TryResolveNestedLayers(BlockShapeCatalog shapeCatalog, List<RuntimeBlockLayerState> result,
            out string validationError)
        {
            validationError = string.Empty;
            result ??= new List<RuntimeBlockLayerState>(2);
            result.Clear();

            var resolvedOuter = ResolveShape(shapeDefinition, ResolveShapeKey(), shapeCatalog);
            if (resolvedOuter == null)
            {
                validationError = "Outer shape reference is missing.";
                return false;
            }

            var outerCells = resolvedOuter.GetLocalCells();
            if (outerCells == null || outerCells.Length == 0)
            {
                validationError = "Outer shape cell data is empty.";
                return false;
            }

            var innerCells = outerCells;
            ResolveLayerExitOrder(out var outerExitOrder, out var innerExitOrder);
            result.Add(new RuntimeBlockLayerState(OuterLayerId, ShapeLayerRole.Outer, outerCells, colorType,
                outerExitOrder));
            result.Add(new RuntimeBlockLayerState(InnerLayerId, ShapeLayerRole.Inner, innerCells, innerColorType,
                innerExitOrder));
            return true;
        }

        private static BlockShapeDefinition ResolveShape(BlockShapeDefinition preferredShape, string fallbackShapeKey,
            BlockShapeCatalog shapeCatalog)
        {
            if (preferredShape != null)
            {
                return preferredShape;
            }

            if (shapeCatalog != null && !string.IsNullOrWhiteSpace(fallbackShapeKey))
            {
                return shapeCatalog.ResolveShape(fallbackShapeKey);
            }

            return null;
        }

        private static void ResolveLayerExitOrder(out int outerExitOrder, out int innerExitOrder)
        {
            outerExitOrder = 0;
            innerExitOrder = 1;
        }

        private static int ResolveLayerRenderPriority(ShapeLayerRole layerRole)
        {
            return layerRole switch
            {
                ShapeLayerRole.Inner => 3,
                ShapeLayerRole.Outer => 2,
                _ => 1
            };
        }

        private static NestedShapeExitOrder SanitizeNestedExitOrder(NestedShapeExitOrder exitOrder)
        {
            return NestedShapeExitOrder.OuterFirst;
        }

        private static NestedShapeContainmentRule SanitizeNestedContainmentRule(
            NestedShapeContainmentRule containmentRule)
        {
            return NestedShapeContainmentRule.AllowInnerOutsideOuter;
        }

        private static Vector2Int[] BuildOccupancyCells(IReadOnlyList<RuntimeBlockLayerState> layers)
        {
            if (layers == null || layers.Count == 0)
            {
                return Array.Empty<Vector2Int>();
            }

            var uniqueCells = new HashSet<Vector2Int>();
            for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                var cells = layers[layerIndex].LocalCells;
                if (cells == null)
                {
                    continue;
                }

                for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                {
                    uniqueCells.Add(cells[cellIndex]);
                }
            }

            var result = new List<Vector2Int>(uniqueCells);
            result.Sort((left, right) =>
            {
                var yCompare = left.y.CompareTo(right.y);
                return yCompare != 0 ? yCompare : left.x.CompareTo(right.x);
            });

            return result.ToArray();
        }
    }
}
