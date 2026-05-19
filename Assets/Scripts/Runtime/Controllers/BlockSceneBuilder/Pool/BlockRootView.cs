using System;
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
        public string ShapeKey { get; set; } = string.Empty;
        public Vector2Int[] ShapeLocalCells { get; set; } = Array.Empty<Vector2Int>();
        public List<GameObject> Cells { get; } = new();
        public int ActiveCellCount { get; set; }
        public List<Renderer> CellRenderers { get; } = new();
        public List<Renderer[]> CellNestedRenderers { get; } = new();
        public List<Material> CellDefaultMaterials { get; } = new();
        public List<Material[]> CellNestedDefaultMaterials { get; } = new();
        public bool HasCachedBlockColor { get; set; }
        public Color CachedBlockColor { get; set; }
        public bool IsUsingLockedAppearance { get; set; }
        public Vector2 LocalCenter { get; set; }
        public GameObject ConditionIndicatorObject { get; set; }
        public TextMesh ConditionIndicatorText { get; set; }
        public Quaternion ConditionIndicatorDefaultLocalRotation { get; set; } = Quaternion.identity;
        public LineRenderer OutlineRenderer { get; set; }
        public bool HasCachedOutlineActiveColor { get; set; }
        public Color CachedOutlineActiveColor { get; set; }
        public bool HasAppliedOutlineColor { get; set; }
        public Color AppliedOutlineColor { get; set; }
        public ParticleSystem DoorExitBurstParticle { get; set; }
        public ParticleSystem[] DoorExitBurstParticles { get; set; } = Array.Empty<ParticleSystem>();
        public Renderer[] DoorExitBurstRenderers { get; set; } = Array.Empty<Renderer>();
    }
}
