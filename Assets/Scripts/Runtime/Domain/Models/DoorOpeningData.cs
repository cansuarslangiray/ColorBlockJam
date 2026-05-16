using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Domain.Models
{
    public struct DoorOpeningData
    {
        public BlockColor ColorType;
        public Vector2Int MinCell;
        public Vector2Int MaxCell;
        public Direction EdgeDirection;

        private Vector2Int Size => new ((MaxCell.x - MinCell.x) + 1, (MaxCell.y - MinCell.y) + 1);

        public int OpeningWidth
        {
            get
            {
                var size = Size;
                return EdgeDirection switch
                {
                    Direction.Left or Direction.Right => size.y,
                    Direction.Down or Direction.Up => size.x,
                    _ => Mathf.Max(size.x, size.y)
                };
            }
        }
    }
}
