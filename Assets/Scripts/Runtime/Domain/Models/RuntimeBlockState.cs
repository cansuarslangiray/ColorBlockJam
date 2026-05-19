using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Domain.Models
{
    public struct RuntimeBlockState
    {
        public readonly int Id;
        public Vector2Int Position;
        public readonly Vector2Int[] LocalCells;
        public readonly BlockFeature BlockFeatures;
        public readonly BlockColor ColorType;
        public readonly int MaxMovesBeforeExit;
        public readonly int MinClearedBlocksBeforeExit;

        public RuntimeBlockState(int id, Vector2Int position, Vector2Int[] localCells,
            BlockFeature blockFeatures, BlockColor colorType, int maxMovesBeforeExit, int minClearedBlocksBeforeExit)
        {
            Id = id;
            Position = position;
            LocalCells = localCells;
            BlockFeatures = blockFeatures;
            ColorType = colorType;
            MaxMovesBeforeExit = maxMovesBeforeExit;
            MinClearedBlocksBeforeExit = minClearedBlocksBeforeExit;
        }
    }
}
