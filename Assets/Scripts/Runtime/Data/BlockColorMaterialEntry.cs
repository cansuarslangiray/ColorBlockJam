using System;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Data
{
    [Serializable]
    public struct BlockColorMaterialEntry
    {
        public BlockColor colorType;
        public Material material;
    }
}