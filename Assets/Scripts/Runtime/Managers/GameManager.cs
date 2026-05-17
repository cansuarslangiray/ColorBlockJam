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
        private bool _transitionInProgress;
        private bool _eventsRegistered;
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
            if (_eventsRegistered)
            {
                return;
            }

            boardController.LevelCompleted += OnLevelCompleted;

            if (StateManager.Instance != null)
                StateManager.Instance.OnStateChanged += HandleStateChanged;

            if (UIManager.Instance != null)
            {
                UIManager.Instance.LevelTimerExpired += HandleTimerExpired;
                UIManager.Instance.StartRequested += HandleStartRequested;
                UIManager.Instance.EndGameActionRequested += HandleEndGameActionRequested;
                UIManager.Instance.ReloadRequested += HandleReloadRequested;
            }

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
            UIManager.Instance.StartRequested -= HandleStartRequested;
            UIManager.Instance.EndGameActionRequested -= HandleEndGameActionRequested;
            UIManager.Instance.ReloadRequested -= HandleReloadRequested;
            _eventsRegistered = false;
        }

        private void InitializeRun()
        {
            _currentLevelIndex = 0;
            _transitionInProgress = false;

            RefreshStaticUI();
            UIManager.Instance.ResetTimerDisplay();
            StateManager.Instance.ChangeState(GameState.StartScreen);
        }

        private void StartCurrentLevel()
        {
            var levelData = levelCollection.GetLevelAt(_currentLevelIndex);
            if (levelData == null)
            {
                return;
            }

            boardController.Setup(levelData, levelCollection.RuntimeShapeRegistry);
            blockSceneBuilder.BuildForLevel(levelData);
            CenterCameraToLevel(levelData);

            StateManager.Instance.ChangeState(GameState.Playing);
            RefreshStaticUI();
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

            _currentLevelIndex++;
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
            var levelData = levelCollection.GetLevelAt(_currentLevelIndex);
            var levelNumber = levelData?.levelNumber ?? _currentLevelIndex + 1;
            UIManager.Instance.SetLevel(levelNumber);
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