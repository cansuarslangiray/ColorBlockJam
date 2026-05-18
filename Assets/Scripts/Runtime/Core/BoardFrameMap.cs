using System.Collections.Generic;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Core
{
    public static class BoardFrameMap
    {
        public static bool IsFrameCell(Vector2Int cell, Vector2Int gridDimensions)
        {
            if (gridDimensions.x <= 0 || gridDimensions.y <= 0)
            {
                return false;
            }

            return cell.x == 0 ||
                   cell.y == 0 ||
                   cell.x == gridDimensions.x - 1 ||
                   cell.y == gridDimensions.y - 1;
        }

        public static void CollectFrameCells(Vector2Int gridDimensions, List<Vector2Int> resultCells)
        {
            resultCells?.Clear();
            if (resultCells == null || gridDimensions.x <= 0 || gridDimensions.y <= 0)
            {
                return;
            }

            var maxX = gridDimensions.x - 1;
            var maxY = gridDimensions.y - 1;

            for (var x = 0; x <= maxX; x++)
            {
                resultCells.Add(new Vector2Int(x, 0));
            }

            for (var y = 1; y < maxY; y++)
            {
                resultCells.Add(new Vector2Int(0, y));
                if (maxX > 0)
                {
                    resultCells.Add(new Vector2Int(maxX, y));
                }
            }

            if (maxY > 0)
            {
                for (var x = 0; x <= maxX; x++)
                {
                    resultCells.Add(new Vector2Int(x, maxY));
                }
            }
        }

        public static void CollectFrameCellsExceptDoorOpenings(
            Vector2Int gridDimensions,
            IReadOnlyList<DoorOpeningData> openings,
            List<Vector2Int> resultCells)
        {
            CollectFrameCells(gridDimensions, resultCells);
            if (resultCells == null || resultCells.Count == 0 || openings == null || openings.Count == 0)
            {
                return;
            }

            for (var i = resultCells.Count - 1; i >= 0; i--)
            {
                if (IsDoorOpeningCell(resultCells[i], openings))
                {
                    resultCells.RemoveAt(i);
                }
            }
        }

        private static bool IsDoorOpeningCell(Vector2Int cell, IReadOnlyList<DoorOpeningData> openings)
        {
            for (var i = 0; i < openings.Count; i++)
            {
                var opening = openings[i];
                if (cell.x < opening.MinCell.x || cell.x > opening.MaxCell.x ||
                    cell.y < opening.MinCell.y || cell.y > opening.MaxCell.y)
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
