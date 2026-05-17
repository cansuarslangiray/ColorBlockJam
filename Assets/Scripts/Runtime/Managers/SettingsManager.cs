using System;
using Runtime.Core;
using UnityEngine;

namespace Runtime.Managers
{
    [DisallowMultipleComponent]
    public class SettingsManager : SingletonMonoBehaviour<SettingsManager>
    {
        public event Action<bool> MusicEnabledChanged;
        public event Action<bool> SfxEnabledChanged;

        public void SetMusicEnabled(bool isEnabled) => MusicEnabledChanged?.Invoke(isEnabled);

        public void SetSfxEnabled(bool isEnabled) => SfxEnabledChanged?.Invoke(isEnabled);
    }
}