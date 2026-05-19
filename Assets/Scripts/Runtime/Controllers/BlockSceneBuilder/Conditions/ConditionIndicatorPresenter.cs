using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Conditions
{
    public sealed class ConditionIndicatorPresenter
    {
        private static readonly Quaternion VerticalIndicatorRotation = Quaternion.Euler(0f, 0f, 90f);

        public void RefreshAll(ConditionIndicatorRefreshRequest request)
        {
            var boardController = request.BoardController;
            var blockViewPool = request.BlockViewPool;
            blockViewPool.ForEachActive((blockId, blockView) =>
            {
                if (blockView == null || !boardController.TryGetRuntimeBlock(blockId, out var runtimeBlock))
                {
                    if (blockView?.ConditionIndicatorObject)
                    {
                        request.SetActiveIfChanged?.Invoke(blockView.ConditionIndicatorObject, false);
                    }

                    return;
                }

                ConfigureConditionIndicator(blockView, runtimeBlock, request);
            });

        }

        private void ConfigureConditionIndicator(BlockRootView blockView, RuntimeBlockState runtimeBlock,
            ConditionIndicatorRefreshRequest request)
        {
            if (!request.ShowBlockConditionIndicators || !ShouldShowConditionIndicator(runtimeBlock))
            {
                if (blockView?.ConditionIndicatorObject)
                {
                    request.SetActiveIfChanged?.Invoke(blockView.ConditionIndicatorObject, false);
                }

                return;
            }

            if (!blockView.ConditionIndicatorObject || blockView.ConditionIndicatorText == null)
            {
                return;
            }

            blockView.ConditionIndicatorObject.transform.localRotation = ResolveIndicatorRotation(runtimeBlock);
            request.SetActiveIfChanged?.Invoke(blockView.ConditionIndicatorObject, true);
        }

        private static bool ShouldShowConditionIndicator(RuntimeBlockState runtimeBlock)
        {
            return runtimeBlock.BlockFeatures != BlockFeature.Default;
        }

        private static Quaternion ResolveIndicatorRotation(RuntimeBlockState runtimeBlock)
        {
            var features = runtimeBlock.BlockFeatures;
            if (features.HasFeature(BlockFeature.Vertical))
            {
                return VerticalIndicatorRotation;
            }

            return Quaternion.identity;
        }

    }
}
