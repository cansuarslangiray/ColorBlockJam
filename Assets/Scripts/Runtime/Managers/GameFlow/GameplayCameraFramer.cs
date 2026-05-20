using System.Collections;
using Runtime.Controllers;
using Runtime.Data;
using UnityEngine;

namespace Runtime.Managers.GameFlow
{
    internal sealed class GameplayCameraFramer
    {
        private readonly BoardController _boardController;
        private readonly Camera _gameplayCamera;
        private readonly MonoBehaviour _coroutineHost;
        private readonly int _closeDistanceLevelCount;
        private readonly float _closeDistanceMultiplier;
        private readonly float _baseDistanceToBoardPlane;
        private readonly float _minimumCameraDistance;
        private readonly float _baseOrthographicSize;
        private readonly float _boundsPaddingInCells;
        private readonly float _safeViewportMargin;
        private readonly float _transitionDuration;
        private readonly float _minimumTransitionDuration;
        private Coroutine _transitionRoutine;

        private readonly struct CameraFrame
        {
            public CameraFrame(Vector3 position, float orthographicSize)
            {
                Position = position;
                OrthographicSize = orthographicSize;
            }

            public Vector3 Position { get; }
            public float OrthographicSize { get; }
        }

        public GameplayCameraFramer(BoardController boardController, Camera gameplayCamera, int closeDistanceLevelCount,
            float closeDistanceMultiplier, MonoBehaviour coroutineHost, float boundsPaddingInCells,
            float safeViewportMargin, float transitionDuration)
        {
            _boardController = boardController;
            _gameplayCamera = gameplayCamera;
            _coroutineHost = coroutineHost;
            _closeDistanceLevelCount = Mathf.Max(0, closeDistanceLevelCount);
            _closeDistanceMultiplier = Mathf.Max(0.01f, closeDistanceMultiplier);
            _boundsPaddingInCells = Mathf.Max(0f, boundsPaddingInCells);
            _safeViewportMargin = Mathf.Clamp(safeViewportMargin, 0f, 0.45f);
            _transitionDuration = Mathf.Max(0f, transitionDuration);
            _minimumCameraDistance = 0.1f;
            _minimumTransitionDuration = 0.01f;

            _baseDistanceToBoardPlane = ResolveDistanceToBoardPlane();
            _baseOrthographicSize = gameplayCamera ? Mathf.Max(0.01f, gameplayCamera.orthographicSize) : 5f;
        }

        public void CenterToLevel(LevelDefinition levelData, int levelDisplayNumber, bool snapInstantly)
        {
            if (!_boardController || !_gameplayCamera)
            {
                return;
            }

            var targetFrame = ResolveTargetFrame(levelData, levelDisplayNumber);
            if (snapInstantly || _transitionDuration <= 0f || !_coroutineHost || !Application.isPlaying)
            {
                StopTransition();
                ApplyFrame(targetFrame);
                return;
            }

            StopTransition();
            _transitionRoutine = _coroutineHost.StartCoroutine(TransitionToFrameRoutine(targetFrame));
        }

        public void StopTransition()
        {
            if (_transitionRoutine == null)
            {
                return;
            }

            if (_coroutineHost)
            {
                _coroutineHost.StopCoroutine(_transitionRoutine);
            }

            _transitionRoutine = null;
        }

        private IEnumerator TransitionToFrameRoutine(CameraFrame targetFrame)
        {
            var cameraTransform = _gameplayCamera.transform;
            var startPosition = cameraTransform.position;
            var startOrthoSize = _gameplayCamera.orthographicSize;
            var duration = Mathf.Max(_minimumTransitionDuration, _transitionDuration);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - (2f * t));

                cameraTransform.position = Vector3.Lerp(startPosition, targetFrame.Position, t);
                if (_gameplayCamera.orthographic)
                {
                    _gameplayCamera.orthographicSize = Mathf.Lerp(startOrthoSize, targetFrame.OrthographicSize, t);
                }

                yield return null;
            }

