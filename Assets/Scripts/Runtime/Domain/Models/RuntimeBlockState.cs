using System;
using System.Collections.Generic;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Domain.Models
{
    public sealed class RuntimeBlockState
    {
        private RuntimeBlockLayerState[] _layers;
        private int _activeLayerCursor;
        private Vector2Int[] _renderableCellsCache = Array.Empty<Vector2Int>();
        private bool _renderableCellsCacheDirty = true;

        public int Id { get; }
        public string PoolKey { get; }
        public Vector2Int Position;
        public BlockFeature BlockFeatures { get; }
        public int MaxMovesBeforeExit { get; }
        public int MinClearedBlocksBeforeExit { get; }

        public IReadOnlyList<RuntimeBlockLayerState> Layers => _layers;
        public bool HasRemainingLayers => _activeLayerCursor < _layers.Length;

        public RuntimeBlockLayerState ActiveExitLayer => HasRemainingLayers
            ? _layers[_activeLayerCursor]
            : default;

        public Vector2Int[] ActiveExitLocalCells => ActiveExitLayer.LocalCells;
        public BlockColor ActiveExitColorType => ActiveExitLayer.ColorType;
        public Vector2Int[] RenderableLocalCells => GetRenderableLocalCells();
        public BlockColor ColorType => ResolvePrimaryVisibleColor();

        public RuntimeBlockState(int id, string poolKey, Vector2Int position, IReadOnlyList<RuntimeBlockLayerState> layers,
            BlockFeature blockFeatures, int maxMovesBeforeExit, int minClearedBlocksBeforeExit)
        {
            Id = id;
            PoolKey = string.IsNullOrWhiteSpace(poolKey) ? "Shape_1x1" : poolKey.Trim();
            Position = position;
            BlockFeatures = blockFeatures;
            MaxMovesBeforeExit = maxMovesBeforeExit;
            MinClearedBlocksBeforeExit = minClearedBlocksBeforeExit;

            _layers = ResolveLayerArray(layers);
            _activeLayerCursor = 0;
            _renderableCellsCacheDirty = true;
        }

        public bool TryResolveVisibleColor(Vector2Int localCell, out BlockColor resolvedColor)
        {
            resolvedColor = default;
            if (!HasRemainingLayers)
            {
                return false;
            }

            var found = false;
            var highestRenderPriority = int.MinValue;
            var highestExitOrder = int.MinValue;
            for (var i = _activeLayerCursor; i < _layers.Length; i++)
            {
                var layer = _layers[i];
                if (!layer.ContainsLocalCell(localCell))
                {
                    continue;
                }

                var renderPriority = ResolveRenderPriority(layer.LayerRole);
                if (!found ||
                    renderPriority > highestRenderPriority ||
                    (renderPriority == highestRenderPriority && layer.ExitOrder >= highestExitOrder))
                {
                    resolvedColor = layer.ColorType;
                    highestRenderPriority = renderPriority;
                    highestExitOrder = layer.ExitOrder;
                }

                found = true;
            }

            return found;
        }

        public bool TryResolveRemainingLayerColor(ShapeLayerRole layerRole, out BlockColor resolvedColor)
        {
            resolvedColor = default;
            if (!HasRemainingLayers)
            {
                return false;
            }

            for (var i = _activeLayerCursor; i < _layers.Length; i++)
            {
                var layer = _layers[i];
                if (layer.LayerRole != layerRole)
                {
                    continue;
                }

                resolvedColor = layer.ColorType;
                return true;
            }

            return false;
        }

        public Vector2Int[] GetRenderableLocalCells()
        {
            if (!_renderableCellsCacheDirty)
            {
                return _renderableCellsCache;
            }

            if (!HasRemainingLayers)
            {
                _renderableCellsCache = Array.Empty<Vector2Int>();
                _renderableCellsCacheDirty = false;
                return _renderableCellsCache;
            }

            var uniqueCells = new HashSet<Vector2Int>();
            for (var layerIndex = _activeLayerCursor; layerIndex < _layers.Length; layerIndex++)
            {
                var localCells = _layers[layerIndex].LocalCells;
                if (localCells == null)
                {
                    continue;
                }

                for (var cellIndex = 0; cellIndex < localCells.Length; cellIndex++)
                {
                    uniqueCells.Add(localCells[cellIndex]);
                }
            }

            var sortedCells = new List<Vector2Int>(uniqueCells);
            sortedCells.Sort((left, right) =>
            {
                var yCompare = left.y.CompareTo(right.y);
                return yCompare != 0 ? yCompare : left.x.CompareTo(right.x);
            });

            _renderableCellsCache = sortedCells.ToArray();
            _renderableCellsCacheDirty = false;
            return _renderableCellsCache;
        }

        public bool ExitActiveLayer()
        {
            if (!HasRemainingLayers)
            {
                return false;
            }

            _activeLayerCursor++;
            _renderableCellsCacheDirty = true;
            return HasRemainingLayers;
        }

        private BlockColor ResolvePrimaryVisibleColor()
        {
            var cells = GetRenderableLocalCells();
            if (cells.Length > 0 && TryResolveVisibleColor(cells[0], out var colorType))
            {
                return colorType;
            }

            return HasRemainingLayers ? ActiveExitLayer.ColorType : BlockColor.Red;
        }

        private static RuntimeBlockLayerState[] ResolveLayerArray(IReadOnlyList<RuntimeBlockLayerState> layers)
        {
            if (layers == null || layers.Count == 0)
            {
                return new[]
                {
                    new RuntimeBlockLayerState("Fallback", ShapeLayerRole.Primary, new[] { Vector2Int.zero },
                        BlockColor.Red, 0)
                };
            }

            var resolved = new RuntimeBlockLayerState[layers.Count];
            for (var i = 0; i < layers.Count; i++)
            {
                resolved[i] = layers[i];
            }

            Array.Sort(resolved, CompareLayersByExitOrder);
            return resolved;
        }

        private static int CompareLayersByExitOrder(RuntimeBlockLayerState left, RuntimeBlockLayerState right)
        {
            var orderCompare = left.ExitOrder.CompareTo(right.ExitOrder);
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            return left.LayerRole.CompareTo(right.LayerRole);
        }

        private static int ResolveRenderPriority(ShapeLayerRole layerRole)
        {
            return layerRole switch
            {
                ShapeLayerRole.Inner => 3,
                ShapeLayerRole.Outer => 2,
                _ => 1
            };
        }
    }
}
