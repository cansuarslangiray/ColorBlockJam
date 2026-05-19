using System.Collections.Generic;
using Runtime.Domain.Enums;
using Runtime.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Runtime.Controllers
{
    [DisallowMultipleComponent]
    public class BoardInputController : MonoBehaviour
    {
        [SerializeField] private BoardController boardController;
        [SerializeField] private EventSystem uiEventSystem;
        [SerializeField] private StateManager stateManager;
        [SerializeField] private InputActionReference pointerPressActionReference;
        [SerializeField] private InputActionReference pointerPositionActionReference;

        private bool _inputActionsBound;
        private bool _stateEventsRegistered;
        private readonly List<RaycastResult> _uiRaycastResults = new();
        private PointerEventData _pointerEventData;
        private InputAction _pointerPressAction;
        private InputAction _pointerPositionAction;

        private void Awake()
        {
            EnsurePointerEventData();
            ResolveInputActions();
        }

        private void OnEnable()
        {
            RegisterStateEvents();
        }

        private void OnDisable()
        {
            UnregisterStateEvents();
            DisableInputActions();
            EndActiveGesture();
        }

        private void EnableInputActions()
        {
            if (_inputActionsBound)
            {
                return;
            }

            _pointerPressAction.started += OnPointerPressStarted;
            _pointerPressAction.canceled += OnPointerPressCanceled;
            _pointerPositionAction.performed += OnPointerPositionPerformed;

            _pointerPositionAction.Enable();
            _pointerPressAction.Enable();
            _inputActionsBound = true;
        }

        private void DisableInputActions()
        {
            if (!_inputActionsBound)
            {
                return;
            }

            _pointerPressAction.started -= OnPointerPressStarted;
            _pointerPressAction.canceled -= OnPointerPressCanceled;
            _pointerPressAction.Disable();

            _pointerPositionAction.performed -= OnPointerPositionPerformed;
            _pointerPositionAction.Disable();
            _inputActionsBound = false;
        }

        private void RegisterStateEvents()
        {
            if (_stateEventsRegistered)
            {
                return;
            }

            stateManager.OnStateChanged += HandleGameStateChanged;
            _stateEventsRegistered = true;
            HandleGameStateChanged(stateManager.CurrentState);
        }

        private void UnregisterStateEvents()
        {
            if (!_stateEventsRegistered)
            {
                return;
            }

            stateManager.OnStateChanged -= HandleGameStateChanged;
            _stateEventsRegistered = false;
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.Playing)
            {
                EnableInputActions();
                return;
            }

            DisableInputActions();
            EndActiveGesture();
        }

        private void OnPointerPressStarted(InputAction.CallbackContext context)
        {
            var pointerPosition = _pointerPositionAction.ReadValue<Vector2>();

            if (ShouldIgnorePointer(pointerPosition))
            {
                return;
            }

            boardController.TryBeginPointerGesture(pointerPosition);
        }

        private void OnPointerPositionPerformed(InputAction.CallbackContext context)
        {
            var pointerPosition = context.ReadValue<Vector2>();
            boardController.TryUpdatePointerGesture(pointerPosition);
        }

        private void OnPointerPressCanceled(InputAction.CallbackContext context) => EndActiveGesture();

        private void EndActiveGesture()
        {
            boardController.EndPointerGesture();
        }

        private bool ShouldIgnorePointer(Vector2 pointerPosition)
        {
            EnsurePointerEventData();
            _pointerEventData.position = pointerPosition;
            _uiRaycastResults.Clear();
            uiEventSystem.RaycastAll(_pointerEventData, _uiRaycastResults);
            return _uiRaycastResults.Count > 0;
        }

        private void ResolveInputActions()
        {
            _pointerPressAction = pointerPressActionReference.action;
            _pointerPositionAction = pointerPositionActionReference.action;
        }

        private void EnsurePointerEventData()
        {
            if (_pointerEventData == null && uiEventSystem != null)
            {
                _pointerEventData = new PointerEventData(uiEventSystem);
            }
        }
        
    }
}
