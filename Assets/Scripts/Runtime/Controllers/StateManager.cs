using System;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Controllers
{
    public class StateManager : MonoBehaviour
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
