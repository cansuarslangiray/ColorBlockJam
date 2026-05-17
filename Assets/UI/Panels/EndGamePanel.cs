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
        private Action _actionHandler = delegate { };
        private Action _continueAction = delegate { };
        private Action _retryAction = delegate { };
        private Action _restartAction = delegate { };
        protected override bool UseSafeAreaPadding => false;

        protected override void CacheElements()
        {
            _titleLabel = Root.Q<Label>("endgame-title");
            _subtitleLabel = Root.Q<Label>("endgame-subtitle");
            _actionButton = Root.Q<Button>("endgame-action");
            _actionButton.clicked += HandleActionClicked;
            Hide();
        }

        public void BindContinueAction(Action onContinueRequested)
        {
            _continueAction = onContinueRequested;
        }

        public void BindRetryAction(Action onRetryRequested)
        {
            _retryAction = onRetryRequested;
        }

        public void BindRestartAction(Action onRestartRequested)
        {
            _restartAction = onRestartRequested;
        }

        public void SubscribeToState()
        {
            UIManager.Instance.GameStateChanged += HandleGameStateChanged;
        }

        public void UnsubscribeFromState()
        {
             UIManager.Instance.GameStateChanged -= HandleGameStateChanged;
        }

        private void HandleGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.LevelCompleted:
                    ConfigureForState(state, _continueAction);
                    break;
                case GameState.LevelFailed:
                    ConfigureForState(state, _retryAction);
                    break;
                case GameState.GameCompleted:
                    ConfigureForState(state, _restartAction);
                    break;
                case GameState.StartScreen:
                case GameState.Playing:
                default:
                    Hide();
                    break;
            }
        }

        private void ConfigureForState(GameState state, Action onAction)
        {
            var stateText = uiTextProfile.GetEndGamePanelText(state);
            ConfigureAction(stateText.actionLabel, stateText.title, stateText.subtitle, onAction);
        }


        private void ConfigureAction(string actionLabel, string title, string subtitle, Action onAction)
        {
            _titleLabel.text = title;
            _subtitleLabel.text = subtitle;
            _actionButton.text = actionLabel;
            _actionHandler = onAction;
            Show();
        }

        private void HandleActionClicked()
        {
             AudioManager.Instance.PlayButtonClick();
            _actionHandler();
        }

        private void OnDestroy()
        {
            if (_actionButton != null)
            {
                _actionButton.clicked -= HandleActionClicked;
            }
        }
    }
}
