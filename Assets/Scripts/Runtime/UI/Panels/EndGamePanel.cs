using System;
using Runtime.Domain.Enums;
using UnityEngine.UIElements;

namespace Runtime.UI.Panels
{
    public class EndGamePanel : GamePanel
    {
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Button _actionButton;
        private Action _actionHandler = delegate { };
        private Action _continueAction = delegate { };
        private Action _retryAction = delegate { };
        private Action _restartAction = delegate { };
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
            switch (state)
            {
                case GameState.LevelCompleted:
                    _titleLabel.text = "Level Complete!";
                    _subtitleLabel.text = "Nice move. Keep the streak going.";
                    ConfigureAction("Continue", _continueAction);
                    break;
                case GameState.LevelFailed:
                    _titleLabel.text = "Time's Up";
                    _subtitleLabel.text = "You were close. Try that route once more.";
                    ConfigureAction("Retry", _retryAction);
                    break;
                case GameState.GameCompleted:
                    _titleLabel.text = "You Cleared All Levels!";
                    _subtitleLabel.text = "Great run. Ready for another full clear?";
                    ConfigureAction("Restart", _restartAction);
                    break;
                case GameState.StartScreen:
                case GameState.Playing:
                default:
                    Hide();
                    break;
            }
        }

        private void ConfigureAction(string label, Action onAction)
        {
            _actionButton.text = label;
            _actionHandler = onAction;
            Show();
        }

        private void HandleActionClicked()
        {
            _actionHandler();
        }

        private void OnDestroy()
        {
            _actionButton.clicked -= HandleActionClicked;
        }
    }
}
