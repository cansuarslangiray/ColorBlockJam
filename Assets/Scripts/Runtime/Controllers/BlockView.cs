using System;
using System.Collections;
using UnityEngine;

namespace Runtime.Controllers
{
    public class BlockView : MonoBehaviour
    {
        [SerializeField] private int blockId;
        [SerializeField] private bool useSmoothMovement= true;
        [SerializeField, Min(0f)] private float movementSmoothingSpeed= 18f;
        [SerializeField, Min(0.05f)] private float doorExitDuration= 0.32f;
        [SerializeField, Min(0.2f)] private float doorExitTravelInCells= 1.15f;
        [SerializeField] private AnimationCurve doorExitMoveCurve= AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve doorExitScaleCurve= AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField, Range(0f, 1f)] private float doorExitMinScaleMultiplier= 0.05f;

        private Vector3 _targetWorldPosition;
        private bool _hasTargetWorldPosition;
        private bool _isDoorExitAnimating;
        private Coroutine _doorExitRoutine;
        private Vector3 _baseLocalScale;

        public int BlockId => blockId;

        private void Awake()
        {
            _baseLocalScale = transform.localScale;
        }

        public void SetBlockId(int id)
        {
            blockId = id;
        }

        public void ConfigureMovementSmoothing(float smoothingSpeed)
        {
            movementSmoothingSpeed = Mathf.Max(0f, smoothingSpeed);
            useSmoothMovement = movementSmoothingSpeed > 0f;
        }

        private void OnEnable()
        {
            if (_isDoorExitAnimating)
            {
                return;
            }

            transform.localScale = _baseLocalScale;
        }

        private void OnDisable()
        {
            if (_doorExitRoutine != null)
            {
                StopCoroutine(_doorExitRoutine);
                _doorExitRoutine = null;
            }

            _isDoorExitAnimating = false;
            transform.localScale = _baseLocalScale;
        }

        public void SetGridPosition(Vector2Int gridPosition, float cellSize, Vector2 boardOrigin)
        {
            var worldPosition = new Vector3(boardOrigin.x + (gridPosition.x * cellSize), boardOrigin.y + (gridPosition.y * cellSize), transform.position.z);

            _targetWorldPosition = worldPosition;
            if (!useSmoothMovement || !_hasTargetWorldPosition)
            {
                transform.position = worldPosition;
            }

            _hasTargetWorldPosition = true;
        }

        public void SnapToGridPosition(Vector2Int gridPosition, float cellSize, Vector2 boardOrigin)
        {
            var worldPosition = new Vector3(boardOrigin.x + (gridPosition.x * cellSize), boardOrigin.y + (gridPosition.y * cellSize), transform.position.z);

            _targetWorldPosition = worldPosition;
            _hasTargetWorldPosition = true;
            transform.position = worldPosition;
        }

        public void PlayDoorExitAnimation(Vector2Int exitDirection, float cellSize, Action onCompleted)
        {
            if (exitDirection == Vector2Int.zero || cellSize <= 0f)
            {
                onCompleted?.Invoke();
                return;
            }

            if (_doorExitRoutine != null)
            {
                StopCoroutine(_doorExitRoutine);
            }

            _doorExitRoutine = StartCoroutine(PlayDoorExitAnimationRoutine(exitDirection, cellSize, onCompleted));
        }

        private void LateUpdate()
        {
            if (_isDoorExitAnimating || !useSmoothMovement || !_hasTargetWorldPosition)
            {
                return;
            }

            var interpolationFactor = 1f - Mathf.Exp(-movementSmoothingSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, _targetWorldPosition, interpolationFactor);
        }

        private IEnumerator PlayDoorExitAnimationRoutine(Vector2Int exitDirection, float cellSize, Action onCompleted)
        {
            _isDoorExitAnimating = true;
            transform.localScale = _baseLocalScale;

            var startPosition = transform.position;
            var exitOffset = new Vector3(exitDirection.x, exitDirection.y, 0f) * (Mathf.Max(0.2f, doorExitTravelInCells) * cellSize);
            var targetPosition = startPosition + exitOffset;
            var duration = Mathf.Max(0.05f, doorExitDuration);
            var elapsed = 0f;
            var minScaleMultiplier = Mathf.Clamp01(doorExitMinScaleMultiplier);
            var minScale = _baseLocalScale * minScaleMultiplier;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var moveLerp = doorExitMoveCurve != null ? Mathf.Clamp01(doorExitMoveCurve.Evaluate(normalized)) : normalized;

                transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, moveLerp);
                var scaleLerp = doorExitScaleCurve != null ? Mathf.Clamp01(doorExitScaleCurve.Evaluate(normalized)) : 1f - normalized;
                transform.localScale = Vector3.LerpUnclamped(minScale, _baseLocalScale, scaleLerp);

                yield return null;
            }

            transform.localScale = minScale;
            _isDoorExitAnimating = false;
            _doorExitRoutine = null;
            onCompleted?.Invoke();
        }
    }
}
