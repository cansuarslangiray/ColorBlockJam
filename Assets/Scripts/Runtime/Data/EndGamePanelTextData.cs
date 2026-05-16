using System;
using UnityEngine;

namespace Runtime.Data
{
    [Serializable]
    public class EndGamePanelTextData
    {
        public string title;
        [TextArea(1, 3)] public string subtitle;
        public string actionLabel;

        public static EndGamePanelTextData CreateEmpty()
        {
            return new EndGamePanelTextData
            {
                title = string.Empty,
                subtitle = string.Empty,
                actionLabel = string.Empty
            };
        }

        public static EndGamePanelTextData CreateLevelCompletedDefault()
        {
            return new EndGamePanelTextData
            {
                title = "Level Complete!",
                subtitle = "Nice move. Keep the streak going.",
                actionLabel = "Continue"
            };
        }

        public static EndGamePanelTextData CreateLevelFailedDefault()
        {
            return new EndGamePanelTextData
            {
                title = "Time's Up",
                subtitle = "You were close. Try that route once more.",
                actionLabel = "Retry"
            };
        }

        public static EndGamePanelTextData CreateGameCompletedDefault()
        {
            return new EndGamePanelTextData
            {
                title = "You Cleared All Levels!",
                subtitle = "Great run. Ready for another full clear?",
                actionLabel = "Restart"
            };
        }
    }
}
