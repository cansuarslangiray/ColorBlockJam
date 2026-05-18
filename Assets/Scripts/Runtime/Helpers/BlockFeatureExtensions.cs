using Runtime.Domain.Enums;

namespace Runtime.Helpers
{
    public static class BlockFeatureExtensions
    {
        private const BlockFeature KnownFeatures =
            BlockFeature.Horizontal |
            BlockFeature.Vertical;

        public static BlockFeature Sanitize(this BlockFeature features)
        {
            var sanitized = features & KnownFeatures;
            if ((sanitized & BlockFeature.Horizontal) != 0 &&
                (sanitized & BlockFeature.Vertical) != 0)
            {
                sanitized &= ~BlockFeature.Vertical;
            }

            return sanitized;
        }

        public static bool HasFeature(this BlockFeature features, BlockFeature feature)
        {
            return feature != BlockFeature.Default && (features & feature) == feature;
        }

        public static BlockMovementConstraint ResolveMovementConstraint(this BlockFeature features,
            BlockMovementConstraint fallback = BlockMovementConstraint.Default)
        {
            if (features.HasFeature(BlockFeature.Horizontal))
            {
                return BlockMovementConstraint.Horizontal;
            }

            if (features.HasFeature(BlockFeature.Vertical))
            {
                return BlockMovementConstraint.Vertical;
            }

            return fallback;
        }
    }
}
