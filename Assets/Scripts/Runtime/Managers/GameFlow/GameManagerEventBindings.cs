using System;
using Runtime.Controllers;
using Runtime.Domain.Enums;
using Runtime.Managers;

namespace Runtime.Managers.GameFlow
{
    internal sealed class GameManagerEventBindings
    {
        private readonly BoardController _boardController;
        private readonly StateManager _stateManager;
        private readonly UIManager _uiManager;
        private readonly Action _onLevelCompleted;
        private readonly Action<GameState> _onStateChanged;
        private readonly Action _onLevelTimerExpired;
        private readonly Action _onStartRequested;
        private readonly Action<GameState> _onEndGameActionRequested;
        private readonly Action _onReloadRequested;

        private bool _boardEventsRegistered;
        private bool _stateEventsRegistered;
        private bool _uiEventsRegistered;

        public GameManagerEventBindings(
            BoardController boardController,
            StateManager stateManager,
            UIManager uiManager,
            Action onLevelCompleted,
            Action<GameState> onStateChanged,
            Action onLevelTimerExpired,
            Action onStartRequested,
            Action<GameState> onEndGameActionRequested,
            Action onReloadRequested)
        {
            _boardController = boardController;
            _stateManager = stateManager;
            _uiManager = uiManager;
            _onLevelCompleted = onLevelCompleted;
            _onStateChanged = onStateChanged;
            _onLevelTimerExpired = onLevelTimerExpired;
            _onStartRequested = onStartRequested;
            _onEndGameActionRequested = onEndGameActionRequested;
            _onReloadRequested = onReloadRequested;
        }

        public void Register()
        {
            if (!_boardEventsRegistered && _boardController != null)
            {
                _boardController.LevelCompleted += _onLevelCompleted;
                _boardEventsRegistered = true;
            }

            if (!_stateEventsRegistered && _stateManager != null)
            {
                _stateManager.OnStateChanged += _onStateChanged;
                _stateEventsRegistered = true;
            }

            if (!_uiEventsRegistered && _uiManager != null)
            {
                _uiManager.LevelTimerExpired += _onLevelTimerExpired;
                _uiManager.StartRequested += _onStartRequested;
                _uiManager.EndGameActionRequested += _onEndGameActionRequested;
                _uiManager.ReloadRequested += _onReloadRequested;
                _uiEventsRegistered = true;
            }
        }

        public void Unregister()
        {
            if (_boardEventsRegistered && _boardController != null)
            {
                _boardController.LevelCompleted -= _onLevelCompleted;
            }
            _boardEventsRegistered = false;

            if (_stateEventsRegistered && _stateManager != null)
            {
                _stateManager.OnStateChanged -= _onStateChanged;
            }
            _stateEventsRegistered = false;

            if (_uiEventsRegistered && _uiManager != null)
            {
                _uiManager.LevelTimerExpired -= _onLevelTimerExpired;
                _uiManager.StartRequested -= _onStartRequested;
                _uiManager.EndGameActionRequested -= _onEndGameActionRequested;
                _uiManager.ReloadRequested -= _onReloadRequested;
            }
            _uiEventsRegistered = false;
        }
    }
}
