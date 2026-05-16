using Runtime.Data;
using UnityEngine;

namespace Runtime.Flow
{
    public static class BoardCameraFramer
    {
        public static bool TryFrame(Camera gameplayCamera, LevelData levelData, Vector2 boardOrigin, float boardCellSize, BoardCameraFramingSettings settings)
        {
            if (levelData == null)
            {
                return false;
            }

            var cellSize = Mathf.Max(0.01f, boardCellSize);
            var width = levelData.gridDimensions.x * cellSize;
            var height = levelData.gridDimensions.y * cellSize;
            var padding = Mathf.Max(0f, settings.paddingInCells * cellSize);
            var contentWidth = width + (padding * 2f);
            var contentHeight = height + (padding * 2f);
            var centerOffset = settings.centerOffsetInCells * cellSize;

            var center = boardOrigin + new Vector2((width * 0.5f) + centerOffset.x, (height * 0.5f) + centerOffset.y);
            var cameraTransform = gameplayCamera.transform;
            cameraTransform.position = new Vector3(center.x, center.y, cameraTransform.position.z);

            if (settings.resetCameraTilt)
            {
                cameraTransform.rotation = Quaternion.identity;
            }

            if (settings.forceOrthographicCamera)
            {
                gameplayCamera.orthographic = true;
                var aspect = Mathf.Max(0.01f, gameplayCamera.aspect);
                var halfHeight = contentHeight * 0.5f;
                var halfWidthAsHeight = (contentWidth * 0.5f) / aspect;
                gameplayCamera.orthographicSize = Mathf.Max(settings.minimumOrthographicSize, halfHeight,
                    halfWidthAsHeight);
                return true;
            }

            gameplayCamera.orthographic = false;
            if (!settings.resetCameraTilt)
            {
                var currentEuler = cameraTransform.eulerAngles;
                cameraTransform.rotation = Quaternion.Euler(-Mathf.Abs(settings.perspectiveTiltDegrees), currentEuler.y,
                    currentEuler.z);
            }

            var halfFovY = Mathf.Max(0.01f, gameplayCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var halfFovX = Mathf.Atan(Mathf.Tan(halfFovY) * Mathf.Max(0.01f, gameplayCamera.aspect));
            var distanceByHeight = (contentHeight * 0.5f) / Mathf.Tan(halfFovY);
            var distanceByWidth = (contentWidth * 0.5f) / Mathf.Tan(halfFovX);
            var requiredDistance = Mathf.Max(settings.minimumPerspectiveDistance, distanceByHeight, distanceByWidth);
            requiredDistance *= Mathf.Clamp(settings.fitDistanceMultiplier, 0.55f, 1.25f);
            var forward = cameraTransform.forward.normalized;
            cameraTransform.position = new Vector3(center.x, center.y, 0f) - (forward * requiredDistance);
            return true;
        }
    }
}
