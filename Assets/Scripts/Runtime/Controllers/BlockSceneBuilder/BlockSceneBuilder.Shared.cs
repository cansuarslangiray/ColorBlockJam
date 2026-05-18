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

            if (target.position != position)
            {
                target.position = position;
            }

            if (target.rotation != Quaternion.identity)
            {
                target.rotation = Quaternion.identity;
            }

            if (target.localScale != scale)
            {
                target.localScale = scale;
            }
        }

        private static void ApplyLocalTransform(Transform target, Vector3 localPosition, Vector3 localScale)
        {
            if (!target)
            {
                return;
            }

            if (target.localPosition != localPosition)
            {
                target.localPosition = localPosition;
            }

            if (target.localRotation != Quaternion.identity)
            {
                target.localRotation = Quaternion.identity;
            }

            if (target.localScale != localScale)
            {
                target.localScale = localScale;
            }
        }

        private void ApplySharedMaterial(GameObject target, Material material)
        {
            _visualCache.ApplySharedMaterial(target, material);
        }

        private Material GetMaterial(BlockColor colorType)
        {
            return _visualCache.ResolveMaterial(colorType, materialsByColor);
        }
    }
}
