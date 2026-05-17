using System.Collections;
using Runtime.Controllers;
using Runtime.Controllers.BlockSceneBuilder;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Flow;
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

        [Header("Camera Framing")] [SerializeField]
        private Camera gameplayCamera;

        [SerializeField] private BoardCameraFramingSettings boardCameraFramingSettings =
            BoardCameraFramingSettings.CreateDefault();

        [Header("Flow Settings")] [SerializeField]
        private bool initializeOnStart = true;

        [SerializeField] private bool lockToPortraitOrientation = true;
        [SerializeField] private float levelCompletePanelDelay = 0.4f;

        private int _currentLevelIndex;
        private bool _transitionInProgress;
        private bool _boardEventsRegistered;
        private bool _stateEventsRegistered;
        private bool _uiEventsRegistered;
        private bool _uiActionsBound;
        private bool _startupInitialized;
        private WaitForSeconds _levelCompletedDelayWait;
        private float _cachedLevelCompleteDelay = -1f;

        protected override void Awake()
        {
            base.Awake();
            ApplyDisplaySettings();
            RefreshLevelCompleteDelayInstruction();
        }

        private void Start()
        {
            TryRegisterEvents();
            TryBindUiActions();

            if (!initializeOnStart || _startupInitialized)
            {
                return;
            }

            InitializeRun();
            _startupInitialized = true;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                ApplyDisplaySettings();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
            {
                ApplyDisplaySettings();
            }
        }

        private void OnValidate()
        {
            RefreshLevelCompleteDelayInstruction();
        }

        private void OnEnable()
        {
            TryRegisterEvents();
            TryBindUiActions();
        }

        private void OnDisable()
        {
            UnregisterEvents();
            _uiActionsBound = false;
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

        private void TryRegisterEvents()
        {
            if (!_boardEventsRegistered && boardController)
            {
                boardController.LevelCompleted += OnLevelCompleted;
                _boardEventsRegistered = true;
            }

            if (!_stateEventsRegistered && StateManager.Instance)
            {
                StateManager.Instance.OnStateChanged += HandleStateChanged;
                _stateEventsRegistered = true;
            }

            if (!_uiEventsRegistered && UIManager.Instance)
            {
                UIManager.Instance.LevelTimerExpired += HandleTimerExpired;
                _uiEventsRegistered = true;
            }
        }

        private void UnregisterEvents()
        {
            if (_boardEventsRegistered && boardController)
            {
                boardController.LevelCompleted -= OnLevelCompleted;
                _boardEventsRegistered = false;
            }

            if (_stateEventsRegistered && StateManager.Instance)
            {
                StateManager.Instance.OnStateChanged -= HandleStateChanged;
            }

            if (_uiEventsRegistered && UIManager.Instance)
            {
                UIManager.Instance.LevelTimerExpired -= HandleTimerExpired;
            }

            _stateEventsRegistered = false;
            _uiEventsRegistered = false;
        }

        private void TryBindUiActions()
        {
            if (_uiActionsBound || !UIManager.Instance)
                return;
            UIManager.Instance.BindStartAction(HandleStartPanelInput);
            UIManager.Instance.BindContinueAction(HandleContinueInput);
            UIManager.Instance.BindRetryAction(HandleRetryInput);
            UIManager.Instance.BindRestartAction(HandleRestartInput);
            UIManager.Instance.BindReloadAction(HandleReloadInput);
            _uiActionsBound = true;
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
            
            boardController.Setup(levelData, levelCollection.RuntimeShapeRegistry);
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
            AudioManager.Instance.PlayLevelFail();
            StateManager.Instance.ChangeState(GameState.LevelFailed);
        }

        private void HandleStateChanged(GameState newState)
        {
            AudioManager.Instance.SyncMusicToState(newState);
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
            UIManager.Instance.SetLevel(levelNumber);
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

        private bool FitCameraToLevel(LevelJsonData levelData)
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
