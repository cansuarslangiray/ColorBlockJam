using System;
using Runtime.Domain.Enums;
using Runtime.Managers;
using Runtime.UI.Panels;
using UnityEngine.UIElements;

namespace UI.Panels
{
    public class SettingsPanel : GamePanel
    {
        public event Action<bool> OpenStateChanged;

        private VisualElement _musicToggle;
        private VisualElement _sfxToggle;
        private Label _musicToggleLabel;
        private Label _sfxToggleLabel;
        private Button _closeButton;
        private VisualElement _scrim;
        private bool _isOpen;

        public void SubscribeToState() => UIManager.Instance.GameStateChanged += HandleGameStateChanged;

        public void UnsubscribeFromState() => UIManager.Instance.GameStateChanged -= HandleGameStateChanged;

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

            RegisterSettingsEvents();
            ApplyToggleState(_musicToggle, _musicToggleLabel, true);
            ApplyToggleState(_sfxToggle, _sfxToggleLabel, true);
            Hide();
        }

        public void Toggle()
        {
            if (_isOpen)
            {
                Hide();
                return;
            }

            Show();
        }

        public override void Show()
        {
            var shouldNotify = !_isOpen;
            _isOpen = true;
            base.Show();
            if (shouldNotify)
            {
                OpenStateChanged?.Invoke(true);
            }
        }

        public override void Hide()
        {
            var shouldNotify = _isOpen;
            _isOpen = false;
            base.Hide();
            if (shouldNotify)
            {
                OpenStateChanged?.Invoke(false);
            }
        }

        private void OnDestroy()
        {
            _closeButton.clicked -= HandleCloseClicked;
            _musicToggle.UnregisterCallback<ClickEvent>(HandleMusicToggleClicked);
            _sfxToggle.UnregisterCallback<ClickEvent>(HandleSfxToggleClicked);
            _scrim.UnregisterCallback<ClickEvent>(HandleScrimClicked);
            UnregisterSettingsEvents();
        }

        private void RegisterSettingsEvents()
        {
            SettingsManager.Instance.MusicEnabledChanged -= HandleMusicEnabledChanged;
            SettingsManager.Instance.SfxEnabledChanged -= HandleSfxEnabledChanged;
            SettingsManager.Instance.MusicEnabledChanged += HandleMusicEnabledChanged;
            SettingsManager.Instance.SfxEnabledChanged += HandleSfxEnabledChanged;
        }

        private void UnregisterSettingsEvents()
        {
            SettingsManager.Instance.MusicEnabledChanged -= HandleMusicEnabledChanged;
            SettingsManager.Instance.SfxEnabledChanged -= HandleSfxEnabledChanged;
        }

        private void HandleMusicEnabledChanged(bool isEnabled) => ApplyToggleState(_musicToggle, _musicToggleLabel, isEnabled);
       
        private void HandleSfxEnabledChanged(bool isEnabled) => ApplyToggleState(_sfxToggle, _sfxToggleLabel, isEnabled);

        private static void ApplyToggleState(VisualElement toggle, Label toggleLabel, bool isEnabled)
        {
            toggleLabel.text = isEnabled ? "On" : "Off";
            toggle.EnableInClassList("settings-switch-on", isEnabled);
            toggle.EnableInClassList("settings-switch-off", !isEnabled);
        }

        private void HandleGameStateChanged(GameState _) => Hide();

        private void HandleCloseClicked() => ClosePanel();

        private void HandleScrimClicked(ClickEvent _) => ClosePanel();

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
            SettingsManager.Instance.SetMusicEnabled(!IsToggleEnabled(_musicToggle));
        }

        private void HandleSfxToggleClicked(ClickEvent _)
        {
            AudioManager.Instance.PlayButtonClick();
            SettingsManager.Instance.SetSfxEnabled(!IsToggleEnabled(_sfxToggle));
        }

        private static bool IsToggleEnabled(VisualElement toggle) => toggle.ClassListContains("settings-switch-on");
    }
}
