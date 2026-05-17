using System;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Managers;
using Runtime.UI.Panels;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Panels
{
    public class EndGamePanel : GamePanel
    {
        [SerializeField] private GameUiTextProfile uiTextProfile;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Button _actionButton;

        public event Action ActionRequested;
        protected override bool UseSafeAreaPadding => false;

        protected override void CacheElements()
        {
            _titleLabel = Root.Q<Label>("endgame-title");
            _subtitleLabel = Root.Q<Label>("endgame-subtitle");
            _actionButton = Root.Q<Button>("endgame-action");
            _actionButton.clicked += HandleActionClicked;
            Hide();
        }

        public void SubscribeToState() => UIManager.Instance.GameStateChanged += HandleGameStateChanged;

        public void UnsubscribeFromState() => UIManager.Instance.GameStateChanged -= HandleGameStateChanged;

        private void HandleGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.LevelCompleted:
                case GameState.LevelFailed:
                case GameState.GameCompleted:
                    ConfigureForState(state);
                    break;
                case GameState.StartScreen:
                case GameState.Playing:
                default:
                    Hide();
                    break;
            }
        }

        private void ConfigureForState(GameState state)
        {
            var stateText = uiTextProfile.GetEndGamePanelText(state);
            ConfigureAction(stateText.actionLabel, stateText.title, stateText.subtitle);
        }


        private void ConfigureAction(string actionLabel, string title, string subtitle)
        {
            _titleLabel.text = title;
            _subtitleLabel.text = subtitle;
            _actionButton.text = actionLabel;
            Show();
        }

        private void HandleActionClicked()
        {
            AudioManager.Instance.PlayButtonClick();
            ActionRequested?.Invoke();
        }

        private void OnDestroy()
        {
            _actionButton.clicked -= HandleActionClicked;
        }
    }
}
