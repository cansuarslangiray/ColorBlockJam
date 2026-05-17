using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Data
{
    public static class BlockShapeJsonSerialization
    {
        public static string Serialize(BlockShapeJsonData shape, bool prettyPrint = true)
        {
            if (shape == null)
            {
                return JsonUtility.ToJson(new BlockShapeJsonData(), prettyPrint);
            }

            shape.Sanitize();
            var model = new BlockShapeJsonData
            {
                shapeKey = shape.ShapeKey,
                width = shape.width,
                height = shape.height,
                useCustomShape = shape.useCustomShape,
                customCells = shape.customCells != null
                    ? new List<Vector2Int>(shape.customCells)
                    : new List<Vector2Int> { Vector2Int.zero }
            };

            return JsonUtility.ToJson(model, prettyPrint);
        }

        public static BlockShapeJsonData Deserialize(string json, string fallbackShapeKey)
        {
            BlockShapeJsonData model = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    model = JsonUtility.FromJson<BlockShapeJsonData>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to parse block shape json '{fallbackShapeKey}': {ex.Message}");
                }
            }

            model ??= new BlockShapeJsonData();

            var shape = new BlockShapeJsonData
            {
                shapeKey = !string.IsNullOrWhiteSpace(model.shapeKey)
                    ? model.shapeKey.Trim()
                    : (string.IsNullOrWhiteSpace(fallbackShapeKey) ? string.Empty : fallbackShapeKey.Trim()),
                width = Mathf.Max(1, model.width),
                height = Mathf.Max(1, model.height),
                useCustomShape = model.useCustomShape,
                customCells = model.customCells != null
                    ? new List<Vector2Int>(model.customCells)
                    : new List<Vector2Int> { Vector2Int.zero }
            };

            shape.Sanitize();
            return shape;
        }
    }
}
