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

            return shapeKey.Trim() switch
            {
                "Shape_1x1" => BlockShapeType.Shape1x1,
                "Shape_1x2" => BlockShapeType.Shape1x2,
                "Shape_2x1" => BlockShapeType.Shape2x1,
                "Shape_1x3" => BlockShapeType.Shape1x3,
                "Shape_3x1" => BlockShapeType.Shape3x1,
                "Shape_2x2" => BlockShapeType.Shape2x2,
                "Shape_L_3x2" => BlockShapeType.ShapeL3x2,
                "Shape_LO_3x2" => BlockShapeType.ShapeLO3x2,
                _ => BlockShapeType.Custom
            };
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
    }
}
