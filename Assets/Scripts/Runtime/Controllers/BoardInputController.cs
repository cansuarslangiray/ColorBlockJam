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
        [SerializeField] private InputActionReference pointerPressActionReference;
        [SerializeField] private InputActionReference pointerPositionActionReference;

        private bool _inputActionsBound;
        private readonly List<RaycastResult> _uiRaycastResults = new();

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

            pointerPressActionReference.action.started += OnPointerPressStarted;
            pointerPressActionReference.action.canceled += OnPointerPressCanceled;
            pointerPositionActionReference.action.performed += OnPointerPositionPerformed;

            pointerPositionActionReference.action.Enable();
            pointerPressActionReference.action.Enable();
            _inputActionsBound = true;
        }

        private void DisableInputActions()
        {
            if (!_inputActionsBound)
            {
                return;
            }

            pointerPressActionReference.action.started -= OnPointerPressStarted;
            pointerPressActionReference.action.canceled -= OnPointerPressCanceled;
            pointerPressActionReference.action.Disable();

            pointerPositionActionReference.action.performed -= OnPointerPositionPerformed;
            pointerPositionActionReference.action.Disable();
            _inputActionsBound = false;
        }

        private void RegisterStateEvents()
        {
            if (StateManager.Instance == null)
                return;
            StateManager.Instance.OnStateChanged -= HandleGameStateChanged;
            StateManager.Instance.OnStateChanged += HandleGameStateChanged;
            HandleGameStateChanged(StateManager.Instance.CurrentState);
        }

        private void UnregisterStateEvents()
        {
            if (StateManager.Instance == null)
                return;
            StateManager.Instance.OnStateChanged -= HandleGameStateChanged;
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
            var pointerPosition = pointerPositionActionReference.action.ReadValue<Vector2>();

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

        private void EndActiveGesture() => boardController.EndPointerGesture();

        private bool ShouldIgnorePointer(Vector2 pointerPosition)
        {
            var pointerEventData = new PointerEventData(uiEventSystem) { position = pointerPosition };
            _uiRaycastResults.Clear();
            uiEventSystem.RaycastAll(pointerEventData, _uiRaycastResults);
            return _uiRaycastResults.Count > 0;
        }
    }
}
