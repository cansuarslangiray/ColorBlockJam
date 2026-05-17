using System;
using System.Collections.Generic;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    [Serializable]
    public sealed class BlockTypePoolEntry
    {
        public BlockShapeType blockType = BlockShapeType.Shape1x1;
        public GameObject blockPrefab;
        public List<GameObject> blockObjects = new(16);
    }
}
