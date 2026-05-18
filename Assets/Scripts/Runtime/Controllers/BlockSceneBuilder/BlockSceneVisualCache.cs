using System.Collections.Generic;
using Runtime.Data;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class BlockSceneVisualCache
    {
        private readonly Dictionary<BlockColor, Material> _materialByColor = new();
        private readonly HashSet<BlockColor> _missingMaterialWarnings = new();
        private bool _isMaterialCacheDirty = true;

        public void InvalidateMaterialCache()
        {
            _isMaterialCacheDirty = true;
            _missingMaterialWarnings.Clear();
        }

        public void ClearRuntimeCaches()
        {
            _missingMaterialWarnings.Clear();
        }

        public Material ResolveMaterial(BlockColor colorType, IReadOnlyList<BlockColorMaterialEntry> materialsByColor,
            Object context = null)
        {
            EnsureMaterialCache(materialsByColor);
            if (_materialByColor.TryGetValue(colorType, out var material) && material)
            {
                return material;
            }

            if (_missingMaterialWarnings.Add(colorType))
            {
                Debug.LogWarning(
                    $"BlockSceneBuilder missing material mapping for {colorType}. Assign this color in materialsByColor to keep visuals configured from Unity.",
                    context);
            }

            return null;
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
