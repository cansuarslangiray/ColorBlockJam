using System;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Domain.Models
{
    [Serializable]
    public struct DoorData
    {
        [Tooltip("Kapının ızgara hücresi")] public Vector2Int position;

        [Tooltip("Kapının kabul ettiği blok rengi")]
        public BlockColor colorType;

        [Min(1)]
        [Tooltip("Kapının kenar boyunca uzunluğu (kaç hücre)")]
        public int openingWidth;
    }
}
