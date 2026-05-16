using System;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Controllers
{
    [DisallowMultipleComponent]
    public class BoardInputController : MonoBehaviour
    {
        [SerializeField] private BoardController boardController;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private float inputPlaneZ;
        [SerializeField, Min(1f)] private float dragThresholdPixels = 25f;
        [SerializeField] private bool allowContinuousStepDrag = true;

        public static event Action OnScreenTapped;

        private int _activeBlockId = -1;
        private Vector2 _lastDragPosition;
        private Plane _inputPlane;

        private void Awake()
        {
            RefreshInputPlane();
        }

        private void OnValidate()
        {
            RefreshInputPlane();
        }

        private void OnDisable()
        {
            ResetDragState();
        }

        private void Update()
        {
            if (!TryReadPrimaryPointer(out var pointer))
            {
                return;
            }

            if (pointer.DownThisFrame)
            {
                HandlePointerDown(pointer.Position);
            }

            if (pointer.IsPressed)
            {
                HandlePointerDrag(pointer.Position);
            }

            if (pointer.UpThisFrame)
            {
                HandlePointerUp();
            }
        }

        private void HandlePointerDown(Vector2 pointerPosition)
        {
            OnScreenTapped?.Invoke();
            ResetDragState();

            if (boardController == null || !boardController.IsInitialized)
            {
                return;
            }

            if (!TryResolveBoardPoint(pointerPosition, out var worldPos))
            {
                return;
            }

            if (boardController.TryWorldToCell(worldPos, out var touchedCell) &&
                boardController.TryGetBlockAtCell(touchedCell, out var blockId))
            {
                _activeBlockId = blockId;
                _lastDragPosition = pointerPosition;
            }
        }

        private void HandlePointerDrag(Vector2 pointerPosition)
        {
            if (_activeBlockId < 0 || boardController == null)
            {
                return;
            }

            var delta = pointerPosition - _lastDragPosition;
            var absoluteX = Mathf.Abs(delta.x);
            var absoluteY = Mathf.Abs(delta.y);
            var isHorizontalDrag = absoluteX >= absoluteY;
            var dominantDistance = isHorizontalDrag ? absoluteX : absoluteY;

            if (dominantDistance < dragThresholdPixels)
            {
                return;
            }

            var direction = isHorizontalDrag
                ? (delta.x > 0f ? Direction.Right : Direction.Left)
                : (delta.y > 0f ? Direction.Up : Direction.Down);

            var moveAttemptCount = allowContinuousStepDrag
                ? Mathf.Max(1, Mathf.FloorToInt(dominantDistance / dragThresholdPixels))
                : 1;

            var movedAnyStep = false;
            for (var i = 0; i < moveAttemptCount; i++)
            {
                if (!boardController.TryMoveBlock(_activeBlockId, direction))
                {
                    break;
                }

                movedAnyStep = true;
            }

            if (allowContinuousStepDrag && movedAnyStep)
            {
                var consumedDelta = isHorizontalDrag
                    ? new Vector2(Mathf.Sign(delta.x) * (moveAttemptCount * dragThresholdPixels), 0f)
                    : new Vector2(0f, Mathf.Sign(delta.y) * (moveAttemptCount * dragThresholdPixels));
                _lastDragPosition += consumedDelta;
            }
            else
            {
                _lastDragPosition = pointerPosition;
            }
        }

        private void HandlePointerUp()
        {
            ResetDragState();
        }

        private bool TryResolveBoardPoint(Vector2 screenPosition, out Vector2 boardWorldPoint)
        {
            var cameraToUse = inputCamera != null ? inputCamera : Camera.main;
            if (cameraToUse == null)
            {
                boardWorldPoint = default;
                return false;
            }

            var ray = cameraToUse.ScreenPointToRay(screenPosition);
            if (!_inputPlane.Raycast(ray, out var hitDistance))
            {
                boardWorldPoint = default;
                return false;
            }

            var hitPoint = ray.GetPoint(hitDistance);
            boardWorldPoint = new Vector2(hitPoint.x, hitPoint.y);
            return true;
        }

        private void RefreshInputPlane()
        {
            _inputPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, inputPlaneZ));
        }

        private void ResetDragState()
        {
            _activeBlockId = -1;
            _lastDragPosition = Vector2.zero;
        }

        private static bool TryReadPrimaryPointer(out PointerSample pointer)
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                pointer = default;
                pointer.Position = touch.position;
                pointer.IsPressed = touch.phase is TouchPhase.Began or TouchPhase.Moved or TouchPhase.Stationary;
                pointer.DownThisFrame = touch.phase == TouchPhase.Began;
                pointer.UpThisFrame = touch.phase is TouchPhase.Ended or TouchPhase.Canceled;
                return true;
            }

            var mousePressed = Input.GetMouseButton(0);
            var mouseDown = Input.GetMouseButtonDown(0);
            var mouseUp = Input.GetMouseButtonUp(0);
            if (!mousePressed && !mouseDown && !mouseUp)
            {
                pointer = default;
                return false;
            }

            pointer = default;
            pointer.Position = Input.mousePosition;
            pointer.IsPressed = mousePressed;
            pointer.DownThisFrame = mouseDown;
            pointer.UpThisFrame = mouseUp;

            return true;
        }
    }
}
