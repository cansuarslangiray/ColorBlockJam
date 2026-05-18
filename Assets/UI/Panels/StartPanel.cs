using System;
using Runtime.Domain.Enums;
using Runtime.Managers;
using UnityEngine.UIElements;

namespace UI.Panels
{
    public class StartPanel : GamePanel
    {
        private Button _startButton;
        public event Action StartRequested;
        protected override bool UseSafeAreaPadding => false;

        protected override void CacheElements()
        {
            _startButton = Root.Q<Button>("start-button");

            _startButton.clicked += HandleStartClicked;
            Show();
        }

        public void SubscribeToState() => UIManager.Instance.GameStateChanged += HandleGameStateChanged;

        public void UnsubscribeFromState() => UIManager.Instance.GameStateChanged -= HandleGameStateChanged;

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.StartScreen)
            {
                Show();
                return;
            }

            Hide();
        }

        private void HandleStartClicked()
        {
            AudioManager.Instance.PlayButtonClick();
            StartRequested?.Invoke();
        }

        protected override void OnDestroy()
        {
            if (_startButton != null)
            {
                _startButton.clicked -= HandleStartClicked;
            }

            base.OnDestroy();
        }
    }
}
