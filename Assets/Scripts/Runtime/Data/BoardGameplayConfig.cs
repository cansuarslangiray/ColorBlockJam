using UnityEngine;

namespace Runtime.Data
{
    [CreateAssetMenu(fileName = "BoardGameplayConfig", menuName = "ColorBlockJam/Board Gameplay Config")]
    public class BoardGameplayConfig : ScriptableObject
    {
        [Header("Block Movement")] [Min(0.05f)]
        public float blockMoveDuration = 0.14f;

        public AnimationCurve blockMoveCurve = new(new Keyframe(0f, 0f, 0f, 2.6f), new Keyframe(0.72f, 1.06f),
            new Keyframe(1f, 1f, 0f, 0f));

        [Header("Door Exit")] public float doorExitDuration = 0.32f;
        public float doorExitTravelInCells = 1.15f;
        public float doorExitMinScaleMultiplier = 0.05f;
        public float doorExitDipDistanceInCells = 0.22f;
        public AnimationCurve doorExitMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public AnimationCurve doorExitScaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("Door Match FX")]
        public float doorMatchDropDistanceInCells = 0.16f;

        public float doorMatchLowerDuration = 0.09f;
        public float doorMatchHoldDuration = 0.12f;
        public float doorMatchRaiseDuration = 0.11f;

    }
}
