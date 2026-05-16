using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Domain.Models
{
    public struct DoorOpeningData
    {
        public BlockColor colorType;
        public Vector2Int minCell;
        public Vector2Int maxCell;
        public EdgeSide edgeSide;

        public Vector2Int Size => new Vector2Int((maxCell.x - minCell.x) + 1, (maxCell.y - minCell.y) + 1);

        public int OpeningWidth
        {
            get
            {
                var size = Size;
                return edgeSide switch
                {
                    EdgeSide.Left or EdgeSide.Right => size.y,
                    EdgeSide.Bottom or EdgeSide.Top => size.x,
                    _ => Mathf.Max(size.x, size.y)
                };
            }
        }
    }
}
