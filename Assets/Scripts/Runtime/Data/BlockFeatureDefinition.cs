using Runtime.Domain.Enums;
using Runtime.Helpers;
using UnityEngine;
using UnityEngine.Localization;

namespace Runtime.Data
{
    [CreateAssetMenu(fileName = "BlockFeatureDefinition", menuName = "ColorBlockJam/Block Feature Definition")]
    public sealed class BlockFeatureDefinition : ScriptableObject
    {
        public BlockFeature featureType = BlockFeature.Default;
        [SerializeField] private LocalizedString displayName = new();
        [SerializeField] private LocalizedString description = new();
        public Sprite icon;

        public bool IsValid => featureType != BlockFeature.Default;

        public void Sanitize()
        {
            featureType = featureType.Sanitize();
            displayName ??= new LocalizedString();
            description ??= new LocalizedString();
        }

        public string ResolveLocalizedDisplayName()
        {
            return ResolveLocalizedText(displayName);
        }

        public string ResolveLocalizedDescription()
        {
            return ResolveLocalizedText(description);
        }

        private static string ResolveLocalizedText(LocalizedString localizedString)
        {
            if (localizedString == null || localizedString.IsEmpty)
            {
                return string.Empty;
            }

            var localized = localizedString.GetLocalizedString();
            return string.IsNullOrEmpty(localized) ? string.Empty : localized;
        }
    }
}
