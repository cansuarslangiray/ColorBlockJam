using System;
using System.Collections.Generic;
using Runtime.Data;
using Runtime.Domain.Enums;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private const string BlockStudRootName = "__BlockStuds";

        private PooledVisual CreateGridCellObject(Transform parent, Vector2Int cell)
        {
            return CreateVisualObject(parent, GetRuntimeName(gridCellNamePrefix, cell.x, cell.y), gridCellPrefab, false);
        }

        private PooledVisual CreateBlockCellObject(Transform parent)
        {
            var prefab = ResolveBlockCellPrefab();
            return CreateVisualObject(parent, GetRuntimeName(blockCellNamePrefix), prefab, true, true);
        }

        private PooledVisual CreateVisualObject(Transform parent, string objectName, GameObject prefab, bool cacheRenderer,
            bool decorateBlockCell = false)
        {
            if (!prefab)
            {
                return new PooledVisual(null, (Renderer[])null);
            }

            var visualParent = parent ? parent : transform;
            var visualObject = Instantiate(prefab, visualParent);

            RenameIfConfigured(visualObject, objectName);
            visualObject.transform.localRotation = Quaternion.identity;
            DisableCollider(visualObject);

            if (decorateBlockCell)
            {
                EnsureBlockCellSurface(visualObject);
            }

            var renderers = cacheRenderer ? ResolveRenderers(visualObject) : null;
            return new PooledVisual(visualObject, renderers);
        }

        private GameObject ResolveBlockCellPrefab()
        {
            if (visualProfile && visualProfile.defaultBlockPrefab)
            {
                return visualProfile.defaultBlockPrefab;
            }

            return null;
        }

        private void EnsureBlockCellSurface(GameObject blockCellObject)
        {
            if (!addStudsToBlockCells || !blockCellObject)
            {
                return;
            }

            if (blockCellObject.transform.Find(BlockStudRootName) != null)
            {
                return;
            }

            var studRoot = new GameObject(BlockStudRootName);
            studRoot.transform.SetParent(blockCellObject.transform, false);
            studRoot.transform.localPosition = Vector3.zero;
            studRoot.transform.localRotation = Quaternion.identity;
            studRoot.transform.localScale = Vector3.one;

            var studCount = Mathf.Max(1, studsPerCellAxis);
            var inset = Mathf.Clamp(studInsetRatio, 0.02f, 0.46f);
            var spacing = studCount > 1 ? (1f - (inset * 2f)) / (studCount - 1) : 0f;
            var start = studCount > 1 ? (-0.5f + inset) : 0f;
            var studScale = new Vector3(
                Mathf.Clamp(studDiameterRatio, 0.04f, 0.42f),
                Mathf.Clamp(studDiameterRatio, 0.04f, 0.42f),
                Mathf.Clamp(studHeightRatio, 0.02f, 0.24f));
            var studCenterZ = 0.5f + (studScale.z * 0.15f) + Mathf.Clamp(studLiftRatio, 0f, 0.08f);

            for (var y = 0; y < studCount; y++)
            {
                for (var x = 0; x < studCount; x++)
                {
                    var studObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    studObject.transform.SetParent(studRoot.transform, false);
                    studObject.transform.localRotation = Quaternion.identity;
                    studObject.transform.localPosition = new Vector3(start + (x * spacing), start + (y * spacing), studCenterZ);
                    studObject.transform.localScale = studScale;
                    RenameIfConfigured(studObject, GetRuntimeName("Stud", y * studCount + x));
                    DisableCollider(studObject);
                }
            }
        }

        private static Renderer[] ResolveRenderers(GameObject target)
        {
            if (!target)
            {
                return Array.Empty<Renderer>();
            }

            return target.GetComponentsInChildren<Renderer>(true);
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

        private static void ApplySharedMaterial(PooledVisual visual, Material material)
        {
            if (visual == null)
            {
                return;
            }

            var renderers = visual.Renderers;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!renderer || renderer.sharedMaterial == material)
                {
                    continue;
                }

                renderer.sharedMaterial = material;
            }
        }

        private Material GetDoorMaterial(BlockColor colorType)
        {
            var configuredMaterial = GetConfiguredMaterial(materialsByColor, colorType);
            if (configuredMaterial != null)
            {
                return configuredMaterial;
            }

            return visualProfile ? visualProfile.GetMaterial(colorType) : null;
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

            return null;
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
