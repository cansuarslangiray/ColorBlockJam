using System.Collections;
using Runtime.Controllers;
using Runtime.Controllers.BlockSceneBuilder;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.UI;
using UnityEngine;

namespace Runtime.Flow
{
    public class GameManager : SingletonMonoBehaviour<GameManager>
    {
        [Header("Core References")] [SerializeField]
        private LevelCollection levelCollection;

        [SerializeField] private BoardController boardController;
        [SerializeField] private BlockSceneBuilder blockSceneBuilder;

        [Header("Camera Framing")] [SerializeField]
        private Camera gameplayCamera;

        [SerializeField]
        private BoardCameraFramingSettings boardCameraFramingSettings = BoardCameraFramingSettings.CreateDefault();

        [Header("Flow Settings")] [SerializeField]
        private bool initializeOnStart = true;

        [SerializeField] private bool lockToPortraitOrientation = true;

        [SerializeField] private float levelCompletePanelDelay = 0.4f;

        private int _currentLevelIndex;
        private bool _transitionInProgress;
        private bool _eventsRegistered;
        private bool _uiActionsBound;
        private bool _startupInitialized;
        private Coroutine _bootstrapRoutine;
        private WaitForSeconds _levelCompletedDelayWait;
        private float _cachedLevelCompleteDelay = -1f;

        protected override void Awake()
        {
            base.Awake();
            ApplyDisplaySettings();
            RefreshLevelCompleteDelayInstruction();
        }

        private void OnValidate()
        {
            RefreshLevelCompleteDelayInstruction();
        }

        private void OnEnable()
        {
            _bootstrapRoutine ??= StartCoroutine(BootstrapWhenReady());
        }

        private void OnDisable()
        {
            if (_bootstrapRoutine != null)
            {
                StopCoroutine(_bootstrapRoutine);
                _bootstrapRoutine = null;
            }

            UnregisterEvents();
        }

        private void ApplyDisplaySettings()
        {
            if (!lockToPortraitOrientation || !Application.isMobilePlatform)
            {
                return;
            }

            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.orientation = ScreenOrientation.Portrait;
        }

        private void RegisterEvents()
        {
            if (_eventsRegistered || !HasRequiredRuntimeReferences())
            {
                return;
            }

            boardController.LevelCompleted += OnLevelCompleted;
            StateManager.Instance.OnStateChanged += HandleStateChanged;
            UIManager.Instance.LevelTimerExpired += HandleTimerExpired;
            _eventsRegistered = true;
        }

        private void UnregisterEvents()
        {
            if (!_eventsRegistered)
            {
                return;
            }

            boardController.LevelCompleted -= OnLevelCompleted;
            StateManager.Instance.OnStateChanged -= HandleStateChanged;
            UIManager.Instance.LevelTimerExpired -= HandleTimerExpired;
            _eventsRegistered = false;
        }

        private void BindUiActions()
        {
            if (_uiActionsBound)
            {
                return;
            }

            UIManager.Instance.BindStartAction(HandleStartPanelInput);
            UIManager.Instance.BindContinueAction(HandleContinueInput);
            UIManager.Instance.BindRetryAction(HandleRetryInput);
            UIManager.Instance.BindRestartAction(HandleRestartInput);
            UIManager.Instance.BindReloadAction(HandleReloadInput);
            _uiActionsBound = true;
        }

        private IEnumerator BootstrapWhenReady()
        {
            while (enabled && !HasRequiredRuntimeReferences())
            {
                yield return null;
            }

            _bootstrapRoutine = null;
            if (!enabled || !HasRequiredRuntimeReferences())
            {
                yield break;
            }

            RegisterEvents();
            BindUiActions();

            if (!initializeOnStart || _startupInitialized) yield break;
            InitializeRun();
            _startupInitialized = true;
        }

        private bool HasRequiredRuntimeReferences()
        {
            return boardController != null && StateManager.HasInstance && UIManager.HasInstance;
        }

        private void InitializeRun()
        {
            _currentLevelIndex = 0;
            _transitionInProgress = false;

            RefreshStaticUI();
            UIManager.Instance.ResetTimerDisplay();
            StateManager.Instance.ChangeState(GameState.StartScreen);
        }

