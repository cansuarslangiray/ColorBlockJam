using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Domain.Models
{
    public struct DoorOpeningData
    {
        public BlockColor colorType;
        public Vector2Int minCell;
        public Vector2Int maxCell;
        public int cellCount;
        public int edgeSide;

        public Vector2Int Size => new Vector2Int((maxCell.x - minCell.x) + 1, (maxCell.y - minCell.y) + 1);

        public int OpeningWidth
        {
            get
            {
                var size = Size;
                return edgeSide switch
                {
                    0 or 1 => size.y,
                    2 or 3 => size.x,
                    _ => Mathf.Max(size.x, size.y)
                };
            }
        }
    }
}