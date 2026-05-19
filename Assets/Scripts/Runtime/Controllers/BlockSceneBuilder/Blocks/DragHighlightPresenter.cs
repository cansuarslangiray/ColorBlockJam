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
            if (blockView == null)
            {
                return;
            }

            var outlineRenderer = blockView.DragOutlineRenderer;
            var outlineObject = outlineRenderer ? outlineRenderer.gameObject : null;
            if (!outlineObject)
            {
                return;
            }

            setActiveIfChanged?.Invoke(outlineObject, isActive);
        }
    }
}
