using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Blocks
{
    public readonly struct DragHighlightSettings
    {
        public DragHighlightSettings(float cellSize, float baseOffsetInCells, float gapInCells,
            float verticalOffsetInCells, float thicknessInCells, Color defaultOutlineColor, Material sourceMaterial)
        {
            CellSize = cellSize;
            BaseOffsetInCells = baseOffsetInCells;
            GapInCells = gapInCells;
            VerticalOffsetInCells = verticalOffsetInCells;
            ThicknessInCells = thicknessInCells;
            DefaultOutlineColor = defaultOutlineColor;
            SourceMaterial = sourceMaterial;
        }

        public float CellSize { get; }
        public float BaseOffsetInCells { get; }
        public float GapInCells { get; }
        public float VerticalOffsetInCells { get; }
        public float ThicknessInCells { get; }
        public Color DefaultOutlineColor { get; }
        public Material SourceMaterial { get; }
    }
}
