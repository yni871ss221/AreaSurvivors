using UnityEngine;

namespace AreaSurvivors
{
    public sealed class DamagePopup : MonoBehaviour
    {
        public TextMesh text;
        public TextMesh[] outlines;
        public float lifetime = 0.9f;
        float age;
        Color baseColor = Color.white;

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
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);
            transform.position += Vector3.up * (0.75f * Time.deltaTime);
            transform.localScale = Vector3.one * EvaluateScale(t);
            float alpha = t < 0.55f ? 1f : 1f - Mathf.InverseLerp(0.55f, 1f, t);
            SetAlpha(text, baseColor, alpha);
            if (outlines != null)
            {
                foreach (var outline in outlines) SetAlpha(outline, Color.black, alpha);
            }
            if (age >= lifetime) Destroy(gameObject);
        }

        float EvaluateScale(float t)
        {
            if (t < 0.18f) return Mathf.Lerp(0.72f, 1.28f, Mathf.SmoothStep(0f, 1f, t / 0.18f));
            if (t < 0.38f) return Mathf.Lerp(1.28f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.18f) / 0.2f));
            return 1f;
        }

        static void SetAlpha(TextMesh mesh, Color color, float alpha)
        {
            if (mesh == null) return;
            mesh.color = new Color(color.r, color.g, color.b, alpha);
        }
    }
}
