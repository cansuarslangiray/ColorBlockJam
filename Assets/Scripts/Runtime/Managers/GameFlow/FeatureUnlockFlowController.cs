using System.Collections.Generic;
using Runtime.Data;
using Runtime.Domain.Enums;
using Runtime.Helpers;

namespace Runtime.Managers.GameFlow
{
    internal sealed class FeatureUnlockFlowController
    {
        private const int MaxFeaturesPerPanel = 2;

        private readonly PlayerFeatureProgress _playerFeatureProgress;
        private readonly BlockFeatureDefinitionStore _definitionStore;
        private readonly HashSet<BlockFeature> _seenInLevelBuffer = new();
        private readonly List<BlockFeature> _unseenFeaturesBuffer = new();
        private readonly List<BlockFeatureDefinition> _resolvedDefinitionsBuffer = new();

        public FeatureUnlockFlowController(PlayerFeatureProgress playerFeatureProgress,
            BlockFeatureDefinitionStore definitionStore)
        {
            _playerFeatureProgress = playerFeatureProgress;
            _definitionStore = definitionStore;
        }

        public bool TryPrepareFeatureUnlock(LevelDefinition nextLevelData, out IReadOnlyList<BlockFeatureDefinition> definitions)
        {
            definitions = null;
            if (nextLevelData == null)
            {
                return false;
            }

            if (!CollectUnseenFeatures(nextLevelData))
            {
                return false;
            }

            _resolvedDefinitionsBuffer.Clear();
            for (var i = 0; i < _unseenFeaturesBuffer.Count; i++)
            {
                var featureType = _unseenFeaturesBuffer[i];
                if (!_definitionStore.TryGetDefinition(featureType, out var resolvedDefinition) || resolvedDefinition == null)
                {
                    continue;
                }

                _resolvedDefinitionsBuffer.Add(resolvedDefinition);
            }

            if (_resolvedDefinitionsBuffer.Count <= 0)
            {
                return false;
            }

            for (var i = 0; i < _resolvedDefinitionsBuffer.Count; i++)
            {
                _playerFeatureProgress.TryMarkFeatureSeen(_resolvedDefinitionsBuffer[i].featureType);
            }

            definitions = new List<BlockFeatureDefinition>(_resolvedDefinitionsBuffer);
            return true;
        }

        private bool CollectUnseenFeatures(LevelDefinition nextLevelData)
        {
            _seenInLevelBuffer.Clear();
            _unseenFeaturesBuffer.Clear();

            var blocks = nextLevelData.blocks;
            if (blocks == null || blocks.Count <= 0)
            {
                return false;
            }

            for (var i = 0; i < blocks.Count; i++)
            {
                var sanitizedFeature = blocks[i].blockFeatures.Sanitize();
                if (sanitizedFeature == BlockFeature.Default)
                {
                    continue;
                }

                if (!_seenInLevelBuffer.Add(sanitizedFeature))
                {
                    continue;
                }

                if (_playerFeatureProgress.HasSeenFeature(sanitizedFeature))
                {
                    continue;
                }

                _unseenFeaturesBuffer.Add(sanitizedFeature);
                if (_unseenFeaturesBuffer.Count >= MaxFeaturesPerPanel)
                {
                    break;
                }
            }

            return _unseenFeaturesBuffer.Count > 0;
        }
    }
}
