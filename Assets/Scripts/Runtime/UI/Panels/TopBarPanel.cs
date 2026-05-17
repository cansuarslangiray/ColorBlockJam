using System;
using System.Collections;
using Runtime.Domain.Enums;
using Runtime.Managers;
using UnityEngine;
using UnityEngine.UIElements;

namespace Runtime.UI.Panels
{
    public class TopBarPanel : GamePanel
    {
        [SerializeField, Min(0.2f)] private float tickIntervalSeconds = 1f;
        [SerializeField, Min(1)] private int warningThresholdSeconds = 30;
        [SerializeField, Min(1)] private int criticalThresholdSeconds = 10;
        
        private Label _levelLabel;
        private Label _timerLabel;
        private VisualElement _timerChip;
        private Button _reloadButton;
        private int _remainingSeconds;
        private Coroutine _tickRoutine;
        private bool _isTimerRunning;
        private Action _reloadAction = delegate { };

        public event Action TimerExpired = delegate { };

        protected override void CacheElements()
        {
            _levelLabel = Root.Q<Label>("topbar-level");
            _timerLabel = Root.Q<Label>("topbar-timer");
            _timerChip = Root.Q<VisualElement>("topbar-timer-chip");
            _reloadButton = Root.Q<Button>("topbar-reload");
            if (_levelLabel == null || _timerLabel == null || _timerChip == null || _reloadButton == null)
            {
                Debug.LogError("TopBarPanel could not find required elements: topbar-level, topbar-timer, topbar-timer-chip, or topbar-reload.", this);
                return;
            }

            _reloadButton.clicked += HandleReloadClicked;
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

        public void BindReloadAction(Action onReloadRequested)
        {
            _reloadAction = onReloadRequested;
        }

        public void StartTimer(float durationSeconds)
        {
            StopTimer();

            _remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(durationSeconds));
            _isTimerRunning = _remainingSeconds > 0;
            SetTimer(_remainingSeconds);

            if (_remainingSeconds <= 0)
            {
                _isTimerRunning = false;
                TimerExpired();
                return;
            }

            _tickRoutine = StartCoroutine(TickRoutine());
        }

        public void StopTimer()
        {
            if (_tickRoutine != null)
            {
                StopCoroutine(_tickRoutine);
                _tickRoutine = null;
            }

            _isTimerRunning = false;
            SetTimerState(_remainingSeconds);
        }

        public void SetLevel(int levelNumber)
        {
            if (_levelLabel == null)
            {
                return;
            }

            _levelLabel.text = Mathf.Max(1, levelNumber).ToString();
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
            SetTimerState(totalSeconds);
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
            _isTimerRunning = false;
            SetTimerState(0);
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

        private void SetTimerState(int totalSeconds)
        {
            if (_timerChip == null)
            {
                return;
            }

            var warningThreshold = Mathf.Max(criticalThresholdSeconds + 1, warningThresholdSeconds);
            var isCritical = _isTimerRunning && totalSeconds > 0 && totalSeconds <= criticalThresholdSeconds;
            var isWarning = _isTimerRunning && totalSeconds > criticalThresholdSeconds && totalSeconds <= warningThreshold;
            var shouldPulse = isCritical && totalSeconds % 2 == 0;

            _timerChip.EnableInClassList("timer-warning", isWarning);
            _timerChip.EnableInClassList("timer-critical", isCritical);
            _timerChip.EnableInClassList("timer-pulse", shouldPulse);
        }

        private void HandleReloadClicked()
        {
            _reloadAction();
        }

        private void OnDisable()
        {
            StopTimer();
        }

        private void OnDestroy()
        {
            if (_reloadButton != null)
            {
                _reloadButton.clicked -= HandleReloadClicked;
            }
        }
    }
}
