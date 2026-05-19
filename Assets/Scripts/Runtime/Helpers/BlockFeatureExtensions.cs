using Runtime.Domain.Enums;

namespace Runtime.Helpers
{
    public static class BlockFeatureExtensions
    {
        private const int LegacyHorizontalFlag = 1;
        private const int LegacyVerticalFlag = 2;
        private const int LegacyMaxMovesBeforeExitFlag = 8;
        private const int LegacyMinClearedBlocksBeforeExitFlag = 16;

        public static BlockFeature Sanitize(this BlockFeature feature)
        {
            return feature switch
            {
                BlockFeature.Default => BlockFeature.Default,
                BlockFeature.Horizontal => BlockFeature.Horizontal,
                BlockFeature.Vertical => BlockFeature.Vertical,
                BlockFeature.MaxMovesBeforeExit => BlockFeature.MaxMovesBeforeExit,
                BlockFeature.MinClearedBlocksBeforeExit => BlockFeature.MinClearedBlocksBeforeExit,
                _ => ResolveLegacyFeature((int)feature)
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

        private static BlockFeature ResolveLegacyFeature(int rawValue)
        {
            if ((rawValue & LegacyMaxMovesBeforeExitFlag) != 0)
            {
                return BlockFeature.MaxMovesBeforeExit;
            }

            if ((rawValue & LegacyMinClearedBlocksBeforeExitFlag) != 0)
            {
                return BlockFeature.MinClearedBlocksBeforeExit;
            }

            if ((rawValue & LegacyHorizontalFlag) != 0)
            {
                return BlockFeature.Horizontal;
            }

            if ((rawValue & LegacyVerticalFlag) != 0)
            {
                return BlockFeature.Vertical;
            }

            return BlockFeature.Default;
        }
    }
}
