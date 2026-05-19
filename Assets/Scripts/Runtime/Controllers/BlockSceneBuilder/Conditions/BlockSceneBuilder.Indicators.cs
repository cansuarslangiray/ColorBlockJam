using Runtime.Controllers.BlockSceneBuilder.Conditions;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private void RefreshAllConditionIndicators()
        {
            var indicatorRefreshRequest = new ConditionIndicatorRefreshRequest
            {
                BoardController = boardController,
                BlockViewPool = _blockViewPool,
                ShowBlockConditionIndicators = showBlockConditionIndicators,
                SetActiveIfChanged = SetActiveIfChanged
            };
            _conditionIndicatorPresenter.RefreshAll(indicatorRefreshRequest);
        }
    }
}
