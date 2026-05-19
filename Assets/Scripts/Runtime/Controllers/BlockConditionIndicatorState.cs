namespace Runtime.Controllers
{
    public readonly struct BlockConditionIndicatorState
    {
        public readonly bool IsVisible;
        public readonly string Text;

        public BlockConditionIndicatorState(bool isVisible, string text)
        {
            IsVisible = isVisible;
            Text = text;
        }
    }
}
