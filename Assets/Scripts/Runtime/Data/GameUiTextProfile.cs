using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Data
{
    [CreateAssetMenu(fileName = "GameUiTextProfile", menuName = "ColorBlockJam/UI/Game UI Text Profile")]
    public class GameUiTextProfile : ScriptableObject
    {
        public StartPanelTextData startPanel = StartPanelTextData.CreateDefault();
        public EndGamePanelTextData levelCompleted = EndGamePanelTextData.CreateLevelCompletedDefault();
        public EndGamePanelTextData levelFailed = EndGamePanelTextData.CreateLevelFailedDefault();
        public EndGamePanelTextData gameCompleted = EndGamePanelTextData.CreateGameCompletedDefault();

        public EndGamePanelTextData GetEndGamePanelText(GameState state)
        {
            return state switch
            {
                GameState.LevelCompleted => levelCompleted,
                GameState.LevelFailed => levelFailed,
                GameState.GameCompleted => gameCompleted,
                _ => EndGamePanelTextData.CreateEmpty()
            };
        }
    }
}
