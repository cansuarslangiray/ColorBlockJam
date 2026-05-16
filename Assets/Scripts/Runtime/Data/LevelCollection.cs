using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Data
{
    [CreateAssetMenu(fileName = "LevelCollection", menuName = "ColorBlockJam/LevelCollection")]
    public class LevelCollection : ScriptableObject
    {
        public List<LevelData> levels = new ();

        public int Count => levels?.Count ?? 0;

        public LevelData GetLevelAt(int index)
        {
            if (levels == null || index < 0 || index >= levels.Count)
                return null;

            return levels[index];
        }
    }
}
