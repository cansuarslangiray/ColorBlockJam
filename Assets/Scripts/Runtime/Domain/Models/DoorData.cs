using System;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Domain.Models
{
    [Serializable]
    public struct DoorData
    {
        public Vector2Int position;
        public BlockColor colorType;
    }
}