            ApplyFrame(targetFrame);
            _transitionRoutine = null;
        }

        private CameraFrame ResolveTargetFrame(LevelDefinition levelData, int levelDisplayNumber)
        {
            var cellSize = Mathf.Max(0.01f, _boardController.CellSize);
            var bounds = ResolveGridBounds(levelData != null ? levelData.gridDimensions : Vector2Int.one, cellSize);
            var boardPlaneZ = _boardController.transform.position.z;
            var focusPoint = new Vector3(bounds.center.x, bounds.center.y, boardPlaneZ);
            var baselineDistance = ResolveBaselineDistance(levelDisplayNumber);
            var forward = _gameplayCamera.transform.forward.normalized;

            if (_gameplayCamera.orthographic)
            {
                var baselineOrtho = ResolveBaselineOrthoSize(levelDisplayNumber);
                var fitOrtho = ResolveOrthographicFitSize(bounds);
                return new CameraFrame(focusPoint - (forward * baselineDistance), Mathf.Max(baselineOrtho, fitOrtho));
            }

            var fitDistance = ResolvePerspectiveFitDistance(bounds, focusPoint);
            var targetDistance = Mathf.Max(_minimumCameraDistance, baselineDistance, fitDistance);
            return new CameraFrame(focusPoint - (forward * targetDistance), _gameplayCamera.orthographicSize);
        }

        private void ApplyFrame(CameraFrame frame)
        {
            var cameraTransform = _gameplayCamera.transform;
            cameraTransform.position = frame.Position;
            if (_gameplayCamera.orthographic)
            {
                _gameplayCamera.orthographicSize = frame.OrthographicSize;
            }
        }

        private Rect ResolveGridBounds(Vector2Int gridDimensions, float cellSize)
        {
            var resolvedWidth = Mathf.Max(1, gridDimensions.x);
            var resolvedHeight = Mathf.Max(1, gridDimensions.y);

            var left = _boardController.BoardOrigin.x;
            var bottom = _boardController.BoardOrigin.y;
            var right = left + (resolvedWidth * cellSize);
            var top = bottom + (resolvedHeight * cellSize);

            var padding = _boundsPaddingInCells * cellSize;
            left -= padding;
            right += padding;
            bottom -= padding;
            top += padding;

            return Rect.MinMaxRect(left, bottom, right, top);
        }

        private float ResolvePerspectiveFitDistance(Rect bounds, Vector3 focusPoint)
        {
            var cameraTransform = _gameplayCamera.transform;
            var right = cameraTransform.right.normalized;
            var up = cameraTransform.up.normalized;
            var forward = cameraTransform.forward.normalized;

            var halfVerticalFovRadians = _gameplayCamera.fieldOfView * Mathf.Deg2Rad * 0.5f;
            var tanHalfVertical = Mathf.Max(0.0001f, Mathf.Tan(halfVerticalFovRadians));
            var tanHalfHorizontal = Mathf.Max(0.0001f, tanHalfVertical * Mathf.Max(0.01f, _gameplayCamera.aspect));

            var safeExtentScale = Mathf.Max(0.05f, 1f - (_safeViewportMargin * 2f));
            var horizontalScale = tanHalfHorizontal * safeExtentScale;
            var verticalScale = tanHalfVertical * safeExtentScale;

            var requiredDistance = _minimumCameraDistance;
            EvaluateCorner(bounds.xMin, bounds.yMin);
            EvaluateCorner(bounds.xMin, bounds.yMax);
            EvaluateCorner(bounds.xMax, bounds.yMin);
            EvaluateCorner(bounds.xMax, bounds.yMax);

            return requiredDistance;

            void EvaluateCorner(float x, float y)
            {
                var corner = new Vector3(x, y, focusPoint.z);
                var offset = corner - focusPoint;
                var offsetForward = Vector3.Dot(offset, forward);
                var requiredByHorizontal = Mathf.Abs(Vector3.Dot(offset, right)) / horizontalScale - offsetForward;
                var requiredByVertical = Mathf.Abs(Vector3.Dot(offset, up)) / verticalScale - offsetForward;
                requiredDistance = Mathf.Max(requiredDistance, requiredByHorizontal, requiredByVertical);
            }
        }

        private float ResolveOrthographicFitSize(Rect bounds)
        {
            var cameraTransform = _gameplayCamera.transform;
            var right = cameraTransform.right.normalized;
            var up = cameraTransform.up.normalized;
            var safeExtentScale = Mathf.Max(0.05f, 1f - (_safeViewportMargin * 2f));
            var halfHeight = 0f;
            var halfWidth = 0f;

            EvaluateCorner(bounds.xMin, bounds.yMin);
            EvaluateCorner(bounds.xMin, bounds.yMax);
            EvaluateCorner(bounds.xMax, bounds.yMin);
            EvaluateCorner(bounds.xMax, bounds.yMax);

            var aspect = Mathf.Max(0.01f, _gameplayCamera.aspect);
            var requiredFromWidth = halfWidth / (aspect * safeExtentScale);
            var requiredFromHeight = halfHeight / safeExtentScale;
            return Mathf.Max(0.01f, requiredFromWidth, requiredFromHeight);

            void EvaluateCorner(float x, float y)
            {
                var offset = new Vector3(x - bounds.center.x, y - bounds.center.y, 0f);
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(offset, right)));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(offset, up)));
            }
        }

        private float ResolveBaselineDistance(int levelDisplayNumber)
        {
            var hasCloseDistance = _closeDistanceLevelCount > 0 && levelDisplayNumber <= _closeDistanceLevelCount;
            var multiplier = hasCloseDistance ? _closeDistanceMultiplier : 1f;
            return Mathf.Max(_minimumCameraDistance, _baseDistanceToBoardPlane * multiplier);
        }

        private float ResolveBaselineOrthoSize(int levelDisplayNumber)
        {
            var hasCloseDistance = _closeDistanceLevelCount > 0 && levelDisplayNumber <= _closeDistanceLevelCount;
            var multiplier = hasCloseDistance ? _closeDistanceMultiplier : 1f;
            return Mathf.Max(0.01f, _baseOrthographicSize * multiplier);
        }

        private float ResolveDistanceToBoardPlane()
        {
            if (!_boardController || !_gameplayCamera)
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
