using System.Collections.Generic;
using Runtime.Core;
using Runtime.Data;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private PooledVisual CreateGridCellObject(Transform parent, Vector2Int cell)
        {
            return CreateVisualObject(parent, GetRuntimeName(gridCellNamePrefix, cell.x, cell.y), gridCellPrefab, false);
        }

        private PooledVisual CreateBlockCellObject(Transform parent)
        {
            var prefab = ResolveBlockCellPrefab();
            return CreateVisualObject(parent, GetRuntimeName(blockCellNamePrefix), prefab, true);
        }

        private PooledVisual CreateVisualObject(Transform parent, string objectName, GameObject prefab, bool cacheRenderer)
        {
            var visualParent = parent ? parent : transform;
            GameObject visualObject;
            if (prefab)
            {
                visualObject = Instantiate(prefab, visualParent);
            }
            else
            {
                if (!_loggedPrimitiveFallback)
                {
                    Debug.LogError(
                        "BlockSceneBuilder could not resolve a prefab and is creating a primitive fallback. Reassign missing prefab references.",
                        this);
                    _loggedPrimitiveFallback = true;
                }

                visualObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visualObject.transform.SetParent(visualParent, false);
            }

            RenameIfConfigured(visualObject, objectName);
            visualObject.transform.localRotation = Quaternion.identity;
            DisableCollider(visualObject);

            var renderer = cacheRenderer ? ResolveRenderer(visualObject) : null;
            return new PooledVisual(visualObject, renderer);
        }

        private GameObject ResolveBlockCellPrefab()
        {
            if (visualProfile && visualProfile.defaultBlockPrefab)
            {
                return visualProfile.defaultBlockPrefab;
            }

            if (gridCellPrefab)
            {
                if (!_loggedMissingBlockCellPrefab)
                {
                    Debug.LogWarning(
                        "BlockVisualProfile.defaultBlockPrefab is missing. BlockSceneBuilder is using gridCellPrefab as fallback.",
                        this);
                    _loggedMissingBlockCellPrefab = true;
                }

                return gridCellPrefab;
            }

            return null;
        }

        private static Renderer ResolveRenderer(GameObject target)
        {
            if (!target)
            {
                return null;
            }

            return target.TryGetComponent<Renderer>(out var renderer) ? renderer : target.GetComponentInChildren<Renderer>(true);
        }

        private static void DisableCollider(GameObject target)
        {
            if (!target)
            {
                return;
            }

            if (target.TryGetComponent<Collider>(out var collider))
            {
                collider.enabled = false;
            }
        }

        private static void SetActiveIfChanged(GameObject target, bool isActive)
        {
            if (target && target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
        }

        private static void ApplyWorldTransform(Transform target, Vector3 position, Vector3 scale)
        {
            ApplyWorldTransform(target, position, Quaternion.identity, scale);
        }

        private static void ApplyWorldTransform(Transform target, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!target)
            {
                return;
            }

            if (target.position != position)
            {
                target.position = position;
            }

            if (target.rotation != rotation)
            {
                target.rotation = rotation;
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

        private Material GetDoorMaterial(BlockColor colorType)
        {
            if (_fallbackDoorMaterialByColor.TryGetValue(colorType, out var existingMaterial) && existingMaterial != null)
            {
                return existingMaterial;
            }

            var doorColor = ResolveColor(colorType);
            var material = CreateColorMaterial(doorColor, "MAT_Runtime_Door_" + colorType);
            _fallbackDoorMaterialByColor[colorType] = material;
            return material;
        }

        private Material GetBlockMaterial(BlockColor colorType)
        {
            var configuredMaterial = GetConfiguredMaterial(materialsByColor, colorType);
            if (configuredMaterial != null)
            {
                return configuredMaterial;
            }

            var profileMaterial = visualProfile ? visualProfile.GetMaterial(colorType) : null;
            if (profileMaterial != null)
            {
                return profileMaterial;
            }

            if (_fallbackBlockMaterialByColor.TryGetValue(colorType, out var existingMaterial) && existingMaterial != null)
            {
                return existingMaterial;
            }

            var material = CreateColorMaterial(ResolveColor(colorType), "MAT_Runtime_Block_" + colorType);
            _fallbackBlockMaterialByColor[colorType] = material;
            return material;
        }

        private Color ResolveColor(BlockColor colorType)
        {
            return BlockColorUtility.GetColor(colorType);
        }

        private static Material GetConfiguredMaterial(IReadOnlyList<BlockColorMaterialEntry> entries, BlockColor colorType)
        {
            if (entries == null)
            {
                return null;
            }

            foreach (var entry in entries)
            {
                if (entry.colorType == colorType)
                {
                    return entry.material;
                }
            }

            return null;
        }

        private static Material CreateColorMaterial(Color color, string materialName)
        {
            var shader = Shader.Find("Unlit/Color");
            if (!shader)
            {
                shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(shader)
            {
                name = materialName,
                color = color,
                enableInstancing = true
            };
            return material;
        }

        private string GetRuntimeName(string fixedName)
        {
            return applyRuntimeNames ? fixedName : null;
        }

        private string GetRuntimeName(string prefix, int index)
        {
            if (!applyRuntimeNames)
            {
                return null;
            }

            var resolvedPrefix = string.IsNullOrWhiteSpace(prefix) ? "Pooled" : prefix;
            return resolvedPrefix + "_" + index;
        }

        private string GetRuntimeName(string prefix, int x, int y)
        {
            if (!applyRuntimeNames)
            {
                return null;
            }

            var resolvedPrefix = string.IsNullOrWhiteSpace(prefix) ? "Pooled" : prefix;
            return resolvedPrefix + "_" + x + "_" + y;
        }

        private static void RenameIfConfigured(GameObject target, string nameValue)
        {
            if (!target || string.IsNullOrWhiteSpace(nameValue))
            {
                return;
            }

            if (target.name != nameValue)
            {
                target.name = nameValue;
            }
        }
    }
}
