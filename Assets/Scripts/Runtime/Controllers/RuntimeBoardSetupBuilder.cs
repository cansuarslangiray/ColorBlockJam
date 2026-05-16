using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Models;

namespace Runtime.Controllers
{
    internal static class RuntimeBoardSetupBuilder
    {
        public static void Populate(LevelData levelData, BoardOccupancyMap occupancyMap,
            Dictionary<int, RuntimeBlockState> runtimeBlocks, List<DoorOpeningData> doorOpenings)
        {
            occupancyMap.Configure(levelData.gridDimensions.x, levelData.gridDimensions.y);

            if (levelData.blockedCells != null && levelData.blockedCells.Count > 0)
            {
                occupancyMap.MarkBlockedCells(levelData.blockedCells);
            }

            var openings = levelData.GetDoorOpenings();
            for (var i = 0; i < openings.Count; i++)
            {
                doorOpenings.Add(openings[i]);
            }

            if (levelData.blocks == null)
            {
                return;
            }

            for (var i = 0; i < levelData.blocks.Count; i++)
            {
                var blockData = levelData.blocks[i];
                var runtimeBlock = new RuntimeBlockState(i, blockData.position, blockData.GetLocalCells(),
                    blockData.movementConstraint, blockData.colorType);

                if (!occupancyMap.CanPlace(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells))
                {
                    continue;
                }

                runtimeBlocks.Add(runtimeBlock.Id, runtimeBlock);
                occupancyMap.FillBlock(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells);
            }
        }
    }
}
