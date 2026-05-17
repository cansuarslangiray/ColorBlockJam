using System;
using System.Collections;
using Runtime.Domain.Enums;
using Runtime.Managers;
using Runtime.UI.Panels;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Panels
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
        private Button _settingsButton;
        private int _remainingSeconds;
        private Coroutine _tickRoutine;
        private bool _isTimerRunning;
        private bool _isTimerPaused;
        private bool _isReady;
        private WaitForSeconds _tickWaitInstruction;
        private float _cachedTickInterval = -1f;
        private int _resolvedWarningThreshold;
        private Action _reloadAction;
        private Action _settingsAction;

        public event Action TimerExpired;

        protected override void CacheElements()
        {
            _levelLabel = Root.Q<Label>("topbar-level");
            _timerLabel = Root.Q<Label>("topbar-timer");
            _timerChip = Root.Q<VisualElement>("topbar-timer-chip");
            _reloadButton = Root.Q<Button>("topbar-reload");
            _settingsButton = Root.Q<Button>("topbar-settings");

            _reloadButton.clicked += HandleReloadClicked;
            _settingsButton.clicked += HandleSettingsClicked;
            _isReady = true;
            RefreshTimerStyleThreshold();
            SetTimer(0);
            Hide();
        }

        private void OnValidate()
        {
            RefreshTimerStyleThreshold();
            _cachedTickInterval = -1f;
            _tickWaitInstruction = null;
        }

        public void SubscribeToState() => UIManager.Instance.GameStateChanged += HandleGameStateChanged;

        public void UnsubscribeFromState() => UIManager.Instance.GameStateChanged -= HandleGameStateChanged;

        public void BindReloadAction(Action onReloadRequested) => _reloadAction = onReloadRequested;

        public void BindSettingsAction(Action onSettingsRequested) => _settingsAction = onSettingsRequested;

        public void StartTimer(float durationSeconds)
        {
            StopTimer();

            _remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(durationSeconds));
            _isTimerPaused = false;
            _isTimerRunning = _remainingSeconds > 0;
            SetTimer(_remainingSeconds);

            if (_remainingSeconds <= 0)
            {
                _isTimerRunning = false;
                TimerExpired?.Invoke();
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
            _isTimerPaused = false;
            SetTimerState(_remainingSeconds);
        }

        public void PauseTimer()
        {
            if (!_isTimerRunning || _isTimerPaused)
            {
                return;
            }

            if (_tickRoutine != null)
            {
                StopCoroutine(_tickRoutine);
                _tickRoutine = null;
            }

            _isTimerRunning = false;
            _isTimerPaused = true;
            SetTimerState(_remainingSeconds);
        }

        public void ResumeTimer()
        {
            if (!_isTimerPaused || _remainingSeconds <= 0 || _tickRoutine != null)
            {
                return;
            }

            _isTimerPaused = false;
            _isTimerRunning = true;
            SetTimerState(_remainingSeconds);
            _tickRoutine = StartCoroutine(TickRoutine());
        }

        public void SetLevel(int levelNumber)
        {
            if (!_isReady)
            {
                return;
            }

            _levelLabel.text = Mathf.Max(1, levelNumber).ToString();
        }

        public void SetTimer(int remainingSeconds)
        {
            if (!_isReady)
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
            var waitInstruction = ResolveTickInstruction();
            while (_remainingSeconds > 0)
            {
                yield return waitInstruction;
                _remainingSeconds = Mathf.Max(0, _remainingSeconds - 1);
                SetTimer(_remainingSeconds);
            }

            _tickRoutine = null;
            _isTimerRunning = false;
            SetTimerState(0);
            TimerExpired?.Invoke();
        }

        private void HandleGameStateChanged(GameState state)
        {
            Show();
            if (state == GameState.StartScreen)
            {
                StopTimer();
                SetTimer(0);
            }
        }

        private void SetTimerState(int totalSeconds)
        {
            if (!_isReady)
            {
                return;
            }

            var isCritical = _isTimerRunning && totalSeconds > 0 && totalSeconds <= criticalThresholdSeconds;
            var isWarning =
                _isTimerRunning && totalSeconds > criticalThresholdSeconds && totalSeconds <= _resolvedWarningThreshold;
            var shouldPulse = isCritical && totalSeconds % 2 == 0;

            _timerChip.EnableInClassList("timer-warning", isWarning);
            _timerChip.EnableInClassList("timer-critical", isCritical);
            _timerChip.EnableInClassList("timer-pulse", shouldPulse);
        }

        private WaitForSeconds ResolveTickInstruction()
        {
            var resolvedInterval = Mathf.Max(0.2f, tickIntervalSeconds);
            if (_tickWaitInstruction == null || !Mathf.Approximately(_cachedTickInterval, resolvedInterval))
            {
                _cachedTickInterval = resolvedInterval;
                _tickWaitInstruction = new WaitForSeconds(resolvedInterval);
            }

            return _tickWaitInstruction;
        }

        private void RefreshTimerStyleThreshold() =>
            _resolvedWarningThreshold = Mathf.Max(criticalThresholdSeconds + 1, warningThresholdSeconds);

        private void HandleReloadClicked()
        {
            AudioManager.Instance.PlayButtonClick();
            _reloadAction();
        }

        private void HandleSettingsClicked()
        {
            AudioManager.Instance.PlayButtonClick();
            _settingsAction();
        }

        private void OnDisable() => StopTimer();

        private void OnDestroy()
        {
            if (!_isReady)
            {
                return;
            }

            _reloadButton.clicked -= HandleReloadClicked;
            _settingsButton.clicked -= HandleSettingsClicked;
        }
    }
}
