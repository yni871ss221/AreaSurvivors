using UnityEngine;

namespace AreaSurvivors
{
    public sealed class LichSummonCircleEffect : MonoBehaviour
    {
        public float durationSeconds = 2.2f;
        public float visualAlpha = 0.72f;
        public float pulseScale = 0.06f;

        PaperMeshVisual visual;
        float age;
        Vector3 baseScale = Vector3.one;

        public void Configure(float radius, float verticalRadiusMultiplier, float duration)
        {
            if (visual == null) visual = GetComponentInChildren<PaperMeshVisual>(true);
            durationSeconds = Mathf.Max(0.1f, duration);
            float safeRadius = Mathf.Max(0.05f, radius);
            float safeVertical = Mathf.Max(0.05f, verticalRadiusMultiplier);
            baseScale = new Vector3(safeRadius, safeRadius * safeVertical, 1f);
            transform.localScale = baseScale;
            age = 0f;
            ApplyAlpha(visualAlpha);
        }

        void Awake()
        {
            if (visual == null) visual = GetComponentInChildren<PaperMeshVisual>(true);
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= durationSeconds)
            {
                Destroy(gameObject);
                return;
            }

            float pulse = 1f + Mathf.Sin(age * 7f) * pulseScale;
            transform.localScale = new Vector3(baseScale.x * pulse, baseScale.y * pulse, baseScale.z);
            float fade = Mathf.Clamp01(1f - age / Mathf.Max(0.1f, durationSeconds));
            ApplyAlpha(visualAlpha * Mathf.SmoothStep(0f, 1f, fade));
        }

        void ApplyAlpha(float alpha)
        {
            if (visual == null) return;
            var color = visual.color;
            color.a = Mathf.Clamp01(alpha);
            visual.color = color;
        }
    }
}
