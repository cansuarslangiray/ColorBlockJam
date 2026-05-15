using System.Collections.Generic;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Data
{
    [CreateAssetMenu(fileName = "BlockVisualProfile", menuName = "ColorBlockJam/Block Visual Profile")]
    public class BlockVisualProfile : ScriptableObject
    {
        public GameObject defaultBlockPrefab;
        public List<BlockColorMaterialEntry> materialsByColor = new();

        public Material GetMaterial(BlockColor color)
        {
            for (var i = 0; i < materialsByColor.Count; i++)
            {
                if (materialsByColor[i].colorType == color)
                {
                    return materialsByColor[i].material;
                }
            }

            return null;
        }
    }
}