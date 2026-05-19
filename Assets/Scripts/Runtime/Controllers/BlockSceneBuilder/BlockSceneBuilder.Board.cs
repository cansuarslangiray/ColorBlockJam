using Runtime.Data;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private void ApplyBoardVisuals(LevelDefinition levelData, in LayoutMetrics layout)
        {
            var dims = levelData.gridDimensions;
            var openings = levelData.GetDoorOpenings();
            BoardRuntimeState.CollectBlockedCellsForLayout(dims, levelData.blockedCells, openings,
                _resolvedBlockedCells);

            var boardVisualRequest = new BoardVisualBuilder.BuildRequest
            {
                GridCellPoolByCell = _gridCellPoolByCell,
                BlockedCellPool = _blockedCellPool,
                BlockedCells = _resolvedBlockedCells,
                BorderObjects = _borderObjects,
                BackdropObject = _backdropObject,
                DoorPool = _doorPool,
                Openings = openings,
                GridDimensions = dims,
                Layout = layout,
                BoardBackdropZOffset = boardBackdropZOffset,
                BlockedCellZOffset = blockedCellZOffsetFromGrid,
                DoorInsetInCells = doorInsetInCells,
                SetActiveIfChanged = SetActiveIfChanged,
                ApplyWorldTransform = ApplyWorldTransform,
                ResolveDoorPlacementTransform = ResolveDoorPlacementTransform,
                StopDoorMatchFxAtIndex = StopDoorMatchFxAtIndex,
                CacheDoorPlacementBaseLocalPosition = CacheDoorPlacementBaseLocalPosition,
                ResolveMaterial = GetMaterial,
                ApplyDoorMaterialAtIndex = ApplyDoorMaterialAtIndex,
                CacheActiveDoorOpenings = CacheActiveDoorOpenings
            };
            _boardVisualBuilder.ApplyBoardVisuals(boardVisualRequest);
        }

        private static int MapLogicalToVisualCellIndex(int logicalIndex, int axisSize)
        {
            if (axisSize < 1)
            {
                return 0;
            }

            if (axisSize <= 2)
            {
                return Mathf.Clamp(logicalIndex, 0, Mathf.Max(0, axisSize - 1));
            }

            return Mathf.Clamp(logicalIndex, 1, axisSize - 2);
        }
    }
}