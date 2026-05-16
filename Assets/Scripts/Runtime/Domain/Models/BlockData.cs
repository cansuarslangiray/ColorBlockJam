using System;
using Runtime.Data;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Domain.Models
{
    [Serializable]
    public struct BlockData
    {
        private static readonly Vector2Int[] DefaultCells = { Vector2Int.zero };

        public Vector2Int position;
        public BlockShapeData shape;
        public BlockMovementConstraint movementConstraint;
        public BlockColor colorType;

        public Vector2Int[] GetLocalCells()
        {
            if (!shape)
            {
                return DefaultCells;
            }

            Vector2Int[] shapeCells = shape.GetLocalCells();
            if (shapeCells == null || shapeCells.Length == 0)
            {
                return DefaultCells;
            }

            return shapeCells;
        }

        public Vector2Int GetSize()
        {
            return !shape ? Vector2Int.one : shape.Size;
        }
    }
}