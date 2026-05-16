using System;
using Runtime.Data;
using Runtime.Domain.Enums;
using UnityEngine;
using UnityEngine.UIElements;

namespace Runtime.UI.Panels
{
    public class StartPanel : GamePanel
    {
        [SerializeField] private GameUiTextProfile uiTextProfile;

        private Label _titleLabel;
        private Label _subtitleLabel;
        private Button _startButton;
        private Action _startAction = delegate { };
        protected override bool UseSafeAreaPadding => false;

        protected override void CacheElements()
        {
            _titleLabel = Root.Q<Label>("start-title");
            _subtitleLabel = Root.Q<Label>("start-subtitle");
            _startButton = Root.Q<Button>("start-button");
            if (_titleLabel == null || _subtitleLabel == null || _startButton == null)
            {
                Debug.LogError(
                    "StartPanel could not find required elements: start-title, start-subtitle, or start-button.", this);
                return;
            }

            var startText = uiTextProfile.startPanel;
            _titleLabel.text = startText.title;
            _subtitleLabel.text = startText.subtitle;
            _startButton.text = startText.actionLabel;
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