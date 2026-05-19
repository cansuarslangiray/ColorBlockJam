using System;
using UnityEditor;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Pool
{
    [DisallowMultipleComponent]
    public sealed class DoorPoolBindings : MonoBehaviour
    {
        private const string PlacementAnchorPrefix = "__DoorPlacementAnchor_";

        [SerializeField] private Transform placementTransform;
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();

        public GameObject DoorObject => gameObject;
        public Transform PlacementTransform => placementTransform ? placementTransform : transform;
        public Renderer[] Renderers => renderers ?? Array.Empty<Renderer>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            RebuildBindings();
        }

        [ContextMenu("Rebuild Door Bindings")]
        private void RebuildBindings()
        {
            placementTransform = ResolvePlacementTransform();

            var rendererComponents = GetComponentsInChildren<Renderer>(true);
            renderers = rendererComponents ?? Array.Empty<Renderer>();
            EditorUtility.SetDirty(this);
        }
#endif

        private Transform ResolvePlacementTransform()
        {
            var doorTransform = transform;
            var existingParent = doorTransform.parent;
            if (existingParent &&
                existingParent.name.StartsWith(PlacementAnchorPrefix, StringComparison.Ordinal) &&
                existingParent.childCount == 1 &&
                existingParent.GetChild(0) == doorTransform)
            {
                return existingParent;
            }

            return doorTransform;
        }
    }
}
