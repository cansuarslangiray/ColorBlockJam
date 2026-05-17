using System;
using Runtime.Core;
using UnityEngine;

namespace Runtime.Managers
{
    [DisallowMultipleComponent]
    public class SettingsManager : SingletonMonoBehaviour<SettingsManager>
    {
        public bool MusicEnabled { get; private set; } = true;
        public bool SfxEnabled { get; private set; } = true;

        public event Action<bool> MusicEnabledChanged;
        public event Action<bool> SfxEnabledChanged;

        public bool SetMusicEnabled(bool isEnabled)
        {
            if (MusicEnabled == isEnabled)
            {
                return false;
            }

            MusicEnabled = isEnabled;
            MusicEnabledChanged?.Invoke(isEnabled);
            return true;
        }

        public bool SetSfxEnabled(bool isEnabled)
        {
            if (SfxEnabled == isEnabled)
            {
                return false;
            }

            SfxEnabled = isEnabled;
            SfxEnabledChanged?.Invoke(isEnabled);
            return true;
        }
    }
}