        private bool StartCurrentLevel()
        {
            var levelData = levelCollection.GetLevelAt(_currentLevelIndex);
            if (levelData == null)
            {
                return false;
            }

            boardController.Setup(levelData);
            blockSceneBuilder.BuildForLevel(levelData);

            if (!FitCameraToLevel(levelData))
            {
                StateManager.Instance.ChangeState(GameState.StartScreen);
                return false;
            }

            StateManager.Instance.ChangeState(GameState.Playing);
            RefreshStaticUI();
            UIManager.Instance.StartLevelTimer(levelData.timeLimit);
            return true;
        }

        private void StartNextLevelOrCompleteRun()
        {
            var hasNextLevel = _currentLevelIndex + 1 < levelCollection.Count;
            if (!hasNextLevel)
            {
                CompleteRun();
                return;
            }

            _currentLevelIndex++;
            StartCurrentLevel();
        }

        private void HandleTimerExpired()
        {
            if (StateManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            FailRun();
        }

        private void OnLevelCompleted()
        {
            if (StateManager.Instance.CurrentState != GameState.Playing || _transitionInProgress)
            {
                return;
            }

            StartCoroutine(ShowLevelCompletedRoutine());
        }

        private IEnumerator ShowLevelCompletedRoutine()
        {
            _transitionInProgress = true;
            UIManager.Instance.StopLevelTimer();

            if (levelCompletePanelDelay > 0f)
            {
                RefreshLevelCompleteDelayInstruction();
                yield return _levelCompletedDelayWait;
            }

            StateManager.Instance.ChangeState(GameState.LevelCompleted);
            _transitionInProgress = false;
        }

        private void CompleteRun()
        {
            UIManager.Instance.StopLevelTimer();
            StateManager.Instance.ChangeState(GameState.GameCompleted);
        }

        private void FailRun()
        {
            UIManager.Instance.StopLevelTimer();
            StateManager.Instance.ChangeState(GameState.LevelFailed);
        }

        private void HandleStateChanged(GameState oldState, GameState newState)
        {
            UIManager.Instance.PublishState(newState);

            if (newState != GameState.Playing)
            {
                UIManager.Instance.StopLevelTimer();
            }
        }

        private void RefreshStaticUI()
        {
            var levelData = levelCollection.GetLevelAt(_currentLevelIndex);
            var levelNumber = levelData != null ? levelData.levelNumber : _currentLevelIndex + 1;
            UIManager.Instance.SetLevel(levelNumber, _currentLevelIndex, levelCollection.Count);
        }

        private void HandleStartPanelInput()
        {
            if (StateManager.Instance.CurrentState != GameState.StartScreen || _transitionInProgress)
            {
                return;
            }

            StartCurrentLevel();
        }

        private void HandleContinueInput()
        {
            if (StateManager.Instance.CurrentState != GameState.LevelCompleted || _transitionInProgress)
            {
                return;
            }

            StartNextLevelOrCompleteRun();
        }

        private void HandleRetryInput()
        {
            if (StateManager.Instance.CurrentState != GameState.LevelFailed || _transitionInProgress)
            {
                return;
            }

            StartCurrentLevel();
        }

        private void HandleRestartInput()
        {
            if (StateManager.Instance.CurrentState != GameState.GameCompleted || _transitionInProgress)
            {
                return;
            }

            InitializeRun();
        }

        private void HandleReloadInput()
        {
            if (StateManager.Instance.CurrentState != GameState.Playing || _transitionInProgress)
            {
                return;
            }

            StartCurrentLevel();
        }

        private bool FitCameraToLevel(LevelData levelData)
        {
            return BoardCameraFramer.TryFrame(gameplayCamera, levelData, boardController.BoardOrigin,
                boardController.CellSize, boardCameraFramingSettings);
        }

        private void RefreshLevelCompleteDelayInstruction()
        {
            if (Mathf.Approximately(_cachedLevelCompleteDelay, levelCompletePanelDelay))
            {
                return;
            }

            _cachedLevelCompleteDelay = levelCompletePanelDelay;
            _levelCompletedDelayWait =
                levelCompletePanelDelay > 0f ? new WaitForSeconds(levelCompletePanelDelay) : null;
        }
    }
}