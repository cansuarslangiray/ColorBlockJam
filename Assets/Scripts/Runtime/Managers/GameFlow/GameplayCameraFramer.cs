using Runtime.Controllers;
using Runtime.Data;
using UnityEngine;

namespace Runtime.Managers.GameFlow
{
    internal sealed class GameplayCameraFramer
    {
        private readonly BoardController _boardController;
        private readonly Camera _gameplayCamera;

        public GameplayCameraFramer(BoardController boardController, Camera gameplayCamera)
        {
            _boardController = boardController;
            _gameplayCamera = gameplayCamera;
        }

        public void CenterToLevel(LevelJsonData levelData)
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
            var forwardZ = Mathf.Abs(forward.z) < 0.001f
                ? (forward.z >= 0f ? 0.001f : -0.001f)
                : forward.z;
            var distanceToBoardPlane = Mathf.Abs((cameraTransform.position.z - boardPlaneZ) / forwardZ);
            cameraTransform.position = new Vector3(center.x, center.y, boardPlaneZ) - (forward * distanceToBoardPlane);
        }
    }
}
