using System;
using System.Collections;
using Runtime.Core;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Runtime.Managers
{
    [DisallowMultipleComponent]
    public class SettingsManager : SingletonMonoBehaviour<SettingsManager>
    {
        [SerializeField] private bool musicEnabled = true;
        [SerializeField] private bool sfxEnabled = true;

        public event Action<bool> MusicEnabledChanged;
        public event Action<bool> SfxEnabledChanged;
        public event Action<string> LocaleCodeChanged;

        public bool IsMusicEnabled => musicEnabled;
        public bool IsSfxEnabled => sfxEnabled;
        public string CurrentLocaleCode => LocalizationSettings.SelectedLocale?.Identifier.Code ?? string.Empty;

        private string _savedLocaleCode;
        private bool _localeEventsRegistered;
        private bool _isApplyingLocaleFromSave;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this)
            {
                return;
            }

            ApplySavedSettingsFromLocalData();
        }

        private void Start()
        {
            if (string.IsNullOrWhiteSpace(_savedLocaleCode))
            {
                return;
            }

            StartCoroutine(ApplySavedLocaleWhenReady(_savedLocaleCode));
        }

        private void OnEnable()
        {
            RegisterLocaleEvents();
            MusicEnabledChanged?.Invoke(musicEnabled);
            SfxEnabledChanged?.Invoke(sfxEnabled);
            LocaleCodeChanged?.Invoke(CurrentLocaleCode);
        }

        private void OnDisable() => UnregisterLocaleEvents();

        public void SetMusicEnabled(bool isEnabled)
        {
            if (musicEnabled == isEnabled)
            {
                return;
            }

            musicEnabled = isEnabled;
            MusicEnabledChanged?.Invoke(isEnabled);
            PersistSettings();
        }

        public void SetSfxEnabled(bool isEnabled)
        {
            if (sfxEnabled == isEnabled)
            {
                return;
            }

            sfxEnabled = isEnabled;
            SfxEnabledChanged?.Invoke(isEnabled);
            PersistSettings();
        }

        private void ApplySavedSettingsFromLocalData()
        {
            var localDataManager = LocalDataManager.Instance;
            var playerData = localDataManager.GetPlayerData();
            musicEnabled = playerData.musicEnabled;
            sfxEnabled = playerData.sfxEnabled;
            _savedLocaleCode = playerData.localeCode;
        }

        private void PersistSettings()
        {
            var localDataManager = LocalDataManager.Instance;
            localDataManager.UpdateSettings(musicEnabled, sfxEnabled, CurrentLocaleCode);
        }

        private IEnumerator ApplySavedLocaleWhenReady(string localeCode)
        {
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                yield break;
            }

            yield return LocalizationSettings.InitializationOperation;

            var locale = LocalizationSettings.AvailableLocales?.GetLocale(localeCode);
            if (locale == null)
            {
                yield break;
            }

            _isApplyingLocaleFromSave = true;
            LocalizationSettings.SelectedLocale = locale;
            _isApplyingLocaleFromSave = false;
            LocaleCodeChanged?.Invoke(CurrentLocaleCode);
        }

        private void RegisterLocaleEvents()
        {
            if (_localeEventsRegistered)
            {
                return;
            }

            LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;
            _localeEventsRegistered = true;
        }

        private void UnregisterLocaleEvents()
        {
            if (!_localeEventsRegistered)
            {
                return;
            }

            LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
            _localeEventsRegistered = false;
        }

        private void HandleSelectedLocaleChanged(Locale locale)
        {
            LocaleCodeChanged?.Invoke(locale?.Identifier.Code ?? string.Empty);
            if (_isApplyingLocaleFromSave)
            {
                return;
            }

            PersistSettings();
        }
    }
}
