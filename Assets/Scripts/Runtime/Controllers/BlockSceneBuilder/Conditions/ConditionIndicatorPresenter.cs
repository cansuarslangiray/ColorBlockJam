using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Conditions
{
    public sealed class ConditionIndicatorPresenter
    {
        private const string ConditionIndicatorObjectName = "ConditionIndicator";
        private const string HorizontalIndicatorText = "<-->";
        private const string VerticalIndicatorText = "^\n|\nv";

        private int _visibleConditionIndicatorCount;
        private bool _needsConditionBillboardRefresh;
        private Quaternion _lastIndicatorCameraRotation;
        private bool _hasLastIndicatorCameraRotation;

        public void ResetRuntimeState()
        {
            _visibleConditionIndicatorCount = 0;
            _needsConditionBillboardRefresh = false;
            _hasLastIndicatorCameraRotation = false;
        }

        public void RefreshAll(ConditionIndicatorRefreshRequest request)
        {
            var boardController = request.BoardController;
            var blockViewPool = request.BlockViewPool;
            if (boardController == null || blockViewPool == null)
            {
                return;
            }

            _visibleConditionIndicatorCount = 0;
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

            _needsConditionBillboardRefresh = _visibleConditionIndicatorCount > 0;
            UpdateBillboards(new ConditionIndicatorBillboardRequest
            {
                ShowBlockConditionIndicators = request.ShowBlockConditionIndicators,
                IndicatorCamera = request.IndicatorCamera,
                BlockViewPool = request.BlockViewPool
            });
        }

        public void RequestBillboardRefresh()
        {
            _needsConditionBillboardRefresh = true;
        }

        public void UpdateBillboards(ConditionIndicatorBillboardRequest request)
        {
            var blockViewPool = request.BlockViewPool;
            var indicatorCamera = request.IndicatorCamera;
            if (!request.ShowBlockConditionIndicators || _visibleConditionIndicatorCount <= 0 || !request.IndicatorCamera ||
                blockViewPool == null)
            {
                return;
            }

            var cameraTransform = indicatorCamera.transform;
            var cameraRotation = cameraTransform.rotation;
            if (!_needsConditionBillboardRefresh &&
                _hasLastIndicatorCameraRotation &&
                cameraRotation == _lastIndicatorCameraRotation)
            {
                return;
            }

            var cameraForward = cameraTransform.forward;
            var cameraUp = cameraTransform.up;
            blockViewPool.ForEachActive((_, blockView) =>
            {
                var indicatorObject = blockView?.ConditionIndicatorObject;
                if (!indicatorObject || !indicatorObject.activeSelf)
                {
                    return;
                }

                indicatorObject.transform.rotation = Quaternion.LookRotation(cameraForward, cameraUp);
            });

            _lastIndicatorCameraRotation = cameraRotation;
            _hasLastIndicatorCameraRotation = true;
            _needsConditionBillboardRefresh = false;
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

            EnsureConditionIndicator(blockView, request.CellSize, request.IndicatorCharacterSizeInCells,
                request.IndicatorFontSize, request.IndicatorTextColor);
            if (!blockView.ConditionIndicatorObject || blockView.ConditionIndicatorText == null)
            {
                return;
            }

            var textMesh = blockView.ConditionIndicatorText;
            textMesh.characterSize = Mathf.Max(0.01f, request.IndicatorCharacterSizeInCells * request.CellSize);
            textMesh.fontSize = Mathf.Max(8, request.IndicatorFontSize);
            textMesh.color = request.IndicatorTextColor;
            textMesh.text = BuildConditionIndicatorText(runtimeBlock);

            blockView.ConditionIndicatorObject.transform.localPosition = blockView.ConditionIndicatorLocalAnchor;
            request.SetActiveIfChanged?.Invoke(blockView.ConditionIndicatorObject, true);
            _visibleConditionIndicatorCount++;
        }

        private static bool ShouldShowConditionIndicator(RuntimeBlockState runtimeBlock)
        {
            return runtimeBlock.BlockFeatures != BlockFeature.Default;
        }

        private void EnsureConditionIndicator(BlockRootView blockView, float cellSize,
            float indicatorCharacterSizeInCells, int indicatorFontSize, Color indicatorTextColor)
        {
            if (blockView?.ConditionIndicatorObject)
            {
                return;
            }

            if (blockView?.RootTransform == null)
            {
                return;
            }

            blockView.ConditionIndicatorObject = blockView.PooledConditionIndicatorObject;
            blockView.ConditionIndicatorText = blockView.PooledConditionIndicatorText;
            if (!blockView.ConditionIndicatorObject || blockView.ConditionIndicatorText == null)
            {
                if (!blockView.HasLoggedMissingConditionIndicator)
                {
                    Debug.LogWarning(
                        $"Block '{blockView.RootObject.name}' is missing pooled '{ConditionIndicatorObjectName}' TextMesh. Runtime creation is disabled.",
                        blockView.RootObject);
                    blockView.HasLoggedMissingConditionIndicator = true;
                }

                return;
            }

            blockView.ConditionIndicatorText.anchor = TextAnchor.MiddleCenter;
            blockView.ConditionIndicatorText.alignment = TextAlignment.Center;
            blockView.ConditionIndicatorText.characterSize =
                Mathf.Max(0.01f, indicatorCharacterSizeInCells * cellSize);
            blockView.ConditionIndicatorText.fontSize = Mathf.Max(8, indicatorFontSize);
            blockView.ConditionIndicatorText.color = indicatorTextColor;
            blockView.ConditionIndicatorObject.transform.localPosition = blockView.ConditionIndicatorLocalAnchor;
            blockView.ConditionIndicatorObject.transform.localRotation = Quaternion.identity;
            blockView.ConditionIndicatorObject.transform.localScale = Vector3.one;
            blockView.ConditionIndicatorObject.SetActive(false);
            blockView.HasLoggedMissingConditionIndicator = false;
        }

        private string BuildConditionIndicatorText(RuntimeBlockState runtimeBlock)
        {
            var features = runtimeBlock.BlockFeatures;
            if (features.HasFeature(BlockFeature.Horizontal))
            {
                return HorizontalIndicatorText;
            }

            if (features.HasFeature(BlockFeature.Vertical))
            {
                return VerticalIndicatorText;
            }

            return string.Empty;
        }

    }
}
