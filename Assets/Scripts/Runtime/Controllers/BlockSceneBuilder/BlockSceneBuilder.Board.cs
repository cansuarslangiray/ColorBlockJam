using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private void ApplyBoardVisuals(LevelJsonData levelData, in LayoutMetrics layout)
        {
            var dims = levelData.gridDimensions;
            var openings = levelData.GetDoorOpenings();

            foreach (var pair in _gridCellPoolByCell)
            {
                var cell = pair.Key;
                var cellObject = pair.Value;
                var isInsideLevel = IsPlayableGridCell(cell, dims);
                SetActiveIfChanged(cellObject, isInsideLevel);
                if (!isInsideLevel)
                {
                    continue;
                }

                var position = ResolveCellCenterWorld(layout, cell.x, cell.y, layout.GridZ);
                cellObject.transform.position = position;
            }

            ApplyBackdrop(dims, layout);
            ApplyBorders(dims, layout);
            ApplyDoors(openings, dims, layout);
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

        private void ApplyBackdrop(Vector2Int dims, in LayoutMetrics layout)
        {
            if (!_backdropObject)
            {
                return;
            }

            if (dims.x <= 0 || dims.y <= 0 || layout.CellSize <= 0f)
            {
                SetActiveIfChanged(_backdropObject, false);
                return;
            }

            ResolveVisualBounds(dims, out var minCellX, out var minCellY, out var maxCellX, out var maxCellY);
            var widthInCells = (maxCellX - minCellX) + 1;
            var heightInCells = (maxCellY - minCellY) + 1;
            var width = widthInCells * layout.CellSize;
            var height = heightInCells * layout.CellSize;
            var depth = Mathf.Max(0.02f, layout.CellSize * 0.08f);

            SetActiveIfChanged(_backdropObject, true);
            var position = new Vector3(
                layout.BoardOrigin.x + ((minCellX + (widthInCells * 0.5f)) * layout.CellSize),
                layout.BoardOrigin.y + ((minCellY + (heightInCells * 0.5f)) * layout.CellSize),
                layout.GridZ + Mathf.Abs(boardBackdropZOffset));
            var scale = new Vector3(width, height, depth);
            ApplyWorldTransform(_backdropObject.transform, position, scale);
        }

        private void ApplyBorders(Vector2Int dims, in LayoutMetrics layout)
        {
            if (_borderObjects.Count == 0)
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

            ApplyBorderAtIndex(
                (int)Direction.Up,
                new Vector3(centerX, topY, layout.BorderZ),
                new Vector3(horizontalLength, layout.FrameThickness, layout.FrameDepth));

            ApplyBorderAtIndex(
                (int)Direction.Down,
                new Vector3(centerX, bottomY, layout.BorderZ),
                new Vector3(horizontalLength, layout.FrameThickness, layout.FrameDepth));

            ApplyBorderAtIndex(
                (int)Direction.Left,
                new Vector3(leftX, centerY, layout.BorderZ),
                new Vector3(layout.FrameThickness, verticalLength, layout.FrameDepth));

            ApplyBorderAtIndex(
                (int)Direction.Right,
                new Vector3(rightX, centerY, layout.BorderZ),
                new Vector3(layout.FrameThickness, verticalLength, layout.FrameDepth));

            for (var i = (int)Direction.Right + 1; i < _borderObjects.Count; i++)
            {
                var borderObject = _borderObjects[i];
                if (borderObject)
                {
                    SetActiveIfChanged(borderObject, false);
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

        private void ApplyBorderAtIndex(int borderIndex, Vector3 position, Vector3 scale)
        {
            if (borderIndex < 0 || borderIndex >= _borderObjects.Count)
            {
                return;
            }

            var borderObject = _borderObjects[borderIndex];
            if (!borderObject)
            {
                return;
            }

            SetActiveIfChanged(borderObject, true);
            ApplyWorldTransform(borderObject.transform, position, scale);
        }

        private void ApplyDoors(IReadOnlyList<DoorOpeningData> openings, Vector2Int gridDimensions, in LayoutMetrics layout)
        {
            var requiredCount = openings?.Count ?? 0;
            var activeDoorCount = Mathf.Min(requiredCount, _doorPool.Count);

            CacheActiveDoorOpenings(openings);

            var cellSize = layout.CellSize;
            var doorOffset = (0.5f * cellSize) + (layout.FrameThickness * 0.5f) -
                             (doorInsetInCells * cellSize);

            for (var i = 0; i < _doorPool.Count; i++)
            {
                var doorVisual = _doorPool[i];
                var isActive = i < activeDoorCount;
                SetActiveIfChanged(doorVisual, isActive);
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
                var span = Mathf.Max(0.01f, (openingWidth * cellSize) - boardCellGap);
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
                ApplyWorldTransform(doorVisual.transform, position, scale);

                var doorMaterial = GetMaterial(opening.ColorType);
                ApplySharedMaterial(doorVisual, doorMaterial);
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
