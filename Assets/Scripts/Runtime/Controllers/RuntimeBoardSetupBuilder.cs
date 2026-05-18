using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers
{
    internal static class RuntimeBoardSetupBuilder
    {
        private static readonly List<Vector2Int> BlockedCellResultBuffer = new(256);
        private static readonly HashSet<Vector2Int> UniqueBlockedCellBuffer = new();
        private static readonly List<Vector2Int> FrameCellBuffer = new(256);

        public static void Populate(LevelJsonData levelData, BlockShapeRegistry shapeRegistry, BoardOccupancyMap occupancyMap,
            Dictionary<int, RuntimeBlockState> runtimeBlocks, List<DoorOpeningData> doorOpenings)
        {
            occupancyMap.Configure(levelData.gridDimensions.x, levelData.gridDimensions.y);

            var openings = levelData.GetDoorOpenings();
            for (var i = 0; i < openings.Count; i++)
            {
                doorOpenings.Add(openings[i]);
            }

            CollectBlockedCells(levelData, openings, BlockedCellResultBuffer);
            if (BlockedCellResultBuffer.Count > 0)
            {
                occupancyMap.MarkBlockedCells(BlockedCellResultBuffer);
            }

            if (levelData.blocks == null)
            {
                return;
            }

            for (var i = 0; i < levelData.blocks.Count; i++)
            {
                var blockData = levelData.blocks[i];
                blockData.NormalizeMovementConstraint();
                var fallbackCellCount = 1;
                var resolvedShapeKey = blockData.ResolveShapeKey(fallbackCellCount);

                if (!string.IsNullOrWhiteSpace(resolvedShapeKey) &&
                    (shapeRegistry == null || !shapeRegistry.TryResolveShape(resolvedShapeKey, out _)))
                {
                    Debug.LogWarning(
                        $"Level '{levelData.levelKey}' has unresolved shape key '{resolvedShapeKey}' on block index {i}. Falling back to 1x1.");
                }

                var runtimeBlock = new RuntimeBlockState(i, blockData.position, blockData.GetLocalCells(shapeRegistry),
                    blockData.blockFeatures, blockData.movementConstraint, blockData.colorType);

                if (!occupancyMap.CanPlace(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells))
                {
                    continue;
                }

                runtimeBlocks.Add(runtimeBlock.Id, runtimeBlock);
                occupancyMap.FillBlock(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells);
            }
        }

        private static void CollectBlockedCells(LevelJsonData levelData, IReadOnlyList<DoorOpeningData> openings,
            List<Vector2Int> result)
        {
            result.Clear();
            if (levelData == null)
            {
                return;
            }

            var grid = levelData.gridDimensions;
            var unique = UniqueBlockedCellBuffer;
            unique.Clear();

            if (levelData.blockedCells != null)
            {
                for (var i = 0; i < levelData.blockedCells.Count; i++)
                {
                    var cell = levelData.blockedCells[i];
                    if (cell.x < 0 || cell.y < 0 || cell.x >= grid.x || cell.y >= grid.y)
                    {
                        continue;
                    }

                    unique.Add(cell);
                }
            }

            if (grid.x >= 3 && grid.y >= 3)
            {
                var frameCells = FrameCellBuffer;
                frameCells.Clear();
                BoardFrameMap.CollectFrameCellsExceptDoorOpenings(grid, openings, frameCells);
                for (var i = 0; i < frameCells.Count; i++)
                {
                    unique.Add(frameCells[i]);
                }
            }

            if (openings != null)
            {
                for (var i = 0; i < openings.Count; i++)
                {
                    var opening = openings[i];
                    for (var y = opening.MinCell.y; y <= opening.MaxCell.y; y++)
                    {
                        for (var x = opening.MinCell.x; x <= opening.MaxCell.x; x++)
                        {
                            unique.Remove(new Vector2Int(x, y));
                        }
                    }
                }
            }

            result.AddRange(unique);
        }
    }
}
