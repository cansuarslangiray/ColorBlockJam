using System.Collections;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public static class DoorMatchFxTween
    {
        public static IEnumerator LowerAndRaise(Transform doorTransform, Vector3 baseWorldPosition, float cellSize,
            float dropDistanceInCells, float lowerDuration, float holdDuration, float raiseDuration)
        {
            var safeLowerDuration = Mathf.Max(0.0001f, lowerDuration);
            var safeRaiseDuration = Mathf.Max(0.0001f, raiseDuration);
            var safeHoldDuration = Mathf.Max(0f, holdDuration);

            var invLowerDuration = 1f / safeLowerDuration;
            var invRaiseDuration = 1f / safeRaiseDuration;

            var loweredPosition = baseWorldPosition + (Vector3.down * dropDistanceInCells * cellSize);
            var t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime * invLowerDuration;
                if (t > 1f)
                {
                    t = 1f;
                }

                var eased = SmoothStep01(t);
                doorTransform.position = Vector3.LerpUnclamped(baseWorldPosition, loweredPosition, eased);
                yield return null;
            }

            doorTransform.position = loweredPosition;

            var holdElapsed = 0f;
            while (holdElapsed < safeHoldDuration)
            {
                holdElapsed += Time.deltaTime;
                yield return null;
            }

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * invRaiseDuration;
                if (t > 1f)
                {
                    t = 1f;
                }

                var eased = SmoothStep01(t);
                doorTransform.position = Vector3.LerpUnclamped(loweredPosition, baseWorldPosition, eased);
                yield return null;
            }

            doorTransform.position = baseWorldPosition;
        }

        private static float SmoothStep01(float value)
        {
            var t = Mathf.Clamp01(value);
            return t * t * (3f - (2f * t));
        }
    }
}