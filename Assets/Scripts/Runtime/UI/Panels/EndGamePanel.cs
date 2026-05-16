using System;
using Runtime.Domain.Enums;
using UnityEngine.UIElements;

namespace Runtime.UI.Panels
{
    public class EndGamePanel : GamePanel
    {
        private Label _titleLabel;
        private Button _actionButton;
        private Action _actionHandler = delegate { };
        private Action _continueAction = delegate { };
        private Action _retryAction = delegate { };
        private Action _restartAction = delegate { };
        protected override void CacheElements()
        {
            _titleLabel = Root.Q<Label>("endgame-title");
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
                    _titleLabel.text = "Level Completed";
                    ConfigureAction("Continue", _continueAction);
                    break;
                case GameState.LevelFailed:
                    _titleLabel.text = "Lose";
                    ConfigureAction("Retry", _retryAction);
                    break;
                case GameState.GameCompleted:
                    _titleLabel.text = "All Levels Completed";
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
