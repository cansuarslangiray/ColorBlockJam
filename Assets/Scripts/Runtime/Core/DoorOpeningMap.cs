using System.Collections.Generic;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Core
{
    public abstract class DoorOpeningMap
    {
        private static readonly Vector2Int[] Neighbors =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        public static void BuildOpenings(List<DoorData> doors, Vector2Int gridDimensions,
            List<DoorOpeningData> resultOpenings)
        {
            if (resultOpenings == null)
            {
                return;
            }

            var doorColorByCell = new Dictionary<Vector2Int, BlockColor>();
            var doorDirectionByCell = new Dictionary<Vector2Int, Direction>();
            var expandedDoorCells = new List<Vector2Int>(8);
            var unvisited = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            var clusterCells = new List<Vector2Int>();

            BuildOpeningsInternal(doors, gridDimensions, resultOpenings, doorColorByCell, doorDirectionByCell,
                expandedDoorCells, unvisited, queue, clusterCells);
        }


        private static void BuildOpeningsInternal(IReadOnlyList<DoorData> doors, Vector2Int gridDimensions,
            List<DoorOpeningData> resultOpenings, Dictionary<Vector2Int, BlockColor> doorColorByCell,
            Dictionary<Vector2Int, Direction> doorDirectionByCell, List<Vector2Int> expandedDoorCells,
            HashSet<Vector2Int> unvisited, Queue<Vector2Int> queue, List<Vector2Int> clusterCells)
        {
            resultOpenings.Clear();
            doorColorByCell.Clear();
            doorDirectionByCell.Clear();
            expandedDoorCells.Clear();
            unvisited.Clear();
            queue.Clear();
            clusterCells.Clear();

            if (doors == null || doors.Count == 0)
            {
                return;
            }

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
                    if (!TryGetDoorDirection(cell, gridDimensions, out var edgeDirection))
                    {
                        continue;
                    }

                    doorColorByCell[cell] = door.colorType;
                    doorDirectionByCell[cell] = edgeDirection;
                }
            }

            if (doorColorByCell.Count == 0)
            {
                return;
            }

            unvisited.UnionWith(doorColorByCell.Keys);

            while (unvisited.Count > 0)
            {
                Vector2Int startCell = GetAnyCell(unvisited);
                BlockColor colorType = doorColorByCell[startCell];
                var edgeDirection = doorDirectionByCell[startCell];

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

                    for (int i = 0; i < Neighbors.Length; i++)
                    {
                        Vector2Int neighbor = current + Neighbors[i];
                        if (!unvisited.Contains(neighbor))
                        {
                            continue;
                        }

                        if (!doorColorByCell.TryGetValue(neighbor, out BlockColor neighborColor) ||
                            neighborColor != colorType)
                        {
                            continue;
                        }

                        if (!doorDirectionByCell.TryGetValue(neighbor, out var neighborDirection) ||
                            neighborDirection != edgeDirection)
                        {
                            continue;
                        }

                        unvisited.Remove(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }

                DoorOpeningData opening = new DoorOpeningData
                {
                    ColorType = colorType,
                    MinCell = new Vector2Int(minX, minY),
                    MaxCell = new Vector2Int(maxX, maxY),
                    EdgeDirection = edgeDirection
                };

                resultOpenings.Add(opening);
            }
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

            if (!TryGetDoorDirection(door.position, gridDimensions, out var edgeDirection))
            {
                return false;
            }

            if (IsCornerCell(door.position, gridDimensions))
            {
                return false;
            }

            if (!TryGetAxisRange(edgeDirection, gridDimensions, out int axisMin, out int axisMax))
            {
                return false;
            }

            int openingWidth = Mathf.Max(1, door.openingWidth);
            int availableCells = (axisMax - axisMin) + 1;
            if (openingWidth > availableCells)
            {
                return false;
            }

            var verticalEdge = edgeDirection.IsHorizontal();
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

        public static bool TryGetDoorDirection(Vector2Int cell, Vector2Int gridDimensions, out Direction edgeDirection)
        {
            int maxX = gridDimensions.x - 1;
            int maxY = gridDimensions.y - 1;

            if (cell.x == 0)
            {
                edgeDirection = Direction.Left;
                return true;
            }

            if (cell.x == maxX)
            {
                edgeDirection = Direction.Right;
                return true;
            }

            if (cell.y == 0)
            {
                edgeDirection = Direction.Down;
                return true;
            }

            if (cell.y == maxY)
            {
                edgeDirection = Direction.Up;
                return true;
            }

            edgeDirection = default;
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

        private static bool TryGetAxisRange(Direction edgeDirection, Vector2Int gridDimensions, out int axisMin,
            out int axisMax)
        {
            if (edgeDirection.IsHorizontal())
            {
                axisMin = 1;
                axisMax = gridDimensions.y - 2;
                return axisMax >= axisMin;
            }

            if (edgeDirection.IsVertical())
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