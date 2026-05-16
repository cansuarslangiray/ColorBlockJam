using System;
using Runtime.Core;
using Runtime.Domain.Enums;
using Runtime.UI.Panels;
using UnityEngine;

namespace Runtime.UI
{
    public class UIManager : SingletonMonoBehaviour<UIManager>
    {
        [Header("Panels")]
        [SerializeField] private StartPanel startPanel;
        [SerializeField] private EndGamePanel endGamePanel;
        [SerializeField] private TopBarPanel topBarPanel;

        public event Action<GameState> GameStateChanged = delegate { };
        public event Action LevelTimerExpired = delegate { };

        protected override void Awake()
        {
            base.Awake();
            if (startPanel == null || endGamePanel == null || topBarPanel == null)
            {
                Debug.LogError("UIManager requires StartPanel, EndGamePanel, and TopBarPanel references.", this);
                enabled = false;
                return;
            }

            startPanel.SubscribeToState(this);
            endGamePanel.SubscribeToState(this);
            topBarPanel.SubscribeToState(this);
            topBarPanel.TimerExpired += HandleTimerExpired;
        }

        private void Start()
        {
            PublishState(GameState.StartScreen);
        }

        protected override void OnDestroy()
        {
            if (topBarPanel != null)
            {
                topBarPanel.TimerExpired -= HandleTimerExpired;
                topBarPanel.UnsubscribeFromState(this);
            }

            if (endGamePanel != null)
            {
                endGamePanel.UnsubscribeFromState(this);
            }

            if (startPanel != null)
            {
                startPanel.UnsubscribeFromState(this);
            }

            base.OnDestroy();
        }

        public void BindStartAction(Action onStartRequested)
        {
            startPanel.BindStartAction(onStartRequested);
        }

        public void BindContinueAction(Action onContinueRequested)
        {
            endGamePanel.BindContinueAction(onContinueRequested);
        }

        public void BindRetryAction(Action onRetryRequested)
        {
            endGamePanel.BindRetryAction(onRetryRequested);
        }

        public void BindRestartAction(Action onRestartRequested)
        {
            endGamePanel.BindRestartAction(onRestartRequested);
        }

        public void PublishState(GameState state)
        {
            GameStateChanged(state);
        }

        public void SetLevel(int levelNumber, int levelIndex, int totalLevels)
        {
            topBarPanel.SetLevel(levelNumber, levelIndex, totalLevels);
        }

        public void ResetTimerDisplay()
        {
            topBarPanel.SetTimer(0);
        }

        public void StartLevelTimer(float durationSeconds)
        {
            topBarPanel.StartTimer(durationSeconds);
        }

        public void StopLevelTimer()
        {
            topBarPanel.StopTimer();
        }

        private void HandleTimerExpired()
        {
            LevelTimerExpired();
        }
    }
}
