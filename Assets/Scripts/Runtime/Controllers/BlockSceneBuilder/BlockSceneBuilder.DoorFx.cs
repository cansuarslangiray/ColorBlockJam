using System.Collections;
using System.Collections.Generic;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        [Header("Door Match FX")] [SerializeField, Min(0.02f)]
        private float doorMatchPulseDuration = 0.16f;

        [SerializeField, Range(0f, 0.4f)] private float doorMatchPulseScaleAmount = 0.16f;

        private readonly List<DoorOpeningData> _activeDoorOpenings = new();
        private readonly Dictionary<int, Coroutine> _doorPulseRoutineByIndex = new();

        private void CacheActiveDoorOpenings(IReadOnlyList<DoorOpeningData> openings)
        {
            _activeDoorOpenings.Clear();
            if (openings == null)
            {
                return;
            }

            for (var i = 0; i < openings.Count; i++)
            {
                _activeDoorOpenings.Add(openings[i]);
            }
        }

        private void PlayDoorMatchFx(DoorOpeningData matchedDoor)
        {
            if (!TryResolveDoorIndex(matchedDoor, out var doorIndex))
            {
                return;
            }

            PlayDoorPulse(doorIndex);
        }

        private bool TryResolveDoorIndex(DoorOpeningData matchedDoor, out int doorIndex)
        {
            doorIndex = -1;
            var maxIndex = Mathf.Min(_activeDoorOpenings.Count, _doorPool.Count);
            if (maxIndex <= 0)
            {
                return false;
            }

            for (var i = 0; i < maxIndex; i++)
            {
                var opening = _activeDoorOpenings[i];
                if (opening.ColorType != matchedDoor.ColorType ||
                    opening.EdgeDirection != matchedDoor.EdgeDirection ||
                    opening.MinCell != matchedDoor.MinCell ||
                    opening.MaxCell != matchedDoor.MaxCell)
                {
                    continue;
                }

                doorIndex = i;
                return true;
            }

            return false;
        }

        private void PlayDoorPulse(int doorIndex)
        {
            if (doorIndex < 0 || doorIndex >= _doorPool.Count)
            {
                return;
            }

            if (_doorPulseRoutineByIndex.TryGetValue(doorIndex, out var activePulse) && activePulse != null)
            {
                StopCoroutine(activePulse);
            }

            var doorTransform = _doorPool[doorIndex].Transform;
            if (doorTransform == null)
            {
                return;
            }

            _doorPulseRoutineByIndex[doorIndex] =
                StartCoroutine(PulseDoorRoutine(doorIndex, doorTransform, doorTransform.localScale));
        }

        private IEnumerator PulseDoorRoutine(int doorIndex, Transform doorTransform, Vector3 baseScale)
        {
            var duration = Mathf.Max(0.02f, doorMatchPulseDuration);
            var elapsed = 0f;
            var pulseScale = baseScale * (1f + Mathf.Max(0f, doorMatchPulseScaleAmount));

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var wave = Mathf.Sin(t * Mathf.PI);
                doorTransform.localScale = Vector3.LerpUnclamped(baseScale, pulseScale, wave);
                yield return null;
            }

            doorTransform.localScale = baseScale;
            _doorPulseRoutineByIndex.Remove(doorIndex);
        }

        private void StopAllDoorMatchFx()
        {
            foreach (var pair in _doorPulseRoutineByIndex)
            {
                if (pair.Value != null)
                {
                    StopCoroutine(pair.Value);
                }
            }

            _doorPulseRoutineByIndex.Clear();
        }
    }
}
