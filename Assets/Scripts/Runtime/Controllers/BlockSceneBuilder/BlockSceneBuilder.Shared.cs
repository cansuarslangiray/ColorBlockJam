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

        private Material GetMaterial(BlockColor colorType)
        {
            var materialCount = materialsByColor?.Count ?? 0;
            for (var i = 0; i < materialCount; i++)
            {
                var entry = materialsByColor[i];
                if (entry.colorType == colorType && entry.material)
                {
                    return entry.material;
                }
            }

            if (_missingMaterialWarnings.Add(colorType))
            {
                Debug.LogWarning(
                    $"BlockSceneBuilder missing material mapping for {colorType}. Assign this color in materialsByColor to keep visuals configured from Unity.",
                    this);
            }

            return null;
        }
    }
}
