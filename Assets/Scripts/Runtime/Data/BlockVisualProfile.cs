using System;
using System.Collections.Generic;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Data
{
    [CreateAssetMenu(fileName = "BlockVisualProfile", menuName = "ColorBlockJam/Block Visual Profile")]
    public class BlockVisualProfile : ScriptableObject
    {
        public GameObject defaultBlockPrefab;
        public List<BlockColorMaterialEntry> materialsByColor = new();
        [NonSerialized] private Dictionary<BlockColor, Material> _materialCache = new();
        [NonSerialized] private bool _isCacheDirty = true;

        public Material GetMaterial(BlockColor color)
        {
            EnsureCache();
            return _materialCache.GetValueOrDefault(color);
        }

        private void OnEnable()
        {
            _isCacheDirty = true;
        }

        private void OnValidate()
        {
            _isCacheDirty = true;
        }

        private void EnsureCache()
        {
            _materialCache ??= new Dictionary<BlockColor, Material>();
            if (!_isCacheDirty)
            {
                return;
            }

            _materialCache.Clear();
            foreach (var entry in materialsByColor)
            {
                if (!entry.material)
                    continue;
                
                _materialCache[entry.colorType] = entry.material;
            }

            _isCacheDirty = false;
        }
    }
}