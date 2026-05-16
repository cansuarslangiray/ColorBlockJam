using System;
using Runtime.Domain.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace Runtime.UI.Panels
{
    public class StartPanel : GamePanel
    {
        private Label _titleLabel;
        private Button _startButton;
        private Action _startAction = delegate { };

        protected override void CacheElements()
        {
            _titleLabel = Root.Q<Label>("start-title");
            _startButton = Root.Q<Button>("start-button");
            if (_titleLabel == null || _startButton == null)
            {
                Debug.LogError("StartPanel could not find required elements: start-title or start-button.", this);
                return;
            }

            _titleLabel.text = "Wait for input";
            _startButton.text = "Tap to Start";
            _startButton.clicked += HandleStartClicked;
            Show();
        }

        public void BindStartAction(Action onStartRequested)
        {
            _startAction = onStartRequested;
        }

        public void SubscribeToState(UIManager uiManager)
        {
            uiManager.GameStateChanged += HandleGameStateChanged;
        }

        public void UnsubscribeFromState(UIManager uiManager)
        {
            uiManager.GameStateChanged -= HandleGameStateChanged;
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
            _startAction();
        }

        private void OnDestroy()
        {
            if (_startButton != null)
            {
                _startButton.clicked -= HandleStartClicked;
            }
        }
    }
}
