using System.Collections.Generic;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public sealed class BlockSceneVisualCache
    {
        private readonly Dictionary<BlockColor, Material> _materialByColor = new();
        private readonly Dictionary<BlockColor, Material> _runtimeFallbackMaterialByColor = new();
        private bool _isMaterialCacheDirty = true;

        public void InvalidateMaterialCache()
        {
            _isMaterialCacheDirty = true;
        }

        public void ClearRuntimeCaches()
        {
            ReleaseRuntimeFallbackMaterials();
        }

        public Material ResolveMaterial(BlockColor colorType, IReadOnlyList<BlockColorMaterialEntry> materialsByColor)
        {
            EnsureMaterialCache(materialsByColor);
            if (_materialByColor.TryGetValue(colorType, out var material) && material)
            {
                return material;
            }

            return GetOrCreateRuntimeFallbackMaterial(colorType);
        }

        public void ApplySharedMaterial(GameObject target, Material material)
        {
            if (!target)
            {
                return;
            }

            var renderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (!renderer || renderer.sharedMaterial == material)
                {
                    continue;
                }

                renderer.sharedMaterial = material;
            }
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

        private Material GetOrCreateRuntimeFallbackMaterial(BlockColor colorType)
        {
            if (_runtimeFallbackMaterialByColor.TryGetValue(colorType, out var fallback) && fallback)
            {
                return fallback;
            }

            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                return null;
            }

            fallback = new Material(shader)
            {
                name = "MAT_Runtime_Block_" + colorType,
                color = BlockColorPalette.GetColor(colorType)
            };

            _runtimeFallbackMaterialByColor[colorType] = fallback;
            return fallback;
        }

        private void ReleaseRuntimeFallbackMaterials()
        {
            foreach (var pair in _runtimeFallbackMaterialByColor)
            {
                var material = pair.Value;
                if (!material)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Object.Destroy(material);
                }
                else
                {
                    Object.DestroyImmediate(material);
                }
            }

            _runtimeFallbackMaterialByColor.Clear();
        }
    }
}
