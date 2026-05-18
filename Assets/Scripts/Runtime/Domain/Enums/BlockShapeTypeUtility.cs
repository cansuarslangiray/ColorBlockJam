namespace Runtime.Domain.Enums
{
    public static class BlockShapeTypeUtility
    {
        public static BlockShapeType FromShapeKey(string shapeKey, int fallbackCellCount = 1)
        {
            if (string.IsNullOrWhiteSpace(shapeKey))
            {
                return ResolveFallbackType(fallbackCellCount);
            }

            string normalizedShapeKey = shapeKey.Trim();
            if (TryResolveLegacyShapeType(normalizedShapeKey, out BlockShapeType legacyShapeType))
            {
                return legacyShapeType;
            }

            if (TryResolveRectangleShapeType(normalizedShapeKey, out BlockShapeType rectangleShapeType))
            {
                return rectangleShapeType;
            }

            return BlockShapeType.Custom;
        }

        public static string ToShapeKey(BlockShapeType blockType)
        {
            return blockType switch
            {
                BlockShapeType.Shape1x1 => "Shape_1x1",
                BlockShapeType.Shape1x2 => "Shape_1x2",
                BlockShapeType.Shape2x1 => "Shape_2x1",
                BlockShapeType.Shape1x3 => "Shape_1x3",
                BlockShapeType.Shape3x1 => "Shape_3x1",
                BlockShapeType.Shape2x2 => "Shape_2x2",
                BlockShapeType.ShapeL3x2 => "Shape_L_3x2",
                BlockShapeType.ShapeLO3x2 => "Shape_LO_3x2",
                _ => string.Empty
            };
        }

        private static BlockShapeType ResolveFallbackType(int fallbackCellCount)
        {
            return fallbackCellCount <= 1 ? BlockShapeType.Shape1x1 : BlockShapeType.Custom;
        }

        private static bool TryResolveLegacyShapeType(string shapeKey, out BlockShapeType blockShapeType)
        {
            switch (shapeKey)
            {
                case "Shape_L_3x2":
                    blockShapeType = BlockShapeType.ShapeL3x2;
                    return true;
                case "Shape_LO_3x2":
                    blockShapeType = BlockShapeType.ShapeLO3x2;
                    return true;
                default:
                    blockShapeType = BlockShapeType.Unknown;
                    return false;
            }
        }

        private static bool TryResolveRectangleShapeType(string shapeKey, out BlockShapeType blockShapeType)
        {
            if (!TryExtractDimensions(shapeKey, out int width, out int height))
            {
                blockShapeType = BlockShapeType.Unknown;
                return false;
            }

            blockShapeType = ResolveRectangleShapeType(width, height);
            return true;
        }

        private static bool TryExtractDimensions(string shapeKey, out int width, out int height)
        {
            const string prefix = "Shape_";
            width = 0;
            height = 0;

            if (string.IsNullOrWhiteSpace(shapeKey) ||
                !shapeKey.StartsWith(prefix, System.StringComparison.Ordinal) ||
                shapeKey.Length <= prefix.Length)
            {
                return false;
            }

            int separatorIndex = shapeKey.IndexOf('x', prefix.Length);
            if (separatorIndex <= prefix.Length || separatorIndex >= shapeKey.Length - 1)
            {
                return false;
            }

            string widthToken = shapeKey.Substring(prefix.Length, separatorIndex - prefix.Length);
            string heightToken = shapeKey.Substring(separatorIndex + 1);
            if (!int.TryParse(widthToken, out width) || !int.TryParse(heightToken, out height))
            {
                return false;
            }

            if (width <= 0 || height <= 0)
            {
                return false;
            }

            return true;
        }

        private static BlockShapeType ResolveRectangleShapeType(int width, int height)
        {
            if (width == 1 && height == 1) return BlockShapeType.Shape1x1;
            if (width == 1 && height == 2) return BlockShapeType.Shape1x2;
            if (width == 2 && height == 1) return BlockShapeType.Shape2x1;
            if (width == 1 && height == 3) return BlockShapeType.Shape1x3;
            if (width == 3 && height == 1) return BlockShapeType.Shape3x1;
            if (width == 2 && height == 2) return BlockShapeType.Shape2x2;
            return BlockShapeType.Custom;
        }
    }
}
