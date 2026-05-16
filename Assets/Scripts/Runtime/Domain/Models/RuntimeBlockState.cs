using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Domain.Models
{
    public struct RuntimeBlockState
    {
        public readonly int Id;
        public Vector2Int Position;
        public readonly Vector2Int[] LocalCells;
        public readonly BlockMovementConstraint MovementConstraint;
        public readonly BlockColor ColorType;

        public RuntimeBlockState(
            int id,
            Vector2Int position,
            Vector2Int[] localCells,
            BlockMovementConstraint movementConstraint,
            BlockColor colorType)
        {
            Id = id;
            Position = position;
            LocalCells = localCells;
            MovementConstraint = movementConstraint;
            ColorType = colorType;
        }
    }
}
