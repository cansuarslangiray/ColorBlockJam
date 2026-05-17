using System;
using Runtime.Core;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Managers
{
    [DisallowMultipleComponent]
    public class StateManager : SingletonMonoBehaviour<StateManager>
    {
        public event Action<GameState> OnStateChanged;

        public GameState CurrentState { get; private set; }
        private bool _hasState;

        public void ChangeState(GameState newState)
        {
            if (_hasState && CurrentState == newState)
            {
                return;
            }

            CurrentState = newState;
            _hasState = true;

            OnStateChanged?.Invoke(newState);
        }
    }
}
