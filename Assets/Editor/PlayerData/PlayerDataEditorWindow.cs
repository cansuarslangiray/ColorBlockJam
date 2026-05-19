#if UNITY_EDITOR
using System;
using System.IO;
using Runtime.Managers;
using Runtime.Persistence;
using UnityEditor;
using UnityEngine;
using PlayerSaveData = Runtime.Persistence.PlayerData;

namespace Editor.PlayerData
{
    public sealed class PlayerDataEditorWindow : EditorWindow
    {
        private const int MinLevel = 1;

        private PlayerSaveData _workingData;
        private int _manualLevelInput = 1;
        private Vector2 _scrollPosition;
        private string _statusMessage = string.Empty;
        private string _playerDataPath = string.Empty;

        [MenuItem("Tools/Color Block Jam/Player Data")]
        private static void OpenWindow()
        {
            var window = GetWindow<PlayerDataEditorWindow>("Player Data");
            window.minSize = new Vector2(360f, 300f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshPlayerDataPath();
            LoadFromDisk();
        }

        private void OnGUI()
        {
            DrawHeader();

            if (_workingData == null)
            {
                EditorGUILayout.HelpBox("Player data could not be loaded.", MessageType.Warning);
                if (GUILayout.Button("Try Reload"))
                {
                    LoadFromDisk();
                }

                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.Space(6f);

            DrawLevelSection();
            EditorGUILayout.Space(10f);
            DrawSettingsSection();
            EditorGUILayout.Space(10f);
            DrawActionsSection();

            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Player Data JSON", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_playerDataPath,
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload", GUILayout.Width(100f)))
                {
                    LoadFromDisk();
                }

                if (GUILayout.Button("Reveal File", GUILayout.Width(100f)))
                {
                    RevealPlayerDataFile();
                }
            }
        }

        private void DrawLevelSection()
        {
            EditorGUILayout.LabelField("Level Progress", EditorStyles.boldLabel);

            _workingData.currentLevel = Mathf.Max(MinLevel,
                EditorGUILayout.IntField("Current Level", _workingData.currentLevel));
            _manualLevelInput = Mathf.Max(MinLevel,
                EditorGUILayout.IntField("Set Level To", _manualLevelInput));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Level"))
                {
                    _workingData.currentLevel = Mathf.Max(MinLevel, _manualLevelInput);
                    SaveToDisk();
                }

                if (GUILayout.Button("Reset To Level 1"))
                {
                    _workingData.currentLevel = 1;
                    _manualLevelInput = 1;
                    SaveToDisk();
                }

                if (GUILayout.Button("Reset Seen Features"))
                {
                    _workingData.ClearFeatureProgress();
                    SaveToDisk();
                }
            }
        }

        private void DrawSettingsSection()
        {
            EditorGUILayout.LabelField("Saved Settings", EditorStyles.boldLabel);

            _workingData.musicEnabled = EditorGUILayout.Toggle("Music Enabled", _workingData.musicEnabled);
            _workingData.sfxEnabled = EditorGUILayout.Toggle("SFX Enabled", _workingData.sfxEnabled);
            _workingData.localeCode = EditorGUILayout.TextField("Locale Code", _workingData.localeCode ?? string.Empty);
        }

        private void DrawActionsSection()
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Save JSON"))
            {
                SaveToDisk();
            }

            if (GUILayout.Button("Save + Reload Runtime Instance"))
            {
                if (!SaveToDisk())
                {
                    return;
                }

                if (Application.isPlaying)
                {
                    LocalDataManager.Instance.ReloadFromDisk(false);
                    _statusMessage = "Saved and reloaded runtime LocalDataManager.";
                }
                else
                {
                    _statusMessage = "Saved. Runtime reload works only in Play Mode.";
                }
            }
        }

        private void RefreshPlayerDataPath()
        {
            _playerDataPath = LocalDataManager.GetDefaultPlayerDataFilePath();
        }

        private void LoadFromDisk()
        {
            RefreshPlayerDataPath();
            var path = _playerDataPath;

            if (!File.Exists(path))
            {
                _workingData = PlayerSaveData.CreateDefault();
                _workingData.Sanitize();
                _manualLevelInput = _workingData.currentLevel;
                _statusMessage = "No file found. Default data loaded in editor. Save JSON to create the file.";
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                _workingData = PlayerDataSerialization.Deserialize(json);
                _workingData.Sanitize();
                _manualLevelInput = _workingData.currentLevel;
                _statusMessage = $"Loaded at {DateTime.Now:HH:mm:ss}.";
            }
            catch (Exception ex)
            {
                _workingData = PlayerSaveData.CreateDefault();
                _workingData.Sanitize();
                _manualLevelInput = _workingData.currentLevel;
                _statusMessage = $"Failed to load file: {ex.Message}";
            }
        }

        private bool SaveToDisk()
        {
            if (_workingData == null)
            {
                _workingData = PlayerSaveData.CreateDefault();
            }

            _workingData.Sanitize();

            RefreshPlayerDataPath();
            var path = _playerDataPath;
            var directory = Path.GetDirectoryName(path);

            try
            {
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = PlayerDataSerialization.Serialize(_workingData, prettyPrint: true);
                File.WriteAllText(path, json);
                _statusMessage = $"Saved at {DateTime.Now:HH:mm:ss}.";
                return true;
            }
            catch (Exception ex)
            {
                _statusMessage = $"Failed to save file: {ex.Message}";
                return false;
            }
        }

        private void RevealPlayerDataFile()
        {
            RefreshPlayerDataPath();
            var path = _playerDataPath;
            if (File.Exists(path))
            {
                EditorUtility.RevealInFinder(path);
                return;
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
                EditorUtility.RevealInFinder(directory);
            }
        }
    }
}
#endif
