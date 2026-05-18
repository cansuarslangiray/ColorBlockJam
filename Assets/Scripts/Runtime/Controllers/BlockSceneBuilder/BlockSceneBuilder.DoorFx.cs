using System.Collections.Generic;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private const string DoorPlacementAnchorPrefix = "__DoorPlacementAnchor_";
        private readonly List<DoorOpeningData> _activeDoorOpenings = new();
        private readonly List<Animator> _doorAnimatorByIndex = new();
        private readonly List<Transform> _doorPlacementRootByIndex = new();
        private readonly List<Renderer[]> _doorRenderersByIndex = new();

        private static readonly int DoorInTriggerHash = Animator.StringToHash("DoorIn");

        private void CacheActiveDoorOpenings(IReadOnlyList<DoorOpeningData> openings)
        {
            _activeDoorOpenings.Clear();
            if (openings == null)
            {
                return;
            }

            var maxIndex = Mathf.Min(openings.Count, _doorPool.Count);
            for (var i = 0; i < maxIndex; i++)
            {
                _activeDoorOpenings.Add(openings[i]);
            }
        }

        private void PlayDoorMatchFx(DoorOpeningData matchedDoor)
        {
            var doorIndex = ResolveDoorIndex(matchedDoor);
            if (doorIndex < 0)
            {
                return;
            }

            PlayDoorInAnimation(doorIndex);
        }

        private void PlayDoorInAnimation(int doorIndex)
        {
            if (doorIndex < 0 || doorIndex >= _doorPool.Count)
            {
                return;
            }

            var doorObject = _doorPool[doorIndex];
            if (!doorObject)
            {
                return;
            }

            var animator = ResolveDoorAnimator(doorIndex);
            if (!animator || !animator.runtimeAnimatorController)
            {
                return;
            }

            if (!animator.isActiveAndEnabled || !animator.gameObject.activeInHierarchy)
            {
                return;
            }

            animator.ResetTrigger(DoorInTriggerHash);
            animator.Play(0, 0, 0f);
            animator.SetTrigger(DoorInTriggerHash);
        }

        private void StopAllDoorMatchFx()
        {
            var count = Mathf.Min(_doorPool.Count, _doorAnimatorByIndex.Count);
            for (var i = 0; i < count; i++)
            {
                var animator = ResolveDoorAnimator(i);
                ResetDoorAnimator(animator);
            }

            _activeDoorOpenings.Clear();
        }

        private static void ResetDoorAnimator(Animator animator)
        {
            if (!animator || !animator.runtimeAnimatorController)
            {
                return;
            }

            if (!animator.isActiveAndEnabled || !animator.gameObject.activeInHierarchy)
            {
                return;
            }

            animator.ResetTrigger(DoorInTriggerHash);
            animator.Play(0, 0, 0f);
        }

        private void ResetDoorAnimatorCache()
        {
            _doorAnimatorByIndex.Clear();
            _doorPlacementRootByIndex.Clear();
            _doorRenderersByIndex.Clear();
        }

        private void SyncDoorAnimatorState(int doorIndex)
        {
            var animator = ResolveDoorAnimator(doorIndex);
            ResetDoorAnimator(animator);
        }

        private Animator ResolveDoorAnimator(int doorIndex)
        {
            if (doorIndex < 0 || doorIndex >= _doorAnimatorByIndex.Count)
            {
                return null;
            }

            return _doorAnimatorByIndex[doorIndex];
        }

        private void EnsureDoorAnimatorSlot(int doorIndex)
        {
            while (_doorAnimatorByIndex.Count <= doorIndex)
            {
                _doorAnimatorByIndex.Add(null);
            }
        }

        private Transform ResolveDoorPlacementTransform(int doorIndex, GameObject doorObject)
        {
            EnsureDoorPlacementSlot(doorIndex);
            var cachedPlacementRoot = _doorPlacementRootByIndex[doorIndex];
            if (cachedPlacementRoot)
            {
                return cachedPlacementRoot;
            }

            if (!doorObject)
            {
                return null;
            }

            var doorTransform = doorObject.transform;
            var existingParent = doorTransform.parent;
            if (existingParent &&
                existingParent.name.StartsWith(DoorPlacementAnchorPrefix, System.StringComparison.Ordinal) &&
                existingParent.childCount == 1 &&
                existingParent.GetChild(0) == doorTransform)
            {
                _doorPlacementRootByIndex[doorIndex] = existingParent;
                return existingParent;
            }

            var anchorObject = new GameObject(DoorPlacementAnchorPrefix + doorObject.GetInstanceID());
            var anchorTransform = anchorObject.transform;
            anchorTransform.SetParent(existingParent, false);
            anchorTransform.SetPositionAndRotation(doorTransform.position, doorTransform.rotation);
            anchorTransform.localScale = doorTransform.localScale;

            doorTransform.SetParent(anchorTransform, true);
            doorTransform.localPosition = Vector3.zero;
            doorTransform.localRotation = Quaternion.identity;
            doorTransform.localScale = Vector3.one;

            _doorPlacementRootByIndex[doorIndex] = anchorTransform;
            return anchorTransform;
        }

        private void EnsureDoorPlacementSlot(int doorIndex)
        {
            while (_doorPlacementRootByIndex.Count <= doorIndex)
            {
                _doorPlacementRootByIndex.Add(null);
            }
        }

        private void EnsureDoorRendererSlot(int doorIndex)
        {
            while (_doorRenderersByIndex.Count <= doorIndex)
            {
                _doorRenderersByIndex.Add(System.Array.Empty<Renderer>());
            }
        }

        private void CacheDoorRuntimeReferences(int doorIndex, GameObject doorObject)
        {
            EnsureDoorAnimatorSlot(doorIndex);
            EnsureDoorPlacementSlot(doorIndex);
            EnsureDoorRendererSlot(doorIndex);

            if (!doorObject)
            {
                _doorAnimatorByIndex[doorIndex] = null;
                _doorPlacementRootByIndex[doorIndex] = null;
                _doorRenderersByIndex[doorIndex] = System.Array.Empty<Renderer>();
                return;
            }

            doorObject.TryGetComponent(out Animator animator);
            _doorAnimatorByIndex[doorIndex] = animator;
            _doorRenderersByIndex[doorIndex] = doorObject.TryGetComponent<Renderer>(out var renderer)
                ? new[] { renderer }
                : System.Array.Empty<Renderer>();
        }

        private void ApplyDoorMaterialAtIndex(int doorIndex, Material material)
        {
            if (doorIndex < 0 || doorIndex >= _doorRenderersByIndex.Count || !material)
            {
                return;
            }

            var renderers = _doorRenderersByIndex[doorIndex];
            if (renderers == null)
            {
                return;
            }

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

        private int ResolveDoorIndex(DoorOpeningData matchedDoor)
        {
            var count = Mathf.Min(_activeDoorOpenings.Count, _doorPool.Count);
            for (var i = 0; i < count; i++)
            {
                var opening = _activeDoorOpenings[i];
                if (opening.ColorType == matchedDoor.ColorType &&
                    opening.EdgeDirection == matchedDoor.EdgeDirection &&
                    opening.MinCell == matchedDoor.MinCell &&
                    opening.MaxCell == matchedDoor.MaxCell)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
