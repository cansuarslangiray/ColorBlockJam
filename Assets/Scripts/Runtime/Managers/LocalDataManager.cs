using System;
using System.IO;
using Runtime.Persistence;
using UnityEngine;

namespace Runtime.Managers
{
    public sealed class LocalDataManager
    {
        private const string PlayerDataFileName = "player-data.json";
        private static readonly LocalDataManager SharedInstance = new();
        private const bool SavePrettyPrintedJson = true;

        private PlayerData _playerData;
        private bool _isLoaded;

        public static LocalDataManager Instance => SharedInstance;

        public string PlayerDataFilePath => GetDefaultPlayerDataFilePath();

        private LocalDataManager() { }

        public static string GetDefaultPlayerDataFilePath() =>
            Path.Combine(Application.persistentDataPath, PlayerDataFileName);

        public PlayerData GetPlayerData()
        {
            EnsureLoaded();
            return _playerData;
        }

        public void SetCurrentLevel(int currentLevel, bool saveImmediately = true)
        {
            UpdatePlayerData(data => data.currentLevel = Mathf.Max(1, currentLevel), saveImmediately);
        }

        public void SetCurrentLevelAsProgress(int currentLevel, bool saveImmediately = true)
        {
            var sanitizedLevel = Mathf.Max(1, currentLevel);
            UpdatePlayerData(data => data.currentLevel = Mathf.Max(data.currentLevel, sanitizedLevel), saveImmediately);
        }

        public void UpdateSettings(bool musicEnabled, bool sfxEnabled, string localeCode, bool saveImmediately = true)
        {
            UpdatePlayerData(data =>
            {
                data.musicEnabled = musicEnabled;
                data.sfxEnabled = sfxEnabled;

                if (!string.IsNullOrWhiteSpace(localeCode))
                {
                    data.localeCode = localeCode;
                }
            }, saveImmediately);
        }

        public void UpdatePlayerData(Action<PlayerData> mutate, bool saveImmediately = true)
        {
            if (mutate == null)
            {
                return;
            }

            var data = GetPlayerData();
            mutate(data);
            data.Sanitize();

            if (saveImmediately)
            {
                SaveInternal(data);
            }
        }

        public void ReloadFromDisk(bool saveDefaultsWhenMissing = true)
        {
            _isLoaded = false;
            _playerData = null;
            EnsureLoaded(saveDefaultsWhenMissing);
        }

        public void Save()
        {
            if (!_isLoaded || _playerData == null)
            {
                EnsureLoaded();
                return;
            }

            SaveInternal(_playerData);
        }

        private void EnsureLoaded(bool saveDefaultsWhenMissing = true)
        {
            if (_isLoaded && _playerData != null)
            {
                return;
            }

            var path = PlayerDataFilePath;
            var fileExists = File.Exists(path);

            if (!fileExists)
            {
                _playerData = PlayerData.CreateDefault();
                _playerData.Sanitize();
                _isLoaded = true;

                if (saveDefaultsWhenMissing)
                {
                    SaveInternal(_playerData);
                }

                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                _playerData = PlayerDataSerialization.Deserialize(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to read player data from '{path}': {ex.Message}");
                _playerData = PlayerData.CreateDefault();
                _playerData.Sanitize();
            }

            _isLoaded = true;
        }

        private void SaveInternal(PlayerData playerData)
        {
            var sanitized = (playerData ?? PlayerData.CreateDefault()).Clone();
            sanitized.Sanitize();

            var path = PlayerDataFilePath;
            var directoryPath = Path.GetDirectoryName(path);

            try
            {
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var json = PlayerDataSerialization.Serialize(sanitized, SavePrettyPrintedJson);
                File.WriteAllText(path, json);
                _playerData = sanitized;
                _isLoaded = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to write player data to '{path}': {ex.Message}");
            }
        }
    }
}
