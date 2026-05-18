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

        private bool _transitionInProgress;
        private WaitForSeconds _levelCompletedDelayWait;
        private float _cachedLevelCompleteDelay = -1f;
        private LevelProgression _levelProgression;
        private GameplayCameraFramer _cameraFramer;
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

            RefreshLevelCompleteDelayInstruction();
            if (!HasRequiredReferences())
            {
                Debug.LogError("GameManager is missing one or more serialized core references.", this);
                enabled = false;
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

        private void OnValidate()
        {
            RefreshLevelCompleteDelayInstruction();
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

        private void InitializeCollaborators()
        {
            _levelProgression = new LevelProgression(levelCollection);
            _cameraFramer = new GameplayCameraFramer(boardController, gameplayCamera);
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
            _levelProgression.ResetToFirstLevel();
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
            _cameraFramer.CenterToLevel(levelData);

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

            StartCoroutine(ShowLevelCompletedRoutine());
        }

        private IEnumerator ShowLevelCompletedRoutine()
        {
            _transitionInProgress = true;
            uiManager.StopLevelTimer();

            if (levelCompletePanelDelay > 0f)
            {
                RefreshLevelCompleteDelayInstruction();
                yield return _levelCompletedDelayWait;
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
                InitializeRun();
            }
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

        private bool HasRequiredReferences() =>
            levelCollection != null &&
            boardController != null &&
            blockSceneBuilder != null &&
            stateManager != null &&
            uiManager != null &&
            audioManager != null &&
            gameplayCamera != null;

    }
}
