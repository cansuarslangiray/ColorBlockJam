using System;
using UnityEngine;

namespace Runtime.Persistence
{
    public static class PlayerDataSerialization
    {
        public static string Serialize(PlayerData playerData, bool prettyPrint = true)
        {
            var sanitized = (playerData ?? PlayerData.CreateDefault()).Clone();
            sanitized.Sanitize();
            return JsonUtility.ToJson(sanitized, prettyPrint);
        }

        public static PlayerData Deserialize(string json)
        {
            PlayerData model = null;

            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    model = JsonUtility.FromJson<PlayerData>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to parse player data json: {ex.Message}");
                }
            }

            model ??= PlayerData.CreateDefault();
            model.Sanitize();
            return model;
        }
    }
}
