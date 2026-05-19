using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder.Board
{
    public readonly struct LayoutMetrics
    {
        public LayoutMetrics(Vector2 boardOrigin, float cellSize, float gridZ, float blockZ, float frameThickness,
            float frameDepth, float borderZ, float doorDepth, float doorZ)
        {
            BoardOrigin = boardOrigin;
            CellSize = cellSize;
            GridZ = gridZ;
            BlockZ = blockZ;
            FrameThickness = frameThickness;
            FrameDepth = frameDepth;
            BorderZ = borderZ;
            DoorDepth = doorDepth;
            DoorZ = doorZ;
        }

        public Vector2 BoardOrigin { get; }
        public float CellSize { get; }
        public float GridZ { get; }
        public float BlockZ { get; }
        public float FrameThickness { get; }
        public float FrameDepth { get; }
        public float BorderZ { get; }
        public float DoorDepth { get; }
        public float DoorZ { get; }
    }
}