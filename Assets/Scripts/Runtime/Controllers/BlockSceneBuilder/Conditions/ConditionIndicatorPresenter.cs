using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Conditions
{
    public sealed class ConditionIndicatorPresenter
    {
        private const string MovementIndicatorText = "<->";

        public void RefreshAll(ConditionIndicatorRefreshRequest request)
        {
            var boardController = request.BoardController;
            var blockViewPool = request.BlockViewPool;
            blockViewPool.ForEachActive((blockId, blockView) =>
            {
                if (blockView == null)
                {
                    return;
                }

                ConfigureConditionIndicator(blockId, blockView, request);
            });
        }

        private static void ConfigureConditionIndicator(int blockId, BlockRootView blockView,
            ConditionIndicatorRefreshRequest request)
        {
            if (!request.ShowBlockConditionIndicators || !blockView.ConditionIndicatorObject ||
                blockView.ConditionIndicatorText == null)
            {
                HideConditionIndicator(blockView, request);
                return;
            }

            if (!request.BoardController.TryGetRuntimeBlock(blockId, out var runtimeBlock))
            {
                HideConditionIndicator(blockView, request);
                return;
            }

            var resolvedIndicatorText = ResolveIndicatorText(request.BoardController, blockId, runtimeBlock);
            if (string.IsNullOrWhiteSpace(resolvedIndicatorText))
            {
                HideConditionIndicator(blockView, request);
                return;
            }

            if (!string.Equals(blockView.ConditionIndicatorText.text, resolvedIndicatorText,
                    System.StringComparison.Ordinal))
            {
                blockView.ConditionIndicatorText.text = resolvedIndicatorText;
            }

            var indicatorTransform = blockView.ConditionIndicatorObject.transform;
            if (TryResolveIndicatorRotation(runtimeBlock, blockView.ConditionIndicatorDefaultLocalRotation,
                    request.VerticalMovementRotationDegrees,
                    out var targetRotation) &&
                HasDifferentRotation(indicatorTransform.localRotation, targetRotation))
            {
                indicatorTransform.localRotation = targetRotation;
            }

            request.SetActiveIfChanged?.Invoke(blockView.ConditionIndicatorObject, true);
        }

        private static string ResolveIndicatorText(BoardController boardController, int blockId,
            RuntimeBlockState runtimeBlock)
        {
            if (boardController.TryGetConditionIndicatorState(blockId, out var indicatorState) &&
                indicatorState.IsVisible &&
                !string.IsNullOrWhiteSpace(indicatorState.Text))
            {
                return indicatorState.Text;
            }

            var feature = runtimeBlock.BlockFeatures.Sanitize();
            return feature switch
            {
                BlockFeature.Horizontal => MovementIndicatorText,
                BlockFeature.Vertical => MovementIndicatorText,
                BlockFeature.MaxMovesBeforeExit => ResolveFallbackCounterText(runtimeBlock.MaxMovesBeforeExit),
                BlockFeature.MinClearedBlocksBeforeExit =>
                    ResolveFallbackCounterText(runtimeBlock.MinClearedBlocksBeforeExit),
                _ => string.Empty
            };
        }

        private static string ResolveFallbackCounterText(int value)
        {
            return value > 0 ? value.ToString() : string.Empty;
        }

        private static bool TryResolveIndicatorRotation(RuntimeBlockState runtimeBlock, Quaternion defaultRotation,
            float verticalRotationDegrees,
            out Quaternion targetRotation)
        {
            var features = runtimeBlock.BlockFeatures;
            if (features.IsMovementVertical())
            {
                targetRotation = Quaternion.Euler(0f, 0f, verticalRotationDegrees);
                return true;
            }

            targetRotation = defaultRotation;
            return true;
        }

        private static bool HasDifferentRotation(Quaternion current, Quaternion target)
        {
            return Quaternion.Angle(current, target) > 0.01f;
        }

        private static void HideConditionIndicator(BlockRootView blockView, ConditionIndicatorRefreshRequest request)
        {
            if (blockView?.ConditionIndicatorObject)
            {
                request.SetActiveIfChanged?.Invoke(blockView.ConditionIndicatorObject, false);
            }
        }
    }
}
