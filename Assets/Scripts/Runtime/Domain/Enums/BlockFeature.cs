using System;

namespace Runtime.Domain.Enums
{
    [Flags]
    public enum BlockFeature
    {
        Default = 0,
        Horizontal = 1 << 0,
        Vertical = 1 << 1,
        MinMovesBeforeExit = 1 << 2,
        MaxMovesBeforeExit = 1 << 3,
        MinClearedBlocksBeforeExit = 1 << 4
    }
}