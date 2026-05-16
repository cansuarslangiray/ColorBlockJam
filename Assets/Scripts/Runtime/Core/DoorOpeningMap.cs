using System.Collections.Generic;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Core
{
    public class DoorOpeningMap
    {
        private static readonly Vector2Int[] _neighbors =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        private readonly Dictionary<Vector2Int, int> _openingIndexByCell = new Dictionary<Vector2Int, int>();
        private readonly List<DoorOpeningData> _openings = new List<DoorOpeningData>();

        public IReadOnlyList<DoorOpeningData> Openings => _openings;

        public void Clear()
        {
            _openingIndexByCell.Clear();
            _openings.Clear();
        }

        public void Build(List<DoorData> doors, Vector2Int gridDimensions)
        {
            Clear();

            if (doors == null || doors.Count == 0)
            {
                return;
            }

            Dictionary<Vector2Int, BlockColor> doorColorByCell = new Dictionary<Vector2Int, BlockColor>();
            Dictionary<Vector2Int, EdgeSide> doorSideByCell = new Dictionary<Vector2Int, EdgeSide>();
            List<Vector2Int> expandedDoorCells = new List<Vector2Int>(8);

            for (int i = 0; i < doors.Count; i++)
            {
                DoorData door = doors[i];
                if (!TryCollectDoorCells(door, gridDimensions, expandedDoorCells))
                {
                    continue;
                }

                for (int cellIndex = 0; cellIndex < expandedDoorCells.Count; cellIndex++)
                {
                    Vector2Int cell = expandedDoorCells[cellIndex];
                    if (!TryGetDoorSide(cell, gridDimensions, out var edgeSide))
                    {
                        continue;
                    }

                    doorColorByCell[cell] = door.colorType;
                    doorSideByCell[cell] = edgeSide;
                }
            }

            HashSet<Vector2Int> unvisited = new HashSet<Vector2Int>(doorColorByCell.Keys);
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            List<Vector2Int> clusterCells = new List<Vector2Int>();

            while (unvisited.Count > 0)
            {
                Vector2Int startCell = GetAnyCell(unvisited);
                BlockColor colorType = doorColorByCell[startCell];
                var edgeSide = doorSideByCell[startCell];

                queue.Clear();
                clusterCells.Clear();
                queue.Enqueue(startCell);
                unvisited.Remove(startCell);

                int minX = startCell.x;
                int maxX = startCell.x;
                int minY = startCell.y;
                int maxY = startCell.y;

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    clusterCells.Add(current);

                    if (current.x < minX) minX = current.x;
                    if (current.x > maxX) maxX = current.x;
                    if (current.y < minY) minY = current.y;
                    if (current.y > maxY) maxY = current.y;

                    for (int i = 0; i < _neighbors.Length; i++)
                    {
                        Vector2Int neighbor = current + _neighbors[i];
                        if (!unvisited.Contains(neighbor))
                        {
                            continue;
                        }

                        if (!doorColorByCell.TryGetValue(neighbor, out BlockColor neighborColor) || neighborColor != colorType)
                        {
                            continue;
                        }

                        if (!doorSideByCell.TryGetValue(neighbor, out var neighborSide) || neighborSide != edgeSide)
                        {
                            continue;
                        }

                        unvisited.Remove(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }

                DoorOpeningData opening = new DoorOpeningData
                {
                    colorType = colorType,
                    minCell = new Vector2Int(minX, minY),
                    maxCell = new Vector2Int(maxX, maxY),
                    edgeSide = edgeSide
                };

                int openingIndex = _openings.Count;
                _openings.Add(opening);

                for (int i = 0; i < clusterCells.Count; i++)
                {
                    _openingIndexByCell[clusterCells[i]] = openingIndex;
                }
            }
        }

        public bool TryGetOpening(Vector2Int cell, out DoorOpeningData openingData)
        {
            if (_openingIndexByCell.TryGetValue(cell, out int openingIndex))
            {
                openingData = _openings[openingIndex];
                return true;
            }

            openingData = default;
            return false;
        }

        private static Vector2Int GetAnyCell(HashSet<Vector2Int> cells)
        {
            foreach (Vector2Int cell in cells)
            {
                return cell;
            }

            return Vector2Int.zero;
        }

        public static bool TryCollectDoorCells(DoorData door, Vector2Int gridDimensions, List<Vector2Int> resultCells)
        {
            resultCells?.Clear();
            if (resultCells == null)
            {
                return false;
            }

            if (!TryGetDoorSide(door.position, gridDimensions, out var edgeSide))
            {
                return false;
            }

            if (IsCornerCell(door.position, gridDimensions))
            {
                return false;
            }

            if (!TryGetAxisRange(edgeSide, gridDimensions, out int axisMin, out int axisMax))
            {
                return false;
            }

            int openingWidth = Mathf.Max(1, door.openingWidth);
            int availableCells = (axisMax - axisMin) + 1;
            if (openingWidth > availableCells)
            {
                return false;
            }

            var verticalEdge = edgeSide.IsVertical();
            int anchorAxis = verticalEdge ? door.position.y : door.position.x;
            int startAxis = Mathf.Clamp(anchorAxis, axisMin, axisMax);
            int endAxis = startAxis + openingWidth - 1;
            if (endAxis > axisMax)
            {
                startAxis = axisMax - openingWidth + 1;
            }

            for (int i = 0; i < openingWidth; i++)
            {
                int axisValue = startAxis + i;
                Vector2Int cell = verticalEdge
                    ? new Vector2Int(door.position.x, axisValue)
                    : new Vector2Int(axisValue, door.position.y);
                resultCells.Add(cell);
            }

            return resultCells.Count > 0;
        }

        public static bool TryGetDoorSide(Vector2Int cell, Vector2Int gridDimensions, out EdgeSide edgeSide)
        {
            int maxX = gridDimensions.x - 1;
            int maxY = gridDimensions.y - 1;

            if (cell.x == 0)
            {
                edgeSide = EdgeSide.Left;
                return true;
            }

            if (cell.x == maxX)
            {
                edgeSide = EdgeSide.Right;
                return true;
            }

            if (cell.y == 0)
            {
                edgeSide = EdgeSide.Bottom;
                return true;
            }

            if (cell.y == maxY)
            {
                edgeSide = EdgeSide.Top;
                return true;
            }

            edgeSide = default;
            return false;
        }

        public static bool IsCornerCell(Vector2Int cell, Vector2Int gridDimensions)
        {
            bool left = cell.x == 0;
            bool right = cell.x == gridDimensions.x - 1;
            bool bottom = cell.y == 0;
            bool top = cell.y == gridDimensions.y - 1;
            return (left || right) && (bottom || top);
        }

        private static bool TryGetAxisRange(EdgeSide edgeSide, Vector2Int gridDimensions, out int axisMin, out int axisMax)
        {
            if (edgeSide.IsVertical())
            {
                axisMin = 1;
                axisMax = gridDimensions.y - 2;
                return axisMax >= axisMin;
            }

            if (edgeSide.IsHorizontal())
            {
                axisMin = 1;
                axisMax = gridDimensions.x - 2;
                return axisMax >= axisMin;
            }

            axisMin = 0;
            axisMax = -1;
            return false;
        }

    }
}
