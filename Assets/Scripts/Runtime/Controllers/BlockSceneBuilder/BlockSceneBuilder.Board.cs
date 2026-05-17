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
        private const float BorderSegmentEpsilon = 0.0001f;
        private readonly List<BorderSpan> _borderGapBuffer = new();
        private readonly List<BorderSpan> _borderSegmentBuffer = new();
        private readonly List<DoorOpeningData> _upEdgeOpeningsBuffer = new();
        private readonly List<DoorOpeningData> _downEdgeOpeningsBuffer = new();
        private readonly List<DoorOpeningData> _leftEdgeOpeningsBuffer = new();
        private readonly List<DoorOpeningData> _rightEdgeOpeningsBuffer = new();

        private void EnsureBoardPool(Vector2Int gridSize)
        {
            var width = Mathf.Max(1, gridSize.x);
            var height = Mathf.Max(1, gridSize.y);
            var boardContentRoot = BoardRoot;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_gridCellPoolByCell.ContainsKey(cell))
                    {
                        continue;
                    }

                    _gridCellPoolByCell[cell] = CreateGridCellObject(boardContentRoot, cell);
                }
            }

            _backdropObject ??= CreateVisualObject(boardContentRoot, BoardBackdropName, backdropPrefab, false);

            while (_borderPool.Count < 4)
            {
                _borderPool.Add(CreateVisualObject(boardContentRoot, GetRuntimeName(BorderNamePrefix, _borderPool.Count), borderPrefab, false));
            }
        }

        private void ApplyBoardVisuals(LevelJsonData levelData)
        {
            var dims = levelData.gridDimensions;
            var openings = levelData.GetDoorOpenings();
            var boardOrigin = BoardOrigin;
            var cellSize = CellSize;
            var tileSize = Mathf.Max(0.01f, cellSize - boardCellGap);
            var tileDepth = Mathf.Max(0.01f, cellSize * boardCellDepthInCells);
            var tileZ = Mathf.Abs((float)boardCellsZOffset);

            foreach (var pair in _gridCellPoolByCell)
            {
                var cell = pair.Key;
                var visual = pair.Value;
                var isInsideLevel = cell.x < dims.x && cell.y < dims.y;
                SetActiveIfChanged(visual.GameObject, isInsideLevel);
                if (!isInsideLevel)
                {
                    continue;
                }

                var position = new Vector3(boardOrigin.x + ((cell.x + 0.5f) * cellSize), boardOrigin.y + ((cell.y + 0.5f) * cellSize), tileZ);
                var scale = new Vector3(tileSize, tileSize, tileDepth);
                ApplyWorldTransform(visual.Transform, position, scale);
            }

            ApplyBackdrop(dims, boardOrigin, cellSize, tileZ);
            ApplyBorders(dims, boardOrigin, cellSize, openings);
            ApplyDoors(openings, boardOrigin, cellSize);
        }

        private void ApplyBackdrop(Vector2Int dims, Vector2 boardOrigin, float cellSize, float tileZ)
        {
            if (_backdropObject == null)
            {
                return;
            }

            if (dims.x <= 0 || dims.y <= 0 || cellSize <= 0f)
            {
                SetActiveIfChanged(_backdropObject.GameObject, false);
                return;
            }

            var width = dims.x * cellSize;
            var height = dims.y * cellSize;
            var padding = Mathf.Max(0f, boardBackdropPaddingInCells * cellSize);
            var depth = Mathf.Max(0.02f, cellSize * 0.08f);

            SetActiveIfChanged(_backdropObject.GameObject, true);
            var position = new Vector3(boardOrigin.x + (width * 0.5f), boardOrigin.y + (height * 0.5f), tileZ + Mathf.Abs((float)boardBackdropZOffset));
            var scale = new Vector3(width + (padding * 2f), height + (padding * 2f), depth);
            ApplyWorldTransform(_backdropObject.Transform, position, scale);
        }

        private void ApplyBorders(Vector2Int dims, Vector2 boardOrigin, float cellSize,
            IReadOnlyList<DoorOpeningData> openings)
        {
            if (dims.x <= 0 || dims.y <= 0 || cellSize <= 0f)
            {
                foreach (var border in _borderPool)
                {
                    SetActiveIfChanged(border.GameObject, false);
                }

                return;
            }

            if (_borderPool.Count == 0)
            {
                return;
            }

            var thickness = Mathf.Max(0.01f, edgeFrameThicknessInCells * cellSize);
            var padding = Mathf.Max(0f, edgeFramePaddingInCells * cellSize);
            var depth = Mathf.Max(0.01f, edgeFrameDepthInCells * cellSize);
            var width = dims.x * cellSize;
            var height = dims.y * cellSize;
            var borderZ = Mathf.Abs((float)boardCellsZOffset) - 0.01f;
            var rangeExtension = thickness + padding;
            var horizontalMin = -rangeExtension;
            var horizontalMax = width + rangeExtension;
            var verticalMin = -rangeExtension;
            var verticalMax = height + rangeExtension;

            CacheDoorOpeningsByEdge(openings);

            var borderIndex = 0;

            borderIndex += ApplyEdgeSegments(
                true,
                boardOrigin.x,
                boardOrigin.y + height + padding + (thickness * 0.5f),
                horizontalMin,
                horizontalMax,
                thickness,
                depth,
                borderZ,
                cellSize,
                _upEdgeOpeningsBuffer,
                borderIndex);

            borderIndex += ApplyEdgeSegments(
                true,
                boardOrigin.x,
                boardOrigin.y - padding - (thickness * 0.5f),
                horizontalMin,
                horizontalMax,
                thickness,
                depth,
                borderZ,
                cellSize,
                _downEdgeOpeningsBuffer,
                borderIndex);

            borderIndex += ApplyEdgeSegments(
                false,
                boardOrigin.y,
                boardOrigin.x - padding - (thickness * 0.5f),
                verticalMin,
                verticalMax,
                thickness,
                depth,
                borderZ,
                cellSize,
                _leftEdgeOpeningsBuffer,
                borderIndex);

            borderIndex += ApplyEdgeSegments(
                false,
                boardOrigin.y,
                boardOrigin.x + width + padding + (thickness * 0.5f),
                verticalMin,
                verticalMax,
                thickness,
                depth,
                borderZ,
                cellSize,
                _rightEdgeOpeningsBuffer,
                borderIndex);

            for (var i = borderIndex; i < _borderPool.Count; i++)
            {
                SetActiveIfChanged(_borderPool[i].GameObject, false);
            }
        }

        private int ApplyEdgeSegments(
            bool isHorizontalEdge,
            float primaryOrigin,
            float secondaryWorldAxis,
            float rangeMin,
            float rangeMax,
            float thickness,
            float depth,
            float borderZ,
            float cellSize,
            IReadOnlyList<DoorOpeningData> edgeOpenings,
            int borderStartIndex)
        {
            BuildBorderSegmentsForRange(rangeMin, rangeMax, cellSize, edgeOpenings, _borderSegmentBuffer);

            var appliedCount = 0;
            foreach (var segment in _borderSegmentBuffer)
            {
                var segmentLength = segment.Max - segment.Min;
                if (segmentLength <= BorderSegmentEpsilon)
                {
                    continue;
                }

                var poolIndex = borderStartIndex + appliedCount;
                EnsureBorderVisual(poolIndex);

                var centerOnPrimaryAxis = primaryOrigin + ((segment.Min + segment.Max) * 0.5f);
                var position = isHorizontalEdge
                    ? new Vector3(centerOnPrimaryAxis, secondaryWorldAxis, borderZ)
                    : new Vector3(secondaryWorldAxis, centerOnPrimaryAxis, borderZ);
                var scale = isHorizontalEdge
                    ? new Vector3(segmentLength, thickness, depth)
                    : new Vector3(thickness, segmentLength, depth);
                ApplyBorderTransform(_borderPool[poolIndex], position, scale);
                appliedCount++;
            }

            return appliedCount;
        }

        private void BuildBorderSegmentsForRange(
            float rangeMin,
            float rangeMax,
            float cellSize,
            IReadOnlyList<DoorOpeningData> edgeOpenings,
            List<BorderSpan> resultSegments)
        {
            resultSegments.Clear();
            _borderGapBuffer.Clear();

            if (rangeMax - rangeMin <= BorderSegmentEpsilon)
            {
                return;
            }

            if (edgeOpenings != null)
            {
                for (var i = 0; i < edgeOpenings.Count; i++)
                {
                    var gap = ResolveDoorGapSpan(edgeOpenings[i], cellSize);
                    if (gap.Max - gap.Min <= BorderSegmentEpsilon)
                    {
                        continue;
                    }

                    _borderGapBuffer.Add(gap);
                }
            }

            if (_borderGapBuffer.Count == 0)
            {
                resultSegments.Add(new BorderSpan(rangeMin, rangeMax));
                return;
            }

            if (_borderGapBuffer.Count > 1)
            {
                _borderGapBuffer.Sort((a, b) => a.Min.CompareTo(b.Min));
            }

            var cursor = rangeMin;
            for (var i = 0; i < _borderGapBuffer.Count; i++)
            {
                var gap = _borderGapBuffer[i];
                var gapMin = Mathf.Clamp(gap.Min, rangeMin, rangeMax);
                var gapMax = Mathf.Clamp(gap.Max, rangeMin, rangeMax);
                if (gapMax - gapMin <= BorderSegmentEpsilon)
                {
                    continue;
                }

                if (gapMin > cursor + BorderSegmentEpsilon)
                {
                    resultSegments.Add(new BorderSpan(cursor, gapMin));
                }

                if (gapMax > cursor)
                {
                    cursor = gapMax;
                }

                if (cursor >= rangeMax - BorderSegmentEpsilon)
                {
                    break;
                }
            }

            if (cursor < rangeMax - BorderSegmentEpsilon)
            {
                resultSegments.Add(new BorderSpan(cursor, rangeMax));
            }
        }

        private BorderSpan ResolveDoorGapSpan(DoorOpeningData opening, float cellSize)
        {
            int minAxis;
            int maxAxis;
            if (opening.EdgeDirection is Direction.Up or Direction.Down)
            {
                minAxis = opening.MinCell.x;
                maxAxis = opening.MaxCell.x;
            }
            else
            {
                minAxis = opening.MinCell.y;
                maxAxis = opening.MaxCell.y;
            }

            var spanInCells = (maxAxis - minAxis) + 1;
            var span = Mathf.Max(0.01f, (spanInCells * cellSize) - boardCellGap);
            var center = ((minAxis + maxAxis + 1) * 0.5f) * cellSize;
            return new BorderSpan(center - (span * 0.5f), center + (span * 0.5f));
        }

        private void CacheDoorOpeningsByEdge(IReadOnlyList<DoorOpeningData> openings)
        {
            _upEdgeOpeningsBuffer.Clear();
            _downEdgeOpeningsBuffer.Clear();
            _leftEdgeOpeningsBuffer.Clear();
            _rightEdgeOpeningsBuffer.Clear();

            if (openings == null)
            {
                return;
            }

            for (var i = 0; i < openings.Count; i++)
            {
                var opening = openings[i];
                switch (opening.EdgeDirection)
                {
                    case Direction.Up:
                        _upEdgeOpeningsBuffer.Add(opening);
                        break;
                    case Direction.Down:
                        _downEdgeOpeningsBuffer.Add(opening);
                        break;
                    case Direction.Left:
                        _leftEdgeOpeningsBuffer.Add(opening);
                        break;
                    case Direction.Right:
                        _rightEdgeOpeningsBuffer.Add(opening);
                        break;
                }
            }
        }

        private void EnsureBorderVisual(int index)
        {
            while (_borderPool.Count <= index)
            {
                _borderPool.Add(CreateVisualObject(BoardRoot, GetRuntimeName(BorderNamePrefix, _borderPool.Count),
                    borderPrefab, false));
            }
        }

        private void ApplyDoors(IReadOnlyList<DoorOpeningData> openings, Vector2 boardOrigin, float cellSize)
        {
            CacheActiveDoorOpenings(openings);
            var requiredCount = openings?.Count ?? 0;
            var doorContentRoot = BoardRoot;

            while (_doorPool.Count < requiredCount)
            {
                _doorPool.Add(CreateVisualObject(doorContentRoot,
                    GetRuntimeName(DoorNamePrefix, _doorPool.Count),
                    doorPrefab,
                    true));
            }

            var frameThickness = Mathf.Max(0.01f, edgeFrameThicknessInCells * cellSize);
            var frameDepth = Mathf.Max(0.01f, edgeFrameDepthInCells * cellSize);
            var framePadding = Mathf.Max(0f, edgeFramePaddingInCells * cellSize);
            var doorOffset = (0.5f * cellSize) + framePadding + (frameThickness * 0.5f) - (doorInsetInCells * cellSize);
            var borderZ = Mathf.Abs((float)boardCellsZOffset) - 0.01f;
            var doorDepth = frameDepth * 1.08f;
            var doorZ = borderZ - Mathf.Max(0.005f, doorDepthBiasFromFrame);

            for (var i = 0; i < _doorPool.Count; i++)
            {
                var doorVisual = _doorPool[i];
                var isActive = i < requiredCount;
                SetActiveIfChanged(doorVisual.GameObject, isActive);
                if (!isActive)
                {
                    continue;
                }

                var opening = openings[i];
                var normal = opening.EdgeDirection.ToVector();

                var openingWidth = Mathf.Max(1, opening.OpeningWidth);
                var span = Mathf.Max(0.01f, (openingWidth * cellSize) - boardCellGap);
                var centerX = (opening.MinCell.x + opening.MaxCell.x + 1) * 0.5f;
                var centerY = (opening.MinCell.y + opening.MaxCell.y + 1) * 0.5f;

                var cellCenter = new Vector2(boardOrigin.x + (centerX * cellSize), boardOrigin.y + (centerY * cellSize));
                var position = new Vector3(cellCenter.x + (normal.x * doorOffset), cellCenter.y + (normal.y * doorOffset), doorZ);
                var isHorizontal = opening.EdgeDirection.IsVertical();
                var scale = isHorizontal ? new Vector3(span, frameThickness, doorDepth) : new Vector3(frameThickness, span, doorDepth);
                ApplyWorldTransform(doorVisual.Transform, position, scale);

                var doorMaterial = GetDoorMaterial(opening.ColorType);
                ApplySharedMaterial(doorVisual, doorMaterial);
            }
        }

        private static void ApplyBorderTransform(PooledVisual borderVisual, Vector3 position, Vector3 scale)
        {
            SetActiveIfChanged(borderVisual.GameObject, true);
            ApplyWorldTransform(borderVisual.Transform, position, scale);
        }

    }
}
