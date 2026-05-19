using System;
using System.Collections.Generic;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Board
{
    public struct BoardVisualBuildRequest
    {
        public IReadOnlyDictionary<Vector2Int, GameObject> GridCellPoolByCell;
        public IReadOnlyList<GameObject> BlockedCellPool;
        public IReadOnlyList<Vector2Int> BlockedCells;
        public IReadOnlyList<GameObject> BorderObjects;
        public GameObject BackdropObject;
        public IReadOnlyList<GameObject> DoorPool;
        public IReadOnlyList<DoorOpeningData> Openings;
        public Vector2Int GridDimensions;
        public LayoutMetrics Layout;
        public float BoardBackdropZOffset;
        public float BlockedCellZOffset;
        public float DoorInsetInCells;
        public Action<GameObject, bool> SetActiveIfChanged;
        public Action<Transform, Vector3, Vector3> ApplyWorldTransform;
        public Func<int, GameObject, Transform> ResolveDoorPlacementTransform;
        public Action<int, bool> StopDoorMatchFxAtIndex;
        public Action<int, Transform> CacheDoorPlacementBaseLocalPosition;
        public Func<BlockColor, Material> ResolveMaterial;
        public Action<int, Material> ApplyDoorMaterialAtIndex;
        public Action<IReadOnlyList<DoorOpeningData>> CacheActiveDoorOpenings;
    }
}
