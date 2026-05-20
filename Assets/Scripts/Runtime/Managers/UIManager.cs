using System;
using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using UI.Panels;
using UnityEngine;
using UnityEngine.Serialization;

namespace Runtime.Managers
{
    [DisallowMultipleComponent]
    public class UIManager : SingletonMonoBehaviour<UIManager>
    {
        [Header("Panels")] [SerializeField] private StartPanel startPanel;
        [SerializeField] private EndGamePanel endGamePanel;
        [SerializeField] private TopBarPanel topBarPanel;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private FeatureUnlockedPanel featureUnlockedPanel;

        public event Action<GameState> GameStateChanged;
        public event Action LevelTimerExpired;
        public event Action StartRequested;
        public event Action<GameState> EndGameActionRequested;
        public event Action ReloadRequested;
        public event Action FeatureUnlockedNextRequested;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this)
            {
                return;
            }

            startPanel.SubscribeToState(this);
            endGamePanel.SubscribeToState(this);
            topBarPanel.SubscribeToState(this);
            settingsPanel.SubscribeToState(this);
            featureUnlockedPanel?.SubscribeToState(this);

            startPanel.StartRequested += HandleStartRequested;
            endGamePanel.ActionRequested += HandleEndGameActionRequested;
            topBarPanel.ReloadRequested += HandleReloadRequested;
            topBarPanel.SettingsRequested += HandleSettingsRequested;
            topBarPanel.TimerExpired += HandleTimerExpired;
            settingsPanel.OpenStateChanged += HandleSettingsPanelOpenStateChanged;
            if (featureUnlockedPanel != null)
            {
                featureUnlockedPanel.NextRequested += HandleFeatureUnlockedNextRequested;
            }
        }

        protected override void OnDestroy()
        {
            if (topBarPanel != null)
            {
                topBarPanel.TimerExpired -= HandleTimerExpired;
                topBarPanel.SettingsRequested -= HandleSettingsRequested;
                topBarPanel.ReloadRequested -= HandleReloadRequested;
                topBarPanel.UnsubscribeFromState();
            }

            if (settingsPanel != null)
            {
                settingsPanel.OpenStateChanged -= HandleSettingsPanelOpenStateChanged;
                settingsPanel.UnsubscribeFromState();
            }

            if (featureUnlockedPanel != null)
            {
                featureUnlockedPanel.NextRequested -= HandleFeatureUnlockedNextRequested;
                featureUnlockedPanel.UnsubscribeFromState();
            }

            if (endGamePanel != null)
            {
                endGamePanel.ActionRequested -= HandleEndGameActionRequested;
                endGamePanel.UnsubscribeFromState();
            }

            if (startPanel != null)
            {
                startPanel.StartRequested -= HandleStartRequested;
                startPanel.UnsubscribeFromState();
            }

            base.OnDestroy();
        }

        public void PublishState(GameState state) => GameStateChanged?.Invoke(state);

        public void SetLevel(int levelNumber) => topBarPanel.SetLevel(levelNumber);

        public void StartLevelTimer(float durationSeconds) => topBarPanel.StartTimer(durationSeconds);

        public void StopLevelTimer() => topBarPanel.StopTimer();

        public void ConfigureFeatureUnlockedPanel(IReadOnlyList<BlockFeatureDefinition> definitions) =>
            featureUnlockedPanel?.Configure(definitions);

        public void HideFeatureUnlockedPanel() => featureUnlockedPanel?.Hide();

        private void PauseLevelTimer() => topBarPanel.PauseTimer();

        private void ResumeLevelTimer() => topBarPanel.ResumeTimer();

        private void HandleStartRequested() => StartRequested?.Invoke();

        private void HandleEndGameActionRequested()
        {
            EndGameActionRequested?.Invoke(StateManager.Instance.CurrentState);
        }

        private void HandleReloadRequested() => ReloadRequested?.Invoke();

        private void HandleSettingsRequested() => settingsPanel.Toggle();

        private void HandleTimerExpired() => LevelTimerExpired?.Invoke();

        private void HandleFeatureUnlockedNextRequested() => FeatureUnlockedNextRequested?.Invoke();

        private void HandleSettingsPanelOpenStateChanged(bool isOpen)
        {
            if (isOpen)
            {
                PauseLevelTimer();
                return;
            }

            ResumeLevelTimer();
        }
    }
}