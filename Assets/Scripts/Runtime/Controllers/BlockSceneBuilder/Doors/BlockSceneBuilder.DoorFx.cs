using System.Collections;
using System.Collections.Generic;
using Runtime.Controllers.BlockSceneBuilder.Doors;
using Runtime.Controllers.BlockSceneBuilder.Pool;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private static readonly Renderer[] EmptyDoorRenderers = System.Array.Empty<Renderer>();
        private readonly List<DoorRuntimeBinding> _doorRuntimeByIndex = new();

        private void CacheActiveDoorOpenings(IReadOnlyList<DoorOpeningData> openings)
        {
            for (var i = 0; i < _doorRuntimeByIndex.Count; i++)
            {
                var doorRuntime = _doorRuntimeByIndex[i];
                doorRuntime.Opening = default;
                doorRuntime.HasOpening = false;
                _doorRuntimeByIndex[i] = doorRuntime;
            }

            if (openings == null)
            {
                return;
            }

            var maxIndex = Mathf.Min(openings.Count, _doorRuntimeByIndex.Count);
            for (var i = 0; i < maxIndex; i++)
            {
                var doorRuntime = _doorRuntimeByIndex[i];
                doorRuntime.Opening = openings[i];
                doorRuntime.HasOpening = true;
                _doorRuntimeByIndex[i] = doorRuntime;
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
            if (!TryGetDoorRuntimeBinding(doorIndex, out var doorRuntime))
            {
                return;
            }

            if (!doorRuntime.DoorObject || !doorRuntime.PlacementTransform)
            {
                return;
            }

            StopDoorMatchFxAtIndex(doorIndex, restoreBasePosition: true);
            SetDoorMatchFxRoutineAtIndex(doorIndex, StartCoroutine(AnimateDoorMatchFx(doorIndex)));
        }

        private IEnumerator AnimateDoorMatchFx(int doorIndex)
        {
            if (!TryGetDoorRuntimeBinding(doorIndex, out var doorRuntime))
            {
                yield break;
            }

            var placementTransform = doorRuntime.PlacementTransform;
            if (!placementTransform)
            {
                SetDoorMatchFxRoutineAtIndex(doorIndex, null);
                yield break;
            }

            var baseLocalPosition = doorRuntime.BaseLocalPosition;
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
            for (var i = 0; i < _doorRuntimeByIndex.Count; i++)
            {
                StopDoorMatchFxAtIndex(i, restoreBasePosition: true);
            }
        }

        private void StopDoorMatchFxAtIndex(int doorIndex, bool restoreBasePosition)
        {
            if (!TryGetDoorRuntimeBinding(doorIndex, out var doorRuntime))
            {
                return;
            }

            if (doorRuntime.MatchFxRoutine != null)
            {
                StopCoroutine(doorRuntime.MatchFxRoutine);
                doorRuntime.MatchFxRoutine = null;
            }

            if (restoreBasePosition && doorRuntime.PlacementTransform)
            {
                doorRuntime.PlacementTransform.localPosition = doorRuntime.BaseLocalPosition;
            }

            _doorRuntimeByIndex[doorIndex] = doorRuntime;
        }

        private void ResetDoorRuntimeCache()
        {
            StopAllDoorMatchFx();
            _doorRuntimeByIndex.Clear();
        }

        private void SetDoorMatchFxRoutineAtIndex(int doorIndex, Coroutine routine)
        {
            if (doorIndex < 0)
            {
                return;
            }

            EnsureDoorRuntimeSlot(doorIndex);
            var doorRuntime = _doorRuntimeByIndex[doorIndex];
            doorRuntime.MatchFxRoutine = routine;
            _doorRuntimeByIndex[doorIndex] = doorRuntime;
        }

        private void CacheDoorPlacementBaseLocalPosition(int doorIndex, Transform placementTransform)
        {
            if (doorIndex < 0 || !placementTransform)
            {
                return;
            }

            EnsureDoorRuntimeSlot(doorIndex);
            var doorRuntime = _doorRuntimeByIndex[doorIndex];
            doorRuntime.BaseLocalPosition = placementTransform.localPosition;
            _doorRuntimeByIndex[doorIndex] = doorRuntime;
        }

        private Transform ResolveDoorPlacementTransform(int doorIndex, GameObject doorObject)
        {
            _ = doorObject;
            EnsureDoorRuntimeSlot(doorIndex);
            return _doorRuntimeByIndex[doorIndex].PlacementTransform;
        }

        private bool TryGetDoorRuntimeBinding(int doorIndex, out DoorRuntimeBinding doorRuntime)
        {
            doorRuntime = null;
            if (doorIndex < 0 || doorIndex >= _doorRuntimeByIndex.Count)
            {
                return false;
            }

            doorRuntime = _doorRuntimeByIndex[doorIndex];
            return doorRuntime != null;
        }

        private void EnsureDoorRuntimeSlot(int doorIndex)
        {
            while (_doorRuntimeByIndex.Count <= doorIndex)
            {
                _doorRuntimeByIndex.Add(new DoorRuntimeBinding
                {
                    Renderers = EmptyDoorRenderers
                });
            }
        }

        private void CacheDoorRuntimeReferences(int doorIndex, DoorPoolBindings doorBinding)
        {
            if (doorIndex < 0)
            {
                return;
            }

            EnsureDoorRuntimeSlot(doorIndex);
            if (!doorBinding || !doorBinding.DoorObject)
            {
                StopDoorMatchFxAtIndex(doorIndex, restoreBasePosition: false);
                _doorRuntimeByIndex[doorIndex] = new DoorRuntimeBinding
                {
                    Renderers = EmptyDoorRenderers
                };
                return;
            }

            var doorObject = doorBinding.DoorObject;
            var doorRuntime = _doorRuntimeByIndex[doorIndex];
            doorRuntime.DoorObject = doorObject;
            doorRuntime.PlacementTransform = doorBinding.PlacementTransform;
            doorRuntime.BaseLocalPosition = Vector3.zero;
            var renderers = doorBinding.Renderers;
            doorRuntime.Renderers = renderers != null && renderers.Length > 0
                ? renderers
                : EmptyDoorRenderers;
            _doorRuntimeByIndex[doorIndex] = doorRuntime;
        }

        private void ApplyDoorMaterialAtIndex(int doorIndex, Material material)
        {
            if (!material || !TryGetDoorRuntimeBinding(doorIndex, out var doorRuntime))
            {
                return;
            }

            var renderers = doorRuntime.Renderers;
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
            for (var i = 0; i < _doorRuntimeByIndex.Count; i++)
            {
                var doorRuntime = _doorRuntimeByIndex[i];
                if (!doorRuntime.HasOpening)
                {
                    continue;
                }

                var opening = doorRuntime.Opening;
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
