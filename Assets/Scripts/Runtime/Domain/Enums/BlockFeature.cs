namespace Runtime.Domain.Enums
{
    public enum BlockFeature
    {
        Default = 0,
        Horizontal = 1,
        Vertical = 2,
        MaxMovesBeforeExit = 8,
        MinClearedBlocksBeforeExit = 16,
        NestedShape = 32
    }
}
