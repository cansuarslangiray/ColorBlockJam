using System;
using UnityEngine;

namespace Runtime.Data
{
    [Serializable]
    public class StartPanelTextData
    {
        public string title;
        [TextArea(1, 3)] public string subtitle;
        public string actionLabel;

        public static StartPanelTextData CreateDefault()
        {
            return new StartPanelTextData
            {
                title = "Ready to Solve?",
                subtitle = "Slide each block to its matching door before the timer runs out.",
                actionLabel = "Start Level"
            };
        }
    }
}
