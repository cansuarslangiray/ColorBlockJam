using System;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Pool
{
    [Serializable]
    public sealed class BlockPoolCellBinding
    {
        public GameObject cellObject;
        public Renderer primaryRenderer;
        public Renderer[] nestedRenderers = Array.Empty<Renderer>();
    }
}
