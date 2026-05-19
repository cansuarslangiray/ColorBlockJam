using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Pool
{
    [DisallowMultipleComponent]
    public sealed class BlockPoolBindings : MonoBehaviour
    {
        private const string PlacementAnchorPrefix = "__BlockPlacementAnchor_";
        private const string ConditionIndicatorObjectName = "ConditionIndicator";
        private const string DragOutlineObjectName = "BlockDragOutline";

        [SerializeField] private Transform placementTransform;
        [SerializeField] private List<BlockPoolCellBinding> cellBindings = new();
        [SerializeField] private TextMesh conditionIndicatorText;
        [SerializeField] private LineRenderer dragOutlineRenderer;

        public GameObject RootObject => gameObject;
        public Transform PlacementTransform => placementTransform ? placementTransform : transform;
        public IReadOnlyList<BlockPoolCellBinding> CellBindings => cellBindings;
        public TextMesh ConditionIndicatorText => conditionIndicatorText;
        public LineRenderer DragOutlineRenderer => dragOutlineRenderer;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            RebuildBindings();
        }

        [ContextMenu("Rebuild Pool Bindings")]
        private void RebuildBindings()
        {
            placementTransform = ResolvePlacementTransform();

            cellBindings ??= new List<BlockPoolCellBinding>();
            cellBindings.Clear();

            var rootTransform = transform;
            var rootChildCount = rootTransform.childCount;
            for (var i = 0; i < rootChildCount; i++)
            {
                var child = rootTransform.GetChild(i);
                if (!child || !child.name.StartsWith("BlockCell_", StringComparison.Ordinal))
                {
                    continue;
                }

                var cellObject = child.gameObject;
                var nestedRenderers = cellObject.GetComponentsInChildren<Renderer>(true);
                var binding = new BlockPoolCellBinding
                {
                    cellObject = cellObject,
                    primaryRenderer = nestedRenderers != null && nestedRenderers.Length > 0 ? nestedRenderers[0] : null,
                    nestedRenderers = nestedRenderers ?? Array.Empty<Renderer>()
                };
                cellBindings.Add(binding);
            }

            conditionIndicatorText = null;
            var textMeshes = rootTransform.GetComponentsInChildren<TextMesh>(true);
            for (var i = 0; i < textMeshes.Length; i++)
            {
                var textMesh = textMeshes[i];
                if (!textMesh ||
                    !textMesh.gameObject ||
                    !string.Equals(textMesh.gameObject.name, ConditionIndicatorObjectName, StringComparison.Ordinal))
                {
                    continue;
                }

                conditionIndicatorText = textMesh;
                break;
            }

            dragOutlineRenderer = null;
            var lineRenderers = rootTransform.GetComponentsInChildren<LineRenderer>(true);
            for (var i = 0; i < lineRenderers.Length; i++)
            {
                var lineRenderer = lineRenderers[i];
                if (!lineRenderer ||
                    !lineRenderer.gameObject ||
                    !string.Equals(lineRenderer.gameObject.name, DragOutlineObjectName, StringComparison.Ordinal))
                {
                    continue;
                }

                dragOutlineRenderer = lineRenderer;
                break;
            }

            EditorUtility.SetDirty(this);
        }
#endif

        private Transform ResolvePlacementTransform()
        {
            var rootTransform = transform;
            var existingParent = rootTransform.parent;
            if (existingParent &&
                existingParent.name.StartsWith(PlacementAnchorPrefix, StringComparison.Ordinal) &&
                existingParent.childCount == 1 &&
                existingParent.GetChild(0) == rootTransform)
            {
                return existingParent;
            }

            return rootTransform;
        }
    }
}
