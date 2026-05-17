using System;
using Runtime.Core;
using Runtime.Domain.Enums;
using UI.Panels;
using UnityEngine;

namespace Runtime.Managers
{
    [DisallowMultipleComponent]
    public class UIManager : SingletonMonoBehaviour<UIManager>
    {
        [Header("Panels")] [SerializeField] private StartPanel startPanel;
        [SerializeField] private EndGamePanel endGamePanel;
        [SerializeField] private TopBarPanel topBarPanel;
        [SerializeField] private SettingsPanel settingsPanel;

        public event Action<GameState> GameStateChanged;
        public event Action LevelTimerExpired;
        public event Action StartRequested;
        public event Action<GameState> EndGameActionRequested;
        public event Action ReloadRequested;

        protected override void Awake()
        {
            base.Awake();
            startPanel.SubscribeToState();
            endGamePanel.SubscribeToState();
            topBarPanel.SubscribeToState();
            settingsPanel.SubscribeToState();

            startPanel.StartRequested += HandleStartRequested;
            endGamePanel.ActionRequested += HandleEndGameActionRequested;
            topBarPanel.ReloadRequested += HandleReloadRequested;
            topBarPanel.SettingsRequested += HandleSettingsRequested;
            topBarPanel.TimerExpired += HandleTimerExpired;
            settingsPanel.OpenStateChanged += HandleSettingsPanelOpenStateChanged;
        }

        private void Start() => PublishState(GameState.StartScreen);

        protected override void OnDestroy()
        {
            topBarPanel.TimerExpired -= HandleTimerExpired;
            settingsPanel.OpenStateChanged -= HandleSettingsPanelOpenStateChanged;
            topBarPanel.SettingsRequested -= HandleSettingsRequested;
            topBarPanel.ReloadRequested -= HandleReloadRequested;
            endGamePanel.ActionRequested -= HandleEndGameActionRequested;
            startPanel.StartRequested -= HandleStartRequested;
            settingsPanel.UnsubscribeFromState();
            topBarPanel.UnsubscribeFromState();
            endGamePanel.UnsubscribeFromState();
            startPanel.UnsubscribeFromState();
            base.OnDestroy();
        }

        public void PublishState(GameState state) => GameStateChanged?.Invoke(state);
        
        public void SetLevel(int levelNumber) => topBarPanel.SetLevel(levelNumber);

        public void ResetTimerDisplay() => topBarPanel.SetTimer(0);

        public void StartLevelTimer(float durationSeconds) => topBarPanel.StartTimer(durationSeconds);

        public void StopLevelTimer() => topBarPanel.StopTimer();

        private void PauseLevelTimer() => topBarPanel.PauseTimer();

        private void ResumeLevelTimer() => topBarPanel.ResumeTimer();

        private void HandleStartRequested() => StartRequested?.Invoke();

        private void HandleEndGameActionRequested() => EndGameActionRequested?.Invoke(StateManager.Instance.CurrentState);

        private void HandleReloadRequested() => ReloadRequested?.Invoke();

        private void HandleSettingsRequested() => settingsPanel.Toggle();

        private void HandleTimerExpired() => LevelTimerExpired?.Invoke();

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
