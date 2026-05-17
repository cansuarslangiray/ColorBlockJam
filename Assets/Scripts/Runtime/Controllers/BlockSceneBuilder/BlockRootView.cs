using System.Collections.Generic;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class BlockRootView
    {
        public BlockRootView(GameObject rootObject)
        {
            RootObject = rootObject;
            RootTransform = rootObject.transform;
        }

        public GameObject RootObject { get; }
        public Transform RootTransform { get; }
        public BlockShapeType BlockType { get; set; }
        public List<GameObject> Cells { get; } = new();
        public Vector2 LocalCenter { get; set; }
    }
}
