using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Pool
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
        public List<Renderer[]> CellNestedRenderers { get; } = new();
        public List<Material> CellDefaultMaterials { get; } = new();
        public List<Material[]> CellNestedDefaultMaterials { get; } = new();
        public bool HasCachedBlockColor { get; set; }
        public Color CachedBlockColor { get; set; }
        public bool IsUsingLockedAppearance { get; set; }
        public Vector2 LocalCenter { get; set; }
        public GameObject PooledConditionIndicatorObject { get; set; }
        public TextMesh PooledConditionIndicatorText { get; set; }
        public GameObject ConditionIndicatorObject { get; set; }
        public TextMesh ConditionIndicatorText { get; set; }
        public Quaternion ConditionIndicatorDefaultLocalRotation { get; set; } = Quaternion.identity;
        public LineRenderer PooledDragOutlineRenderer { get; set; }
        public LineRenderer DragOutlineRenderer { get; set; }
        public ParticleSystem PooledDoorExitBurstParticle { get; set; }
        public ParticleSystemRenderer PooledDoorExitBurstRenderer { get; set; }
    }
}
