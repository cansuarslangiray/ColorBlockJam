using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Runtime.Data
{
    [CreateAssetMenu(fileName = "LevelCollection", menuName = "ColorBlockJam/LevelCollection")]
    public sealed class LevelCollection : ScriptableObject
    {
        [Header("Runtime Source")]
        [SerializeField] private BlockShapeCatalog blockShapeCatalog;
        [SerializeField] private List<LevelDefinition> levels = new();

        public BlockShapeCatalog RuntimeShapeCatalog => blockShapeCatalog;
        public int Count => levels?.Count ?? 0;

        public bool TryGetLevelAt(int index, out LevelDefinition levelData)
        {
            levelData = null;
            if (levels == null || index < 0 || index >= levels.Count)
            {
                return false;
            }

            levelData = levels[index];
            return levelData != null;
        }

        public void SetRuntimeSources(BlockShapeCatalog catalog, List<LevelDefinition> nextLevels)
        {
            blockShapeCatalog = catalog;
            levels = nextLevels ?? new List<LevelDefinition>();
            SortLevelsByNumber();
        }

        private void OnValidate()
        {
            SortLevelsByNumber();
        }

        private void SortLevelsByNumber()
        {
            if (levels == null)
            {
                return;
            }

            levels.RemoveAll(level => level == null);
            if (levels.Count <= 1)
            {
                return;
            }

            levels.Sort((left, right) =>
            {
                var levelCompare = left.levelNumber.CompareTo(right.levelNumber);
                return levelCompare != 0
                    ? levelCompare
                    : string.CompareOrdinal(left.levelKey, right.levelKey);
            });
        }

#if UNITY_EDITOR
        [ContextMenu("Sort Levels")]
        private void SortLevelsFromContextMenu()
        {
            SortLevelsByNumber();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
#endif
    }
}
