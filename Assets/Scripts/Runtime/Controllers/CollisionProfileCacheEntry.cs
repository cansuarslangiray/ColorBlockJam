using UnityEngine;

namespace Runtime.Controllers
{
    internal struct CollisionProfileCacheEntry
    {
        public Vector2Int[] LocalCells;
        public CollisionProfile Profile;
    }
}
