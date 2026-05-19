using System.Collections.Generic;
using Runtime.Domain.Enums;
using Runtime.Helpers;
using UnityEngine;

namespace Runtime.Data
{
    public sealed class BlockFeatureDefinitionStore
    {
        private const string ResourceFolderPath = "BlockFeatures";

        private readonly Dictionary<BlockFeature, BlockFeatureDefinition> _definitionsByType = new();

        public BlockFeatureDefinitionStore()
        {
            LoadDefinitions();
        }

        public bool TryGetDefinition(BlockFeature featureType, out BlockFeatureDefinition definition)
        {
            var sanitizedFeature = featureType.Sanitize();
            return _definitionsByType.TryGetValue(sanitizedFeature, out definition);
        }

        private void LoadDefinitions()
        {
            _definitionsByType.Clear();

            var definitions = Resources.LoadAll<BlockFeatureDefinition>(ResourceFolderPath);
            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definition == null)
                {
                    continue;
                }

                definition.Sanitize();
                if (!definition.IsValid)
                {
                    continue;
                }

                _definitionsByType[definition.featureType] = definition;
            }
        }
    }
}
