using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class PooledVisual
    {
        public PooledVisual(GameObject gameObject, Renderer renderer)
        {
            GameObject = gameObject;
            Transform = gameObject.transform;
            Renderer = renderer;
        }

        public GameObject GameObject { get; }
        public Transform Transform { get; }
        public Renderer Renderer { get; }
    }
}
