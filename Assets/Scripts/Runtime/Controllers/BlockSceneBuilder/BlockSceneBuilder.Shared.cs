using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private static void SetActiveIfChanged(GameObject target, bool isActive)
        {
            if (target && target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
        }

        private static void ApplyWorldTransform(Transform target, Vector3 position, Vector3 scale)
        {
            if (!target)
            {
                return;
            }

            target.SetPositionAndRotation(position, Quaternion.identity);
            target.localScale = scale;
        }

        private static void ApplyWorldPosition(Transform target, Vector3 position)
        {
            target.SetPositionAndRotation(position, Quaternion.identity);
        }

        private Material GetMaterial(BlockColor colorType)
        {
            var materialCount = materialsByColor.Count;
            for (var i = 0; i < materialCount; i++)
            {
                var entry = materialsByColor[i];
                if (entry.colorType == colorType && entry.material)
                {
                    return entry.material;
                }
            }

            return null;
        }
    }
}
