using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Pool
{
    [DisallowMultipleComponent]
    public sealed class BlockPoolBindings : MonoBehaviour
    {
        private const string ConditionIndicatorObjectName = "ConditionIndicator";
        private const string OutlineObjectName = "BlockOutline";
        private const string DoorExitParticleObjectName = "DoorExitParticle";

        [SerializeField] private Transform placementTransform;
        [SerializeField] private string shapeKey = string.Empty;
        [SerializeField] private List<Vector2Int> shapeLocalCells = new();
        [SerializeField] private List<BlockPoolCellBinding> cellBindings = new();
        [SerializeField] private TextMesh conditionIndicatorText;
        [SerializeField] private LineRenderer outlineRenderer;
        [SerializeField] private ParticleSystem doorExitParticle;
        
        public GameObject RootObject => gameObject;
        public Transform PlacementTransform => placementTransform;
        public string ShapeKey => string.IsNullOrWhiteSpace(shapeKey) ? string.Empty : shapeKey.Trim();
        public IReadOnlyList<Vector2Int> ShapeLocalCells => shapeLocalCells;
        public IReadOnlyList<BlockPoolCellBinding> CellBindings => cellBindings;
        public TextMesh ConditionIndicatorText => conditionIndicatorText;
        public LineRenderer OutlineRenderer => outlineRenderer;
        public ParticleSystem DoorExitParticle => doorExitParticle;

#if UNITY_EDITOR
        [ContextMenu("Rebuild Pool Bindings")]
        private void RebuildBindings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            placementTransform = transform;

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

            outlineRenderer = null;
            var lineRenderers = rootTransform.GetComponentsInChildren<LineRenderer>(true);
            for (var i = 0; i < lineRenderers.Length; i++)
            {
                var lineRenderer = lineRenderers[i];
                if (!lineRenderer || !lineRenderer.gameObject)
                {
                    continue;
                }

                if (string.Equals(lineRenderer.gameObject.name, OutlineObjectName, StringComparison.Ordinal))
                {
                    outlineRenderer = lineRenderer;
                    break;
                }
            }

            doorExitParticle = null;
            var particles = rootTransform.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                var particle = particles[i];
                if (!particle ||
                    !particle.gameObject ||
                    !string.Equals(particle.gameObject.name, DoorExitParticleObjectName, StringComparison.Ordinal))
                {
                    continue;
                }

                doorExitParticle = particle;
                break;
            }

            SanitizeShapeDefinition();
            EditorUtility.SetDirty(this);
        }

        public void EditorSetShapeDefinition(string nextShapeKey, IReadOnlyList<Vector2Int> nextLocalCells)
        {
            shapeKey = string.IsNullOrWhiteSpace(nextShapeKey) ? string.Empty : nextShapeKey.Trim();
            shapeLocalCells = nextLocalCells != null ? new List<Vector2Int>(nextLocalCells) : new List<Vector2Int>();
            SanitizeShapeDefinition();
            EditorUtility.SetDirty(this);
        }

        public void EditorRebuildBindingsFromHierarchy()
        {
            RebuildBindings();
        }
#endif

        private void OnValidate()
        {
            SanitizeShapeDefinition();
        }

        private void SanitizeShapeDefinition()
        {
            shapeKey = string.IsNullOrWhiteSpace(shapeKey) ? string.Empty : shapeKey.Trim();
            shapeLocalCells ??= new List<Vector2Int>();
            if (shapeLocalCells.Count == 0)
            {
                shapeLocalCells.Add(Vector2Int.zero);
            }

            var uniqueCells = new HashSet<Vector2Int>(shapeLocalCells);
            shapeLocalCells.Clear();
            shapeLocalCells.AddRange(uniqueCells);
            shapeLocalCells.Sort((left, right) =>
            {
                var yCompare = left.y.CompareTo(right.y);
                return yCompare != 0 ? yCompare : left.x.CompareTo(right.x);
            });
        }
    }
}
