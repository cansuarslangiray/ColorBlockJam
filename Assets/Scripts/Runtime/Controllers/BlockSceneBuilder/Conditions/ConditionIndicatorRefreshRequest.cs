using System;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Conditions
{
    public struct ConditionIndicatorRefreshRequest
    {
        public BoardController BoardController;
        public BlockViewRuntimePool BlockViewPool;
        public bool ShowBlockConditionIndicators;
        public Action<GameObject, bool> SetActiveIfChanged;
    }
}
