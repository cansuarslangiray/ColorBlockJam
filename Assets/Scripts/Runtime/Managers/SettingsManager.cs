using Runtime.Core;
using UnityEngine;

namespace Runtime.Managers
{
    [DisallowMultipleComponent]
    public class SettingsManager : SingletonMonoBehaviour<SettingsManager>
    {
        public bool MusicEnabled { get; private set; } = true;
        public bool SfxEnabled { get; private set; } = true;
        
        public bool SetMusicEnabled(bool isEnabled)
        {
            if (MusicEnabled == isEnabled)
            {
                return false;
            }

            MusicEnabled = isEnabled;
            return true;
        }

        public bool SetSfxEnabled(bool isEnabled)
        {
            if (SfxEnabled == isEnabled)
            {
                return false;
            }

            SfxEnabled = isEnabled;
            return true;
        }
    }
}
