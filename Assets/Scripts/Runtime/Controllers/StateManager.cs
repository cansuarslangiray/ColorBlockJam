using System;
using Runtime.Core;
using Runtime.Domain.Enums;

namespace Runtime.Controllers
{
    public class StateManager : SingletonMonoBehaviour<StateManager>
    {
        public event Action<GameState, GameState> OnStateChanged;

        public GameState CurrentState { get; private set; }
        private bool _hasState;

        public void ChangeState(GameState newState)
        {
            if (_hasState && CurrentState == newState)
            {
                return;
            }

            var oldState = CurrentState;
            CurrentState = newState;
            _hasState = true;

            OnStateChanged?.Invoke(oldState, newState);
        }
    }
}
