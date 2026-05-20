using System;
using Runtime.Domain.Enums;
using Runtime.Localization;
using Runtime.Managers;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Panels
{
    public class EndGamePanel : GamePanel
    {
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

        protected override void OnGameStateChanged(GameState state)
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
            ResolveStateKeys(state, out var actionKey, out var titleKey, out var subtitleKey);
            ConfigureAction(actionKey, titleKey, subtitleKey);
        }

        private void ConfigureAction(string actionKey, string titleKey, string subtitleKey)
        {
            _titleLabel.text = LocalizeKey(titleKey);
            _subtitleLabel.text = LocalizeKey(subtitleKey);
            _actionButton.text = LocalizeKey(actionKey);
            Show();
        }

        public override void RefreshLocalization()
        {
            base.RefreshLocalization();
            var stateManager = StateManager.Instance;
            if (stateManager == null)
            {
                return;
            }

            var currentState = stateManager.CurrentState;

            if (IsEndGameState(currentState))
            {
                ConfigureForState(currentState);
            }
        }

        private static bool IsEndGameState(GameState state) =>
            state is GameState.LevelCompleted or GameState.LevelFailed or GameState.GameCompleted;

        private static void ResolveStateKeys(GameState state, out string actionKey, out string titleKey, out string subtitleKey)
        {
            switch (state)
            {
                case GameState.LevelCompleted:
                    actionKey = LocalizationKeys.EndLevelCompletedAction;
                    titleKey = LocalizationKeys.EndLevelCompletedTitle;
                    subtitleKey = LocalizationKeys.EndLevelCompletedSubtitle;
                    return;
                case GameState.LevelFailed:
                    actionKey = LocalizationKeys.EndLevelFailedAction;
                    titleKey = LocalizationKeys.EndLevelFailedTitle;
                    subtitleKey = LocalizationKeys.EndLevelFailedSubtitle;
                    return;
                case GameState.GameCompleted:
                    actionKey = LocalizationKeys.EndGameCompletedAction;
                    titleKey = LocalizationKeys.EndGameCompletedTitle;
                    subtitleKey = LocalizationKeys.EndGameCompletedSubtitle;
                    return;
                default:
                    actionKey = string.Empty;
                    titleKey = string.Empty;
                    subtitleKey = string.Empty;
                    return;
            }
        }

        private void HandleActionClicked()
        {
            AudioManager.Instance?.PlayButtonClick();
            ActionRequested?.Invoke();
        }

        protected override void OnDestroy()
        {
            if (_actionButton != null)
            {
                _actionButton.clicked -= HandleActionClicked;
            }

            base.OnDestroy();
        }
    }
}
