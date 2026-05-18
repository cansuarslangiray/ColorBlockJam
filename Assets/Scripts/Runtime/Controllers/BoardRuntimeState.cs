using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers
{
    internal sealed class BoardRuntimeState
    {
        private readonly Dictionary<int, RuntimeBlockState> _runtimeBlocks = new();
        private readonly List<DoorOpeningData> _doorOpenings = new();
        private readonly BoardOccupancyMap _occupancyMap = new();

        public bool IsInitialized { get; private set; }
        public Vector2Int GridDimensions { get; private set; }
        public Dictionary<int, RuntimeBlockState> RuntimeBlocks => _runtimeBlocks;
        public IReadOnlyList<DoorOpeningData> DoorOpenings => _doorOpenings;
        public BoardOccupancyMap OccupancyMap => _occupancyMap;
        public int ActiveBlockCount => _runtimeBlocks.Count;

        public void Setup(LevelDefinition levelData, BlockShapeCatalog shapeCatalog)
        {
            IsInitialized = false;
            _runtimeBlocks.Clear();
            _doorOpenings.Clear();

            if (levelData == null)
            {
                GridDimensions = Vector2Int.zero;
                _occupancyMap.Configure(0, 0);
                _occupancyMap.RebuildDoorOverlap(_doorOpenings);
                return;
            }

            GridDimensions = levelData.gridDimensions;
            RuntimeBoardSetupBuilder.Populate(levelData, shapeCatalog, _occupancyMap, _runtimeBlocks, _doorOpenings);
            _occupancyMap.RebuildDoorOverlap(_doorOpenings);
            IsInitialized = true;
        }

        public bool TryGetRuntimeBlock(int blockId, out RuntimeBlockState block)
        {
            block = default;
            return IsInitialized && _runtimeBlocks.TryGetValue(blockId, out block);
        }

        public bool TryGetBlockAtCell(Vector2Int cell, out int blockId)
        {
            blockId = -1;
            return IsInitialized && _occupancyMap.TryGetBlockAt(cell.x, cell.y, out blockId);
        }
    }
}
