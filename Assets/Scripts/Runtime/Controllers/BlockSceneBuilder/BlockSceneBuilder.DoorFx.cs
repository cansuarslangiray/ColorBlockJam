using System.Collections;
using System.Collections.Generic;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private const string DoorPlacementAnchorPrefix = "__DoorPlacementAnchor_";
        private readonly List<DoorOpeningData> _activeDoorOpenings = new();
        private readonly List<Transform> _doorPlacementRootByIndex = new();
        private readonly List<Renderer[]> _doorRenderersByIndex = new();
        private readonly List<Vector3> _doorPlacementBaseLocalPositionByIndex = new();
        private readonly List<Coroutine> _doorMatchFxRoutineByIndex = new();


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

            PlayDoorMatchFxAtIndex(doorIndex);
        }

        private void PlayDoorMatchFxAtIndex(int doorIndex)
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

            var placementTransform = ResolveDoorPlacementTransform(doorIndex, doorObject);
            if (!placementTransform)
            {
                return;
            }

            StopDoorMatchFxAtIndex(doorIndex, restoreBasePosition: true);
            SetDoorMatchFxRoutineAtIndex(doorIndex, StartCoroutine(AnimateDoorMatchFx(doorIndex)));
        }

        private IEnumerator AnimateDoorMatchFx(int doorIndex)
        {
            if (doorIndex < 0 || doorIndex >= _doorPool.Count)
            {
                yield break;
            }

            var doorObject = _doorPool[doorIndex];
            var placementTransform = ResolveDoorPlacementTransform(doorIndex, doorObject);
            if (!placementTransform)
            {
                SetDoorMatchFxRoutineAtIndex(doorIndex, null);
                yield break;
            }

            EnsureDoorPlacementBaseLocalPositionSlot(doorIndex);
            var baseLocalPosition = _doorPlacementBaseLocalPositionByIndex[doorIndex];
            var dipDistance = Mathf.Max(0f, CellSize * doorMatchDipInCells);
            var duration = Mathf.Max(0.02f, doorMatchFxDuration);

            if (dipDistance <= Mathf.Epsilon || duration <= Mathf.Epsilon)
            {
                placementTransform.localPosition = baseLocalPosition;
                SetDoorMatchFxRoutineAtIndex(doorIndex, null);
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (!placementTransform)
                {
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                var dip = Mathf.Sin(progress * Mathf.PI) * dipDistance;
                placementTransform.localPosition = baseLocalPosition + (Vector3.down * dip);
                yield return null;
            }

            if (placementTransform)
            {
                placementTransform.localPosition = baseLocalPosition;
            }

            SetDoorMatchFxRoutineAtIndex(doorIndex, null);
        }

        private void StopAllDoorMatchFx()
        {
            var count = _doorMatchFxRoutineByIndex.Count;
            for (var i = 0; i < count; i++)
            {
                StopDoorMatchFxAtIndex(i, restoreBasePosition: true);
            }

            _activeDoorOpenings.Clear();
        }

        private void StopDoorMatchFxAtIndex(int doorIndex, bool restoreBasePosition)
        {
            if (doorIndex < 0 || doorIndex >= _doorMatchFxRoutineByIndex.Count)
            {
                return;
            }

            var routine = _doorMatchFxRoutineByIndex[doorIndex];
            if (routine != null)
            {
                StopCoroutine(routine);
                _doorMatchFxRoutineByIndex[doorIndex] = null;
            }

            if (!restoreBasePosition || doorIndex >= _doorPlacementRootByIndex.Count)
            {
                return;
            }

            var placementTransform = _doorPlacementRootByIndex[doorIndex];
            if (!placementTransform || doorIndex >= _doorPlacementBaseLocalPositionByIndex.Count)
            {
                return;
            }

            placementTransform.localPosition = _doorPlacementBaseLocalPositionByIndex[doorIndex];
        }

        private void ResetDoorRuntimeCache()
        {
            StopAllDoorMatchFx();
            _doorPlacementRootByIndex.Clear();
            _doorRenderersByIndex.Clear();
            _doorPlacementBaseLocalPositionByIndex.Clear();
            _doorMatchFxRoutineByIndex.Clear();
        }

        private void SetDoorMatchFxRoutineAtIndex(int doorIndex, Coroutine routine)
        {
            EnsureDoorMatchFxRoutineSlot(doorIndex);
            _doorMatchFxRoutineByIndex[doorIndex] = routine;
        }

        private void CacheDoorPlacementBaseLocalPosition(int doorIndex, Transform placementTransform)
        {
            if (!placementTransform)
            {
                return;
            }

            EnsureDoorPlacementBaseLocalPositionSlot(doorIndex);
            _doorPlacementBaseLocalPositionByIndex[doorIndex] = placementTransform.localPosition;
        }

        private void EnsureDoorPlacementBaseLocalPositionSlot(int doorIndex)
        {
            while (_doorPlacementBaseLocalPositionByIndex.Count <= doorIndex)
            {
                _doorPlacementBaseLocalPositionByIndex.Add(Vector3.zero);
            }
        }

        private void EnsureDoorMatchFxRoutineSlot(int doorIndex)
        {
            while (_doorMatchFxRoutineByIndex.Count <= doorIndex)
            {
                _doorMatchFxRoutineByIndex.Add(null);
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

            _doorPlacementRootByIndex[doorIndex] = doorTransform;
            return doorTransform;
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
            EnsureDoorPlacementSlot(doorIndex);
            EnsureDoorRendererSlot(doorIndex);
            EnsureDoorPlacementBaseLocalPositionSlot(doorIndex);
            EnsureDoorMatchFxRoutineSlot(doorIndex);

            if (!doorObject)
            {
                StopDoorMatchFxAtIndex(doorIndex, restoreBasePosition: false);
                _doorPlacementRootByIndex[doorIndex] = null;
                _doorRenderersByIndex[doorIndex] = System.Array.Empty<Renderer>();
                _doorPlacementBaseLocalPositionByIndex[doorIndex] = Vector3.zero;
                return;
            }

            if (doorObject.TryGetComponent<Animator>(out var legacyDoorAnimator) && legacyDoorAnimator.enabled)
            {
                legacyDoorAnimator.enabled = false;
            }

            _doorPlacementBaseLocalPositionByIndex[doorIndex] = Vector3.zero;
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