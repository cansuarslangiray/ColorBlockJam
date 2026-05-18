using Runtime.Controllers;
using Runtime.Data;
using UnityEngine;

namespace Runtime.Managers.GameFlow
{
    internal sealed class GameplayCameraFramer
    {
        private readonly BoardController _boardController;
        private readonly Camera _gameplayCamera;
        private readonly int _closeDistanceLevelCount;
        private readonly float _closeDistanceMultiplier;
        private readonly float _baseDistanceToBoardPlane;
        private readonly float _minimumCameraDistance;

        public GameplayCameraFramer(BoardController boardController, Camera gameplayCamera, int closeDistanceLevelCount,
            float closeDistanceMultiplier)
        {
            _boardController = boardController;
            _gameplayCamera = gameplayCamera;
            _closeDistanceLevelCount = Mathf.Max(0, closeDistanceLevelCount);
            _closeDistanceMultiplier = Mathf.Max(0.01f, closeDistanceMultiplier);
            _minimumCameraDistance = 0.1f;

            _baseDistanceToBoardPlane = ResolveDistanceToBoardPlane();
        }

        public void CenterToLevel(LevelDefinition levelData, int levelDisplayNumber)
        {
            if (!_gameplayCamera || _boardController == null || levelData == null)
            {
                return;
            }

            var cellSize = Mathf.Max(0.01f, _boardController.CellSize);
            var width = Mathf.Max(0, levelData.gridDimensions.x) * cellSize;
            var height = Mathf.Max(0, levelData.gridDimensions.y) * cellSize;
            var center = _boardController.BoardOrigin + new Vector2(width * 0.5f, height * 0.5f);

            var cameraTransform = _gameplayCamera.transform;
            var forward = cameraTransform.forward.normalized;
            var boardPlaneZ = _boardController.transform.position.z;
            var targetDistance = ResolveTargetDistance(levelDisplayNumber);
            cameraTransform.position = new Vector3(center.x, center.y, boardPlaneZ) - (forward * targetDistance);
        }

        private float ResolveTargetDistance(int levelDisplayNumber)
        {
            var hasCloseDistance = _closeDistanceLevelCount > 0 && levelDisplayNumber <= _closeDistanceLevelCount;
            if (hasCloseDistance)
            {
                return Mathf.Max(_minimumCameraDistance, _baseDistanceToBoardPlane * _closeDistanceMultiplier);
            }

            return _baseDistanceToBoardPlane;
        }

        private float ResolveDistanceToBoardPlane()
        {
            if (!_gameplayCamera || _boardController == null)
            {
                return _minimumCameraDistance;
            }

            var cameraTransform = _gameplayCamera.transform;
            var boardPlaneZ = _boardController.transform.position.z;
            var forwardZ = ResolveForwardZ(cameraTransform.forward.z);
            var resolvedDistance = Mathf.Abs((cameraTransform.position.z - boardPlaneZ) / forwardZ);

            return Mathf.Max(_minimumCameraDistance, resolvedDistance);
        }

        private static float ResolveForwardZ(float z)
        {
            if (Mathf.Abs(z) >= 0.001f)
            {
                return z;
            }

            return z >= 0f ? 0.001f : -0.001f;
        }
    }
}
