using System.Collections;
using Runtime.Controllers;
using Runtime.Controllers.BlockSceneBuilder;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Managers.GameFlow;
using UnityEngine;

namespace Runtime.Managers
{
    [DisallowMultipleComponent]
    public class GameManager : SingletonMonoBehaviour<GameManager>
    {
        [Header("Core References")] [SerializeField]
        private LevelCollection levelCollection;

        [SerializeField] private BoardController boardController;
        [SerializeField] private BlockSceneBuilder blockSceneBuilder;
        [SerializeField] private StateManager stateManager;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private Camera gameplayCamera;

        [Header("Flow Settings")] [SerializeField]
        private float levelCompletePanelDelay = 0.4f;

        [Header("Camera Framing")] [SerializeField] [Min(0)]
        private int closeCameraLevelCount = 2;

        [SerializeField] [Range(0.6f, 1f)] private float closeCameraDistanceMultiplier = 0.9f;

        private bool _transitionInProgress;
        private LevelProgression _levelProgression;
        private GameplayCameraFramer _cameraFramer;
        private LocalDataManager _localDataManager;
        private bool _boardEventsRegistered;
        private bool _stateEventsRegistered;
        private bool _uiEventsRegistered;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this)
            {
                return;
            }

            TryResolveSceneReferences();
            if (!HasRequiredReferences())
            {
                Debug.LogError("GameManager is missing one or more serialized core references.", this);
                enabled = false;
                return;
            }

