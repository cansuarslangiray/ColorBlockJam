using Runtime.Data;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private void ApplyBoardVisuals(LevelJsonData levelData, in LayoutMetrics layout)
        {
            var dims = levelData.gridDimensions;
            var openings = levelData.GetDoorOpenings();

            var boardVisualRequest = new BoardVisualBuilder.BuildRequest
            {
                GridCellPoolByCell = _gridCellPoolByCell,
                BorderObjects = _borderObjects,
                BackdropObject = _backdropObject,
                DoorPool = _doorPool,
                Openings = openings,
                GridDimensions = dims,
                Layout = layout,
                BoardBackdropZOffset = boardBackdropZOffset,
                DoorInsetInCells = doorInsetInCells,
                SetActiveIfChanged = SetActiveIfChanged,
                ApplyWorldTransform = ApplyWorldTransform,
                ResolveDoorPlacementTransform = ResolveDoorPlacementTransform,
                SyncDoorAnimatorState = SyncDoorAnimatorState,
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

            if (axisSize >= 3)
            {
                return Mathf.Clamp(logicalIndex, 1, axisSize - 2);
            }

            return Mathf.Clamp(logicalIndex, 0, axisSize - 1);
        }
    }
}
