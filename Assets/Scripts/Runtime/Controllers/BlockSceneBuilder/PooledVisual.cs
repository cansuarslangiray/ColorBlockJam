using System;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class PooledVisual
    {
        public PooledVisual(GameObject gameObject, Renderer[] renderers)
        {
            GameObject = gameObject;
            Transform = gameObject ? gameObject.transform : null;
            Renderers = renderers ?? Array.Empty<Renderer>();
        }

        public GameObject GameObject { get; }
        public Transform Transform { get; }
        public Renderer[] Renderers { get; }
    }
}
