using UnityEngine;

namespace AreaSurvivors
{
    public sealed class AttackBounceAnimator : MonoBehaviour
    {
        public Transform[] visualTargets;
        [Min(0.05f)] public float duration = 0.28f;
        [Min(0f)] public float amplitude = 0.14f;
        [Min(0f)] public float verticalStretch = 0.16f;
        [Range(0f, 0.5f)] public float horizontalSquash = 0.07f;

        Vector3[] baseLocalPositions;
        Vector3[] baseLocalScales;
        float elapsed;
        bool playing;

        void Awake()
        {
            CacheBasePose();
        }

        void OnDisable()
        {
            RestoreBasePose();
            playing = false;
        }

        void LateUpdate()
        {
            if (!playing) return;

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, duration));
            float bounce = -Mathf.Sin(progress * Mathf.PI * 3f) * (1f - progress) * amplitude;
            float stretchProgress = Mathf.Clamp01(progress / 0.45f);
            float stretch = Mathf.Sin(stretchProgress * Mathf.PI);
            ApplyPose(bounce, stretch);
            if (progress < 1f) return;

            RestoreBasePose();
            playing = false;
        }

        public void PlayBounce()
        {
            if (playing) RestoreBasePose();
            CacheBasePose();
            if (baseLocalPositions == null || baseLocalPositions.Length == 0) return;
            elapsed = 0f;
            playing = true;
        }

        void CacheBasePose()
        {
            int count = visualTargets != null ? visualTargets.Length : 0;
            baseLocalPositions = new Vector3[count];
            baseLocalScales = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                baseLocalPositions[i] = visualTargets[i] != null ? visualTargets[i].localPosition : Vector3.zero;
                baseLocalScales[i] = visualTargets[i] != null ? visualTargets[i].localScale : Vector3.one;
            }
        }

        void ApplyPose(float offset, float stretch)
        {
            for (int i = 0; i < baseLocalPositions.Length; i++)
            {
                if (visualTargets[i] == null) continue;
                visualTargets[i].localPosition = baseLocalPositions[i] + Vector3.up * offset;
                Vector3 baseScale = baseLocalScales[i];
                visualTargets[i].localScale = new Vector3(
                    baseScale.x * (1f - horizontalSquash * stretch),
                    baseScale.y * (1f + verticalStretch * stretch),
                    baseScale.z);
            }
        }

        void RestoreBasePose()
        {
            if (baseLocalPositions == null || baseLocalScales == null || visualTargets == null) return;
            int count = Mathf.Min(baseLocalPositions.Length, baseLocalScales.Length, visualTargets.Length);
            for (int i = 0; i < count; i++)
            {
                if (visualTargets[i] == null) continue;
                visualTargets[i].localPosition = baseLocalPositions[i];
                visualTargets[i].localScale = baseLocalScales[i];
            }
        }
    }
}
