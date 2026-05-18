using System;
using Runtime.Core;
using UnityEngine;

namespace Runtime.Managers
{
    [DisallowMultipleComponent]
    public class SettingsManager : SingletonMonoBehaviour<SettingsManager>
    {
        [SerializeField] private bool musicEnabled = true;
        [SerializeField] private bool sfxEnabled = true;

        public event Action<bool> MusicEnabledChanged;
        public event Action<bool> SfxEnabledChanged;

        public bool IsMusicEnabled => musicEnabled;
        public bool IsSfxEnabled => sfxEnabled;

        private void OnEnable()
        {
            MusicEnabledChanged?.Invoke(musicEnabled);
            SfxEnabledChanged?.Invoke(sfxEnabled);
        }

        public void SetMusicEnabled(bool isEnabled)
        {
            if (musicEnabled == isEnabled)
            {
                return;
            }

            musicEnabled = isEnabled;
            MusicEnabledChanged?.Invoke(isEnabled);
        }

        public void SetSfxEnabled(bool isEnabled)
        {
            if (sfxEnabled == isEnabled)
            {
                return;
            }

            sfxEnabled = isEnabled;
            SfxEnabledChanged?.Invoke(isEnabled);
        }
    }
}
