using Runtime.Data;

namespace Runtime.Managers.GameFlow
{
    internal sealed class LevelProgression
    {
        private readonly LevelCollection _levelCollection;
        private int _currentLevelIndex;
        private int _cachedLevelIndex = -1;
        private bool _isCurrentLevelCacheResolved;
        private LevelJsonData _cachedCurrentLevelData;

        public LevelProgression(LevelCollection levelCollection)
        {
            _levelCollection = levelCollection;
            ResetToFirstLevel();
        }

        public int CurrentLevelDisplayNumber => _currentLevelIndex + 1;

        public BlockShapeRegistry RuntimeShapeRegistry => _levelCollection.RuntimeShapeRegistry;

        public void ResetToFirstLevel() => SetCurrentLevelIndex(0);

        public bool TryMoveNextLevel()
        {
            if (_levelCollection == null || _currentLevelIndex + 1 >= _levelCollection.Count)
            {
                return false;
            }

            SetCurrentLevelIndex(_currentLevelIndex + 1);
            return true;
        }

        public bool TryGetCurrentLevelData(out LevelJsonData levelData)
        {
            if (_isCurrentLevelCacheResolved && _cachedLevelIndex == _currentLevelIndex)
            {
                levelData = _cachedCurrentLevelData;
                return levelData != null;
            }

            _isCurrentLevelCacheResolved = true;
            _cachedLevelIndex = _currentLevelIndex;

            if (_levelCollection != null && _levelCollection.TryGetLevelAt(_currentLevelIndex, out var resolvedLevelData))
            {
                _cachedCurrentLevelData = resolvedLevelData;
                levelData = resolvedLevelData;
                return true;
            }

            _cachedCurrentLevelData = null;
            levelData = null;
            return false;
        }

        private void SetCurrentLevelIndex(int levelIndex)
        {
            _currentLevelIndex = levelIndex;
            _isCurrentLevelCacheResolved = false;
            _cachedCurrentLevelData = null;
            _cachedLevelIndex = -1;
        }
    }
}
