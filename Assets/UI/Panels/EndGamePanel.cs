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
        [SerializeField] private StateManager stateManager;
        [SerializeField] private AudioManager audioManager;

        private Label _titleLabel;
        private Label _subtitleLabel;
        private Button _actionButton;
        private UIManager _uiManager;

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
            var currentState = stateManager != null
                ? stateManager.CurrentState
                : GameState.StartScreen;

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
                    actionKey = LocalizationKeys.Gameplay.EndLevelCompletedAction;
                    titleKey = LocalizationKeys.Gameplay.EndLevelCompletedTitle;
                    subtitleKey = LocalizationKeys.Gameplay.EndLevelCompletedSubtitle;
                    return;
                case GameState.LevelFailed:
                    actionKey = LocalizationKeys.Gameplay.EndLevelFailedAction;
                    titleKey = LocalizationKeys.Gameplay.EndLevelFailedTitle;
                    subtitleKey = LocalizationKeys.Gameplay.EndLevelFailedSubtitle;
                    return;
                case GameState.GameCompleted:
                    actionKey = LocalizationKeys.Gameplay.EndGameCompletedAction;
                    titleKey = LocalizationKeys.Gameplay.EndGameCompletedTitle;
                    subtitleKey = LocalizationKeys.Gameplay.EndGameCompletedSubtitle;
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
            audioManager?.PlayButtonClick();
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
