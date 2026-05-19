using System;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Conditions
{
    public struct ConditionIndicatorRefreshRequest
    {
        public BoardController BoardController;
        public BlockViewRuntimePool BlockViewPool;
        public float CellSize;
        public bool ShowBlockConditionIndicators;
        public float IndicatorCharacterSizeInCells;
        public int IndicatorFontSize;
        public Color IndicatorTextColor;
        public Camera IndicatorCamera;
        public Action<GameObject, bool> SetActiveIfChanged;
    }
}
