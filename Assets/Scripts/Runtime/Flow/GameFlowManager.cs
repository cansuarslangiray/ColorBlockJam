using System;
using System.Collections;
using Runtime.Controllers;
using Runtime.Controllers.BlockSceneBuilder;
using Runtime.Data;
using Runtime.Domain.Enums;
using UnityEngine;
using UnityEngine.UI;

namespace Runtime.Flow
{
    public class GameFlowManager : MonoBehaviour
    {
        [Header("Core References")] [SerializeField]
        private LevelCollection levelCollection;

        [SerializeField] private BoardController boardController;
        [SerializeField] private BlockSceneBuilder blockSceneBuilder;
        [SerializeField] private BoardInputController boardInputController;
        [SerializeField] private StateManager stateManager;
        [SerializeField] private LevelCountdownTimer levelCountdownTimer;

        [Header("Screen Roots")] [SerializeField]
        private GameObject startScreenRoot;

        [SerializeField] private GameObject gameplayScreenRoot;
        [SerializeField] private GameObject endScreenRoot;

        [Header("UI Texts")] [SerializeField] private Text levelText;

        [SerializeField] private Text timerText;

        [SerializeField] private Text endMessageText;

        [Header("Camera Framing")] [SerializeField]
        private Camera gameplayCamera;

        [SerializeField] private bool forceOrthographicCamera;
        [SerializeField] private bool resetCameraTilt;
        [SerializeField, Min(0f)] private float cameraPaddingInCells = 1.1f;
        [SerializeField, Min(0.01f)] private float minimumOrthographicSize = 5f;
        [SerializeField, Min(0.01f)] private float minimumPerspectiveDistance = 14f;

        [Header("Flow Settings")] [SerializeField]
        private bool initializeOnStart = true;

        [SerializeField] private float levelCompleteDelay = 0.25f;

        private int _currentLevelIndex;
        private bool _transitionInProgress;

        private void OnEnable()
        {
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            boardController.LevelCompleted += OnLevelCompleted;
            stateManager.OnStateChanged += HandleStateChanged;
            levelCountdownTimer.SecondChanged += HandleTimerSecondChanged;
            levelCountdownTimer.TimerExpired += HandleTimerExpired;
        }

        private void OnDisable()
        {
            UnregisterEvents();
        }

        private void UnregisterEvents()
        {
            if (boardController != null)
            {
                boardController.LevelCompleted -= OnLevelCompleted;
            }

            if (stateManager != null)
            {
                stateManager.OnStateChanged -= HandleStateChanged;
            }

            if (levelCountdownTimer != null)
            {
                levelCountdownTimer.SecondChanged -= HandleTimerSecondChanged;
                levelCountdownTimer.TimerExpired -= HandleTimerExpired;
            }
        }

        private void Start()
        {
            if (initializeOnStart)
            {
                InitializeRun();
            }
        }

        private void InitializeRun()
        {
            _currentLevelIndex = 0;
            _transitionInProgress = false;

            stateManager.ChangeState(GameState.StartScreen);

            RefreshStaticUI();
            RefreshTimerUI(0);
        }

