using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Data
{
    [CreateAssetMenu(fileName = "LevelCollection", menuName = "ColorBlockJam/LevelCollection")]
    public class LevelCollection : ScriptableObject
    {
        [Header("Json Source")]
        [SerializeField] private List<TextAsset> levelJsonFiles = new();
        [SerializeField] private List<TextAsset> blockShapeJsonFiles = new();

        [System.NonSerialized] private List<LevelJsonData> _runtimeLevels;
        [System.NonSerialized] private BlockShapeRegistry _runtimeShapeRegistry;
        [System.NonSerialized] private bool _isRuntimeCacheReady;

        public BlockShapeRegistry RuntimeShapeRegistry
        {
            get
            {
                EnsureRuntimeCache();
                return _runtimeShapeRegistry;
            }
        }

        public int Count
        {
            get
            {
                EnsureRuntimeCache();
                return _runtimeLevels.Count;
            }
        }

        public bool TryGetLevelAt(int index, out LevelJsonData levelData)
        {
            EnsureRuntimeCache();

            if ((uint)index >= (uint)_runtimeLevels.Count)
            {
                levelData = null;
                return false;
            }

            levelData = _runtimeLevels[index];
            return true;
        }

        private void OnEnable()
        {
            InvalidateRuntimeCache();
        }

        private void OnValidate()
        {
            InvalidateRuntimeCache();
        }

        private void InvalidateRuntimeCache()
        {
            _isRuntimeCacheReady = false;
            _runtimeLevels = null;
            _runtimeShapeRegistry = null;
        }

        private void EnsureRuntimeCache()
        {
            if (_isRuntimeCacheReady && _runtimeLevels != null)
            {
                return;
            }

            _runtimeLevels = new List<LevelJsonData>();
            _isRuntimeCacheReady = true;
            _runtimeShapeRegistry = BlockShapeRegistry.FromJsonAssets(blockShapeJsonFiles);

            if (levelJsonFiles == null || levelJsonFiles.Count == 0)
            {
                return;
            }

            foreach (var levelJson in levelJsonFiles)
            {
                if (!levelJson)
                {
                    continue;
                }

                var levelData = LevelJsonSerialization.Deserialize(levelJson.text, levelJson.name);
                if (levelData == null)
                {
                    continue;
                }

                _runtimeLevels.Add(levelData);
            }
        }
    }
}
