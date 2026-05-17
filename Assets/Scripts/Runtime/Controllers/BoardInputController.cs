using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Runtime.Controllers
{
    [DisallowMultipleComponent]
    public class BoardInputController : MonoBehaviour
    {
        [SerializeField] private BoardController boardController;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private bool ignoreInputWhenPointerIsOverUi = true;
        [SerializeField] private InputActionReference pointerPressActionReference;
        [SerializeField] private InputActionReference pointerPositionActionReference;

        private bool _gestureActive;
        private readonly List<RaycastResult> _uiRaycastResults = new();
        private PointerEventData _pointerEventData;
        private EventSystem _cachedEventSystem;

        private InputAction PointerPressAction =>
            pointerPressActionReference != null ? pointerPressActionReference.action : null;

        private InputAction PointerPositionAction =>
            pointerPositionActionReference != null ? pointerPositionActionReference.action : null;

        private void OnEnable()
        {
            EnableInputActions();
        }

        private void OnDisable()
        {
            DisableInputActions();
            EndActiveGesture();
        }

        private void EnableInputActions()
        {
            PointerPressAction.started += OnPointerPressStarted;
            PointerPressAction.canceled += OnPointerPressCanceled;
            PointerPositionAction.performed += OnPointerPositionPerformed;

            PointerPositionAction.Enable();
            PointerPressAction.Enable();
        }

        private void DisableInputActions()
        {
            PointerPressAction.started -= OnPointerPressStarted;
            PointerPressAction.canceled -= OnPointerPressCanceled;
            PointerPressAction.Disable();
            
            PointerPositionAction.performed -= OnPointerPositionPerformed;
            PointerPositionAction.Disable();
        }

        private void OnPointerPressStarted(InputAction.CallbackContext context)
        {
            var pointerPosition = PointerPositionAction != null
                ? PointerPositionAction.ReadValue<Vector2>()
                : Vector2.zero;

            if (_gestureActive || ShouldIgnorePointer(pointerPosition))
            {
                return;
            }
            _gestureActive = boardController.TryBeginPointerGesture(pointerPosition, inputCamera);
        }

        private void OnPointerPositionPerformed(InputAction.CallbackContext context)
        {
            if (!_gestureActive)
            {
                return;
            }

            if (!PointerPressAction.IsPressed())
            {
                EndActiveGesture();
                return;
            }

            var pointerPosition = context.ReadValue<Vector2>();
            boardController.TryUpdatePointerGesture(pointerPosition, inputCamera);
        }

        private void OnPointerPressCanceled(InputAction.CallbackContext context)
        {
            EndActiveGesture();
        }

        private void EndActiveGesture()
        {
            if (_gestureActive)
            {
                boardController.EndPointerGesture();
            }

            _gestureActive = false;
        }

        private bool ShouldIgnorePointer(Vector2 pointerPosition)
        {
            if (!ignoreInputWhenPointerIsOverUi)
            {
                return false;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            if (_pointerEventData == null || _cachedEventSystem != eventSystem)
            {
                _pointerEventData = new PointerEventData(eventSystem);
                _cachedEventSystem = eventSystem;
            }
            else
            {
                _pointerEventData.Reset();
            }

            _pointerEventData.position = pointerPosition;
            _uiRaycastResults.Clear();
            eventSystem.RaycastAll(_pointerEventData, _uiRaycastResults);
            return _uiRaycastResults.Count > 0;
        }
    }
}
