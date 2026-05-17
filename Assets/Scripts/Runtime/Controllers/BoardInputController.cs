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
        private bool _inputActionsReady;
        private bool _inputActionsBound;
        private InputAction _pointerPressAction;
        private InputAction _pointerPositionAction;
        private readonly List<RaycastResult> _uiRaycastResults = new();
        private PointerEventData _pointerEventData;
        private EventSystem _cachedEventSystem;

        private void Awake() => ResolveInputActions();

        private void OnEnable()
        {
            if (!_inputActionsReady)
            {
                ResolveInputActions();
            }

            if (!_inputActionsReady || _inputActionsBound)
            {
                return;
            }

            EnableInputActions();
        }

        private void OnDisable()
        {
            if (_inputActionsBound)
            {
                DisableInputActions();
            }

            EndActiveGesture();
        }

        private void ResolveInputActions()
        {
            _pointerPressAction = pointerPressActionReference ? pointerPressActionReference.action : null;
            _pointerPositionAction = pointerPositionActionReference ? pointerPositionActionReference.action : null;
            _inputActionsReady = boardController != null && _pointerPressAction != null && _pointerPositionAction != null;

            if (_inputActionsReady)
            {
                return;
            }

            Debug.LogError($"{nameof(BoardInputController)} is missing required references.", this);
        }

        private void EnableInputActions()
        {
            _pointerPressAction.started += OnPointerPressStarted;
            _pointerPressAction.canceled += OnPointerPressCanceled;
            _pointerPositionAction.performed += OnPointerPositionPerformed;

            _pointerPositionAction.Enable();
            _pointerPressAction.Enable();
            _inputActionsBound = true;
        }

        private void DisableInputActions()
        {
            _pointerPressAction.started -= OnPointerPressStarted;
            _pointerPressAction.canceled -= OnPointerPressCanceled;
            _pointerPressAction.Disable();

            _pointerPositionAction.performed -= OnPointerPositionPerformed;
            _pointerPositionAction.Disable();
            _inputActionsBound = false;
        }

        private void OnPointerPressStarted(InputAction.CallbackContext context)
        {
            var pointerPosition = _pointerPositionAction.ReadValue<Vector2>();

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

            if (!_pointerPressAction.IsPressed())
            {
                EndActiveGesture();
                return;
            }

            var pointerPosition = context.ReadValue<Vector2>();
            boardController.TryUpdatePointerGesture(pointerPosition, inputCamera);
        }

        private void OnPointerPressCanceled(InputAction.CallbackContext context) => EndActiveGesture();

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
