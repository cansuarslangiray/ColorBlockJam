using System;
using System.Collections.Generic;
using Runtime.Domain.Enums;
using Runtime.Helpers;
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
        public List<int> seenFeatureIds = new();

        public static PlayerData CreateDefault() => new();

        public PlayerData Clone()
        {
            return new PlayerData
            {
                schemaVersion = schemaVersion,
                currentLevel = currentLevel,
                musicEnabled = musicEnabled,
                sfxEnabled = sfxEnabled,
                localeCode = localeCode,
                seenFeatureIds = new List<int>(seenFeatureIds ?? new List<int>())
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
            }
            else
            {
                localeCode = localeCode.Trim().ToLowerInvariant();
            }

            SanitizeSeenFeatures();
        }

        public bool HasSeenFeature(BlockFeature feature)
        {
            var sanitized = feature.Sanitize();
            if (sanitized == BlockFeature.Default)
            {
                return true;
            }

            seenFeatureIds ??= new List<int>();
            var targetRawValue = (int)sanitized;
            for (var i = 0; i < seenFeatureIds.Count; i++)
            {
                if (seenFeatureIds[i] == targetRawValue)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryMarkFeatureSeen(BlockFeature feature)
        {
            var sanitized = feature.Sanitize();
            if (sanitized == BlockFeature.Default)
            {
                return false;
            }

            seenFeatureIds ??= new List<int>();
            var targetRawValue = (int)sanitized;
            for (var i = 0; i < seenFeatureIds.Count; i++)
            {
                if (seenFeatureIds[i] == targetRawValue)
                {
                    return false;
                }
            }

            seenFeatureIds.Add(targetRawValue);
            return true;
        }

        public void ClearFeatureProgress()
        {
            seenFeatureIds ??= new List<int>();
            seenFeatureIds.Clear();
        }

        private void SanitizeSeenFeatures()
        {
            seenFeatureIds ??= new List<int>();
            if (seenFeatureIds.Count <= 0)
            {
                return;
            }

            var unique = new HashSet<int>();
            for (var i = seenFeatureIds.Count - 1; i >= 0; i--)
            {
                var sanitized = ((BlockFeature)seenFeatureIds[i]).Sanitize();
                if (sanitized == BlockFeature.Default)
                {
                    seenFeatureIds.RemoveAt(i);
                    continue;
                }

                var rawValue = (int)sanitized;
                if (unique.Add(rawValue))
                {
                    seenFeatureIds[i] = rawValue;
                    continue;
                }

                seenFeatureIds.RemoveAt(i);
            }
        }
    }
}
