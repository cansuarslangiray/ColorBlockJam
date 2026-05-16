using System;
using System.Collections;
using UnityEngine;

namespace Runtime.Flow
{
    public class LevelCountdownTimer : MonoBehaviour
    {
        [SerializeField, Min(0.2f)] private float tickIntervalSeconds = 1f;

        private int _remainingSeconds;
        private Coroutine _tickRoutine;

        public event Action<int> SecondChanged;
        public event Action TimerExpired;

        public void Begin(float durationSeconds)
        {
            Stop();

            _remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(durationSeconds));
            SecondChanged?.Invoke(_remainingSeconds);

            if (_remainingSeconds <= 0)
            {
                TimerExpired?.Invoke();
                return;
            }

            _tickRoutine = StartCoroutine(TickRoutine());
        }

        public void Stop()
        {
            if (_tickRoutine == null)
            {
                return;
            }

            StopCoroutine(_tickRoutine);
            _tickRoutine = null;
        }

        private IEnumerator TickRoutine()
        {
            var waitInstruction = new WaitForSeconds(Mathf.Max(0.2f, tickIntervalSeconds));
            while (_remainingSeconds > 0)
            {
                yield return waitInstruction;
                _remainingSeconds = Mathf.Max(0, _remainingSeconds - 1);
                SecondChanged?.Invoke(_remainingSeconds);
            }

            _tickRoutine = null;
            TimerExpired?.Invoke();
        }

        private void OnDisable()
        {
            Stop();
        }
    }
}
