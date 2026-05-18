using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private void RefreshAllConditionIndicators()
        {
            if (boardController == null)
            {
                return;
            }

            var resolvedCellSize = CellSize;
            _visibleConditionIndicatorCount = 0;

            _blockViewPool.ForEachActive((blockId, blockView) =>
            {
                if (blockView == null || !boardController.TryGetRuntimeBlock(blockId, out var runtimeBlock))
                {
                    if (blockView?.ConditionIndicatorObject)
                    {
                        SetActiveIfChanged(blockView.ConditionIndicatorObject, false);
                    }

                    return;
                }

                ConfigureConditionIndicator(blockView, runtimeBlock, resolvedCellSize);
            });

            _needsConditionBillboardRefresh = _visibleConditionIndicatorCount > 0;
            UpdateConditionIndicatorBillboards();
        }

        public void RefreshConditionIndicatorBillboards()
        {
            _needsConditionBillboardRefresh = true;
            UpdateConditionIndicatorBillboards();
        }

        private void ConfigureConditionIndicator(BlockRootView blockView, RuntimeBlockState runtimeBlock, float cellSize)
        {
            if (!showBlockConditionIndicators || !ShouldShowConditionIndicator(runtimeBlock))
            {
                if (blockView?.ConditionIndicatorObject)
                {
                    SetActiveIfChanged(blockView.ConditionIndicatorObject, false);
                }

                return;
            }

            EnsureConditionIndicator(blockView);
            if (!blockView.ConditionIndicatorObject || blockView.ConditionIndicatorText == null)
            {
                return;
            }

            var textMesh = blockView.ConditionIndicatorText;
            textMesh.characterSize = Mathf.Max(0.01f, indicatorCharacterSizeInCells * cellSize);
            textMesh.fontSize = Mathf.Max(8, indicatorFontSize);
            textMesh.color = indicatorTextColor;
            textMesh.text = BuildConditionIndicatorText(runtimeBlock);

            var indicatorTransform = blockView.ConditionIndicatorObject.transform;
            indicatorTransform.localPosition = blockView.ConditionIndicatorLocalAnchor;
            SetActiveIfChanged(blockView.ConditionIndicatorObject, true);
            _visibleConditionIndicatorCount++;
        }

        private void EnsureConditionIndicator(BlockRootView blockView)
        {
            if (blockView?.ConditionIndicatorObject)
            {
                return;
            }

            var indicatorObject = new GameObject("ConditionIndicator");
            var indicatorTransform = indicatorObject.transform;
            indicatorTransform.SetParent(blockView.RootTransform, false);
            indicatorTransform.localPosition = blockView.ConditionIndicatorLocalAnchor;
            indicatorTransform.localScale = Vector3.one;

            var textMesh = indicatorObject.AddComponent<TextMesh>();
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = Mathf.Max(8, indicatorFontSize);
            textMesh.characterSize = Mathf.Max(0.01f, indicatorCharacterSizeInCells * CellSize);
            textMesh.color = indicatorTextColor;
            textMesh.text = string.Empty;

            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (!font)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (font)
            {
                textMesh.font = font;
            }

            indicatorObject.SetActive(false);
            blockView.ConditionIndicatorObject = indicatorObject;
            blockView.ConditionIndicatorText = textMesh;
        }

        private static bool ShouldShowConditionIndicator(RuntimeBlockState runtimeBlock)
        {
            return runtimeBlock.BlockFeatures != BlockFeature.Default;
        }

        private string BuildConditionIndicatorText(RuntimeBlockState runtimeBlock)
        {
            _indicatorTextBuilder.Clear();

            var features = runtimeBlock.BlockFeatures;
            if (features.HasFeature(BlockFeature.Horizontal))
            {
                _indicatorTextBuilder.Append("<-->");
            }
            else if (features.HasFeature(BlockFeature.Vertical))
            {
                _indicatorTextBuilder.Append("^");
                _indicatorTextBuilder.Append('\n');
                _indicatorTextBuilder.Append("|");
                _indicatorTextBuilder.Append('\n');
                _indicatorTextBuilder.Append("v");
            }

            if (features.HasFeature(BlockFeature.MinMovesBeforeExit))
            {
                AppendFeatureLine("MinMv");
            }

            if (features.HasFeature(BlockFeature.MaxMovesBeforeExit))
            {
                AppendFeatureLine("MaxMv");
            }

            if (features.HasFeature(BlockFeature.MinClearedBlocksBeforeExit))
            {
                AppendFeatureLine("Unlock");
            }

            return _indicatorTextBuilder.ToString();
        }

        private void AppendFeatureLine(string label)
        {
            if (_indicatorTextBuilder.Length > 0)
            {
                _indicatorTextBuilder.Append('\n');
            }

            _indicatorTextBuilder.Append(label);
        }

        private void UpdateConditionIndicatorBillboards()
        {
            if (!showBlockConditionIndicators)
            {
                return;
            }

            if (_visibleConditionIndicatorCount <= 0)
            {
                return;
            }

            if (!indicatorCamera)
            {
                return;
            }

            var cameraRotation = indicatorCamera.transform.rotation;
            if (!_needsConditionBillboardRefresh &&
                _hasLastIndicatorCameraRotation &&
                cameraRotation == _lastIndicatorCameraRotation)
            {
                return;
            }

            var cameraForward = indicatorCamera.transform.forward;
            var cameraUp = indicatorCamera.transform.up;

            _blockViewPool.ForEachActive((_, blockView) =>
            {
                var indicatorObject = blockView?.ConditionIndicatorObject;
                if (!indicatorObject || !indicatorObject.activeSelf)
                {
                    return;
                }

                var indicatorTransform = indicatorObject.transform;
                indicatorTransform.rotation = Quaternion.LookRotation(cameraForward, cameraUp);
            });

            _lastIndicatorCameraRotation = cameraRotation;
            _hasLastIndicatorCameraRotation = true;
            _needsConditionBillboardRefresh = false;
        }
    }
}
