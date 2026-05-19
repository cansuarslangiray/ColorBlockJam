using System;
using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Pool
{
    [Serializable]
    public sealed class BlockTypePoolEntry
    {
        public string shapeKey = "Shape_1x1";
        public List<BlockPoolBindings> blockBindings = new();
        [HideInInspector] public List<GameObject> blockObjects = new();

        public string ResolvePoolKey()
        {
            if (!string.IsNullOrWhiteSpace(shapeKey))
            {
                return shapeKey.Trim();
            }

            return string.Empty;
        }
    }
}
