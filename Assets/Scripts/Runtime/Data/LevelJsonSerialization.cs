using System;
using UnityEngine;

namespace Runtime.Data
{
    public static class LevelJsonSerialization
    {
        public static string Serialize(LevelJsonData levelData, bool prettyPrint = true)
        {
            var sanitized = levelData ?? new LevelJsonData();
            sanitized.Sanitize();
            return JsonUtility.ToJson(sanitized, prettyPrint);
        }

        public static LevelJsonData Deserialize(string json, string levelName = null)
        {
            LevelJsonData model = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    model = JsonUtility.FromJson<LevelJsonData>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to parse level json '{levelName}': {ex.Message}");
                }
            }

            model ??= new LevelJsonData();
            if (string.IsNullOrWhiteSpace(model.levelKey) && !string.IsNullOrWhiteSpace(levelName))
            {
                model.levelKey = levelName.Trim();
            }

            model.Sanitize();
            return model;
        }
    }
}
