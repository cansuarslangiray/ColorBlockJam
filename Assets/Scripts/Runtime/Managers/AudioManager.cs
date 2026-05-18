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
        [SerializeField] private SettingsManager settingsManager;
        [SerializeField] private StateManager stateManager;

        [Header("Music")]
        [SerializeField] private AudioClip gameplayAmbientMusic;

        [Header("UI SFX")]
        [SerializeField] private AudioClip buttonClick;
        [SerializeField] private AudioClip levelFail;

        [Header("Gameplay SFX")]
        [SerializeField] private AudioClip blockSelect;
        [SerializeField] private AudioClip blockMatchSuccess;
        private bool _settingsEventsRegistered;

        private void OnEnable()
        {
            TryRegisterSettingsEvents();
        }

        private void OnDisable() => UnregisterSettingsEvents();

        public void SyncMusicToState(GameState state)
        {
            ApplyMusicState(state);
        }

        private void ApplyMusicState(GameState state)
        {
            if (musicSource != null && musicSource.mute)
            {
                StopMusic();
                return;
            }

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
            if (_settingsEventsRegistered || settingsManager == null)
            {
                return;
            }

            settingsManager.MusicEnabledChanged += HandleMusicEnabledChanged;
            settingsManager.SfxEnabledChanged += HandleSfxEnabledChanged;
            _settingsEventsRegistered = true;

            HandleMusicEnabledChanged(settingsManager.IsMusicEnabled);
            HandleSfxEnabledChanged(settingsManager.IsSfxEnabled);
        }

        private void UnregisterSettingsEvents()
        {
            if (!_settingsEventsRegistered)
            {
                return;
            }

            if (settingsManager != null)
            {
                settingsManager.MusicEnabledChanged -= HandleMusicEnabledChanged;
                settingsManager.SfxEnabledChanged -= HandleSfxEnabledChanged;
            }

            _settingsEventsRegistered = false;
        }

        private void HandleMusicEnabledChanged(bool isEnabled)
        {
            if (musicSource)
            {
                musicSource.mute = !isEnabled;
            }

            if (!isEnabled)
            {
                StopMusic();
                return;
            }

            ApplyMusicState(stateManager != null ? stateManager.CurrentState : GameState.StartScreen);
        }

        private void HandleSfxEnabledChanged(bool isEnabled)
        {
            if (sfxSource)
            {
                sfxSource.mute = !isEnabled;
            }

            if (!isEnabled)
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
            if (musicSource == null || clip == null)
            {
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
            if (clip == null || sfxSource == null || sfxSource.mute)
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