            InitializeCollaborators();
        }

        private void OnValidate()
        {
            TryResolveSceneReferences();
        }

        private void Start()
        {
            if (!enabled)
            {
                return;
            }

            InitializeRun();
        }

        private void OnEnable()
        {
            if (!enabled)
            {
                return;
            }

            RegisterEvents();
        }

        private void OnDisable() => UnregisterEvents();

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                _localDataManager?.Save();
            }
        }

        private void OnApplicationQuit() => _localDataManager?.Save();

        private void InitializeCollaborators()
        {
            _localDataManager = LocalDataManager.Instance;
            _levelProgression = new LevelProgression(levelCollection);
            _cameraFramer = new GameplayCameraFramer(boardController, gameplayCamera, closeCameraLevelCount,
                closeCameraDistanceMultiplier);
        }

        private void RegisterEvents()
        {
            if (!_boardEventsRegistered && boardController != null)
            {
                boardController.LevelCompleted += OnLevelCompleted;
                _boardEventsRegistered = true;
            }

            if (!_stateEventsRegistered && stateManager != null)
            {
                stateManager.OnStateChanged += HandleStateChanged;
                _stateEventsRegistered = true;
            }

            if (!_uiEventsRegistered && uiManager != null)
            {
                uiManager.LevelTimerExpired += HandleTimerExpired;
                uiManager.StartRequested += HandleStartRequested;
                uiManager.EndGameActionRequested += HandleEndGameActionRequested;
                uiManager.ReloadRequested += HandleReloadRequested;
                _uiEventsRegistered = true;
            }
        }

        private void UnregisterEvents()
        {
            if (_boardEventsRegistered && boardController != null)
            {
                boardController.LevelCompleted -= OnLevelCompleted;
            }

            _boardEventsRegistered = false;

            if (_stateEventsRegistered && stateManager != null)
            {
                stateManager.OnStateChanged -= HandleStateChanged;
            }

            _stateEventsRegistered = false;

            if (_uiEventsRegistered && uiManager != null)
            {
                uiManager.LevelTimerExpired -= HandleTimerExpired;
                uiManager.StartRequested -= HandleStartRequested;
                uiManager.EndGameActionRequested -= HandleEndGameActionRequested;
                uiManager.ReloadRequested -= HandleReloadRequested;
            }

            _uiEventsRegistered = false;
        }

        private void InitializeRun()
        {
            _levelProgression.SetCurrentLevelFromSavedNumber(ResolveSavedCurrentLevelNumber());
            _transitionInProgress = false;

            RefreshStaticUI();
            stateManager.ChangeState(GameState.StartScreen);
        }

        private void StartCurrentLevel()
        {
            if (!_levelProgression.TryGetCurrentLevelData(out var levelData))
            {
                return;
            }

            boardController.Setup(levelData, _levelProgression.RuntimeShapeRegistry);
            blockSceneBuilder.BuildForLevel(levelData);
            _cameraFramer.CenterToLevel(levelData, _levelProgression.CurrentLevelDisplayNumber);
            blockSceneBuilder.RefreshConditionIndicatorBillboards();
            PersistCurrentLevelProgress(levelData);

            stateManager.ChangeState(GameState.Playing);
            RefreshStaticUI(levelData);
            uiManager.StartLevelTimer(levelData.timeLimit);
        }

        private void StartNextLevelOrCompleteRun()
        {
            if (!_levelProgression.TryMoveNextLevel())
            {
                CompleteRun();
                return;
            }

            StartCurrentLevel();
        }

        private void HandleTimerExpired()
        {
            if (_transitionInProgress)
                return;

            FailRun();
        }

        private void OnLevelCompleted()
        {
            if (_transitionInProgress)
                return;

            PersistUnlockedLevelProgress();
            StartCoroutine(ShowLevelCompletedRoutine());
        }

        private IEnumerator ShowLevelCompletedRoutine()
        {
            _transitionInProgress = true;
            uiManager.StopLevelTimer();

            if (levelCompletePanelDelay > 0f)
            {
                yield return new WaitForSeconds(levelCompletePanelDelay);
            }

            stateManager.ChangeState(GameState.LevelCompleted);
            _transitionInProgress = false;
        }

        private void CompleteRun()
        {
            uiManager.StopLevelTimer();
            stateManager.ChangeState(GameState.GameCompleted);
        }

        private void FailRun()
        {
            uiManager.StopLevelTimer();
            audioManager.PlayLevelFail();
            stateManager.ChangeState(GameState.LevelFailed);
        }

        private void HandleStateChanged(GameState newState)
        {
            audioManager.SyncMusicToState(newState);
            uiManager.PublishState(newState);

            if (newState != GameState.Playing)
            {
                uiManager.StopLevelTimer();
            }
        }

        private void RefreshStaticUI()
        {
            if (_levelProgression.TryGetCurrentLevelData(out var levelData))
            {
                RefreshStaticUI(levelData);
                return;
            }

            uiManager.SetLevel(_levelProgression.CurrentLevelDisplayNumber);
        }

        private void RefreshStaticUI(LevelJsonData levelData)
        {
            var levelNumber = levelData?.levelNumber ?? _levelProgression.CurrentLevelDisplayNumber;
            uiManager.SetLevel(levelNumber);
        }

        private void HandleStartRequested()
        {
            if (_transitionInProgress || !IsCurrentState(GameState.StartScreen))
            {
                return;
            }

            StartCurrentLevel();
        }

        private void HandleEndGameActionRequested(GameState newState)
        {
            if (_transitionInProgress)
            {
                return;
            }

            if (newState == GameState.LevelCompleted)
            {
                StartNextLevelOrCompleteRun();
                return;
            }

            if (newState == GameState.LevelFailed)
            {
                StartCurrentLevel();
                return;
            }

            if (newState == GameState.GameCompleted)
            {
                RestartRunFromFirstLevel();
            }
        }

        private void RestartRunFromFirstLevel()
        {
            _localDataManager?.SetCurrentLevel(1);
            _levelProgression.SetCurrentLevelFromSavedNumber(1);
            _transitionInProgress = false;
            StartCurrentLevel();
        }

        private void HandleReloadRequested()
        {
            if (_transitionInProgress || !IsCurrentState(GameState.Playing))
            {
                return;
            }

            StartCurrentLevel();
        }

        private bool IsCurrentState(GameState state) => stateManager.CurrentState == state;

        private bool HasRequiredReferences() =>
            levelCollection != null &&
            boardController != null &&
            blockSceneBuilder != null &&
            stateManager != null &&
            uiManager != null &&
            audioManager != null &&
            gameplayCamera != null;

        private int ResolveSavedCurrentLevelNumber()
        {
            if (_localDataManager == null)
            {
                return 1;
            }

            var playerData = _localDataManager.GetPlayerData();
            var maxLevelNumber = ResolveMaxPersistableLevelNumber();
            return Mathf.Clamp(playerData.currentLevel, 1, maxLevelNumber);
        }

        private void PersistCurrentLevelProgress(LevelJsonData levelData)
        {
            if (_localDataManager == null)
            {
                return;
            }

            var levelNumber = levelData?.levelNumber ?? _levelProgression.CurrentLevelDisplayNumber;
            _localDataManager.SetCurrentLevelAsProgress(ClampLevelNumberToProgressRange(levelNumber));
        }

        private void PersistUnlockedLevelProgress()
        {
            if (_localDataManager == null)
            {
                return;
            }

            if (_levelProgression.TryGetNextLevelData(out var nextLevelData))
            {
                var nextLevelNumber = nextLevelData?.levelNumber ?? _levelProgression.CurrentLevelDisplayNumber + 1;
                _localDataManager.SetCurrentLevelAsProgress(ClampLevelNumberToProgressRange(nextLevelNumber));
                return;
            }

            if (_levelProgression.TryGetCurrentLevelData(out var currentLevelData))
            {
                var currentLevelNumber = currentLevelData?.levelNumber ?? _levelProgression.CurrentLevelDisplayNumber;
                _localDataManager.SetCurrentLevelAsProgress(ClampLevelNumberToProgressRange(currentLevelNumber));
                return;
            }

            _localDataManager.SetCurrentLevelAsProgress(ClampLevelNumberToProgressRange(_levelProgression.CurrentLevelDisplayNumber));
        }

        private int ClampLevelNumberToProgressRange(int levelNumber)
        {
            var sanitizedLevelNumber = Mathf.Max(1, levelNumber);
            return Mathf.Clamp(sanitizedLevelNumber, 1, ResolveMaxPersistableLevelNumber());
        }

        private int ResolveMaxPersistableLevelNumber()
        {
            if (levelCollection == null || levelCollection.Count <= 0)
            {
                return 1;
            }

            var maxLevelNumber = 1;
            for (var i = 0; i < levelCollection.Count; i++)
            {
                if (levelCollection.TryGetLevelAt(i, out var levelData) && levelData != null)
                {
                    maxLevelNumber = Mathf.Max(maxLevelNumber, Mathf.Max(1, levelData.levelNumber));
                    continue;
                }

                maxLevelNumber = Mathf.Max(maxLevelNumber, i + 1);
            }

            return Mathf.Max(1, maxLevelNumber);
        }

        private void TryResolveSceneReferences()
        {
            if (!gameObject.scene.IsValid())
            {
                return;
            }

            boardController ??= FindObjectOfType<BoardController>();
            blockSceneBuilder ??= FindObjectOfType<BlockSceneBuilder>();
            stateManager ??= StateManager.Instance != null ? StateManager.Instance : FindObjectOfType<StateManager>();
            uiManager ??= UIManager.Instance != null ? UIManager.Instance : FindObjectOfType<UIManager>();
            audioManager ??= AudioManager.Instance != null ? AudioManager.Instance : FindObjectOfType<AudioManager>();

            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
                if (gameplayCamera == null)
                {
                    gameplayCamera = FindObjectOfType<Camera>();
                }
            }
        }

    }
}
