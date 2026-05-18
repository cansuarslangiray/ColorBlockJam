using System.Collections;
using UnityEngine;

namespace Runtime.Controllers.BlockSceneBuilder
{
    public static class BlockMotionTween
    {
        public static IEnumerator TweenMove(Transform blockTransform, Vector3 targetPosition, float duration,
            AnimationCurve easingCurve)
        {
            var startPosition = blockTransform.position;
            if ((startPosition - targetPosition).sqrMagnitude <= 0.0001f)
            {
                blockTransform.position = targetPosition;
                yield break;
            }

            var elapsed = 0f;
            var safeDuration = Mathf.Max(0.0001f, duration);
            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed / safeDuration);
                var eased = EvaluateCurve01(easingCurve, normalized, normalized);
                blockTransform.position = Vector3.LerpUnclamped(startPosition, targetPosition, eased);
                yield return null;
            }

            blockTransform.position = targetPosition;
        }

        public static IEnumerator TweenExitThroughDoor(Transform blockTransform, Vector2Int resolvedExitDirection,
            Vector2 blockLocalCenter, Vector2 doorWorldCenter, float doorWorldZ, float cellSize, float exitDuration,
            float exitTravelInCells, AnimationCurve exitMoveCurve, AnimationCurve exitScaleCurve,
            float exitMinScaleMultiplier)
        {
            var startPosition = blockTransform.position;
            var startScale = blockTransform.localScale;
            var exitVector = new Vector3(resolvedExitDirection.x, resolvedExitDirection.y, 0f);

            var doorCenterTargetPosition = new Vector3(
                doorWorldCenter.x - blockLocalCenter.x,
                doorWorldCenter.y - blockLocalCenter.y,
                Mathf.Max(startPosition.z, doorWorldZ + (cellSize * 0.05f)));

            var passThroughDistance = exitTravelInCells * cellSize * 1.56f;
            var finalTargetPosition = doorCenterTargetPosition + (exitVector * passThroughDistance);
            finalTargetPosition.z = doorCenterTargetPosition.z + (cellSize * 0.16f);

            var totalDuration = Mathf.Max(0.0001f, exitDuration);
            var approachDuration = totalDuration * 0.58f;
            var passDuration = Mathf.Max(0.0001f, totalDuration - approachDuration);
            var invApproachDuration = 1f / Mathf.Max(0.0001f, approachDuration);
            var invPassDuration = 1f / passDuration;
            var minScale = startScale * exitMinScaleMultiplier;
            var elapsed = 0f;

            while (elapsed < approachDuration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed * invApproachDuration);
                var pullT = normalized * normalized;
                blockTransform.position = Vector3.LerpUnclamped(startPosition, doorCenterTargetPosition, pullT);
                blockTransform.localScale = startScale;
                yield return null;
            }

            blockTransform.position = doorCenterTargetPosition;
            blockTransform.localScale = startScale;
            elapsed = 0f;

            while (elapsed < passDuration)
            {
                elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(elapsed * invPassDuration);
                var moveT = EvaluateCurve01(exitMoveCurve, normalized, normalized);
                var shrinkNormalized = Mathf.Clamp01((normalized - 0.55f) / 0.45f);
                var scaleT = EvaluateCurve01(exitScaleCurve, shrinkNormalized, 1f - shrinkNormalized);

                blockTransform.position = Vector3.LerpUnclamped(doorCenterTargetPosition, finalTargetPosition, moveT);
                blockTransform.localScale = Vector3.LerpUnclamped(minScale, startScale, scaleT);
                yield return null;
            }

            blockTransform.position = finalTargetPosition;
        }

        private static float EvaluateCurve01(AnimationCurve curve, float normalized, float fallbackValue)
        {
            return curve != null ? Mathf.Clamp01(curve.Evaluate(normalized)) : fallbackValue;
        }
    }
}