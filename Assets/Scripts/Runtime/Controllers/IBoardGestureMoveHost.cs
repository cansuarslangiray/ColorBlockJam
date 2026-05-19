using Runtime.Domain.Enums;

namespace Runtime.Controllers
{
    internal interface IBoardGestureMoveHost
    {
        bool TryMoveGestureBlock(int blockId, Direction direction, int requestedCellCount, out int movedCellCount,
            out bool blockCleared);
    }
}
