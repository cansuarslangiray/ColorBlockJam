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

        private bool _gestureActive;
        private InputAction _pointerPressAction;
        private InputAction _pointerPositionAction;
        private readonly List<RaycastResult> _uiRaycastResults = new();
        private PointerEventData _pointerEventData;
        private EventSystem _cachedEventSystem;

        private void Awake()
        {
            InitializeInputActions();
        }

        private void OnEnable()
        {
            EnableInputActions();
        }

        private void OnDisable()
        {
            DisableInputActions();
            EndActiveGesture();
        }

        private void OnDestroy()
        {
            DisposeInputActions();
        }

        private void InitializeInputActions()
        {
            if (_pointerPressAction != null && _pointerPositionAction != null)
            {
                return;
            }

            _pointerPressAction = new InputAction("BoardPointerPress", InputActionType.Button, "<Pointer>/press");
            _pointerPositionAction =
                new InputAction("BoardPointerPosition", InputActionType.PassThrough, "<Pointer>/position");

            _pointerPressAction.started += OnPointerPressStarted;
            _pointerPressAction.canceled += OnPointerPressCanceled;
            _pointerPositionAction.performed += OnPointerPositionPerformed;
        }

        private void EnableInputActions()
        {
            _pointerPositionAction?.Enable();
            _pointerPressAction?.Enable();
        }

        private void DisableInputActions()
        {
            _pointerPressAction?.Disable();
            _pointerPositionAction?.Disable();
        }

        private void DisposeInputActions()
        {
            if (_pointerPressAction != null)
            {
                _pointerPressAction.started -= OnPointerPressStarted;
                _pointerPressAction.canceled -= OnPointerPressCanceled;
                _pointerPressAction.Dispose();
                _pointerPressAction = null;
            }

            if (_pointerPositionAction != null)
            {
                _pointerPositionAction.performed -= OnPointerPositionPerformed;
                _pointerPositionAction.Dispose();
                _pointerPositionAction = null;
            }
        }

        private void OnPointerPressStarted(InputAction.CallbackContext context)
        {
            var pointerPosition = _pointerPositionAction != null
                ? _pointerPositionAction.ReadValue<Vector2>()
                : Vector2.zero;

            if (_gestureActive || !boardController || ShouldIgnorePointer(pointerPosition))
            {
                return;
            }

            var pointerCamera = ResolveInputCamera();
            _gestureActive = boardController.TryBeginPointerGesture(pointerPosition, pointerCamera);
        }

        private void OnPointerPositionPerformed(InputAction.CallbackContext context)
        {
            if (!_gestureActive || !boardController)
            {
                return;
            }

            if (_pointerPressAction == null || !_pointerPressAction.IsPressed())
            {
                EndActiveGesture();
                return;
            }

            var pointerCamera = ResolveInputCamera();
            var pointerPosition = context.ReadValue<Vector2>();
            boardController.TryUpdatePointerGesture(pointerPosition, pointerCamera);
        }

        private void OnPointerPressCanceled(InputAction.CallbackContext context)
        {
            EndActiveGesture();
        }

        private void EndActiveGesture()
        {
            if (_gestureActive && boardController)
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

        private Camera ResolveInputCamera()
        {
            return inputCamera;
        }
    }
}
