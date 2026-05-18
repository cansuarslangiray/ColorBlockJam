using System.Collections.Generic;
using Runtime.Data;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class BlockSceneVisualCache
    {
        private readonly Dictionary<BlockColor, Material> _materialByColor = new();
        private readonly Dictionary<int, Renderer[]> _rendererCacheByObjectId = new();
        private bool _isMaterialCacheDirty = true;

        public void InvalidateMaterialCache()
        {
            _isMaterialCacheDirty = true;
        }

        public void ClearRuntimeCaches()
        {
            _rendererCacheByObjectId.Clear();
        }

        public Material ResolveMaterial(BlockColor colorType, IReadOnlyList<BlockColorMaterialEntry> materialsByColor)
        {
            EnsureMaterialCache(materialsByColor);
            return _materialByColor.GetValueOrDefault(colorType);
        }

        public void ApplySharedMaterial(GameObject target, Material material)
        {
            if (!target)
            {
                return;
            }

            var renderers = GetCachedRenderers(target);
            foreach (var renderer in renderers)
            {
                if (!renderer || renderer.sharedMaterial == material)
                {
                    continue;
                }

                renderer.sharedMaterial = material;
            }
        }

        private Renderer[] GetCachedRenderers(GameObject target)
        {
            var objectId = target.GetInstanceID();
            if (_rendererCacheByObjectId.TryGetValue(objectId, out var cachedRenderers) && cachedRenderers != null)
            {
                return cachedRenderers;
            }

            cachedRenderers = target.GetComponentsInChildren<Renderer>(true);
            _rendererCacheByObjectId[objectId] = cachedRenderers;
            return cachedRenderers;
        }

        private void EnsureMaterialCache(IReadOnlyList<BlockColorMaterialEntry> materialsByColor)
        {
            if (!_isMaterialCacheDirty)
            {
                return;
            }

            _materialByColor.Clear();
            if (materialsByColor == null)
            {
                _isMaterialCacheDirty = false;
                return;
            }

            for (var i = 0; i < materialsByColor.Count; i++)
            {
                var entry = materialsByColor[i];
                if (!entry.material || _materialByColor.ContainsKey(entry.colorType))
                {
                    continue;
                }

                _materialByColor[entry.colorType] = entry.material;
            }

            _isMaterialCacheDirty = false;
        }
    }
}
