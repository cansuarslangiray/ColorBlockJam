using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers
{
    internal static class RuntimeBoardSetupBuilder
    {
        public static void Populate(LevelJsonData levelData, BlockShapeRegistry shapeRegistry, BoardOccupancyMap occupancyMap,
            Dictionary<int, RuntimeBlockState> runtimeBlocks, List<DoorOpeningData> doorOpenings)
        {
            occupancyMap.Configure(levelData.gridDimensions.x, levelData.gridDimensions.y);

            var openings = levelData.GetDoorOpenings();
            for (var i = 0; i < openings.Count; i++)
            {
                doorOpenings.Add(openings[i]);
            }

            var blockedCells = BuildBlockedCells(levelData, openings);
            if (blockedCells.Count > 0)
            {
                occupancyMap.MarkBlockedCells(blockedCells);
            }

            if (levelData.blocks == null)
            {
                return;
            }

            for (var i = 0; i < levelData.blocks.Count; i++)
            {
                var blockData = levelData.blocks[i];
                var fallbackCellCount = 1;
                var resolvedBlockType = blockData.ResolveBlockType(fallbackCellCount);
                var resolvedShapeKey = BlockShapeTypeUtility.ToShapeKey(resolvedBlockType);
                blockData.blockType = resolvedBlockType;
                blockData.shapeKey = resolvedShapeKey;

                if (!string.IsNullOrWhiteSpace(resolvedShapeKey) &&
                    (shapeRegistry == null || !shapeRegistry.TryResolveShape(resolvedShapeKey, out _)))
                {
                    Debug.LogWarning(
                        $"Level '{levelData.levelKey}' has unresolved shape key '{resolvedShapeKey}' on block index {i}. Falling back to 1x1.");
                }

                var runtimeBlock = new RuntimeBlockState(i, blockData.position, blockData.GetLocalCells(shapeRegistry),
                    blockData.movementConstraint, blockData.colorType);

                if (!occupancyMap.CanPlace(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells))
                {
                    continue;
                }

                runtimeBlocks.Add(runtimeBlock.Id, runtimeBlock);
                occupancyMap.FillBlock(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells);
            }
        }

        private static List<Vector2Int> BuildBlockedCells(LevelJsonData levelData, IReadOnlyList<DoorOpeningData> openings)
        {
            var result = new List<Vector2Int>();
            if (levelData == null)
            {
                return result;
            }

            var grid = levelData.gridDimensions;
            var unique = new HashSet<Vector2Int>();

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
                var frameCells = new List<Vector2Int>();
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
            return result;
        }
    }
}
