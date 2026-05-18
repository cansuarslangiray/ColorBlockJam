using System.Collections;
using System.Collections.Generic;
using Runtime.Domain.Models;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public partial class BlockSceneBuilder
    {
        private readonly Dictionary<int, int> _doorIndexByKey = new();
        private readonly List<Coroutine> _doorMotionRoutineByIndex = new();

        private float DoorMatchDropDistanceInCells => _gameplayConfig.doorMatchDropDistanceInCells;
        private float DoorMatchLowerDuration => _gameplayConfig.doorMatchLowerDuration;
        private float DoorMatchHoldDuration => _gameplayConfig.doorMatchHoldDuration;
        private float DoorMatchRaiseDuration => _gameplayConfig.doorMatchRaiseDuration;

        private void CacheActiveDoorOpenings(IReadOnlyList<DoorOpeningData> openings)
        {
            _doorIndexByKey.Clear();
            if (openings == null)
            {
                return;
            }

            var maxIndex = Mathf.Min(openings.Count, _doorPool.Count);
            for (var i = 0; i < maxIndex; i++)
            {
                _doorIndexByKey[BuildDoorLookupKey(openings[i])] = i;
            }
        }

        private void PlayDoorMatchFx(DoorOpeningData matchedDoor)
        {
            if (!_doorIndexByKey.TryGetValue(BuildDoorLookupKey(matchedDoor), out var doorIndex))
            {
                return;
            }

            PlayDoorLowerAndRaise(doorIndex);
        }

        private void PlayDoorLowerAndRaise(int doorIndex)
        {
            if (doorIndex < 0 || doorIndex >= _doorPool.Count)
            {
                return;
            }

            var doorObject = _doorPool[doorIndex];
            var doorTransform = doorObject ? doorObject.transform : null;
            if (!doorTransform)
            {
                return;
            }

            EnsureDoorRoutineSlot(doorIndex);
            if (_doorMotionRoutineByIndex[doorIndex] != null)
            {
                return;
            }

            var basePosition = doorTransform.position;
            var layout = GetCurrentLayout();
            _doorMotionRoutineByIndex[doorIndex] =
                StartCoroutine(LowerAndRaiseDoorRoutine(doorIndex, doorTransform, basePosition, layout.CellSize));
        }

        private IEnumerator LowerAndRaiseDoorRoutine(
            int doorIndex,
            Transform doorTransform,
            Vector3 baseWorldPosition,
            float cellSize)
        {
            yield return BlockMotionTween.LowerAndRaiseDoor(doorTransform, baseWorldPosition, cellSize,
                DoorMatchDropDistanceInCells, DoorMatchLowerDuration, DoorMatchHoldDuration, DoorMatchRaiseDuration);

            if (doorIndex >= 0 && doorIndex < _doorMotionRoutineByIndex.Count)
            {
                _doorMotionRoutineByIndex[doorIndex] = null;
            }
        }

        private void StopAllDoorMatchFx()
        {
            for (var i = 0; i < _doorMotionRoutineByIndex.Count; i++)
            {
                if (_doorMotionRoutineByIndex[i] != null)
                {
                    StopCoroutine(_doorMotionRoutineByIndex[i]);
                    _doorMotionRoutineByIndex[i] = null;
                }
            }

            _doorIndexByKey.Clear();
        }

        private void EnsureDoorRoutineSlot(int doorIndex)
        {
            while (_doorMotionRoutineByIndex.Count <= doorIndex)
            {
                _doorMotionRoutineByIndex.Add(null);
            }
        }

        private static int BuildDoorLookupKey(DoorOpeningData opening)
        {
            unchecked
            {
                var key = 17;
                key = (key * 31) + (int)opening.ColorType;
                key = (key * 31) + (int)opening.EdgeDirection;
                key = (key * 31) + opening.MinCell.x;
                key = (key * 31) + opening.MinCell.y;
                key = (key * 31) + opening.MaxCell.x;
                key = (key * 31) + opening.MaxCell.y;
                return key;
            }
        }
    }
}