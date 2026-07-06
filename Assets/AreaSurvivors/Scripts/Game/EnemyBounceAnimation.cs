using UnityEngine;

namespace AreaSurvivors
{
    public sealed class EnemyBounceAnimation : MonoBehaviour
    {
        public float scaleAmplitude = 0.06f;
        public float bobAmplitude = 0.04f;
        public float cycleSeconds = 1f;
        public float phaseOffsetSeconds;

        Vector3 baseLocalPosition;
        Vector3 baseLocalScale;
        float phase;

        void Awake()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalScale = transform.localScale;
            phase = Random.Range(0f, Mathf.Max(0.01f, cycleSeconds)) + phaseOffsetSeconds;
        }

        void LateUpdate()
        {
            float cycle = Mathf.Max(0.01f, cycleSeconds);
            float progress = Mathf.PingPong((Time.time + phase) * 2f / cycle, 1f);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            float stretch = 1f + eased * scaleAmplitude;
            float lift = eased * bobAmplitude;

            transform.localScale = new Vector3(baseLocalScale.x, baseLocalScale.y * stretch, baseLocalScale.z);
            transform.localPosition = baseLocalPosition + new Vector3(0f, lift, 0f);
        }
    }
}
