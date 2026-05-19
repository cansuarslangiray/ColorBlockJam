using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Doors
{
    internal sealed class DoorRuntimeBinding
    {
        public GameObject DoorObject;
        public Transform PlacementTransform;
        public Renderer[] Renderers = System.Array.Empty<Renderer>();
        public Vector3 BaseLocalPosition;
        public Coroutine MatchFxRoutine;
        public DoorOpeningData Opening;
        public bool HasOpening;
    }
}
