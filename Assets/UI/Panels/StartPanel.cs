using System;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Managers;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Panels
{
    public class StartPanel : GamePanel
    {
        [SerializeField] private GameUiTextProfile uiTextProfile;

        private Label _titleLabel;
        private Label _subtitleLabel;
        private Button _startButton;
        public event Action StartRequested;
        protected override bool UseSafeAreaPadding => false;

        protected override void CacheElements()
        {
            _titleLabel = Root.Q<Label>("start-title");
            _subtitleLabel = Root.Q<Label>("start-subtitle");
            _startButton = Root.Q<Button>("start-button");

            var startText = uiTextProfile.startPanel;
            _titleLabel.text = startText.title;
            _subtitleLabel.text = startText.subtitle;
            _startButton.text = startText.actionLabel;
            _startButton.clicked += HandleStartClicked;
            Show();
        }

        public void SubscribeToState() => UIManager.Instance.GameStateChanged += HandleGameStateChanged;

        public void UnsubscribeFromState() => UIManager.Instance.GameStateChanged -= HandleGameStateChanged;

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
            AudioManager.Instance.PlayButtonClick();
            StartRequested?.Invoke();
        }

        private void OnDestroy()
        {
            _startButton.clicked -= HandleStartClicked;
        }
    }
}