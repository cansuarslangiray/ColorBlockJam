using System;
using UnityEngine;

namespace Runtime.Persistence
{
    [Serializable]
    public sealed class PlayerData
    {
        private const int CurrentSchemaVersion = 1;
        private const string DefaultLocaleCode = "en";

        public int schemaVersion = CurrentSchemaVersion;
        public int currentLevel = 1;
        public bool musicEnabled = true;
        public bool sfxEnabled = true;
        public string localeCode = DefaultLocaleCode;

        public static PlayerData CreateDefault() => new();

        public PlayerData Clone()
        {
            return new PlayerData
            {
                schemaVersion = schemaVersion,
                currentLevel = currentLevel,
                musicEnabled = musicEnabled,
                sfxEnabled = sfxEnabled,
                localeCode = localeCode
            };
        }

        public void Sanitize(int maxLevel = int.MaxValue)
        {
            schemaVersion = Mathf.Max(1, schemaVersion);
            var resolvedMaxLevel = maxLevel > 0 ? maxLevel : int.MaxValue;
            currentLevel = Mathf.Clamp(currentLevel, 1, resolvedMaxLevel);

            if (string.IsNullOrWhiteSpace(localeCode))
            {
                localeCode = DefaultLocaleCode;
                return;
            }

            localeCode = localeCode.Trim().ToLowerInvariant();
        }
    }
}
