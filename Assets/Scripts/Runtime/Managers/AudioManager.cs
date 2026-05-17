using Runtime.Core;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Managers
{
    [DisallowMultipleComponent]
    public class AudioManager : SingletonMonoBehaviour<AudioManager>
    {
        [Header("Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Music")]
        [SerializeField] private AudioClip gameplayAmbientMusic;

        [Header("UI SFX")]
        [SerializeField] private AudioClip buttonClick;
        [SerializeField] private AudioClip levelFail;

        [Header("Gameplay SFX")]
        [SerializeField] private AudioClip blockSelect;
        [SerializeField] private AudioClip blockMatchSuccess;

        private GameState _lastSyncedState;
        private bool _hasSyncedState;
        private bool _settingsEventsRegistered;
        private bool _musicEnabled = true;
        private bool _sfxEnabled = true;

        private void OnEnable()
        {
            TryRegisterSettingsEvents();
            RefreshSettingsSnapshot();
        }

        private void OnDisable() => UnregisterSettingsEvents();

        public void SyncMusicToState(GameState state)
        {
            TryRegisterSettingsEvents();
            _lastSyncedState = state;
            _hasSyncedState = true;

            switch (state)
            {
                case GameState.Playing:
                case GameState.LevelCompleted:
                case GameState.LevelFailed:
                    PlayMusic(gameplayAmbientMusic);
                    return;
                case GameState.StartScreen:
                case GameState.GameCompleted:
                default:
                    StopMusic();
                    return;
            }
        }

        private void TryRegisterSettingsEvents()
        {
            if (_settingsEventsRegistered || !SettingsManager.Instance)
            {
                return;
            }

            SettingsManager.Instance.MusicEnabledChanged += HandleMusicEnabledChanged;
            SettingsManager.Instance.SfxEnabledChanged += HandleSfxEnabledChanged;
            _settingsEventsRegistered = true;
            _musicEnabled = SettingsManager.Instance.MusicEnabled;
            _sfxEnabled = SettingsManager.Instance.SfxEnabled;
        }

        private void UnregisterSettingsEvents()
        {
            if (!_settingsEventsRegistered || !SettingsManager.Instance)
            {
                _settingsEventsRegistered = false;
                return;
            }

            SettingsManager.Instance.MusicEnabledChanged -= HandleMusicEnabledChanged;
            SettingsManager.Instance.SfxEnabledChanged -= HandleSfxEnabledChanged;
            _settingsEventsRegistered = false;
        }

        private void RefreshSettingsSnapshot()
        {
            if (!SettingsManager.Instance)
            {
                _musicEnabled = true;
                _sfxEnabled = true;
                return;
            }

            _musicEnabled = SettingsManager.Instance.MusicEnabled;
            _sfxEnabled = SettingsManager.Instance.SfxEnabled;
            HandleMusicEnabledChanged(_musicEnabled);
            HandleSfxEnabledChanged(_sfxEnabled);
        }

        private void HandleMusicEnabledChanged(bool isEnabled)
        {
            _musicEnabled = isEnabled;
            if (!_musicEnabled)
            {
                StopMusic();
                return;
            }

            if (_hasSyncedState)
            {
                SyncMusicToState(_lastSyncedState);
            }
        }

        private void HandleSfxEnabledChanged(bool isEnabled)
        {
            _sfxEnabled = isEnabled;
            if (!_sfxEnabled)
            {
                StopAllSfx();
            }
        }

        public void PlayButtonClick() => PlaySfx(buttonClick);

        public void PlayLevelFail() => PlaySfx(levelFail);

        public void PlayBlockSelect() => PlaySfx(blockSelect);

        public void PlayBlockMatchSuccess() => PlaySfx(blockMatchSuccess);

        private void PlayMusic(AudioClip clip)
        {
            if (!_settingsEventsRegistered)
            {
                TryRegisterSettingsEvents();
            }

            if (!_musicEnabled || clip == null || musicSource == null)
            {
                StopMusic();
                return;
            }

            if (musicSource.isPlaying && musicSource.clip == clip)
            {
                return;
            }

            musicSource.loop = true;
            musicSource.clip = clip;
            musicSource.pitch = 1f;
            musicSource.Play();
        }

        private void StopMusic()
        {
            if (musicSource == null)
            {
                return;
            }

            if (musicSource.isPlaying)
            {
                musicSource.Stop();
            }

            musicSource.clip = null;
        }

        private void PlaySfx(AudioClip clip)
        {
            if (!_settingsEventsRegistered)
            {
                TryRegisterSettingsEvents();
            }

            if (!_sfxEnabled || clip == null || sfxSource == null)
            {
                return;
            }

            sfxSource.Stop();
            sfxSource.clip = clip;
            sfxSource.loop = false;
            sfxSource.pitch = 1f;
            sfxSource.Play();
        }

        private void StopAllSfx()
        {
            if (sfxSource == null)
            {
                return;
            }

            if (sfxSource.isPlaying)
            {
                sfxSource.Stop();
            }

            sfxSource.clip = null;
        }
    }
}
