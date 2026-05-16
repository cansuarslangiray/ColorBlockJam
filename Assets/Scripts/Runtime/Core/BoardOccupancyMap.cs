using System;
using UnityEngine;

namespace Runtime.Core
{
    public class BoardOccupancyMap
    {
        private const int EmptyCell = -1;

        private int _width;
        private int _height;
        private int[] _cells = Array.Empty<int>();

        public void Configure(int boardWidth, int boardHeight)
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
            if (cellValue == EmptyCell)
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
