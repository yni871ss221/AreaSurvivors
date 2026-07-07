using UnityEngine;

namespace AreaSurvivors
{
    public sealed class ProjectileImpactFlash : MonoBehaviour
    {
        [SerializeField] PaperMeshVisual visual;

        float lifetime = 0.12f;
        float age;
        Vector3 startScale;
        bool playing;

        public void Play(Sprite sprite, Color color, float scale, float seconds)
        {
            if (visual == null) visual = GetComponentInChildren<PaperMeshVisual>(true);
            transform.localScale = Vector3.one * Mathf.Max(0.05f, scale);
            startScale = transform.localScale;
            lifetime = Mathf.Max(0.04f, seconds);
            age = 0f;
            playing = true;

            if (visual != null)
            {
                visual.gameObject.SetActive(true);
                visual.Configure(sprite, color, WeaponSortingOrders.Impact);
            }
        }

        void Update()
        {
            if (!playing) return;

            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);
            transform.localScale = startScale * Mathf.Lerp(0.75f, 1.85f, t);
            if (visual != null)
            {
                var color = visual.color;
                color.a = Mathf.Lerp(color.a, 0f, t);
                visual.color = color;
            }

            if (age >= lifetime) Destroy(gameObject);
        }
    }
}
