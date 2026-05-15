using System.Collections.Generic;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Core
{
    public static class DoorCellResolver
    {
        public static bool TryCollectDoorCells(DoorData door, Vector2Int gridDimensions, List<Vector2Int> resultCells)
        {
            return DoorOpeningMap.TryCollectDoorCells(door, gridDimensions, resultCells);
        }

        public static bool TryGetDoorSide(Vector2Int cell, Vector2Int gridDimensions, out int edgeSide)
        {
            return DoorOpeningMap.TryGetDoorSide(cell, gridDimensions, out edgeSide);
        }

        public static bool IsCornerCell(Vector2Int cell, Vector2Int gridDimensions)
        {
            return DoorOpeningMap.IsCornerCell(cell, gridDimensions);
        }
    }
}
