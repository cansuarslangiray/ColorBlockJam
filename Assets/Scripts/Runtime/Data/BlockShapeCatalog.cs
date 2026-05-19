using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Data
{
    [CreateAssetMenu(fileName = "BlockShapeCatalog", menuName = "ColorBlockJam/Block Shape Catalog")]
    public sealed class BlockShapeCatalog : ScriptableObject
    {
        private const string DefaultShapeKey = "Shape_1x1";

        [SerializeField] private List<BlockShapeDefinition> shapes = new();

        [NonSerialized] private Dictionary<string, BlockShapeDefinition> _shapeByKey;
        [NonSerialized] private bool _lookupReady;

        public IReadOnlyList<BlockShapeDefinition> Shapes => shapes;

        private void OnEnable()
        {
            _lookupReady = false;
        }

        public bool TryResolveShape(string shapeKey, out BlockShapeDefinition shape)
        {
            shape = null;
            if (string.IsNullOrWhiteSpace(shapeKey))
            {
                return false;
            }

            EnsureLookup();
            return _shapeByKey.TryGetValue(shapeKey.Trim(), out shape);
        }

        public BlockShapeDefinition ResolveShape(string shapeKey)
        {
            if (TryResolveShape(shapeKey, out var shape))
            {
                return shape;
            }

            if (TryResolveShape(DefaultShapeKey, out shape))
            {
                return shape;
            }

            return null;
        }

        public void SetShapes(List<BlockShapeDefinition> nextShapes)
        {
            shapes = nextShapes ?? new List<BlockShapeDefinition>();
            _lookupReady = false;
        }

        private void EnsureLookup()
        {
            if (_lookupReady)
            {
                return;
            }

            _shapeByKey = new Dictionary<string, BlockShapeDefinition>(StringComparer.Ordinal);
            if (shapes != null)
            {
                for (var i = 0; i < shapes.Count; i++)
                {
                    var shape = shapes[i];
                    if (!shape)
                    {
                        continue;
                    }

                    shape.Sanitize();
                    var shapeKey = shape.ShapeKey;
                    if (!string.IsNullOrWhiteSpace(shapeKey) && !_shapeByKey.ContainsKey(shapeKey))
                    {
                        _shapeByKey.Add(shapeKey, shape);
                    }
                }
            }

            _lookupReady = true;
        }
    }
}
