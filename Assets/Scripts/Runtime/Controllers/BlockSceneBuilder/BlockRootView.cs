using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class BlockRootView
    {
        public BlockRootView(GameObject rootObject)
        {
            RootObject = rootObject;
            RootTransform = rootObject.transform;
            PlacementTransform = RootTransform;
        }

        public GameObject RootObject { get; }
        public Transform RootTransform { get; }
        public Transform PlacementTransform { get; set; }
        public string PoolKey { get; set; } = "Shape_1x1";
        public List<GameObject> Cells { get; } = new();
        public List<Renderer> CellRenderers { get; } = new();
        public List<Vector2Int> CachedOutlineGridLoop { get; } = new();
        public bool HasCachedLocalBounds { get; set; }
        public Vector3 CachedLocalBoundsMin { get; set; }
        public Vector3 CachedLocalBoundsMax { get; set; }
        public bool HasCachedBlockColor { get; set; }
        public Color CachedBlockColor { get; set; }
        public bool HasLoggedMissingBlockCells { get; set; }
        public bool HasLoggedMissingConditionIndicator { get; set; }
        public bool HasLoggedMissingDragOutline { get; set; }
        public Vector2 LocalCenter { get; set; }
        public Vector3 ConditionIndicatorLocalAnchor { get; set; }
        public GameObject ConditionIndicatorObject { get; set; }
        public TextMesh ConditionIndicatorText { get; set; }
        public LineRenderer DragOutlineRenderer { get; set; }
        public Animator Animator { get; set; }
        public ParticleSystem DoorExitBurstParticle { get; set; }
        public ParticleSystemRenderer DoorExitBurstRenderer { get; set; }
        public Coroutine DoorExitBurstCleanupRoutine { get; set; }
        public List<Transform> DoorPassThroughCellTransformsBuffer { get; } = new();
        public List<Vector3> DoorPassThroughInitialScalesBuffer { get; } = new();
        public List<Vector3> DoorPassThroughInitialPositionsBuffer { get; } = new();
        public List<Vector3> DoorPassThroughScatterDirectionBuffer { get; } = new();
        public List<Quaternion> DoorPassThroughInitialRotationsBuffer { get; } = new();
        public List<float> DoorPassThroughScatterRotationBuffer { get; } = new();
        public List<float> DoorPassThroughScatterDelayBuffer { get; } = new();
        public List<Renderer> DoorPassThroughCellRendererBuffer { get; } = new();
    }
}
