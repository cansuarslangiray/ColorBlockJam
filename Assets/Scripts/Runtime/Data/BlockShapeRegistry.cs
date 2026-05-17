using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Data
{
    public sealed class BlockShapeRegistry
    {
        private readonly List<BlockShapeJsonData> _shapes;
        private readonly Dictionary<string, BlockShapeJsonData> _shapeByKey;

        public IReadOnlyList<BlockShapeJsonData> Shapes => _shapes;

        private BlockShapeRegistry(List<BlockShapeJsonData> shapes)
        {
            _shapes = shapes ?? new List<BlockShapeJsonData>();
            _shapeByKey = new Dictionary<string, BlockShapeJsonData>();

            for (var i = 0; i < _shapes.Count; i++)
            {
                var shape = _shapes[i];
                if (shape == null)
                {
                    continue;
                }

                shape.Sanitize();
                var key = shape.ShapeKey;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (_shapeByKey.ContainsKey(key))
                {
                    Debug.LogWarning($"Duplicate block shape key '{key}' detected. The first entry will be used.");
                    continue;
                }

                _shapeByKey.Add(key, shape);
            }
        }

        public static BlockShapeRegistry FromJsonAssets(IReadOnlyList<TextAsset> shapeJsonFiles)
        {
            var shapes = new List<BlockShapeJsonData>();
            if (shapeJsonFiles != null)
            {
                for (var i = 0; i < shapeJsonFiles.Count; i++)
                {
                    var file = shapeJsonFiles[i];
                    if (file == null)
                    {
                        continue;
                    }

                    var shape = BlockShapeJsonSerialization.Deserialize(file.text, file.name);
                    if (shape == null)
                    {
                        continue;
                    }

                    shapes.Add(shape);
                }
            }

            return new BlockShapeRegistry(shapes);
        }

        public string GetShapeKey(BlockShapeJsonData shape)
        {
            return shape != null ? shape.ShapeKey : string.Empty;
        }

        public bool TryResolveShape(string shapeKey, out BlockShapeJsonData shape)
        {
            shape = null;
            if (string.IsNullOrWhiteSpace(shapeKey))
            {
                return false;
            }

            return _shapeByKey.TryGetValue(shapeKey.Trim(), out shape) && shape != null;
        }
    }
}
