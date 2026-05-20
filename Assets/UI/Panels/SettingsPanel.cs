using System;
using Runtime.Domain.Enums;
using Runtime.Managers;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace UI.Panels
{
    public class SettingsPanel : GamePanel
    {
        private const string EnglishLocaleCode = "en";
        private const string TurkishLocaleCode = "tr";

        public event Action<bool> OpenStateChanged;

        private VisualElement _musicToggle;
        private VisualElement _sfxToggle;
        private Label _musicToggleLabel;
        private Label _sfxToggleLabel;
        private Button _languageToggleButton;
        private Button _closeButton;
        private VisualElement _scrim;
        private bool _isOpen;
        private bool _settingsEventsRegistered;

        protected override void CacheElements()
        {
            _musicToggle = Root.Q<VisualElement>("settings-music-toggle");
            _sfxToggle = Root.Q<VisualElement>("settings-sfx-toggle");
            _musicToggleLabel = Root.Q<Label>("settings-music-toggle-label");
            _sfxToggleLabel = Root.Q<Label>("settings-sfx-toggle-label");
            _languageToggleButton = Root.Q<Button>("settings-language-toggle");
            _closeButton = Root.Q<Button>("settings-close");
            _scrim = Root.Q<VisualElement>("settings-scrim");

            _closeButton.clicked += HandleCloseClicked;
            _languageToggleButton.clicked += HandleLanguageToggleClicked;
            _musicToggle.RegisterCallback<ClickEvent>(HandleMusicToggleClicked);
            _sfxToggle.RegisterCallback<ClickEvent>(HandleSfxToggleClicked);
            _scrim.RegisterCallback<ClickEvent>(HandleScrimClicked);

            RegisterSettingsEvents();
            ApplyToggleState(_musicToggle, _musicToggleLabel, SettingsManager.Instance.IsMusicEnabled);
            ApplyToggleState(_sfxToggle, _sfxToggleLabel, SettingsManager.Instance.IsSfxEnabled);
            ApplyLanguageSelection(LocalizationSettings.SelectedLocale);
            Hide();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RegisterSettingsEvents();
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

        protected override void OnDestroy()
        {
            if (_closeButton != null)
            {
                _closeButton.clicked -= HandleCloseClicked;
            }

            if (_languageToggleButton != null)
            {
                _languageToggleButton.clicked -= HandleLanguageToggleClicked;
            }

            _musicToggle?.UnregisterCallback<ClickEvent>(HandleMusicToggleClicked);
            _sfxToggle?.UnregisterCallback<ClickEvent>(HandleSfxToggleClicked);
            _scrim?.UnregisterCallback<ClickEvent>(HandleScrimClicked);
            UnregisterSettingsEvents();
            base.OnDestroy();
        }

        private void RegisterSettingsEvents()
        {
            if (_settingsEventsRegistered)
            {
                return;
            }

      
            SettingsManager.Instance.MusicEnabledChanged += HandleMusicEnabledChanged;
            SettingsManager.Instance.SfxEnabledChanged += HandleSfxEnabledChanged;
            _settingsEventsRegistered = true;
            HandleMusicEnabledChanged(SettingsManager.Instance.IsMusicEnabled);
            HandleSfxEnabledChanged(SettingsManager.Instance.IsSfxEnabled);
        }

        private void UnregisterSettingsEvents()
        {
            if (!_settingsEventsRegistered)
            {
                return;
            }

            var instance = SettingsManager.Instance;
            if (instance != null) instance.MusicEnabledChanged -= HandleMusicEnabledChanged;
            var manager = SettingsManager.Instance;
            if (manager != null) manager.SfxEnabledChanged -= HandleSfxEnabledChanged;
            _settingsEventsRegistered = false;
        }

        private void HandleMusicEnabledChanged(bool isEnabled) =>
            ApplyToggleState(_musicToggle, _musicToggleLabel, isEnabled);

        private void HandleSfxEnabledChanged(bool isEnabled) =>
            ApplyToggleState(_sfxToggle, _sfxToggleLabel, isEnabled);

        private void ApplyToggleState(VisualElement toggle, Label toggleLabel, bool isEnabled)
        {
            toggleLabel.text = string.Empty;
            toggle.EnableInClassList("settings-switch-on", isEnabled);
            toggle.EnableInClassList("settings-switch-off", !isEnabled);
        }

        public override void RefreshLocalization()
        {
            base.RefreshLocalization();
            ApplyToggleState(_musicToggle, _musicToggleLabel, SettingsManager.Instance.IsMusicEnabled);
            ApplyToggleState(_sfxToggle, _sfxToggleLabel, SettingsManager.Instance.IsSfxEnabled);
            ApplyLanguageSelection(LocalizationSettings.SelectedLocale);
        }

        private void ApplyLanguageSelection(Locale locale)
        {
            var localeCode = locale?.Identifier.Code ?? string.Empty;
            var shouldSwitchToTurkish = localeCode.StartsWith(EnglishLocaleCode, StringComparison.OrdinalIgnoreCase);
            _languageToggleButton.text = shouldSwitchToTurkish ? "TR" : "EN";
        }

        protected override void OnGameStateChanged(GameState _) => Hide();

        private void HandleCloseClicked() => ClosePanel();

        private void HandleScrimClicked(ClickEvent _) => ClosePanel();

        private void ClosePanel()
        {
            if (!_isOpen)
            {
                return;
            }

            AudioManager.Instance?.PlayButtonClick();
            Hide();
        }

        private void HandleMusicToggleClicked(ClickEvent _)
        {
            AudioManager.Instance.PlayButtonClick();
            SettingsManager.Instance.SetMusicEnabled(!SettingsManager.Instance.IsMusicEnabled);
        }

        private void HandleSfxToggleClicked(ClickEvent _)
        {
            AudioManager.Instance.PlayButtonClick();
            SettingsManager.Instance.SetSfxEnabled(!SettingsManager.Instance.IsSfxEnabled);
        }

        private void HandleLanguageToggleClicked()
        {
            AudioManager.Instance?.PlayButtonClick();
            var currentLocaleCode = LocalizationSettings.SelectedLocale?.Identifier.Code ?? string.Empty;
            var targetLocaleCode = currentLocaleCode.StartsWith(EnglishLocaleCode, StringComparison.OrdinalIgnoreCase)
                ? TurkishLocaleCode
                : EnglishLocaleCode;
            SetLocaleByCode(targetLocaleCode);
        }

        private static void SetLocaleByCode(string localeCode)
        {
            var locale = LocalizationSettings.AvailableLocales?.GetLocale(localeCode);
            if (locale != null)
            {
                LocalizationSettings.SelectedLocale = locale;
            }
        }

    }
}
