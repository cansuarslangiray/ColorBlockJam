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
        [SerializeField] private AudioClip blockMove;
        [SerializeField] private AudioClip blockMatchSuccess;

        private GameState _lastSyncedState;
        private bool _hasSyncedState;
        

        public void SyncMusicToState(GameState state)
        {
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

        public void OnMusicToggleChanged()
        {
            if (!SettingsManager.Instance.MusicEnabled)
            {
                StopMusic();
                return;
            }

            if (_hasSyncedState)
            {
                SyncMusicToState(_lastSyncedState);
            }
        }

        public void OnSfxToggleChanged()
        {
            if (!SettingsManager.Instance.SfxEnabled)
            {
                StopAllSfx();
            }
        }

        public void PlayButtonClick()
        {
            PlaySfx(buttonClick);
        }

        public void PlayLevelFail()
        {
            PlaySfx(levelFail);
        }

        public void PlayBlockSelect()
        {
            PlaySfx(blockSelect);
        }

        public void PlayBlockMove()
        {
            PlaySfx(blockMove);
        }

        public void PlayBlockMatchSuccess()
        {
            PlaySfx(blockMatchSuccess);
        }

        private void PlayMusic(AudioClip clip)
        {
            if (!SettingsManager.Instance.MusicEnabled || clip == null || musicSource == null)
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
            if (!SettingsManager.Instance.SfxEnabled || clip == null || sfxSource == null)
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