        public void OnStartButtonPressed()
        {
            if (_transitionInProgress)
            {
                return;
            }

            switch (stateManager.CurrentState)
            {
                case GameState.StartScreen:
                    StartCurrentLevel();
                    break;
                case GameState.LevelFailed:
                case GameState.GameCompleted:
                    InitializeRun();
                    break;
                case GameState.Playing:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool StartCurrentLevel()
        {
            var levelData = levelCollection.GetLevelAt(_currentLevelIndex);
            if (!levelData)
            {
                return false;
            }

            boardController.Setup(levelData);

            blockSceneBuilder.BuildForLevel(levelData);
            boardInputController?.ForceFitInputArea();

            if (!FitCameraToLevel(levelData))
            {
                stateManager.ChangeState(GameState.StartScreen);
                return false;
            }

            stateManager.ChangeState(GameState.Playing);

            RefreshStaticUI();
            StartTimer(levelData.timeLimit);

            return true;
        }

        private void StartTimer(float durationSeconds)
        {
            if (!levelCountdownTimer)
            {
                var fallbackSeconds = Mathf.Max(1, Mathf.CeilToInt(durationSeconds));
                RefreshTimerUI(fallbackSeconds);
                return;
            }

            levelCountdownTimer.Begin(durationSeconds);
        }

        private void StopTimer()
        {
            levelCountdownTimer.Stop();
        }

        private void HandleTimerSecondChanged(int remainingSeconds)
        {
            RefreshTimerUI(remainingSeconds);
        }

        private void HandleTimerExpired()
        {
            if (stateManager.CurrentState != GameState.Playing)
            {
                return;
            }

            RefreshTimerUI(0);
            FailRun("Time is up!");
        }

        private void OnLevelCompleted()
        {
            if (stateManager.CurrentState != GameState.Playing || _transitionInProgress)
            {
                return;
            }

            StartCoroutine(AdvanceLevelRoutine());
        }

        private IEnumerator AdvanceLevelRoutine()
        {
            _transitionInProgress = true;
            yield return new WaitForSeconds(levelCompleteDelay);

            var hasNextLevel = _currentLevelIndex + 1 < levelCollection.Count;
            if (!hasNextLevel)
            {
                CompleteRun();
                _transitionInProgress = false;
                yield break;
            }

            _currentLevelIndex++;

            if (!StartCurrentLevel())
            {
                stateManager.ChangeState(GameState.LevelFailed);
            }

            _transitionInProgress = false;
        }

        private void CompleteRun()
        {
            endMessageText.text = "Congratulations! All levels completed.";

            StopTimer();
            stateManager.ChangeState(GameState.GameCompleted);
        }

        private void FailRun(string message)
        {
            endMessageText.text = message;

            StopTimer();
            stateManager.ChangeState(GameState.LevelFailed);
        }

        private void HandleStateChanged(GameState oldState, GameState newState)
        {
            var showStart = newState == GameState.StartScreen;
            var showGameplay = newState == GameState.Playing;
            var showEnd = newState is GameState.LevelFailed or GameState.GameCompleted;

            startScreenRoot.SetActive(showStart);
            gameplayScreenRoot.SetActive(showGameplay);
            endScreenRoot.SetActive(showEnd);

            if (newState != GameState.Playing)
            {
                StopTimer();
            }
        }

        private void RefreshStaticUI()
        {
            LevelData levelData = levelCollection ? levelCollection.GetLevelAt(_currentLevelIndex) : null;
            int levelNumber = levelData ? levelData.levelNumber : _currentLevelIndex + 1;
            levelText.text = $"Level {levelNumber}";
        }

        private void RefreshTimerUI(int remainingSeconds)
        {
            var totalSeconds = Mathf.Max(0, remainingSeconds);
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        private bool FitCameraToLevel(LevelData levelData)
        {
            var cellSize = Mathf.Max(0.01f, boardController.CellSize);
            var width = levelData.gridDimensions.x * cellSize;
            var height = levelData.gridDimensions.y * cellSize;
            var padding = Mathf.Max(0f, cameraPaddingInCells * cellSize);
            var contentWidth = width + (padding * 2f);
            var contentHeight = height + (padding * 2f);

            var center = boardController.BoardOrigin + new Vector2(width * 0.5f, height * 0.5f);
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

            var halfFovY = Mathf.Max(0.01f, gameplayCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var halfFovX = Mathf.Atan(Mathf.Tan(halfFovY) * Mathf.Max(0.01f, gameplayCamera.aspect));
            var distanceByHeight = (contentHeight * 0.5f) / Mathf.Tan(halfFovY);
            var distanceByWidth = (contentWidth * 0.5f) / Mathf.Tan(halfFovX);
            var requiredDistance = Mathf.Max(minimumPerspectiveDistance, distanceByHeight, distanceByWidth);
            var forward = cameraTransform.forward.normalized;
            cameraTransform.position = new Vector3(center.x, center.y, 0f) - (forward * requiredDistance);
            return true;
        }
    }
}