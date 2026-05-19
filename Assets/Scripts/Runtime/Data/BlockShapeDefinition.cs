using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Data
{
    [CreateAssetMenu(fileName = "BlockShapeDefinition", menuName = "ColorBlockJam/Block Shape Definition")]
    public sealed class BlockShapeDefinition : ScriptableObject
    {
        [SerializeField] private string shapeKey = "Shape_1x1";
        [SerializeField] private List<Vector2Int> localCells = new() { Vector2Int.zero };

        [NonSerialized] private Vector2Int[] _cachedLocalCells = Array.Empty<Vector2Int>();
        [NonSerialized] private bool _cacheDirty = true;

        public string ShapeKey => string.IsNullOrWhiteSpace(shapeKey) ? string.Empty : shapeKey.Trim();

        public Vector2Int[] GetLocalCells()
        {
            EnsureCache();
            return _cachedLocalCells;
        }

        public void ApplyImportedData(string nextShapeKey, IList<Vector2Int> nextLocalCells)
        {
            shapeKey = string.IsNullOrWhiteSpace(nextShapeKey) ? string.Empty : nextShapeKey.Trim();
            localCells = nextLocalCells != null ? new List<Vector2Int>(nextLocalCells) : new List<Vector2Int>();
            Sanitize();
        }

        private void OnValidate()
        {
            Sanitize();
        }

        public void Sanitize()
        {
            shapeKey = string.IsNullOrWhiteSpace(shapeKey) ? string.Empty : shapeKey.Trim();
            localCells ??= new List<Vector2Int>();

            if (localCells.Count == 0)
            {
                localCells.Add(Vector2Int.zero);
            }

            var unique = new HashSet<Vector2Int>(localCells);
            localCells.Clear();
            localCells.AddRange(unique);
            localCells.Sort((a, b) =>
            {
                var yCompare = a.y.CompareTo(b.y);
                return yCompare != 0 ? yCompare : a.x.CompareTo(b.x);
            });

            _cacheDirty = true;
        }

        private void EnsureCache()
        {
            if (!_cacheDirty)
            {
                return;
            }

            if (localCells == null || localCells.Count == 0)
            {
                _cachedLocalCells = new[] { Vector2Int.zero };
            }
            else
            {
                _cachedLocalCells = localCells.ToArray();
            }

            _cacheDirty = false;
        }
    }
}
