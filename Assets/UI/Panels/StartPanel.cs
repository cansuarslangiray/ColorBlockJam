using System;
using Runtime.Domain.Enums;
using Runtime.Managers;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Panels
{
    public class StartPanel : GamePanel
    {
        [SerializeField] private AudioManager audioManager;

        private Button _startButton;
        private UIManager _uiManager;
        public event Action StartRequested;
        protected override bool UseSafeAreaPadding => false;

        protected override void CacheElements()
        {
            _startButton = Root.Q<Button>("start-button");
            _startButton.clicked += HandleStartClicked;
            Show();
        }

        public void SubscribeToState(UIManager uiManager)
        {
            _uiManager = uiManager;
            _uiManager.GameStateChanged += HandleGameStateChanged;
        }

        public void UnsubscribeFromState()
        {
            if (_uiManager != null)
            {
                _uiManager.GameStateChanged -= HandleGameStateChanged;
                _uiManager = null;
            }
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.StartScreen)
            {
                Show();
                return;
            }

            Hide();
        }

        private void HandleStartClicked()
        {
            audioManager?.PlayButtonClick();
            StartRequested?.Invoke();
        }

        protected override void OnDestroy()
        {
            if (_startButton != null)
            {
                _startButton.clicked -= HandleStartClicked;
            }

            base.OnDestroy();
        }
    }
}
