using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class BlockRootView
    {
        public BlockRootView(int blockId, GameObject rootObject)
        {
            BlockId = blockId;
            RootObject = rootObject;
            RootTransform = rootObject.transform;
        }

        public int BlockId { get; }
        public GameObject RootObject { get; }
        public Transform RootTransform { get; }
        public List<PooledVisual> Cells { get; } = new();
    }
}
