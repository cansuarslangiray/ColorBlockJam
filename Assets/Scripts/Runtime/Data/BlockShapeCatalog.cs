using System;
using System.Collections.Generic;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Data
{
    [CreateAssetMenu(fileName = "BlockShapeCatalog", menuName = "ColorBlockJam/Block Shape Catalog")]
    public sealed class BlockShapeCatalog : ScriptableObject
    {
        [SerializeField] private List<BlockShapeDefinition> shapes = new();

        [NonSerialized] private Dictionary<string, BlockShapeDefinition> _shapeByKey;
        [NonSerialized] private Dictionary<BlockShapeType, BlockShapeDefinition> _shapeByType;
        [NonSerialized] private bool _lookupReady;

        public IReadOnlyList<BlockShapeDefinition> Shapes => shapes;

        private void OnEnable()
        {
            _lookupReady = false;
        }

        private void OnValidate()
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
            return _shapeByKey.TryGetValue(shapeKey.Trim(), out shape) && shape != null;
        }

        public bool TryResolveByType(BlockShapeType blockShapeType, out BlockShapeDefinition shape)
        {
            shape = null;
            EnsureLookup();
            return _shapeByType.TryGetValue(blockShapeType, out shape) && shape != null;
        }

        public BlockShapeDefinition ResolveShape(string shapeKey, BlockShapeType fallbackType)
        {
            if (TryResolveShape(shapeKey, out var shape))
            {
                return shape;
            }

            if (fallbackType != BlockShapeType.Unknown && TryResolveByType(fallbackType, out shape))
            {
                return shape;
            }

            if (TryResolveByType(BlockShapeType.Shape1x1, out shape))
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
            _shapeByType = new Dictionary<BlockShapeType, BlockShapeDefinition>();

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

                    var shapeType = shape.BlockType;
                    if (!_shapeByType.ContainsKey(shapeType))
                    {
                        _shapeByType.Add(shapeType, shape);
                    }
                }
            }

            _lookupReady = true;
        }
    }
}
