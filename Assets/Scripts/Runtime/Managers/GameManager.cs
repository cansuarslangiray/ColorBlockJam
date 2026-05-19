using System.Collections;
using System.Collections.Generic;
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
        private enum FeatureUnlockContinuation
        {
            None = 0,
            StartPreparedLevel = 1,
            AdvanceToNextLevel = 2
        }

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

        [SerializeField, Min(0f)] private float panelTransitionDuration = 0.22f;

        [Header("Camera Framing")] [SerializeField] [Min(0)]
        private int closeCameraLevelCount = 2;

        [SerializeField] [Range(0.6f, 1f)] private float closeCameraDistanceMultiplier = 0.9f;
        [SerializeField, Min(0f)] private float cameraBoundsPaddingInCells = 0.36f;
        [SerializeField, Range(0f, 0.4f)] private float cameraSafeViewportMargin = 0.08f;
        [SerializeField, Min(0f)] private float cameraTransitionDuration = 0.22f;

        private bool _transitionInProgress;
        private LevelProgression _levelProgression;
        private GameplayCameraFramer _cameraFramer;
        private LocalDataManager _localDataManager;
        private PlayerFeatureProgress _playerFeatureProgress;
        private BlockFeatureDefinitionStore _blockFeatureDefinitionStore;
        private FeatureUnlockFlowController _featureUnlockFlowController;
        private bool _boardEventsRegistered;
        private bool _stateEventsRegistered;
        private bool _uiEventsRegistered;
        private bool _completionHandledForCurrentLevel;
        private Coroutine _levelCompletionWatchdogRoutine;
        private Coroutine _transitionRoutine;
        private FeatureUnlockContinuation _featureUnlockContinuation = FeatureUnlockContinuation.None;
        private LevelDefinition _pendingPreparedLevelForFeatureUnlock;

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
                _localDataManager.Save();
            }
        }

        private void OnApplicationQuit() => _localDataManager.Save();

        private void InitializeCollaborators()
        {
            _localDataManager = LocalDataManager.Instance;
            _levelProgression = new LevelProgression(levelCollection);
            _playerFeatureProgress = new PlayerFeatureProgress(_localDataManager);
            _blockFeatureDefinitionStore = new BlockFeatureDefinitionStore();
            _featureUnlockFlowController = new FeatureUnlockFlowController(_playerFeatureProgress, _blockFeatureDefinitionStore);
            _cameraFramer = new GameplayCameraFramer(boardController, gameplayCamera, closeCameraLevelCount,
                closeCameraDistanceMultiplier, this, cameraBoundsPaddingInCells, cameraSafeViewportMargin,
                cameraTransitionDuration);
        }

        private void RegisterEvents()
        {
            if (!_boardEventsRegistered)
            {
                boardController.LevelCompleted += OnLevelCompleted;
                boardController.ConditionFailed += HandleConditionFailed;
                _boardEventsRegistered = true;
            }

            if (!_stateEventsRegistered)
            {
                stateManager.OnStateChanged += HandleStateChanged;
                _stateEventsRegistered = true;
            }

            if (!_uiEventsRegistered)
            {
                uiManager.LevelTimerExpired += HandleTimerExpired;
                uiManager.StartRequested += HandleStartRequested;
                uiManager.EndGameActionRequested += HandleEndGameActionRequested;
                uiManager.ReloadRequested += HandleReloadRequested;
                uiManager.FeatureUnlockedNextRequested += HandleFeatureUnlockedNextRequested;
                _uiEventsRegistered = true;
            }
        }

        private void UnregisterEvents()
        {
            if (_boardEventsRegistered)
            {
                boardController.LevelCompleted -= OnLevelCompleted;
                boardController.ConditionFailed -= HandleConditionFailed;
            }

            _boardEventsRegistered = false;

            if (_stateEventsRegistered)
            {
                stateManager.OnStateChanged -= HandleStateChanged;
            }

            _stateEventsRegistered = false;

            if (_uiEventsRegistered)
            {
                uiManager.LevelTimerExpired -= HandleTimerExpired;
                uiManager.StartRequested -= HandleStartRequested;
                uiManager.EndGameActionRequested -= HandleEndGameActionRequested;
                uiManager.ReloadRequested -= HandleReloadRequested;
                uiManager.FeatureUnlockedNextRequested -= HandleFeatureUnlockedNextRequested;
            }

            _uiEventsRegistered = false;
        }

        private void InitializeRun()
        {
            _levelProgression.SetCurrentLevelFromSavedNumber(ResolveSavedCurrentLevelNumber());
            StopRuntimeRoutines();
            _transitionInProgress = false;
            _completionHandledForCurrentLevel = false;
            ResetFeatureUnlockContinuation();

            RefreshStaticUI();
            stateManager.ChangeState(GameState.StartScreen);
        }

        private void StartCurrentLevel()
        {
            if (!TryPrepareCurrentLevel(out var levelData))
            {
                return;
            }

            EnterPreparedLevel(levelData);
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

        private void HandleConditionFailed()
        {
            if (_transitionInProgress || _completionHandledForCurrentLevel || !IsCurrentState(GameState.Playing))
            {
                return;
            }

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
            if (_levelProgression.TryGetNextLevelData(out var nextLevelData))
            {
                if (TryPrepareFeatureUnlockForLevel(nextLevelData, out var featureDefinitions))
                {
                    ConfigureFeatureUnlockPanel(featureDefinitions, FeatureUnlockContinuation.AdvanceToNextLevel, null);
                    StartTransitionRoutine(ShowFeatureUnlockedRoutine());
                    return;
                }

                ResetFeatureUnlockContinuation();
                StartTransitionRoutine(ShowLevelCompletedRoutine());
            }
            else
            {
                ResetFeatureUnlockContinuation();
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

        private IEnumerator ShowFeatureUnlockedRoutine()
        {
            if (levelCompletePanelDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(levelCompletePanelDelay);
            }

            stateManager.ChangeState(GameState.FeatureUnlocked);
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
            uiManager.SetLevel(levelData.levelNumber);
        }

        private void HandleStartRequested()
        {
            if (_transitionInProgress || !IsCurrentState(GameState.StartScreen))
            {
                return;
            }

            StartCurrentLevelFromStartScreen();
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
            _localDataManager.SetCurrentLevel(1);
            _levelProgression.SetCurrentLevelFromSavedNumber(1);
            _transitionInProgress = false;
            _completionHandledForCurrentLevel = false;
            ResetFeatureUnlockContinuation();
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

        private void HandleFeatureUnlockedNextRequested()
        {
            if (_transitionInProgress || !IsCurrentState(GameState.FeatureUnlocked))
            {
                return;
            }

            uiManager.HideFeatureUnlockedPanel();
            StartTransitionRoutine(AdvanceFromFeatureUnlockedRoutine());
        }

        private IEnumerator AdvanceFromFeatureUnlockedRoutine()
        {
            if (panelTransitionDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(panelTransitionDuration);
            }

            var continuation = _featureUnlockContinuation;
            var preparedLevel = _pendingPreparedLevelForFeatureUnlock;
            ResetFeatureUnlockContinuation();

            switch (continuation)
            {
                case FeatureUnlockContinuation.StartPreparedLevel:
                    if (preparedLevel != null)
                    {
                        EnterPreparedLevel(preparedLevel);
                    }
                    else
                    {
                        StartCurrentLevel();
                    }

                    yield break;
                case FeatureUnlockContinuation.AdvanceToNextLevel:
                case FeatureUnlockContinuation.None:
                default:
                    StartNextLevelOrCompleteRun();
                    yield break;
            }
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
            _cameraFramer?.StopTransition();

            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            _transitionInProgress = false;
        }

        private void StartCurrentLevelFromStartScreen()
        {
            if (!TryPrepareCurrentLevel(out var levelData))
            {
                stateManager.ChangeState(GameState.StartScreen);
                return;
            }

            if (TryPrepareFeatureUnlockForLevel(levelData, out var featureDefinitions))
            {
                ConfigureFeatureUnlockPanel(featureDefinitions, FeatureUnlockContinuation.StartPreparedLevel, levelData);
                stateManager.ChangeState(GameState.FeatureUnlocked);
                return;
            }

            EnterPreparedLevel(levelData);
        }

        private bool TryPrepareCurrentLevel(out LevelDefinition levelData)
        {
            levelData = null;
            if (!_levelProgression.TryGetCurrentLevelData(out levelData))
            {
                return false;
            }

            StopRuntimeRoutines();
            _completionHandledForCurrentLevel = false;
            ResetFeatureUnlockContinuation();
            uiManager.ConfigureFeatureUnlockedPanel(null);

            boardController.Setup(levelData, _levelProgression.RuntimeShapeCatalog);
            _cameraFramer.CenterToLevel(levelData, _levelProgression.CurrentLevelDisplayNumber, true);
            blockSceneBuilder.BuildForLevel(levelData);
            PersistCurrentLevelProgress(levelData);
            RefreshStaticUI(levelData);
            return true;
        }

        private void EnterPreparedLevel(LevelDefinition levelData)
        {
            if (levelData == null)
            {
                return;
            }

            ResetFeatureUnlockContinuation();
            stateManager.ChangeState(GameState.Playing);
            uiManager.StartLevelTimer(levelData.timeLimit);
            StartLevelCompletionWatchdog();
        }

        private bool TryPrepareFeatureUnlockForLevel(LevelDefinition levelData,
            out IReadOnlyList<BlockFeatureDefinition> featureDefinitions)
        {
            featureDefinitions = null;
            if (_featureUnlockFlowController == null || levelData == null)
            {
                return false;
            }

            if (!_featureUnlockFlowController.TryPrepareFeatureUnlock(levelData, out featureDefinitions) ||
                featureDefinitions == null ||
                featureDefinitions.Count <= 0)
            {
                return false;
            }
            return true;
        }

        private void ConfigureFeatureUnlockPanel(IReadOnlyList<BlockFeatureDefinition> featureDefinitions,
            FeatureUnlockContinuation continuation,
            LevelDefinition preparedLevel)
        {
            if (featureDefinitions == null || featureDefinitions.Count <= 0)
            {
                ResetFeatureUnlockContinuation();
                return;
            }

            _featureUnlockContinuation = continuation;
            _pendingPreparedLevelForFeatureUnlock = preparedLevel;
            uiManager.ConfigureFeatureUnlockedPanel(featureDefinitions);
        }

        private void ResetFeatureUnlockContinuation()
        {
            _featureUnlockContinuation = FeatureUnlockContinuation.None;
            _pendingPreparedLevelForFeatureUnlock = null;
        }

        private int ResolveSavedCurrentLevelNumber()
        {
            var playerData = _localDataManager.GetPlayerData();
            var maxLevelNumber = ResolveMaxPersistableLevelNumber();
            return Mathf.Clamp(playerData.currentLevel, 1, maxLevelNumber);
        }

        private void PersistCurrentLevelProgress(LevelDefinition levelData)
        {
            var levelNumber = levelData.levelNumber;
            _localDataManager.SetCurrentLevelAsProgress(ClampLevelNumberToProgressRange(levelNumber));
        }

        private void PersistUnlockedLevelProgress()
        {
            if (_levelProgression.TryGetNextLevelData(out var nextLevelData))
            {
                var nextLevelNumber = nextLevelData.levelNumber;
                _localDataManager.SetCurrentLevelAsProgress(ClampLevelNumberToProgressRange(nextLevelNumber));
                return;
            }

            if (_levelProgression.TryGetCurrentLevelData(out var currentLevelData))
            {
                var currentLevelNumber = currentLevelData.levelNumber;
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
