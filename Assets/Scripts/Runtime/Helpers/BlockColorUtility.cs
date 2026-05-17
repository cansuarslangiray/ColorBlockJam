using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Helpers
{
    public static class BlockColorUtility
    {
        public static Color GetColor(BlockColor color)
        {
            return color switch
            {
                BlockColor.Red => new Color(0.9f, 0.25f, 0.25f),
                BlockColor.Blue => new Color(0.2f, 0.45f, 0.95f),
                BlockColor.Green => new Color(0.2f, 0.78f, 0.35f),
                BlockColor.Yellow => new Color(0.95f, 0.82f, 0.2f),
                BlockColor.Purple => new Color(0.62f, 0.32f, 0.88f),
                _ => Color.white
            };
        }
    }
}