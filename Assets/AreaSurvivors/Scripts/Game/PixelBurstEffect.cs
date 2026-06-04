using UnityEngine;

namespace AreaSurvivors
{
    public sealed class PixelBurstEffect : MonoBehaviour
    {
        PaperMeshVisual visual;
        Vector3 velocity;
        Vector3 startScale;
        Color baseColor;
        float lifetime = 0.22f;
        float age;
        float spin;

        public static void Spawn(Sprite sprite, Vector3 position, Color color, int count, float scale, float duration, int sortingOrder = 3350)
        {
            if (sprite == null) return;
            count = Mathf.Clamp(count, 1, 12);
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Pixel Burst");
                go.transform.position = position + new Vector3(Random.Range(-0.05f, 0.05f), Random.Range(-0.04f, 0.08f), 0f);
                go.transform.localScale = Vector3.one * Random.Range(scale * 0.62f, scale * 1.18f);
                go.AddComponent<PaperBillboard>();
                var mesh = go.AddComponent<PaperMeshVisual>();
                mesh.Configure(sprite, color, sortingOrder + i);
                var piece = go.AddComponent<PixelBurstEffect>();
                piece.Configure(mesh, color, duration);
            }
        }

        void Configure(PaperMeshVisual mesh, Color color, float seconds)
        {
            visual = mesh;
            baseColor = color;
            lifetime = Mathf.Max(0.06f, seconds);
            startScale = transform.localScale;
            velocity = new Vector3(Random.Range(-1f, 1f), Random.Range(0.35f, 1.25f), 0f) * Random.Range(0.22f, 0.58f);
            spin = Random.Range(-220f, 220f);
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);
            transform.position += velocity * Time.deltaTime;
            velocity *= Mathf.Pow(0.18f, Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, 0f, spin * age);
            transform.localScale = startScale * Mathf.Lerp(1f, 0.18f, t);
            if (visual != null)
            {
                visual.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(baseColor.a, 0f, t));
            }
            if (age >= lifetime) Destroy(gameObject);
        }
    }
}
