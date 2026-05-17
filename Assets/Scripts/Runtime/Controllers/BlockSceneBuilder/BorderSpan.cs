namespace Runtime.Controllers.BlockSceneBuilder
{
    internal readonly struct BorderSpan
    {
        public readonly float Min;
        public readonly float Max;

        public BorderSpan(float min, float max)
        {
            Min = min;
            Max = max;
        }
    }
}
