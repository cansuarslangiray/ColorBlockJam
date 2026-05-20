using UnityEngine;

namespace Runtime.Controllers
{
    internal readonly struct CollisionProfile
    {
        public readonly Vector2Int[] Cells;
        public readonly int MinX;
        public readonly int MaxX;
        public readonly int MinY;
        public readonly int MaxY;

        public CollisionProfile(Vector2Int[] cells, int minX, int maxX, int minY, int maxY)
        {
            Cells = cells;
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        public bool IsValid => Cells != null && Cells.Length > 0;
    }
}
