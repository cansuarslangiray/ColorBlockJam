using System.Collections;
using System.Collections.Generic;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private void StartBlockConditionUnlockTransition(int blockId, BlockRootView blockView, RuntimeBlockState runtimeBlock)
        {
            if (blockView == null)
            {
                return;
            }

            var targetMaterial = GetMaterial(runtimeBlock.ColorType);
            var targetBlockColor = blockView.HasCachedBlockColor ? blockView.CachedBlockColor : Color.white;
            if (TryResolveMaterialColor(targetMaterial, out var materialColor))
            {
                targetBlockColor = materialColor;
            }

            var outlineAlpha = blockView.HasCachedOutlineActiveColor
                ? Mathf.Max(0.0001f, blockView.CachedOutlineActiveColor.a)
                : 1f;
            var darkenFactor = Mathf.Clamp(outlineIdleDarkenFactor, 0.2f, 1f);
            var targetOutlineColor = targetBlockColor * darkenFactor;
            targetOutlineColor.a = outlineAlpha;

            var startBlockColor = new Color(1f, 1f, 1f, targetBlockColor.a <= 0f ? 1f : targetBlockColor.a);
            var startOutlineColor = new Color(1f, 1f, 1f, outlineAlpha);
            var duration = Mathf.Max(0.05f, minClearedUnlockColorTransitionDuration);

            var routine = StartCoroutine(AnimateBlockConditionUnlockTransition(blockId, blockView, duration,
                startBlockColor, targetBlockColor, startOutlineColor, targetOutlineColor));
            _blockConditionUnlockTransitionRoutineById[blockId] = routine;
        }

        private IEnumerator AnimateBlockConditionUnlockTransition(int blockId, BlockRootView blockView, float duration,
            Color startBlockColor, Color targetBlockColor, Color startOutlineColor, Color targetOutlineColor)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (blockView == null)
                {
                    break;
                }

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var easedT = Mathf.SmoothStep(0f, 1f, t);

                ApplyBlockColorOverride(blockView, Color.Lerp(startBlockColor, targetBlockColor, easedT));
                _blockOutlinePresenter.ApplyOutlineColorOverride(blockView,
                    Color.Lerp(startOutlineColor, targetOutlineColor, easedT));

                yield return null;
            }

            if (blockView != null)
            {
                ClearBlockColorOverrides(blockView);
                ApplyOutlineDragState(blockView, false);
            }

            _blockConditionUnlockTransitionRoutineById.Remove(blockId);
        }

        private void StopBlockConditionUnlockTransition(int blockId, bool clearVisualOverrides)
        {
            if (_blockConditionUnlockTransitionRoutineById.TryGetValue(blockId, out var routine))
            {
                StopCoroutine(routine);
                _blockConditionUnlockTransitionRoutineById.Remove(blockId);
            }

            if (!clearVisualOverrides || !_blockViewPool.TryGetActive(blockId, out var blockView) || blockView == null)
            {
                return;
            }

            ClearBlockColorOverrides(blockView);
            ApplyOutlineDragState(blockView, false);
        }

        private void StopAllBlockConditionUnlockTransitions(bool clearVisualOverrides)
        {
            if (_blockConditionUnlockTransitionRoutineById.Count == 0)
            {
                return;
            }

            var blockIds = new List<int>(_blockConditionUnlockTransitionRoutineById.Keys);
            for (var i = 0; i < blockIds.Count; i++)
            {
                StopBlockConditionUnlockTransition(blockIds[i], clearVisualOverrides);
            }
        }

        private void ApplyBlockColorOverride(BlockRootView blockView, Color color)
        {
            if (blockView == null)
            {
                return;
            }

            var activeCellCount = Mathf.Max(0, blockView.ActiveCellCount);
            var primaryCount = Mathf.Min(activeCellCount, blockView.CellRenderers.Count);
            for (var i = 0; i < primaryCount; i++)
            {
                ApplyRendererColorOverride(blockView.CellRenderers[i], color);
            }

            var nestedCount = Mathf.Min(activeCellCount, blockView.CellNestedRenderers.Count);
            for (var i = 0; i < nestedCount; i++)
            {
                var nestedRenderers = blockView.CellNestedRenderers[i];
                if (nestedRenderers == null)
                {
                    continue;
                }

                for (var nestedIndex = 0; nestedIndex < nestedRenderers.Length; nestedIndex++)
                {
                    ApplyRendererColorOverride(nestedRenderers[nestedIndex], color);
                }
            }
        }

        private void ClearBlockColorOverrides(BlockRootView blockView)
        {
            if (blockView == null)
            {
                return;
            }

            var activeCellCount = Mathf.Max(0, blockView.ActiveCellCount);
            var primaryCount = Mathf.Min(activeCellCount, blockView.CellRenderers.Count);
            for (var i = 0; i < primaryCount; i++)
            {
                ClearRendererColorOverride(blockView.CellRenderers[i]);
            }

            var nestedCount = Mathf.Min(activeCellCount, blockView.CellNestedRenderers.Count);
            for (var i = 0; i < nestedCount; i++)
            {
                var nestedRenderers = blockView.CellNestedRenderers[i];
                if (nestedRenderers == null)
                {
                    continue;
                }

                for (var nestedIndex = 0; nestedIndex < nestedRenderers.Length; nestedIndex++)
                {
                    ClearRendererColorOverride(nestedRenderers[nestedIndex]);
                }
            }
        }

        private void ApplyRendererColorOverride(Renderer renderer, Color color)
        {
            if (!renderer)
            {
                return;
            }

            _blockColorPropertyBlock ??= new MaterialPropertyBlock();
            _blockColorPropertyBlock.Clear();
            _blockColorPropertyBlock.SetColor(ColorPropertyId, color);
            _blockColorPropertyBlock.SetColor(BaseColorPropertyId, color);
            renderer.SetPropertyBlock(_blockColorPropertyBlock);
        }

        private static void ClearRendererColorOverride(Renderer renderer)
        {
            if (renderer)
            {
                renderer.SetPropertyBlock(null);
            }
        }
    }
}
