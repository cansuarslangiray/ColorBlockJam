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
        [SerializeField] private StateManager stateManager;

        public event Action<GameState> GameStateChanged;
        public event Action LevelTimerExpired;
        public event Action StartRequested;
        public event Action<GameState> EndGameActionRequested;
        public event Action ReloadRequested;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this)
            {
                return;
            }

            TryResolveSceneReferences();
            if (!HasAllPanelsAssigned())
            {
                Debug.LogError("UIManager is missing one or more panel references.", this);
                enabled = false;
                return;
            }

            startPanel.SubscribeToState(this);
            endGamePanel.SubscribeToState(this);
            topBarPanel.SubscribeToState(this);
            settingsPanel.SubscribeToState(this);

            startPanel.StartRequested += HandleStartRequested;
            endGamePanel.ActionRequested += HandleEndGameActionRequested;
            topBarPanel.ReloadRequested += HandleReloadRequested;
            topBarPanel.SettingsRequested += HandleSettingsRequested;
            topBarPanel.TimerExpired += HandleTimerExpired;
            settingsPanel.OpenStateChanged += HandleSettingsPanelOpenStateChanged;
        }

        private void OnValidate()
        {
            TryResolveSceneReferences();
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

        private void PauseLevelTimer() => topBarPanel.PauseTimer();

        private void ResumeLevelTimer() => topBarPanel.ResumeTimer();

        private void HandleStartRequested() => StartRequested?.Invoke();

        private void HandleEndGameActionRequested()
        { 
            EndGameActionRequested?.Invoke(stateManager.CurrentState);
        }

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

        private bool HasAllPanelsAssigned() =>
            startPanel != null &&
            endGamePanel != null &&
            topBarPanel != null &&
            settingsPanel != null &&
            stateManager != null;

        private void TryResolveSceneReferences()
        {
            if (!gameObject.scene.IsValid())
            {
                return;
            }

            startPanel ??= GetComponentInChildren<StartPanel>(true);
            endGamePanel ??= GetComponentInChildren<EndGamePanel>(true);
            topBarPanel ??= GetComponentInChildren<TopBarPanel>(true);
            settingsPanel ??= GetComponentInChildren<SettingsPanel>(true);

            startPanel ??= FindObjectOfType<StartPanel>();
            endGamePanel ??= FindObjectOfType<EndGamePanel>();
            topBarPanel ??= FindObjectOfType<TopBarPanel>();
            settingsPanel ??= FindObjectOfType<SettingsPanel>();
            stateManager ??= StateManager.Instance != null ? StateManager.Instance : FindObjectOfType<StateManager>();
        }

    }
}
