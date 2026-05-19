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
        private bool _completionHandledForCurrentLevel;
        private Coroutine _levelCompletionWatchdogRoutine;
        private Coroutine _transitionRoutine;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this)
            {
                return;
            }

            InitializeCollaborators();
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

        private void OnDisable()
        {
            UnregisterEvents();
            StopRuntimeRoutines();
        }

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
            StopRuntimeRoutines();
            _transitionInProgress = false;
            _completionHandledForCurrentLevel = false;

            RefreshStaticUI();
            stateManager.ChangeState(GameState.StartScreen);
        }

        private void StartCurrentLevel()
        {
            if (!_levelProgression.TryGetCurrentLevelData(out var levelData))
            {
                return;
            }

            StopRuntimeRoutines();
            _completionHandledForCurrentLevel = false;

            boardController.Setup(levelData, _levelProgression.RuntimeShapeCatalog);
            blockSceneBuilder.BuildForLevel(levelData);
            _cameraFramer.CenterToLevel(levelData, _levelProgression.CurrentLevelDisplayNumber);
            PersistCurrentLevelProgress(levelData);

            stateManager.ChangeState(GameState.Playing);
            RefreshStaticUI(levelData);
            uiManager.StartLevelTimer(levelData.timeLimit);
            StartLevelCompletionWatchdog();
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
            if (_transitionInProgress || _completionHandledForCurrentLevel)
                return;

            FailRun();
        }

        private void OnLevelCompleted() => TryHandleLevelCompletedFlow();

        private void TryHandleLevelCompletedFlow()
        {
            if (_transitionInProgress || _completionHandledForCurrentLevel)
                return;

            _completionHandledForCurrentLevel = true;
            StopLevelCompletionWatchdog();
            PersistUnlockedLevelProgress();
            uiManager.StopLevelTimer();
            if (_levelProgression != null && _levelProgression.TryGetNextLevelData(out _))
            {
                StartTransitionRoutine(ShowLevelCompletedRoutine());
            }
            else
            {
                StartTransitionRoutine(ShowRunCompletedRoutine());
            }
        }

        private IEnumerator ShowLevelCompletedRoutine()
        {
            if (levelCompletePanelDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(levelCompletePanelDelay);
            }

            stateManager.ChangeState(GameState.LevelCompleted);
        }

        private IEnumerator ShowRunCompletedRoutine()
        {
            if (levelCompletePanelDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(levelCompletePanelDelay);
            }

            stateManager.ChangeState(GameState.GameCompleted);
        }

        private void CompleteRun()
        {
            StopRuntimeRoutines();
            _completionHandledForCurrentLevel = true;
            uiManager.StopLevelTimer();
            stateManager.ChangeState(GameState.GameCompleted);
        }

        private void FailRun()
        {
            StopRuntimeRoutines();
            _completionHandledForCurrentLevel = true;
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
                StopLevelCompletionWatchdog();
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

        private void RefreshStaticUI(LevelDefinition levelData)
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
            StopRuntimeRoutines();
            _localDataManager?.SetCurrentLevel(1);
            _levelProgression.SetCurrentLevelFromSavedNumber(1);
            _transitionInProgress = false;
            _completionHandledForCurrentLevel = false;
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

        private void StartTransitionRoutine(IEnumerator routine)
        {
            if (routine == null)
            {
                return;
            }

            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
            }

            _transitionInProgress = true;
            _transitionRoutine = StartCoroutine(RunTransitionRoutine(routine));
        }

        private IEnumerator RunTransitionRoutine(IEnumerator routine)
        {
            yield return routine;
            _transitionRoutine = null;
            _transitionInProgress = false;
        }

        private void StartLevelCompletionWatchdog()
        {
            StopLevelCompletionWatchdog();
            if (boardController == null || stateManager == null)
            {
                return;
            }

            _levelCompletionWatchdogRoutine = StartCoroutine(LevelCompletionWatchdogRoutine());
        }

        private void StopLevelCompletionWatchdog()
        {
            if (_levelCompletionWatchdogRoutine == null)
            {
                return;
            }

            StopCoroutine(_levelCompletionWatchdogRoutine);
            _levelCompletionWatchdogRoutine = null;
        }

        private IEnumerator LevelCompletionWatchdogRoutine()
        {
            while (enabled &&
                   boardController != null &&
                   stateManager != null &&
                   stateManager.CurrentState == GameState.Playing &&
                   !_completionHandledForCurrentLevel)
            {
                if (!_transitionInProgress && boardController.RemainingBlockCount <= 0)
                {
                    TryHandleLevelCompletedFlow();
                    break;
                }

                yield return null;
            }

            _levelCompletionWatchdogRoutine = null;
        }

        private void StopRuntimeRoutines()
        {
            StopLevelCompletionWatchdog();

            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            _transitionInProgress = false;
        }

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

        private void PersistCurrentLevelProgress(LevelDefinition levelData)
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

            _localDataManager.SetCurrentLevelAsProgress(
                ClampLevelNumberToProgressRange(_levelProgression.CurrentLevelDisplayNumber));
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
    }
}
