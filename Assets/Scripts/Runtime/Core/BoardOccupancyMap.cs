using System;
using System.Collections.Generic;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Core
{
    public class BoardOccupancyMap
    {
        private const int EmptyCell = -1;
        private const int BlockedCell = -2;

        private int _width;
        private int _height;
        private int[] _cells = new int[0];
        private bool[] _doorCells = new bool[0];

        public void Configure(int boardWidth, int boardHeight)
        {
            _width = Mathf.Max(1, boardWidth);
            _height = Mathf.Max(1, boardHeight);

            var total = _width * _height;
            if (_cells.Length != total)
            {
                _cells = new int[total];
            }
            if (_doorCells.Length != total)
            {
                _doorCells = new bool[total];
            }

            for (var i = 0; i < total; i++)
            {
                _cells[i] = EmptyCell;
                _doorCells[i] = false;
            }
        }

        public void MarkBlockedCells(IReadOnlyList<Vector2Int> blockedCells)
        {
            if (blockedCells == null || blockedCells.Count == 0)
            {
                return;
            }

            foreach (var blockedCell in blockedCells)
            {
                if (!IsInside(blockedCell.x, blockedCell.y))
                {
                    continue;
                }

                _cells[GetIndex(blockedCell.x, blockedCell.y)] = BlockedCell;
            }
        }

        public bool CanPlace(int blockId, Vector2Int anchorPosition, Vector2Int[] localCells)
        {
            if (localCells == null || localCells.Length == 0)
            {
                return false;
            }

            foreach (var cell in localCells)
            {
                var worldCell = anchorPosition + cell;
                if (!IsInside(worldCell))
                {
                    return false;
                }

                var index = GetIndex(worldCell.x, worldCell.y);
                var cellValue = _cells[index];
                if (cellValue != EmptyCell && cellValue != blockId)
                {
                    return false;
                }
            }

            return true;
        }

        public void FillBlock(int blockId, Vector2Int anchorPosition, Vector2Int[] localCells)
        {
            if (localCells == null || localCells.Length == 0)
            {
                return;
            }

            foreach (var cell in localCells)
            {
                var worldCell = anchorPosition + cell;
                if (!IsInside(worldCell))
                {
                    continue;
                }

                var index = GetIndex(worldCell.x, worldCell.y);
                if (_cells[index] != BlockedCell)
                {
                    _cells[index] = blockId;
                }
            }
        }

        public void RebuildDoorOverlap(IReadOnlyList<DoorOpeningData> doorOpenings)
        {
            var totalCellCount = _width * _height;
            if (_doorCells.Length != totalCellCount)
            {
                _doorCells = new bool[totalCellCount];
            }
            else if (totalCellCount > 0)
            {
                Array.Clear(_doorCells, 0, totalCellCount);
            }

            if (totalCellCount == 0 || doorOpenings == null || doorOpenings.Count == 0)
            {
                return;
            }

            for (var openingIndex = 0; openingIndex < doorOpenings.Count; openingIndex++)
            {
                var opening = doorOpenings[openingIndex];
                var minX = Mathf.Clamp(opening.MinCell.x, 0, _width - 1);
                var maxX = Mathf.Clamp(opening.MaxCell.x, 0, _width - 1);
                var minY = Mathf.Clamp(opening.MinCell.y, 0, _height - 1);
                var maxY = Mathf.Clamp(opening.MaxCell.y, 0, _height - 1);

                for (var y = minY; y <= maxY; y++)
                {
                    var rowStartIndex = y * _width;
                    for (var x = minX; x <= maxX; x++)
                    {
                        _doorCells[rowStartIndex + x] = true;
                    }
                }
            }
        }

        public bool IsDoorOverlapping(RuntimeBlockState block, Vector2Int anchorPosition)
        {
            if (_width == 0 || _height == 0 || block.LocalCells == null || block.LocalCells.Length == 0)
            {
                return false;
            }

            for (var cellIndex = 0; cellIndex < block.LocalCells.Length; cellIndex++)
            {
                var worldCell = anchorPosition + block.LocalCells[cellIndex];
                if (!IsInside(worldCell.x, worldCell.y))
                {
                    continue;
                }

                if (_doorCells[GetIndex(worldCell.x, worldCell.y)])
                {
                    return true;
                }
            }

            return false;
        }

        public void ClearBlock(int blockId, Vector2Int anchorPosition, Vector2Int[] localCells)
        {
            if (localCells == null || localCells.Length == 0)
            {
                return;
            }

            for (var i = 0; i < localCells.Length; i++)
            {
                Vector2Int worldCell = anchorPosition + localCells[i];
                if (!IsInside(worldCell))
                {
                    continue;
                }

                int index = GetIndex(worldCell.x, worldCell.y);
                if (_cells[index] == blockId)
                {
                    _cells[index] = EmptyCell;
                }
            }
        }

        public bool TryGetBlockAt(int x, int y, out int blockId)
        {
            blockId = EmptyCell;
            if (!IsInside(x, y))
            {
                return false;
            }

            var index = GetIndex(x, y);
            var cellValue = _cells[index];
            if (cellValue is EmptyCell or BlockedCell)
            {
                return false;
            }

            blockId = cellValue;
            return true;
        }

        private bool IsInside(Vector2Int cell)
        {
            return IsInside(cell.x, cell.y);
        }

        public bool IsInside(int x, int y)
        {
            return x >= 0 && y >= 0 && x < _width && y < _height;
        }

        private int GetIndex(int x, int y)
        {
            return y * _width + x;
        }
    }
}
