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

        public LevelJsonData GetLevelAt(int index)
        {
            EnsureRuntimeCache();

            if (index < 0 || index >= _runtimeLevels.Count)
                return null;

            return _runtimeLevels[index];
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

            for (var i = 0; i < levelJsonFiles.Count; i++)
            {
                var levelJson = levelJsonFiles[i];
                if (levelJson == null)
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
