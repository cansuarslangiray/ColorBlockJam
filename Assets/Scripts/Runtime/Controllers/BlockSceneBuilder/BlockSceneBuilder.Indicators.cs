namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private void RefreshAllConditionIndicators()
        {
            var indicatorRefreshRequest = new ConditionIndicatorPresenter.RefreshRequest
            {
                BoardController = boardController,
                BlockViewPool = _blockViewPool,
                CellSize = CellSize,
                ShowBlockConditionIndicators = showBlockConditionIndicators,
                IndicatorCharacterSizeInCells = indicatorCharacterSizeInCells,
                IndicatorFontSize = indicatorFontSize,
                IndicatorTextColor = indicatorTextColor,
                IndicatorCamera = indicatorCamera,
                SetActiveIfChanged = SetActiveIfChanged
            };
            _conditionIndicatorPresenter.RefreshAll(indicatorRefreshRequest);
        }

        public void RefreshConditionIndicatorBillboards()
        {
            _conditionIndicatorPresenter.RequestBillboardRefresh();
            var billboardRequest = new ConditionIndicatorPresenter.BillboardRequest
            {
                ShowBlockConditionIndicators = showBlockConditionIndicators,
                IndicatorCamera = indicatorCamera,
                BlockViewPool = _blockViewPool
            };
            _conditionIndicatorPresenter.UpdateBillboards(billboardRequest);
        }
    }
}
