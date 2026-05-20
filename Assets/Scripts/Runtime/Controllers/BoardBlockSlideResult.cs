using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers
{
    internal readonly struct BoardBlockSlideResult
    {
        public readonly int BlockId;
        public readonly Vector2Int StartPosition;
        public readonly Vector2Int EndPosition;
        public readonly int MovedCellCount;
        public readonly bool ClearedThroughDoor;
        public readonly bool BlockRemovedFromBoard;
        public readonly bool LayerExitedWithRemainingBlock;
        public readonly DoorOpeningData MatchedDoor;

        public BoardBlockSlideResult(int blockId, Vector2Int startPosition, Vector2Int endPosition, int movedCellCount,
            bool clearedThroughDoor, bool blockRemovedFromBoard, bool layerExitedWithRemainingBlock,
            DoorOpeningData matchedDoor)
        {
            BlockId = blockId;
            StartPosition = startPosition;
            EndPosition = endPosition;
            MovedCellCount = movedCellCount;
            ClearedThroughDoor = clearedThroughDoor;
            BlockRemovedFromBoard = blockRemovedFromBoard;
            LayerExitedWithRemainingBlock = layerExitedWithRemainingBlock;
            MatchedDoor = matchedDoor;
        }
    }
}
