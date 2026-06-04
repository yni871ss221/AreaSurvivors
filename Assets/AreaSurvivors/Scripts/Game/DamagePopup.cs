using UnityEngine;

namespace AreaSurvivors
{
    public sealed class DamagePopup : MonoBehaviour
    {
        public TextMesh text;
        public TextMesh[] outlines;
        public float lifetime = 0.78f;
        float age;
        Color baseColor = Color.white;
        float drift;

        public static void Show(GameObject prefab, Vector3 position, int amount, Color color)
        {
            if (prefab == null) return;
            var go = Instantiate(prefab, position, Quaternion.identity);
            var popup = go.GetComponent<DamagePopup>();
            if (popup != null)
            {
                popup.text.text = amount.ToString();
                popup.text.color = color;
                if (popup.outlines != null)
                {
                    foreach (var outline in popup.outlines)
                    {
                        if (outline == null) continue;
                        outline.text = amount.ToString();
                        outline.color = Color.black;
                    }
                }
                popup.baseColor = color;
                popup.drift = UnityEngine.Random.Range(-0.12f, 0.12f);
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);
            transform.position += new Vector3(drift, 0.42f, 0f) * Time.deltaTime;
            transform.localScale = Vector3.one * EvaluateScale(t);
            float alpha = t < 0.48f ? 1f : 1f - Mathf.InverseLerp(0.48f, 1f, t);
            SetAlpha(text, baseColor, alpha);
            if (outlines != null)
            {
                foreach (var outline in outlines) SetAlpha(outline, Color.black, alpha);
            }
            if (age >= lifetime) Destroy(gameObject);
        }

        float EvaluateScale(float t)
        {
            if (t < 0.16f) return Mathf.Lerp(0.48f, 0.82f, Mathf.SmoothStep(0f, 1f, t / 0.16f));
            if (t < 0.34f) return Mathf.Lerp(0.82f, 0.68f, Mathf.SmoothStep(0f, 1f, (t - 0.16f) / 0.18f));
            return 0.68f;
        }

        static void SetAlpha(TextMesh mesh, Color color, float alpha)
        {
            if (mesh == null) return;
            mesh.color = new Color(color.r, color.g, color.b, alpha);
        }
    }
}
