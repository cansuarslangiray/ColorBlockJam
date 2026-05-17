using System;
using Runtime.Domain.Enums;
using Runtime.Managers;
using Runtime.UI.Panels;
using UnityEngine.UIElements;

namespace UI.Panels
{
    public class SettingsPanel : GamePanel
    {
        public event Action<bool> OpenStateChanged = delegate { };

        private VisualElement _musicToggle;
        private VisualElement _sfxToggle;
        private Label _musicToggleLabel;
        private Label _sfxToggleLabel;
        private Button _closeButton;
        private VisualElement _scrim;
        private bool _isOpen;
        private bool _isReady;

        public void SubscribeToState()
        {
            UIManager.Instance.GameStateChanged += HandleGameStateChanged;
        }

        public void UnsubscribeFromState()
        {
            UIManager.Instance.GameStateChanged -= HandleGameStateChanged;
        }

        protected override void CacheElements()
        {
            _musicToggle = Root.Q<VisualElement>("settings-music-toggle");
            _sfxToggle = Root.Q<VisualElement>("settings-sfx-toggle");
            _musicToggleLabel = Root.Q<Label>("settings-music-toggle-label");
            _sfxToggleLabel = Root.Q<Label>("settings-sfx-toggle-label");
            _closeButton = Root.Q<Button>("settings-close");
            _scrim = Root.Q<VisualElement>("settings-scrim");
            
            _closeButton.clicked += HandleCloseClicked;
            _musicToggle.RegisterCallback<ClickEvent>(HandleMusicToggleClicked);
            _sfxToggle.RegisterCallback<ClickEvent>(HandleSfxToggleClicked);
            _scrim.RegisterCallback<ClickEvent>(HandleScrimClicked);

            _isReady = true;
            RefreshFromSettings();
            Hide();
        }

        public void Toggle()
        {
            if (!_isReady)
            {
                return;
            }

            if (_isOpen)
            {
                Hide();
                return;
            }

            Show();
        }

        public override void Show()
        {
            if (!_isReady)
            {
                return;
            }

            RefreshFromSettings();
            var shouldNotify = !_isOpen;
            _isOpen = true;
            base.Show();
            if (shouldNotify)
            {
                OpenStateChanged(true);
            }
        }

        public override void Hide()
        {
            if (!_isReady)
            {
                return;
            }

            var shouldNotify = _isOpen;
            _isOpen = false;
            base.Hide();
            if (shouldNotify)
            {
                OpenStateChanged(false);
            }
        }

        private void OnDestroy()
        {
            if (!_isReady)
            {
                return;
            }

            _closeButton.clicked -= HandleCloseClicked;
            _musicToggle.UnregisterCallback<ClickEvent>(HandleMusicToggleClicked);
            _sfxToggle.UnregisterCallback<ClickEvent>(HandleSfxToggleClicked);
            _scrim.UnregisterCallback<ClickEvent>(HandleScrimClicked);
        }

        private void RefreshFromSettings()
        {
            var settings = SettingsManager.Instance;
            ApplyToggleState(_musicToggle, _musicToggleLabel, settings.MusicEnabled);
            ApplyToggleState(_sfxToggle, _sfxToggleLabel, settings.SfxEnabled);
        }

        private static void ApplyToggleState(VisualElement toggle, Label toggleLabel, bool isEnabled)
        {
            toggleLabel.text = isEnabled ? "On" : "Off";
            toggle.EnableInClassList("settings-switch-on", isEnabled);
            toggle.EnableInClassList("settings-switch-off", !isEnabled);
        }

        private void HandleGameStateChanged(GameState _)
        {
            Hide();
        }

        private void HandleCloseClicked()
        {
            ClosePanel();
        }

        private void HandleScrimClicked(ClickEvent _)
        {
            ClosePanel();
        }

        private void ClosePanel()
        {
            if (!_isOpen)
            {
                return;
            }

            AudioManager.Instance.PlayButtonClick();
            Hide();
        }

        private void HandleMusicToggleClicked(ClickEvent _)
        {
            AudioManager.Instance.PlayButtonClick();
            var settings = SettingsManager.Instance;
            settings.SetMusicEnabled(!settings.MusicEnabled);
            AudioManager.Instance.OnMusicToggleChanged();
            RefreshFromSettings();
        }

        private void HandleSfxToggleClicked(ClickEvent _)
        {
            AudioManager.Instance.PlayButtonClick();
            var settings = SettingsManager.Instance;
            settings.SetSfxEnabled(!settings.SfxEnabled);
            AudioManager.Instance.OnSfxToggleChanged();
            RefreshFromSettings();
        }
    }
}
