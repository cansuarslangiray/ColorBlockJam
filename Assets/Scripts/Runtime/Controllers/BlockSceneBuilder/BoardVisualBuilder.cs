using System;
using System.Collections.Generic;
using Runtime.Core;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class BoardVisualBuilder
    {
        public struct BuildRequest
        {
            public IReadOnlyDictionary<Vector2Int, GameObject> GridCellPoolByCell;
            public IReadOnlyList<GameObject> BorderObjects;
            public GameObject BackdropObject;
            public IReadOnlyList<GameObject> DoorPool;
            public IReadOnlyList<DoorOpeningData> Openings;
            public Vector2Int GridDimensions;
            public LayoutMetrics Layout;
            public float BoardBackdropZOffset;
            public float DoorInsetInCells;
            public Action<GameObject, bool> SetActiveIfChanged;
            public Action<Transform, Vector3, Vector3> ApplyWorldTransform;
            public Func<int, GameObject, Transform> ResolveDoorPlacementTransform;
            public Action<int> SyncDoorAnimatorState;
            public Func<BlockColor, Material> ResolveMaterial;
            public Action<int, Material> ApplyDoorMaterialAtIndex;
            public Action<IReadOnlyList<DoorOpeningData>> CacheActiveDoorOpenings;
        }

        public void ApplyBoardVisuals(in BuildRequest request)
        {
            var gridCellPoolByCell = request.GridCellPoolByCell;
            var gridDimensions = request.GridDimensions;
            if (gridCellPoolByCell != null)
            {
                foreach (var pair in gridCellPoolByCell)
                {
                    var cell = pair.Key;
                    var cellObject = pair.Value;
                    var isInsideLevel = IsPlayableGridCell(cell, gridDimensions);
                    request.SetActiveIfChanged?.Invoke(cellObject, isInsideLevel);
                    if (!isInsideLevel)
                    {
                        continue;
                    }

                    var position = ResolveCellCenterWorld(request.Layout, cell.x, cell.y, request.Layout.GridZ);
                    if (cellObject)
                    {
                        cellObject.transform.position = position;
                    }
                }
            }

            ApplyBackdrop(request);
            ApplyBorders(request);
            ApplyDoors(request);
        }

        private static bool IsPlayableGridCell(Vector2Int cell, Vector2Int gridDimensions)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= gridDimensions.x || cell.y >= gridDimensions.y)
            {
                return false;
            }

            if (gridDimensions.x < 3 || gridDimensions.y < 3)
            {
                return true;
            }

            return !BoardFrameMap.IsFrameCell(cell, gridDimensions);
        }

        private static void ApplyBackdrop(in BuildRequest request)
        {
            var backdropObject = request.BackdropObject;
            var dims = request.GridDimensions;
            var layout = request.Layout;
            if (!backdropObject)
            {
                return;
            }

            if (dims.x <= 0 || dims.y <= 0 || layout.CellSize <= 0f)
            {
                request.SetActiveIfChanged?.Invoke(backdropObject, false);
                return;
            }

            ResolveVisualBounds(dims, out var minCellX, out var minCellY, out var maxCellX, out var maxCellY);
            var widthInCells = (maxCellX - minCellX) + 1;
            var heightInCells = (maxCellY - minCellY) + 1;
            var width = widthInCells * layout.CellSize;
            var height = heightInCells * layout.CellSize;
            var depth = Mathf.Max(0.02f, layout.CellSize * 0.08f);

            request.SetActiveIfChanged?.Invoke(backdropObject, true);
            var position = new Vector3(
                layout.BoardOrigin.x + ((minCellX + (widthInCells * 0.5f)) * layout.CellSize),
                layout.BoardOrigin.y + ((minCellY + (heightInCells * 0.5f)) * layout.CellSize),
                layout.GridZ + Mathf.Abs(request.BoardBackdropZOffset));
            var scale = new Vector3(width, height, depth);
            if (backdropObject)
            {
                request.ApplyWorldTransform?.Invoke(backdropObject.transform, position, scale);
            }
        }

        private static void ApplyBorders(in BuildRequest request)
        {
            var borderObjects = request.BorderObjects;
            var dims = request.GridDimensions;
            var layout = request.Layout;
            if (borderObjects == null || borderObjects.Count == 0)
            {
                return;
            }

            if (dims.x <= 0 || dims.y <= 0 || layout.CellSize <= 0f)
            {
                return;
            }

            ResolveVisualBounds(dims, out var minCellX, out var minCellY, out var maxCellX, out var maxCellY);
            var widthInCells = (maxCellX - minCellX) + 1;
            var heightInCells = (maxCellY - minCellY) + 1;
            var width = widthInCells * layout.CellSize;
            var height = heightInCells * layout.CellSize;
            var horizontalLength = width + (layout.FrameThickness * 2f);
            var verticalLength = height + (layout.FrameThickness * 2f);

            var leftEdgeX = layout.BoardOrigin.x + (minCellX * layout.CellSize);
            var rightEdgeX = layout.BoardOrigin.x + ((maxCellX + 1) * layout.CellSize);
            var bottomEdgeY = layout.BoardOrigin.y + (minCellY * layout.CellSize);
            var topEdgeY = layout.BoardOrigin.y + ((maxCellY + 1) * layout.CellSize);

            var topY = topEdgeY + (layout.FrameThickness * 0.5f);
            var bottomY = bottomEdgeY - (layout.FrameThickness * 0.5f);
            var leftX = leftEdgeX - (layout.FrameThickness * 0.5f);
            var rightX = rightEdgeX + (layout.FrameThickness * 0.5f);

            var centerX = (leftEdgeX + rightEdgeX) * 0.5f;
            var centerY = (bottomEdgeY + topEdgeY) * 0.5f;

            ApplyBorderAtIndex(borderObjects, (int)Direction.Up, new Vector3(centerX, topY, layout.BorderZ),
                new Vector3(horizontalLength, layout.FrameThickness, layout.FrameDepth), request.SetActiveIfChanged,
                request.ApplyWorldTransform);

            ApplyBorderAtIndex(borderObjects, (int)Direction.Down, new Vector3(centerX, bottomY, layout.BorderZ),
                new Vector3(horizontalLength, layout.FrameThickness, layout.FrameDepth), request.SetActiveIfChanged,
                request.ApplyWorldTransform);

            ApplyBorderAtIndex(borderObjects, (int)Direction.Left, new Vector3(leftX, centerY, layout.BorderZ),
                new Vector3(layout.FrameThickness, verticalLength, layout.FrameDepth), request.SetActiveIfChanged,
                request.ApplyWorldTransform);

            ApplyBorderAtIndex(borderObjects, (int)Direction.Right, new Vector3(rightX, centerY, layout.BorderZ),
                new Vector3(layout.FrameThickness, verticalLength, layout.FrameDepth), request.SetActiveIfChanged,
                request.ApplyWorldTransform);

            for (var i = (int)Direction.Right + 1; i < borderObjects.Count; i++)
            {
                var borderObject = borderObjects[i];
                if (borderObject)
                {
                    request.SetActiveIfChanged?.Invoke(borderObject, false);
                }
            }
        }

        private static void ResolveVisualBounds(Vector2Int gridDimensions, out int minCellX, out int minCellY,
            out int maxCellX, out int maxCellY)
        {
            if (gridDimensions.x <= 0 || gridDimensions.y <= 0)
            {
                minCellX = 0;
                minCellY = 0;
                maxCellX = 0;
                maxCellY = 0;
                return;
            }

            if (gridDimensions.x >= 3 && gridDimensions.y >= 3)
            {
                minCellX = 1;
                minCellY = 1;
                maxCellX = gridDimensions.x - 2;
                maxCellY = gridDimensions.y - 2;
                return;
            }

            minCellX = 0;
            minCellY = 0;
            maxCellX = gridDimensions.x - 1;
            maxCellY = gridDimensions.y - 1;
        }

        private static void ApplyBorderAtIndex(IReadOnlyList<GameObject> borderObjects, int borderIndex,
            Vector3 position, Vector3 scale, Action<GameObject, bool> setActiveIfChanged,
            Action<Transform, Vector3, Vector3> applyWorldTransform)
        {
            if (borderObjects == null || borderIndex < 0 || borderIndex >= borderObjects.Count)
            {
                return;
            }

            var borderObject = borderObjects[borderIndex];
            if (!borderObject)
            {
                return;
            }

            setActiveIfChanged?.Invoke(borderObject, true);
            applyWorldTransform?.Invoke(borderObject.transform, position, scale);
        }

        private static void ApplyDoors(in BuildRequest request)
        {
            var doorPool = request.DoorPool;
            var openings = request.Openings;
            var gridDimensions = request.GridDimensions;
            var layout = request.Layout;
            var doorCount = doorPool?.Count ?? 0;
            var requiredCount = openings?.Count ?? 0;
            var activeDoorCount = Mathf.Min(requiredCount, doorCount);

            request.CacheActiveDoorOpenings?.Invoke(openings);

            var cellSize = layout.CellSize;
            var doorOffset = (0.5f * cellSize) + (layout.FrameThickness * 0.5f) -
                             (request.DoorInsetInCells * cellSize);

            for (var i = 0; i < doorCount; i++)
            {
                var doorVisual = doorPool[i];
                var isActive = i < activeDoorCount;
                request.SetActiveIfChanged?.Invoke(doorVisual, isActive);
                if (!isActive || !doorVisual)
                {
                    continue;
                }

                var opening = openings[i];
                var normal = opening.EdgeDirection.ToVector();

                var mappedMinX = MapLogicalToVisualCellIndex(opening.MinCell.x, gridDimensions.x);
                var mappedMaxX = MapLogicalToVisualCellIndex(opening.MaxCell.x, gridDimensions.x);
                var mappedMinY = MapLogicalToVisualCellIndex(opening.MinCell.y, gridDimensions.y);
                var mappedMaxY = MapLogicalToVisualCellIndex(opening.MaxCell.y, gridDimensions.y);

                var openingWidth = opening.EdgeDirection.IsVertical()
                    ? Mathf.Max(1, (mappedMaxX - mappedMinX) + 1)
                    : Mathf.Max(1, (mappedMaxY - mappedMinY) + 1);
                var span = Mathf.Max(0.01f, (openingWidth * cellSize));
                var centerX = (mappedMinX + mappedMaxX + 1) * 0.5f;
                var centerY = (mappedMinY + mappedMaxY + 1) * 0.5f;

                var cellCenter = new Vector2(layout.BoardOrigin.x + (centerX * cellSize),
                    layout.BoardOrigin.y + (centerY * cellSize));
                var position = new Vector3(cellCenter.x + (normal.x * doorOffset),
                    cellCenter.y + (normal.y * doorOffset), layout.DoorZ);
                var isHorizontal = opening.EdgeDirection.IsVertical();
                var scale = isHorizontal
                    ? new Vector3(span, layout.FrameThickness, layout.DoorDepth)
                    : new Vector3(layout.FrameThickness, span, layout.DoorDepth);
                var placementTransform = request.ResolveDoorPlacementTransform?.Invoke(i, doorVisual);
                if (placementTransform)
                {
                    request.ApplyWorldTransform?.Invoke(placementTransform, position, scale);
                }

                request.SyncDoorAnimatorState?.Invoke(i);

                var doorMaterial = request.ResolveMaterial?.Invoke(opening.ColorType);
                request.ApplyDoorMaterialAtIndex?.Invoke(i, doorMaterial);
            }
        }

        private static int MapLogicalToVisualCellIndex(int logicalIndex, int axisSize)
        {
            if (axisSize < 1)
            {
                return 0;
            }

            if (axisSize >= 3)
            {
                return Mathf.Clamp(logicalIndex, 1, axisSize - 2);
            }

            return Mathf.Clamp(logicalIndex, 0, axisSize - 1);
        }

        private static Vector3 ResolveCellCenterWorld(in LayoutMetrics layout, int cellX, int cellY, float worldZ)
        {
            return new Vector3(
                layout.BoardOrigin.x + ((cellX + 0.5f) * layout.CellSize),
                layout.BoardOrigin.y + ((cellY + 0.5f) * layout.CellSize),
                worldZ);
        }
    }
}
