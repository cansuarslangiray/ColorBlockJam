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

        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState)
            {
                return;
            }

            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }
    }
}
