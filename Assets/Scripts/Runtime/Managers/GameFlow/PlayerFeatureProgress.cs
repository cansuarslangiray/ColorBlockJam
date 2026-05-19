using Runtime.Domain.Enums;
using Runtime.Helpers;

namespace Runtime.Managers.GameFlow
{
    internal sealed class PlayerFeatureProgress
    {
        private readonly LocalDataManager _localDataManager;

        public PlayerFeatureProgress(LocalDataManager localDataManager)
        {
            _localDataManager = localDataManager;
        }

        public bool HasSeenFeature(BlockFeature feature)
        {
            var sanitized = feature.Sanitize();
            if (sanitized == BlockFeature.Default)
            {
                return true;
            }

            var playerData = _localDataManager.GetPlayerData();
            return playerData.HasSeenFeature(sanitized);
        }

        public bool TryMarkFeatureSeen(BlockFeature feature)
        {
            var sanitized = feature.Sanitize();
            if (sanitized == BlockFeature.Default)
            {
                return false;
            }

            var changed = false;
            _localDataManager.UpdatePlayerData(data =>
            {
                changed = data.TryMarkFeatureSeen(sanitized);
            });

            return changed;
        }
    }
}
