using Runtime.Domain.Enums;

namespace Runtime.Helpers
{
    public static class BlockFeatureExtensions
    {
        public static BlockFeature Sanitize(this BlockFeature feature)
        {
            return feature switch
            {
                BlockFeature.Default => BlockFeature.Default,
                BlockFeature.Horizontal => BlockFeature.Horizontal,
                BlockFeature.Vertical => BlockFeature.Vertical,
                BlockFeature.MaxMovesBeforeExit => BlockFeature.MaxMovesBeforeExit,
                BlockFeature.MinClearedBlocksBeforeExit => BlockFeature.MinClearedBlocksBeforeExit,
                _ => BlockFeature.Default
            };
        }

        public static bool HasFeature(this BlockFeature source, BlockFeature feature)
        {
            return source.Sanitize() == feature;
        }

        public static bool IsMovementHorizontal(this BlockFeature feature)
        {
            return feature.Sanitize() == BlockFeature.Horizontal;
        }

        public static bool IsMovementVertical(this BlockFeature feature)
        {
            return feature.Sanitize() == BlockFeature.Vertical;
        }

        public static bool IsDirectionAllowed(this BlockFeature feature, Direction direction)
        {
            var sanitizedFeature = feature.Sanitize();
            if (sanitizedFeature == BlockFeature.Horizontal)
            {
                return direction is Direction.Left or Direction.Right;
            }

            if (sanitizedFeature == BlockFeature.Vertical)
            {
                return direction is Direction.Up or Direction.Down;
            }

            return true;
        }
    }
}
