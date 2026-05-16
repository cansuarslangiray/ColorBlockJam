using Runtime.Core;
using Runtime.Data;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
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

            _backdropObject ??= CreateVisualObject(boardContentRoot, GetRuntimeName(boardBackdropName), backdropPrefab, false);

            while (_borderPool.Count < 4)
            {
                _borderPool.Add(CreateVisualObject(boardContentRoot, GetRuntimeName(borderNamePrefix, _borderPool.Count), borderPrefab, false));
            }
        }

        private void ApplyBoardVisuals(LevelData levelData)
        {
            var dims = levelData.gridDimensions;
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
            ApplyBorders(dims, boardOrigin, cellSize);
            ApplyDoors(levelData, boardOrigin, cellSize);
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

        private void ApplyBorders(Vector2Int dims, Vector2 boardOrigin, float cellSize)
        {
            if (_borderPool.Count < 4)
            {
                return;
            }

            var thickness = Mathf.Max(0.01f, edgeFrameThicknessInCells * cellSize);
            var padding = Mathf.Max(0f, edgeFramePaddingInCells * cellSize);
            var depth = Mathf.Max(0.01f, edgeFrameDepthInCells * cellSize);
            var width = dims.x * cellSize;
            var height = dims.y * cellSize;
            var frameHeight = height + (2f * (thickness + padding));
            var frameWidth = width + (2f * (thickness + padding));
            var borderZ = Mathf.Abs((float)boardCellsZOffset) - 0.01f;

            var leftPosition = new Vector3(boardOrigin.x - padding - (thickness * 0.5f), boardOrigin.y + (height * 0.5f), borderZ);
            var rightPosition = new Vector3(boardOrigin.x + width + padding + (thickness * 0.5f), boardOrigin.y + (height * 0.5f), borderZ);
            var topPosition = new Vector3(boardOrigin.x + (width * 0.5f), boardOrigin.y + height + padding + (thickness * 0.5f), borderZ);
            var bottomPosition = new Vector3(boardOrigin.x + (width * 0.5f), boardOrigin.y - padding - (thickness * 0.5f), borderZ);

            ApplyBorderTransform(_borderPool[0], leftPosition, new Vector3(thickness, frameHeight, depth));
            ApplyBorderTransform(_borderPool[1], rightPosition, new Vector3(thickness, frameHeight, depth));
            ApplyBorderTransform(_borderPool[2], topPosition, new Vector3(frameWidth, thickness, depth));
            ApplyBorderTransform(_borderPool[3], bottomPosition, new Vector3(frameWidth, thickness, depth));
        }

        private void ApplyDoors(LevelData levelData, Vector2 boardOrigin, float cellSize)
        {
            var openings = levelData.GetDoorOpenings();
            var requiredCount = openings?.Count ?? 0;
            var doorContentRoot = BoardRoot;

            while (_doorPool.Count < requiredCount)
            {
                _doorPool.Add(CreateVisualObject(doorContentRoot,
                    GetRuntimeName(doorNamePrefix, _doorPool.Count),
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

                if (doorVisual.Renderer == null) continue;
                var doorMaterial = GetDoorMaterial(opening.ColorType);
                if (doorVisual.Renderer.sharedMaterial != doorMaterial)
                {
                    doorVisual.Renderer.sharedMaterial = doorMaterial;
                }
            }
        }

        private static void ApplyBorderTransform(PooledVisual borderVisual, Vector3 position, Vector3 scale)
        {
            SetActiveIfChanged(borderVisual.GameObject, true);
            ApplyWorldTransform(borderVisual.Transform, position, scale);
        }
    }
}
