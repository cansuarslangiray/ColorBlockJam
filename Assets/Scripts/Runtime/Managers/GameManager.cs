using System;
using System.Collections;
using Runtime.Controllers;
using Runtime.Controllers.BlockSceneBuilder;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
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
        [SerializeField] private Camera gameplayCamera;

        [Header("Flow Settings")] [SerializeField]
        private float levelCompletePanelDelay = 0.4f;

        private int _currentLevelIndex;
        private int _cachedLevelIndex = -1;
        private bool _transitionInProgress;
        private bool _boardEventsRegistered;
        private bool _stateEventsRegistered;
        private bool _uiEventsRegistered;
        private bool _isCurrentLevelCacheResolved;
        private LevelJsonData _cachedCurrentLevelData;
        private WaitForSeconds _levelCompletedDelayWait;
        private float _cachedLevelCompleteDelay = -1f;

        protected override void Awake()
        {
            base.Awake();
            RefreshLevelCompleteDelayInstruction();
        }

        private void Start()
        {
            TryRegisterEvents();

            if (StateManager.Instance == null || UIManager.Instance == null)
            {
                Debug.LogError("GameManager requires active StateManager and UIManager instances.", this);
                enabled = false;
                return;
            }

            if (levelCollection == null || boardController == null || blockSceneBuilder == null)
            {
                Debug.LogError("GameManager is missing one or more serialized core references.", this);
                enabled = false;
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
            TryRegisterEvents();
        }

        private void OnDisable()
        {
            UnregisterEvents();
        }

        private void TryRegisterEvents()
        {
            if (!_boardEventsRegistered && boardController != null)
            {
                boardController.LevelCompleted += OnLevelCompleted;
                _boardEventsRegistered = true;
            }

            if (!_stateEventsRegistered && StateManager.Instance != null)
            {
                StateManager.Instance.OnStateChanged += HandleStateChanged;
                _stateEventsRegistered = true;
            }

            if (!_uiEventsRegistered && UIManager.Instance != null)
            {
                UIManager.Instance.LevelTimerExpired += HandleTimerExpired;
                UIManager.Instance.StartRequested += HandleStartRequested;
                UIManager.Instance.EndGameActionRequested += HandleEndGameActionRequested;
                UIManager.Instance.ReloadRequested += HandleReloadRequested;
                _uiEventsRegistered = true;
            }
        }

        private void UnregisterEvents()
        {
            if (_boardEventsRegistered && boardController != null)
            {
                boardController.LevelCompleted -= OnLevelCompleted;
                _boardEventsRegistered = false;
            }

            if (_stateEventsRegistered && StateManager.Instance != null)
            {
                StateManager.Instance.OnStateChanged -= HandleStateChanged;
            }
            _stateEventsRegistered = false;

            if (_uiEventsRegistered && UIManager.Instance != null)
            {
                UIManager.Instance.LevelTimerExpired -= HandleTimerExpired;
                UIManager.Instance.StartRequested -= HandleStartRequested;
                UIManager.Instance.EndGameActionRequested -= HandleEndGameActionRequested;
                UIManager.Instance.ReloadRequested -= HandleReloadRequested;
            }
            _uiEventsRegistered = false;
        }

        private void InitializeRun()
        {
            SetCurrentLevelIndex(0);
            _transitionInProgress = false;

            RefreshStaticUI();
            StateManager.Instance.ChangeState(GameState.StartScreen);
        }

        private void StartCurrentLevel()
        {
            if (!TryGetCurrentLevelData(out var levelData))
            {
                return;
            }

            boardController.Setup(levelData, levelCollection.RuntimeShapeRegistry);
            blockSceneBuilder.BuildForLevel(levelData);
            CenterCameraToLevel(levelData);

            StateManager.Instance.ChangeState(GameState.Playing);
            RefreshStaticUI(levelData);
            UIManager.Instance.StartLevelTimer(levelData.timeLimit);
        }

        private void StartNextLevelOrCompleteRun()
        {
            var hasNextLevel = _currentLevelIndex + 1 < levelCollection.Count;
            if (!hasNextLevel)
            {
                CompleteRun();
                return;
            }

            SetCurrentLevelIndex(_currentLevelIndex + 1);
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
            if (TryGetCurrentLevelData(out var levelData))
            {
                RefreshStaticUI(levelData);
                return;
            }

            UIManager.Instance.SetLevel(_currentLevelIndex + 1);
        }

        private void RefreshStaticUI(LevelJsonData levelData)
        {
            var levelNumber = levelData?.levelNumber ?? _currentLevelIndex + 1;
            UIManager.Instance.SetLevel(levelNumber);
        }

        private void SetCurrentLevelIndex(int levelIndex)
        {
            _currentLevelIndex = levelIndex;
            _isCurrentLevelCacheResolved = false;
            _cachedCurrentLevelData = null;
            _cachedLevelIndex = -1;
        }

        private bool TryGetCurrentLevelData(out LevelJsonData levelData)
        {
            if (_isCurrentLevelCacheResolved && _cachedLevelIndex == _currentLevelIndex)
            {
                levelData = _cachedCurrentLevelData;
                return levelData != null;
            }

            _isCurrentLevelCacheResolved = true;
            _cachedLevelIndex = _currentLevelIndex;

            if (levelCollection != null && levelCollection.TryGetLevelAt(_currentLevelIndex, out var resolvedLevelData))
            {
                _cachedCurrentLevelData = resolvedLevelData;
                levelData = resolvedLevelData;
                return true;
            }

            _cachedCurrentLevelData = null;
            levelData = null;
            return false;
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

            switch (newState)
            {
                case GameState.LevelCompleted:
                    StartNextLevelOrCompleteRun();
                    break;
                case GameState.LevelFailed:
                    StartCurrentLevel();
                    break;
                case GameState.GameCompleted:
                    InitializeRun();
                    break;
                case GameState.StartScreen:
                    break;
                case GameState.Playing:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
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

        private bool IsCurrentState(GameState state) => StateManager.Instance.CurrentState == state;

        private void CenterCameraToLevel(LevelJsonData levelData)
        {
            if (!gameplayCamera || levelData == null)
            {
                return;
            }

            var cellSize = Mathf.Max(0.01f, boardController.CellSize);
            var width = Mathf.Max(0, levelData.gridDimensions.x) * cellSize;
            var height = Mathf.Max(0, levelData.gridDimensions.y) * cellSize;
            var center = boardController.BoardOrigin +
                         new Vector2(width * 0.5f, height * 0.5f);
            var cameraTransform = gameplayCamera.transform;
            var forward = cameraTransform.forward.normalized;
            var boardPlaneZ = boardController.transform.position.z;
            var forwardZ = Mathf.Abs(forward.z) < 0.001f
                ? (forward.z >= 0f ? 0.001f : -0.001f)
                : forward.z;
            var distanceToBoardPlane = Mathf.Abs((cameraTransform.position.z - boardPlaneZ) / forwardZ);
            cameraTransform.position = new Vector3(center.x, center.y, boardPlaneZ) - (forward * distanceToBoardPlane);
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
