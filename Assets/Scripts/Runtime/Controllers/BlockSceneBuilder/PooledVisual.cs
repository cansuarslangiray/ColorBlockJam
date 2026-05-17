using System;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class PooledVisual
    {
        public PooledVisual(GameObject gameObject, Renderer renderer)
            : this(gameObject, renderer != null ? new[] { renderer } : null)
        {
        }

        public PooledVisual(GameObject gameObject, Renderer[] renderers)
        {
            GameObject = gameObject;
            Transform = gameObject ? gameObject.transform : null;
            Renderers = renderers ?? Array.Empty<Renderer>();
            Renderer = Renderers.Length > 0 ? Renderers[0] : null;
        }

        public GameObject GameObject { get; }
        public Transform Transform { get; }
        public Renderer Renderer { get; }
        public Renderer[] Renderers { get; }
    }
}
