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

        private static void ApplyLocalTransform(Transform target, Vector3 localPosition, Vector3 localScale)
        {
            if (!target)
            {
                return;
            }

            target.localPosition = localPosition;
            target.localRotation = Quaternion.identity;
            target.localScale = localScale;
        }

        private Material GetMaterial(BlockColor colorType)
        {
            return _visualCache.ResolveMaterial(colorType, materialsByColor, this);
        }
    }
}
