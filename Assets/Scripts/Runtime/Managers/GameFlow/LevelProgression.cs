using Runtime.Data;
using UnityEngine;

namespace Runtime.Managers.GameFlow
{
    internal sealed class LevelProgression
    {
        private readonly LevelCollection _levelCollection;
        private int _currentLevelIndex;

        public LevelProgression(LevelCollection levelCollection)
        {
            _levelCollection = levelCollection;
            _currentLevelIndex = 0;
        }

        public int CurrentLevelDisplayNumber
        {
            get
            {
                if (TryGetCurrentLevelData(out var levelData) && levelData != null)
                {
                    return Mathf.Max(1, levelData.levelNumber);
                }

                return _currentLevelIndex + 1;
            }
        }
        public BlockShapeCatalog RuntimeShapeCatalog => _levelCollection != null ? _levelCollection.RuntimeShapeCatalog : null;

        public void SetCurrentLevelFromSavedNumber(int savedLevelNumber)
        {
            if (_levelCollection == null || _levelCollection.Count <= 0)
            {
                SetCurrentLevelIndex(0);
                return;
            }

            var fallbackLevelIndex = Mathf.Clamp(savedLevelNumber - 1, 0, _levelCollection.Count - 1);
            var resolvedLevelIndex = fallbackLevelIndex;

            if (savedLevelNumber > 0)
            {
                for (var i = 0; i < _levelCollection.Count; i++)
                {
                    if (!_levelCollection.TryGetLevelAt(i, out var levelData) || levelData == null)
                    {
                        continue;
                    }

                    if (levelData.levelNumber == savedLevelNumber)
                    {
                        resolvedLevelIndex = i;
                        break;
                    }
                }
            }

            SetCurrentLevelIndex(resolvedLevelIndex);
        }

        public bool TryMoveNextLevel()
        {
            if (_levelCollection == null || _currentLevelIndex + 1 >= _levelCollection.Count)
            {
                return false;
            }

            var currentLevelNumber = CurrentLevelDisplayNumber;
            for (var i = _currentLevelIndex + 1; i < _levelCollection.Count; i++)
            {
                if (!_levelCollection.TryGetLevelAt(i, out var candidateLevelData) || candidateLevelData == null)
                {
                    continue;
                }

                if (Mathf.Max(1, candidateLevelData.levelNumber) <= currentLevelNumber)
                {
                    continue;
                }

                SetCurrentLevelIndex(i);
                return true;
            }

            return false;
        }

        public bool TryGetNextLevelData(out LevelDefinition levelData)
        {
            levelData = null;
            if (_levelCollection == null || _currentLevelIndex + 1 >= _levelCollection.Count)
            {
                return false;
            }

            var currentLevelNumber = CurrentLevelDisplayNumber;
            for (var i = _currentLevelIndex + 1; i < _levelCollection.Count; i++)
            {
                if (!_levelCollection.TryGetLevelAt(i, out var candidateLevelData) || candidateLevelData == null)
                {
                    continue;
                }

                if (Mathf.Max(1, candidateLevelData.levelNumber) <= currentLevelNumber)
                {
                    continue;
                }

                levelData = candidateLevelData;
                return true;
            }

            return false;
        }

        public bool TryGetCurrentLevelData(out LevelDefinition levelData)
        {
            if (_levelCollection != null && _levelCollection.TryGetLevelAt(_currentLevelIndex, out var resolvedLevelData))
            {
                levelData = resolvedLevelData;
                return true;
            }

            levelData = null;
            return false;
        }

        private void SetCurrentLevelIndex(int levelIndex)
        {
            _currentLevelIndex = levelIndex;
        }
    }
}
