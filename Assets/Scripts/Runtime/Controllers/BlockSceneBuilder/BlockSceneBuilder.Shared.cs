using System.Collections.Generic;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private readonly Dictionary<int, Renderer[]> _rendererCacheByObjectId = new();

        private static void SetActiveIfChanged(GameObject target, bool isActive)
        {
            if (target && target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
        }

        private static float SmoothStep01(float value)
        {
            var t = Mathf.Clamp01(value);
            return t * t * (3f - (2f * t));
        }

        private static float EvaluateCurve01(AnimationCurve curve, float normalized, float fallbackValue)
        {
            return curve != null ? Mathf.Clamp01(curve.Evaluate(normalized)) : fallbackValue;
        }

        private static void ApplyWorldTransform(Transform target, Vector3 position, Vector3 scale)
        {
            if (!target)
            {
                return;
            }

            if (target.position != position)
            {
                target.position = position;
            }

            if (target.rotation != Quaternion.identity)
            {
                target.rotation = Quaternion.identity;
            }

            if (target.localScale != scale)
            {
                target.localScale = scale;
            }
        }

        private static void ApplyLocalTransform(Transform target, Vector3 localPosition, Vector3 localScale)
        {
            if (!target)
            {
                return;
            }

            if (target.localPosition != localPosition)
            {
                target.localPosition = localPosition;
            }

            if (target.localRotation != Quaternion.identity)
            {
                target.localRotation = Quaternion.identity;
            }

            if (target.localScale != localScale)
            {
                target.localScale = localScale;
            }
        }

        private void ApplySharedMaterial(GameObject target, Material material)
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

        private Material GetMaterial(BlockColor colorType)
        {
            return GetConfiguredMaterial(colorType);
        }

        private Material GetConfiguredMaterial(BlockColor colorType)
        {
            EnsureConfiguredMaterialCache();
            return _configuredMaterialByColor.GetValueOrDefault(colorType);
        }

        private void EnsureConfiguredMaterialCache()
        {
            if (!_isConfiguredMaterialCacheDirty)
            {
                return;
            }

            _configuredMaterialByColor.Clear();
            if (materialsByColor == null)
            {
                _isConfiguredMaterialCacheDirty = false;
                return;
            }

            for (var i = 0; i < materialsByColor.Count; i++)
            {
                var entry = materialsByColor[i];
                if (!entry.material || _configuredMaterialByColor.ContainsKey(entry.colorType))
                {
                    continue;
                }

                _configuredMaterialByColor[entry.colorType] = entry.material;
            }

            _isConfiguredMaterialCacheDirty = false;
        }
    }
}
