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

        [Header("Landing Juice")] [Min(0.03f)] public float landingSquashDuration = 0.1f;
        [Range(0f, 0.35f)] public float landingSquashAmount = 0.14f;

        public AnimationCurve landingSquashCurve = new(new Keyframe(0f, 0f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0f));

        [Header("Door Exit")] [Min(0.05f)] public float doorExitDuration = 0.32f;
        [Min(0.2f)] public float doorExitTravelInCells = 1.15f;
        [Range(0f, 1f)] public float doorExitMinScaleMultiplier = 0.05f;
        public AnimationCurve doorExitMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public AnimationCurve doorExitScaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        private void OnEnable()
        {
            EnsureDefaults();
        }

        private void OnValidate()
        {
            EnsureDefaults();
        }

        private void EnsureDefaults()
        {
            if (blockMoveDuration < 0.05f) blockMoveDuration = 0.05f;
            if (landingSquashDuration < 0.03f) landingSquashDuration = 0.03f;
            if (doorExitDuration < 0.05f) doorExitDuration = 0.05f;
            if (doorExitTravelInCells < 0.2f) doorExitTravelInCells = 0.2f;

            blockMoveCurve ??= AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            landingSquashCurve ??= AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            doorExitMoveCurve ??= AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            doorExitScaleCurve ??= AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        }
    }
}
