using System;
using System.Collections;
using Runtime.Domain.Enums;
using Runtime.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Runtime.UI.Panels
{
    public class TopBarPanel : GamePanel
    {
        [SerializeField, Min(0.2f)] private float tickIntervalSeconds = 1f;
        private Label _levelLabel;
        private Label _timerLabel;
        private int _remainingSeconds;
        private Coroutine _tickRoutine;

        public event Action TimerExpired = delegate { };

        protected override void CacheElements()
        {
            _levelLabel = Root.Q<Label>("topbar-level");
            _timerLabel = Root.Q<Label>("topbar-timer");
            if (_levelLabel == null || _timerLabel == null)
            {
                Debug.LogError("TopBarPanel could not find required elements: topbar-level or topbar-timer.", this);
                return;
            }

            SetTimer(0);
            Hide();
        }

        public void SubscribeToState(UIManager uiManager)
        {
            uiManager.GameStateChanged += HandleGameStateChanged;
        }

        public void UnsubscribeFromState(UIManager uiManager)
        {
            uiManager.GameStateChanged -= HandleGameStateChanged;
        }

        public void StartTimer(float durationSeconds)
        {
            StopTimer();

            _remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(durationSeconds));
            SetTimer(_remainingSeconds);

            if (_remainingSeconds <= 0)
            {
                TimerExpired();
                return;
            }

            _tickRoutine = StartCoroutine(TickRoutine());
        }

        public void StopTimer()
        {
            if (_tickRoutine == null)
            {
                return;
            }

            StopCoroutine(_tickRoutine);
            _tickRoutine = null;
        }

        public void SetLevel(int levelNumber, int levelIndex, int totalLevels)
        {
            if (_levelLabel == null)
            {
                return;
            }

            var displayLevel = levelNumber > 0 ? levelNumber : levelIndex + 1;
            _levelLabel.text = totalLevels > 0 ? $"Level {displayLevel}/{totalLevels}" : $"Level {displayLevel}";
        }

        public void SetTimer(int remainingSeconds)
        {
            if (_timerLabel == null)
            {
                return;
            }

            var totalSeconds = Mathf.Max(0, remainingSeconds);
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            _timerLabel.text = $"{minutes:00}:{seconds:00}";
        }

        private IEnumerator TickRoutine()
        {
            var waitInstruction = new WaitForSeconds(Mathf.Max(0.2f, tickIntervalSeconds));
            while (_remainingSeconds > 0)
            {
                yield return waitInstruction;
                _remainingSeconds = Mathf.Max(0, _remainingSeconds - 1);
                SetTimer(_remainingSeconds);
            }

            _tickRoutine = null;
            TimerExpired();
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.Playing)
            {
                Show();
                return;
            }

            Hide();

            if (state == GameState.StartScreen)
            {
                StopTimer();
                SetTimer(0);
            }
        }

        private void OnDisable()
        {
            StopTimer();
        }
    }
}
