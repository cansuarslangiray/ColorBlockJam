using System;
using UnityEngine;

namespace Runtime.Flow
{
    [Serializable]
    public class BoardCameraFramingSettings
    {
        public bool forceOrthographicCamera;
        public bool resetCameraTilt;
        [Min(0f)] public float paddingInCells;
        public Vector2 centerOffsetInCells;
        [Range(0f, 60f)] public float perspectiveTiltDegrees;
        [Min(0.01f)] public float minimumOrthographicSize;
        [Min(0.01f)] public float minimumPerspectiveDistance;
        [Range(0.55f, 1.25f)] public float fitDistanceMultiplier;

        public static BoardCameraFramingSettings CreateDefault()
        {
            return new BoardCameraFramingSettings
            {
                forceOrthographicCamera = false,
                resetCameraTilt = false,
                paddingInCells = 0.9f,
                centerOffsetInCells = new Vector2(0f, 1.2f),
                perspectiveTiltDegrees = 60f,
                minimumOrthographicSize = 5f,
                minimumPerspectiveDistance = 14f,
                fitDistanceMultiplier = 0.85f
            };
        }
    }
}
