using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Animations
{
    internal readonly struct DoorPassThroughMotion
    {
        public DoorPassThroughMotion(Transform placementTransform, Vector3 startPosition, Vector3 endPosition,
            float travelDuration, float burstAt)
        {
            PlacementTransform = placementTransform;
            StartPosition = startPosition;
            EndPosition = endPosition;
            TravelDuration = travelDuration;
            BurstAt = burstAt;
        }

        public Transform PlacementTransform { get; }
        public Vector3 StartPosition { get; }
        public Vector3 EndPosition { get; }
        public float TravelDuration { get; }
        public float BurstAt { get; }
    }
}
