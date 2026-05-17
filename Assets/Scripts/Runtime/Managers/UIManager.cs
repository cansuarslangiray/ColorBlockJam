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

        protected override void Awake()
        {
            base.Awake();
            startPanel.SubscribeToState();
            endGamePanel.SubscribeToState();
            topBarPanel.SubscribeToState();
            settingsPanel.SubscribeToState();

            topBarPanel.TimerExpired += HandleTimerExpired;
            topBarPanel.BindSettingsAction(settingsPanel.Toggle);
            settingsPanel.OpenStateChanged += HandleSettingsPanelOpenStateChanged;
        }

        private void Start() => PublishState(GameState.StartScreen);

        protected override void OnDestroy()
        {
            topBarPanel.TimerExpired -= HandleTimerExpired;
            settingsPanel.OpenStateChanged -= HandleSettingsPanelOpenStateChanged;
            settingsPanel.UnsubscribeFromState();
            topBarPanel.UnsubscribeFromState();
            endGamePanel.UnsubscribeFromState();
            startPanel.UnsubscribeFromState();
            base.OnDestroy();
        }

        public void BindStartAction(Action onStartRequested) => startPanel.BindStartAction(onStartRequested);

        public void BindContinueAction(Action onContinueRequested) => endGamePanel.BindContinueAction(onContinueRequested);

        public void BindRetryAction(Action onRetryRequested) => endGamePanel.BindRetryAction(onRetryRequested);

        public void BindRestartAction(Action onRestartRequested) => endGamePanel.BindRestartAction(onRestartRequested);

        public void BindReloadAction(Action onReloadRequested) => topBarPanel.BindReloadAction(onReloadRequested);

        public void PublishState(GameState state) => GameStateChanged?.Invoke(state);
        
        public void SetLevel(int levelNumber) => topBarPanel.SetLevel(levelNumber);

        public void ResetTimerDisplay() => topBarPanel.SetTimer(0);

        public void StartLevelTimer(float durationSeconds) => topBarPanel.StartTimer(durationSeconds);

        public void StopLevelTimer() => topBarPanel.StopTimer();

        private void PauseLevelTimer() => topBarPanel.PauseTimer();

        private void ResumeLevelTimer() => topBarPanel.ResumeTimer();

        private void HandleTimerExpired() => LevelTimerExpired?.Invoke();

        private void HandleSettingsPanelOpenStateChanged(bool isOpen)
        {
            if (!StateManager.Instance || StateManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            if (isOpen)
            {
                PauseLevelTimer();
                return;
            }

            ResumeLevelTimer();
        }
    }
}