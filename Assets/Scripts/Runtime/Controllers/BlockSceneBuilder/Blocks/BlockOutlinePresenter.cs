using Runtime.Controllers.BlockSceneBuilder.Pool;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Blocks
{
    public sealed class BlockOutlinePresenter
    {
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _outlinePropertyBlock;

        public void ApplyOutlineState(BlockRootView blockView, bool isDragActive, float idleDarkenFactor)
        {
            if (blockView == null)
            {
                return;
            }

            var outlineRenderer = blockView.OutlineRenderer;
            if (!outlineRenderer || !outlineRenderer.gameObject)
            {
                return;
            }

            if (!outlineRenderer.gameObject.activeSelf)
            {
                outlineRenderer.gameObject.SetActive(true);
            }

            if (!outlineRenderer.enabled)
            {
                outlineRenderer.enabled = true;
            }

            if (!blockView.HasCachedOutlineActiveColor)
            {
                blockView.CachedOutlineActiveColor = ResolveOutlineActiveColor(outlineRenderer);
                blockView.HasCachedOutlineActiveColor = true;
            }

            var nextColor = isDragActive
                ? ResolveDragOutlineColor(blockView.CachedOutlineActiveColor)
                : ResolveIdleOutlineColor(blockView, idleDarkenFactor);
            ApplyOutlineColor(blockView, outlineRenderer, nextColor);
        }

        private static Color ResolveDragOutlineColor(Color activeOutlineColor)
        {
            return new Color(1f, 1f, 1f, Mathf.Max(0.0001f, activeOutlineColor.a));
        }

        private static Color ResolveOutlineActiveColor(LineRenderer outlineRenderer)
        {
            var fallbackColor = Color.white;
            var material = outlineRenderer.sharedMaterial;
            if (!material)
            {
                return fallbackColor;
            }

            if (material.HasProperty(BaseColorPropertyId))
            {
                return material.GetColor(BaseColorPropertyId);
            }

            if (material.HasProperty(ColorPropertyId))
            {
                return material.GetColor(ColorPropertyId);
            }

            return fallbackColor;
        }

        private static Color ResolveIdleOutlineColor(BlockRootView blockView, float idleDarkenFactor)
        {
            var sourceColor = blockView.HasCachedBlockColor
                ? blockView.CachedBlockColor
                : blockView.CachedOutlineActiveColor;
            var darkenFactor = Mathf.Clamp(idleDarkenFactor, 0.2f, 1f);
            var idleColor = sourceColor * darkenFactor;
            idleColor.a = Mathf.Max(0.0001f, blockView.CachedOutlineActiveColor.a);
            return idleColor;
        }

        private void ApplyOutlineColor(BlockRootView blockView, Renderer outlineRenderer, Color color)
        {
            if (blockView.HasAppliedOutlineColor && blockView.AppliedOutlineColor.Equals(color))
            {
                return;
            }

            _outlinePropertyBlock ??= new MaterialPropertyBlock();
            _outlinePropertyBlock.Clear();
            _outlinePropertyBlock.SetColor(ColorPropertyId, color);
            _outlinePropertyBlock.SetColor(BaseColorPropertyId, color);
            outlineRenderer.SetPropertyBlock(_outlinePropertyBlock);

            blockView.AppliedOutlineColor = color;
            blockView.HasAppliedOutlineColor = true;
        }
    }
}
