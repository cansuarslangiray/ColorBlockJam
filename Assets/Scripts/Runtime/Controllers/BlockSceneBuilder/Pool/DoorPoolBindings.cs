using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Pool
{
    [DisallowMultipleComponent]
    public sealed class DoorPoolBindings : MonoBehaviour
    {
        [SerializeField] private Transform placementTransform;
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();

        public GameObject DoorObject => gameObject;
        public Transform PlacementTransform => placementTransform;
        public Renderer[] Renderers => renderers;

#if UNITY_EDITOR
        [ContextMenu("Rebuild Door Bindings")]
        private void RebuildBindings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            placementTransform = transform;

            var rendererComponents = GetComponentsInChildren<Renderer>(true);
            renderers = rendererComponents ?? Array.Empty<Renderer>();
            EditorUtility.SetDirty(this);
        }

        public void EditorRebuildBindingsFromHierarchy()
        {
            RebuildBindings();
        }
#endif
    }
}
