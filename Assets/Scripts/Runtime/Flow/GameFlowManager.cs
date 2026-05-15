using System.Collections;
using Runtime.Controllers;
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

        [Header("Screen Roots")] [SerializeField]
        private GameObject startScreenRoot;

        [SerializeField] private GameObject gameplayScreenRoot;
        [SerializeField] private GameObject endScreenRoot;

        [Header("UI Texts")] [SerializeField] private Text levelText;

        [SerializeField] private Text timerText;

        [SerializeField] private Text endMessageText;

        [Header("Flow Settings")] [SerializeField]
        private bool initializeOnStart = true;

        [SerializeField] private float levelCompleteDelay = 0.25f;

        private int _currentLevelIndex;
        private float _remainingTime;
        private GameState _state;
        private bool _transitionInProgress;

        private void Awake()
        {
            if (boardController != null)
            {
                boardController.LevelCompleted += OnLevelCompleted;
            }
        }

        private void OnDestroy()
        {
            if (boardController != null)
            {
                boardController.LevelCompleted -= OnLevelCompleted;
            }
        }

        private void Start()
        {
            if (initializeOnStart)
            {
                InitializeRun();
            }
        }

        private void Update()
        {
            switch (_state)
            {
                case GameState.StartScreen:
                {
                    if (IsTapDetected())
                    {
                        StartCurrentLevel();
                    }

                    return;
                }
                case GameState.Playing:
                    UpdateTimer();
                    return;
                case GameState.LevelFailed or GameState.GameCompleted when IsTapDetected():
                    InitializeRun();
                    break;
            }
        }

        public void InitializeRun()
        {
            if (!levelCollection)
            {
                Debug.LogError("GameFlowManager: LevelCollection reference is missing.");
                return;
            }

            if (levelCollection.Count < 5)
            {
                Debug.LogWarning("Case requirement: prepare at least 5 unique levels in LevelCollection.");
            }

            _currentLevelIndex = 0;
            _transitionInProgress = false;
            SetState(GameState.StartScreen);
            RefreshStaticUI();
        }

        private void StartCurrentLevel()
        {
            var levelData = levelCollection.GetLevelAt(_currentLevelIndex);
            if (levelData == null)
            {
                Debug.LogError($"GameFlowManager: Level at index {_currentLevelIndex} is missing.");
                return;
            }

            if (boardController == null)
            {
                Debug.LogError("GameFlowManager: BoardController reference is missing.");
                return;
            }

            if (blockSceneBuilder != null)
            {
                blockSceneBuilder.BuildForLevel(levelData);
            }

            boardController.Setup(levelData);
            _remainingTime = Mathf.Max(1f, levelData.timeLimit);
            _transitionInProgress = false;

            SetState(GameState.Playing);
            RefreshStaticUI();
            RefreshTimerUI();
        }

        private void UpdateTimer()
        {
            _remainingTime -= Time.deltaTime;
            if (_remainingTime <= 0f)
            {
                _remainingTime = 0f;
                RefreshTimerUI();
                FailRun("Time is up!");
                return;
            }

            RefreshTimerUI();
        }

        private void OnLevelCompleted()
        {
            if (_state != GameState.Playing || _transitionInProgress)
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
                yield break;
            }

            _currentLevelIndex++;
            StartCurrentLevel();
        }

        private void CompleteRun()
        {
            if (endMessageText != null)
            {
                endMessageText.text = "Congratulations! All levels completed.";
            }

            SetState(GameState.GameCompleted);
        }

        private void FailRun(string message)
        {
            if (endMessageText != null)
            {
                endMessageText.text = message;
            }

            SetState(GameState.LevelFailed);
        }

        private void SetState(GameState nextState)
        {
            _state = nextState;

            var showStart = nextState == GameState.StartScreen;
            var showGameplay = nextState == GameState.Playing;
            var showEnd = nextState == GameState.LevelFailed || nextState == GameState.GameCompleted;

            if (startScreenRoot != null) startScreenRoot.SetActive(showStart);
            if (gameplayScreenRoot != null) gameplayScreenRoot.SetActive(showGameplay);
            if (endScreenRoot != null) endScreenRoot.SetActive(showEnd);
        }

        private void RefreshStaticUI()
        {
            LevelData levelData = levelCollection != null ? levelCollection.GetLevelAt(_currentLevelIndex) : null;
            int levelNumber = levelData != null ? levelData.levelNumber : _currentLevelIndex + 1;

            if (levelText != null)
            {
                levelText.text = $"Level {levelNumber}";
            }
        }

        private void RefreshTimerUI()
        {
            if (timerText == null)
            {
                return;
            }

            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(_remainingTime));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        private static bool IsTapDetected()
        {
            if (Input.GetMouseButtonDown(0))
            {
                return true;
            }

            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                return true;
            }

            return Input.GetKeyDown(KeyCode.Space);
        }
    }
}
