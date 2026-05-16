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
        [SerializeField] private BoardInputController boardInputController;

        [Header("Camera Framing")] [SerializeField]
        private Camera gameplayCamera;

        [SerializeField] private bool forceOrthographicCamera;
        [SerializeField] private bool resetCameraTilt;
        [SerializeField, Min(0f)] private float cameraPaddingInCells = 0.9f;
        [SerializeField] private Vector2 cameraCenterOffsetInCells = new(0f, 1.2f);
        [SerializeField, Range(0f, 30f)] private float perspectiveCameraTiltDegrees = 8.5f;
        [SerializeField, Min(0.01f)] private float minimumOrthographicSize = 5f;
        [SerializeField, Min(0.01f)] private float minimumPerspectiveDistance = 14f;
        [SerializeField, Range(0.55f, 1.25f)] private float cameraFitDistanceMultiplier = 1.15f;

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

        protected override void Awake()
        {
            base.Awake();
            ApplyDisplaySettings();
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
            boardInputController.ForceFitInputArea();

            if (!FitCameraToLevel(levelData))
            {
                StateManager.Instance.ChangeState(GameState.StartScreen);
                return false;
            }

            StateManager.Instance.ChangeState(GameState.Playing);
            RefreshStaticUI();
            StartTimer(levelData.timeLimit);
            return true;
        }

        private void StartNextLevelOrCompleteRun()
        {
            var hasNextLevel = _currentLevelIndex + 1 < GetTotalLevelCount();
            if (!hasNextLevel)
            {
                CompleteRun();
                return;
            }

            _currentLevelIndex++;
            StartCurrentLevel();
        }

        private void RetryCurrentLevel()
        {
            StartCurrentLevel();
        }

        private void StartTimer(float durationSeconds)
        {
            UIManager.Instance.StartLevelTimer(durationSeconds);
        }

        private void StopTimer()
        {
            UIManager.Instance.StopLevelTimer();
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
            StopTimer();

            if (levelCompletePanelDelay > 0f)
            {
                yield return new WaitForSeconds(levelCompletePanelDelay);
            }

            StateManager.Instance.ChangeState(GameState.LevelCompleted);
            _transitionInProgress = false;
        }

        private void CompleteRun()
        {
            StopTimer();
            StateManager.Instance.ChangeState(GameState.GameCompleted);
        }

        private void FailRun()
        {
            StopTimer();
            StateManager.Instance.ChangeState(GameState.LevelFailed);
        }

        private void HandleStateChanged(GameState oldState, GameState newState)
        {
            UIManager.Instance.PublishState(newState);

            if (newState != GameState.Playing)
            {
                StopTimer();
            }
        }

        private void RefreshStaticUI()
        {
            var levelData = levelCollection.GetLevelAt(_currentLevelIndex);
            var levelNumber = levelData != null ? levelData.levelNumber : _currentLevelIndex + 1;
            UIManager.Instance.SetLevel(levelNumber, _currentLevelIndex, GetTotalLevelCount());
        }

        private int GetTotalLevelCount()
        {
            return levelCollection.Count;
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

            RetryCurrentLevel();
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

            RetryCurrentLevel();
        }

        private bool FitCameraToLevel(LevelData levelData)
        {
            if (levelData == null)
            {
                return false;
            }

            var cellSize = Mathf.Max(0.01f, boardController.CellSize);
            var width = levelData.gridDimensions.x * cellSize;
            var height = levelData.gridDimensions.y * cellSize;
            var padding = Mathf.Max(0f, cameraPaddingInCells * cellSize);
            var contentWidth = width + (padding * 2f);
            var contentHeight = height + (padding * 2f);
            var centerOffset = cameraCenterOffsetInCells * cellSize;

            var center = boardController.BoardOrigin + new Vector2((width * 0.5f) + centerOffset.x, (height * 0.5f) + centerOffset.y);
            var cameraTransform = gameplayCamera.transform;
            cameraTransform.position = new Vector3(center.x, center.y, cameraTransform.position.z);

            if (resetCameraTilt)
            {
                cameraTransform.rotation = Quaternion.identity;
            }

            if (forceOrthographicCamera)
            {
                gameplayCamera.orthographic = true;

                var aspect = Mathf.Max(0.01f, gameplayCamera.aspect);
                var halfHeight = contentHeight * 0.5f;
                var halfWidthAsHeight = (contentWidth * 0.5f) / aspect;
                gameplayCamera.orthographicSize = Mathf.Max(minimumOrthographicSize, halfHeight, halfWidthAsHeight);
                return true;
            }

            gameplayCamera.orthographic = false;
            if (!resetCameraTilt)
            {
                var currentEuler = cameraTransform.eulerAngles;
                cameraTransform.rotation = Quaternion.Euler(-Mathf.Abs(perspectiveCameraTiltDegrees), currentEuler.y,
                    currentEuler.z);
            }

            var halfFovY = Mathf.Max(0.01f, gameplayCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var halfFovX = Mathf.Atan(Mathf.Tan(halfFovY) * Mathf.Max(0.01f, gameplayCamera.aspect));
            var distanceByHeight = (contentHeight * 0.5f) / Mathf.Tan(halfFovY);
            var distanceByWidth = (contentWidth * 0.5f) / Mathf.Tan(halfFovX);
            var requiredDistance = Mathf.Max(minimumPerspectiveDistance, distanceByHeight, distanceByWidth);
            requiredDistance *= Mathf.Clamp(cameraFitDistanceMultiplier, 0.55f, 1.25f);
            var forward = cameraTransform.forward.normalized;
            cameraTransform.position = new Vector3(center.x, center.y, 0f) - (forward * requiredDistance);
            return true;
        }
    }
}