using UnityEngine;

namespace AreaSurvivors
{
    public sealed class EnemySlowEffect : MonoBehaviour
    {
        float multiplier = 1f;
        float expiresAt;

        public float Multiplier
        {
            get
            {
                if (Time.time >= expiresAt) return 1f;
                return Mathf.Clamp(multiplier, 0.05f, 1f);
            }
        }

        public static void Apply(GameObject target, float slowAmount, float durationSeconds)
        {
            if (target == null || slowAmount <= 0f || durationSeconds <= 0f) return;
            var effect = target.GetComponent<EnemySlowEffect>();
            if (effect == null) effect = target.AddComponent<EnemySlowEffect>();
            float nextMultiplier = Mathf.Clamp01(1f - slowAmount);
            effect.multiplier = Mathf.Min(effect.Multiplier, Mathf.Clamp(nextMultiplier, 0.05f, 1f));
            effect.expiresAt = Mathf.Max(effect.expiresAt, Time.time + durationSeconds);
        }
    }
}
