using System;
using System.Text;
using Runtime.Domain.Enums;
using Runtime.Domain.Models;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class ConditionIndicatorPresenter
    {
        public struct RefreshRequest
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

        public struct BillboardRequest
        {
            public bool ShowBlockConditionIndicators;
            public Camera IndicatorCamera;
            public BlockViewRuntimePool BlockViewPool;
        }

        private readonly StringBuilder _indicatorTextBuilder = new(96);
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

        public void RefreshAll(RefreshRequest request)
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
            UpdateBillboards(new BillboardRequest
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

        public void UpdateBillboards(BillboardRequest request)
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
            RefreshRequest request)
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

            var indicatorObject = new GameObject("ConditionIndicator");
            var indicatorTransform = indicatorObject.transform;
            if (blockView == null) return;
            indicatorTransform.SetParent(blockView.RootTransform, false);
            indicatorTransform.localPosition = blockView.ConditionIndicatorLocalAnchor;
            indicatorTransform.localScale = Vector3.one;

            var textMesh = indicatorObject.AddComponent<TextMesh>();
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = Mathf.Max(8, indicatorFontSize);
            textMesh.characterSize = Mathf.Max(0.01f, indicatorCharacterSizeInCells * cellSize);
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

            return _indicatorTextBuilder.ToString();
        }

    }
}
