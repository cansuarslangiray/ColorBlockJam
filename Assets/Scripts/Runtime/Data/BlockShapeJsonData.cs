using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Data
{
    [Serializable]
    public sealed class BlockShapeJsonData
    {
        public string shapeKey = string.Empty;
        public int width = 1;
        public int height = 1;
        public bool useCustomShape;
        public List<Vector2Int> customCells = new() { Vector2Int.zero };

        [NonSerialized] private Vector2Int[] _cachedCells = Array.Empty<Vector2Int>();
        [NonSerialized] private Vector2Int _cachedSize = Vector2Int.one;
        [NonSerialized] private bool _isCacheDirty = true;

        public string ShapeKey => string.IsNullOrWhiteSpace(shapeKey) ? string.Empty : shapeKey.Trim();

        public Vector2Int Size
        {
            get
            {
                EnsureCache();
                return _cachedSize;
            }
        }

        public Vector2Int[] GetLocalCells()
        {
            EnsureCache();
            return _cachedCells;
        }

        public void Sanitize()
        {
            shapeKey = string.IsNullOrWhiteSpace(shapeKey) ? string.Empty : shapeKey.Trim();
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            if (customCells == null)
            {
                customCells = new List<Vector2Int>();
            }

            if (customCells.Count == 0)
            {
                customCells.Add(Vector2Int.zero);
            }

            if (useCustomShape)
            {
                customCells = GetSanitizedCustomCells();
                var maxX = 0;
                var maxY = 0;
                for (var i = 0; i < customCells.Count; i++)
                {
                    var cell = customCells[i];
                    if (cell.x > maxX)
                    {
                        maxX = cell.x;
                    }

                    if (cell.y > maxY)
                    {
                        maxY = cell.y;
                    }
                }

                width = Mathf.Max(1, maxX + 1);
                height = Mathf.Max(1, maxY + 1);
            }
            else
            {
                customCells = new List<Vector2Int> { Vector2Int.zero };
            }

            _isCacheDirty = true;
        }

        private void EnsureCache()
        {
            if (!_isCacheDirty)
            {
                return;
            }

            if (useCustomShape)
            {
                BuildCustomCache();
            }
            else
            {
                BuildRectangleCache();
            }

            _isCacheDirty = false;
        }

        private void BuildRectangleCache()
        {
            var safeWidth = Mathf.Max(1, width);
            var safeHeight = Mathf.Max(1, height);

            _cachedSize = new Vector2Int(safeWidth, safeHeight);
            _cachedCells = new Vector2Int[safeWidth * safeHeight];

            var index = 0;
            for (var y = 0; y < safeHeight; y++)
            {
                for (var x = 0; x < safeWidth; x++)
                {
                    _cachedCells[index] = new Vector2Int(x, y);
                    index++;
                }
            }
        }

        private void BuildCustomCache()
        {
            List<Vector2Int> normalizedCells = GetSanitizedCustomCells();
            if (normalizedCells.Count == 0)
            {
                normalizedCells.Add(Vector2Int.zero);
            }

            customCells = normalizedCells;
            _cachedCells = normalizedCells.ToArray();

            var maxX = 0;
            var maxY = 0;
            foreach (var localCell in _cachedCells)
            {
                if (localCell.x > maxX)
                {
                    maxX = localCell.x;
                }

                if (localCell.y > maxY)
                {
                    maxY = localCell.y;
                }
            }

            _cachedSize = new Vector2Int(maxX + 1, maxY + 1);
        }

        private List<Vector2Int> GetSanitizedCustomCells()
        {
            var source = customCells ?? new List<Vector2Int>();
            if (source.Count == 0)
            {
                source.Add(Vector2Int.zero);
            }

            var uniqueCells = new HashSet<Vector2Int>();
            var minX = int.MaxValue;
            var minY = int.MaxValue;

            foreach (var cell in source)
            {
                uniqueCells.Add(cell);

                if (cell.x < minX)
                {
                    minX = cell.x;
                }

                if (cell.y < minY)
                {
                    minY = cell.y;
                }
            }

            if (uniqueCells.Count == 0)
            {
                uniqueCells.Add(Vector2Int.zero);
                minX = 0;
                minY = 0;
            }

            var normalizedCells = new List<Vector2Int>(uniqueCells.Count);
            foreach (Vector2Int cell in uniqueCells)
            {
                normalizedCells.Add(new Vector2Int(cell.x - minX, cell.y - minY));
            }

            normalizedCells.Sort((a, b) =>
            {
                var yCompare = a.y.CompareTo(b.y);
                return yCompare != 0 ? yCompare : a.x.CompareTo(b.x);
            });

            return normalizedCells;
        }
    }
}
