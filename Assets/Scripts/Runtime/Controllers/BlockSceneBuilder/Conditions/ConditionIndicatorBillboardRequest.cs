using Runtime.Controllers.BlockSceneBuilder.Pool;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Conditions
{
    public struct ConditionIndicatorBillboardRequest
    {
        public bool ShowBlockConditionIndicators;
        public Camera IndicatorCamera;
        public BlockViewRuntimePool BlockViewPool;
    }
}
