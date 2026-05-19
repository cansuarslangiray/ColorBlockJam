using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers
{
    internal sealed class BoardRuntimeState
    {
        private static readonly List<Vector2Int> BlockedCellBuffer = new(256);
        private static readonly HashSet<Vector2Int> UniqueBlockedCellBuffer = new();

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
            PopulateRuntimeBoard(levelData, shapeCatalog);
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

        private void PopulateRuntimeBoard(LevelDefinition levelData, BlockShapeCatalog shapeCatalog)
        {
            _occupancyMap.Configure(levelData.gridDimensions.x, levelData.gridDimensions.y);

            var openings = levelData.GetDoorOpenings();
            for (var i = 0; i < openings.Count; i++)
            {
                _doorOpenings.Add(openings[i]);
            }

            CollectBlockedCellsForLayout(levelData.gridDimensions, levelData.blockedCells, openings, BlockedCellBuffer);
            if (BlockedCellBuffer.Count > 0)
            {
                _occupancyMap.MarkBlockedCells(BlockedCellBuffer);
            }

            // Keep the outer frame non-walkable for blocks so they stay inside the bordered play area.
            BoardFrameMap.CollectFrameCellsExceptDoorOpenings(levelData.gridDimensions, openings, BlockedCellBuffer);
            if (BlockedCellBuffer.Count > 0)
            {
                _occupancyMap.MarkBlockedCells(BlockedCellBuffer);
            }

            if (levelData.blocks == null)
            {
                return;
            }

            for (var i = 0; i < levelData.blocks.Count; i++)
            {
                var blockData = levelData.blocks[i];
                blockData.Normalize();
                var resolvedShapeKey = blockData.ResolvePoolKey();
                var resolvedShape = shapeCatalog != null
                    ? shapeCatalog.ResolveShape(resolvedShapeKey)
                    : null;
                var localCells = resolvedShape != null ? resolvedShape.GetLocalCells() : blockData.GetLocalCells(shapeCatalog);

                if (resolvedShape == null)
                {
                    Debug.LogWarning(
                        $"Level '{levelData.levelKey}' has unresolved shape key '{resolvedShapeKey}' on block index {i}. Falling back to 1x1.");
                }

                var runtimeBlock = new RuntimeBlockState(i, blockData.position, localCells,
                    blockData.blockFeatures, blockData.colorType);

                if (!_occupancyMap.CanPlace(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells))
                {
                    continue;
                }

                _runtimeBlocks.Add(runtimeBlock.Id, runtimeBlock);
                _occupancyMap.FillBlock(runtimeBlock.Id, runtimeBlock.Position, runtimeBlock.LocalCells);
            }
        }

        internal static void CollectBlockedCellsForLayout(
            Vector2Int gridDimensions,
            IReadOnlyList<Vector2Int> authoredBlockedCells,
            IReadOnlyList<DoorOpeningData> openings,
            List<Vector2Int> result)
        {
            result?.Clear();
            if (result == null)
            {
                return;
            }

            var unique = UniqueBlockedCellBuffer;
            unique.Clear();

            if (authoredBlockedCells != null)
            {
                for (var i = 0; i < authoredBlockedCells.Count; i++)
                {
                    var cell = authoredBlockedCells[i];
                    if (cell.x < 0 || cell.y < 0 || cell.x >= gridDimensions.x || cell.y >= gridDimensions.y)
                    {
                        continue;
                    }

                    unique.Add(cell);
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
