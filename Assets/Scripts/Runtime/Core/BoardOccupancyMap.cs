using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Core
{
    public class BoardOccupancyMap
    {
        private const int EmptyCell = -1;
        private const int BlockedCell = -2;

        private int _width;
        private int _height;
        private int[] _cells = Array.Empty<int>();

        public void Configure(int boardWidth, int boardHeight, List<Vector2Int> blockedCells)
        {
            _width = Mathf.Max(1, boardWidth);
            _height = Mathf.Max(1, boardHeight);

            var total = _width * _height;
            if (_cells.Length != total)
            {
                _cells = new int[total];
            }

            for (var i = 0; i < total; i++)
            {
                _cells[i] = EmptyCell;
            }

            if (blockedCells == null)
            {
                return;
            }

            for (var i = 0; i < blockedCells.Count; i++)
            {
                var cell = blockedCells[i];
                if (IsInside(cell))
                {
                    _cells[GetIndex(cell.x, cell.y)] = BlockedCell;
                }
            }
        }

        public bool CanPlace(int blockId, Vector2Int anchorPosition, Vector2Int[] localCells)
        {
            if (localCells == null || localCells.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < localCells.Length; i++)
            {
                var worldCell = anchorPosition + localCells[i];
                if (!IsInside(worldCell))
                {
                    return false;
                }

                int index = GetIndex(worldCell.x, worldCell.y);
                int cellValue = _cells[index];
                if (cellValue == BlockedCell)
                {
                    return false;
                }

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

            for (var i = 0; i < localCells.Length; i++)
            {
                Vector2Int worldCell = anchorPosition + localCells[i];
                if (!IsInside(worldCell))
                {
                    continue;
                }

                _cells[GetIndex(worldCell.x, worldCell.y)] = blockId;
            }
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

            if (cellValue is EmptyCell or BlockedCell) return false;
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
