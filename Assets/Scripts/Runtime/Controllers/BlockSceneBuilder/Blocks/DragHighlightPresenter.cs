using System;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Blocks
{
    public sealed class DragHighlightPresenter
    {
        public void SetDragHighlightActive(BlockRootView blockView, bool isActive,
            Action<GameObject, bool> setActiveIfChanged)
        {
            if (blockView == null || blockView.RootTransform == null)
            {
                return;
            }

            var outlineRenderer = blockView.DragOutlineRenderer;
            var outlineObject = outlineRenderer ? outlineRenderer.gameObject : null;
            if (!outlineObject)
            {
                if (!blockView.HasLoggedMissingDragOutline)
                {
                    Debug.LogWarning(
                        $"Block '{blockView.RootObject.name}' is missing pooled 'BlockDragOutline' LineRenderer.",
                        blockView.RootObject);
                    blockView.HasLoggedMissingDragOutline = true;
                }

                return;
            }

            blockView.HasLoggedMissingDragOutline = false;
            setActiveIfChanged?.Invoke(outlineObject, isActive);
        }
    }
}
