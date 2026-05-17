using System.Collections.Generic;
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
                var isInsideLevel = cell.x < dims.x && cell.y < dims.y;
                SetActiveIfChanged(cellObject, isInsideLevel);
                if (!isInsideLevel)
                {
                    continue;
                }

                var position = ResolveCellCenterWorld(layout, cell.x, cell.y, layout.GridZ);
                if (cellObject.transform.position != position)
                {
                    cellObject.transform.position = position;
                }
            }

            ApplyBackdrop(dims, layout);
            ApplyBorders(dims, layout);
            ApplyDoors(openings, layout);
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

            var width = dims.x * layout.CellSize;
            var height = dims.y * layout.CellSize;
            var padding = Mathf.Max(0f, boardBackdropPaddingInCells * layout.CellSize);
            var depth = Mathf.Max(0.02f, layout.CellSize * 0.08f);

            SetActiveIfChanged(_backdropObject, true);
            var position = new Vector3(
                layout.BoardOrigin.x + (width * 0.5f),
                layout.BoardOrigin.y + (height * 0.5f),
                layout.GridZ + Mathf.Abs(boardBackdropZOffset));
            var scale = new Vector3(width + (padding * 2f), height + (padding * 2f), depth);
            ApplyWorldTransform(_backdropObject.transform, position, scale);
        }

        private void ApplyBorders(Vector2Int dims, in LayoutMetrics layout)
        {
            if (_borderObjects == null || _borderObjects.Count == 0)
            {
                return;
            }

            if (dims.x <= 0 || dims.y <= 0 || layout.CellSize <= 0f)
            {
                return;
            }

            var width = dims.x * layout.CellSize;
            var height = dims.y * layout.CellSize;
            var horizontalLength = width + ((layout.FrameThickness + layout.FramePadding) * 2f);
            var verticalLength = height + ((layout.FrameThickness + layout.FramePadding) * 2f);

            var topY = layout.BoardOrigin.y + height + layout.FramePadding + (layout.FrameThickness * 0.5f);
            var bottomY = layout.BoardOrigin.y - layout.FramePadding - (layout.FrameThickness * 0.5f);
            var leftX = layout.BoardOrigin.x - layout.FramePadding - (layout.FrameThickness * 0.5f);
            var rightX = layout.BoardOrigin.x + width + layout.FramePadding + (layout.FrameThickness * 0.5f);

            var centerX = layout.BoardOrigin.x + (width * 0.5f);
            var centerY = layout.BoardOrigin.y + (height * 0.5f);

            ApplyBorderAtDirection(
                Direction.Up,
                new Vector3(centerX, topY, layout.BorderZ),
                new Vector3(horizontalLength, layout.FrameThickness, layout.FrameDepth));

            ApplyBorderAtDirection(
                Direction.Down,
                new Vector3(centerX, bottomY, layout.BorderZ),
                new Vector3(horizontalLength, layout.FrameThickness, layout.FrameDepth));

            ApplyBorderAtDirection(
                Direction.Left,
                new Vector3(leftX, centerY, layout.BorderZ),
                new Vector3(layout.FrameThickness, verticalLength, layout.FrameDepth));

            ApplyBorderAtDirection(
                Direction.Right,
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

        private void ApplyBorderAtDirection(Direction direction, Vector3 position, Vector3 scale)
        {
            var borderIndex = (int)direction;
            if (_borderObjects == null || borderIndex < 0 || borderIndex >= _borderObjects.Count)
            {
                return;
            }

            ApplyBorderTransform(_borderObjects[borderIndex], position, scale);
        }

        private void ApplyDoors(IReadOnlyList<DoorOpeningData> openings, in LayoutMetrics layout)
        {
            var requiredCount = openings?.Count ?? 0;
            var activeDoorCount = Mathf.Min(requiredCount, _doorPool.Count);

            CacheActiveDoorOpenings(openings);

            var cellSize = layout.CellSize;
            var doorOffset = (0.5f * cellSize) + layout.FramePadding + (layout.FrameThickness * 0.5f) -
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

                var openingWidth = Mathf.Max(1, opening.OpeningWidth);
                var span = Mathf.Max(0.01f, (openingWidth * cellSize) - boardCellGap);
                var centerX = (opening.MinCell.x + opening.MaxCell.x + 1) * 0.5f;
                var centerY = (opening.MinCell.y + opening.MaxCell.y + 1) * 0.5f;

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

        private static Vector3 ResolveCellCenterWorld(in LayoutMetrics layout, int cellX, int cellY, float worldZ)
        {
            return new Vector3(
                layout.BoardOrigin.x + ((cellX + 0.5f) * layout.CellSize),
                layout.BoardOrigin.y + ((cellY + 0.5f) * layout.CellSize),
                worldZ);
        }

        private static void ApplyBorderTransform(GameObject borderObject, Vector3 position, Vector3 scale)
        {
            if (!borderObject)
            {
                return;
            }

            SetActiveIfChanged(borderObject, true);
            ApplyWorldTransform(borderObject.transform, position, scale);
        }
    }
}
