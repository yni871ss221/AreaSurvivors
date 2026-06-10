using UnityEngine;

namespace AreaSurvivors
{
    public sealed class HarvestResourcePopup : MonoBehaviour
    {
        const int TextOrder = 23001;
        const int IconOrder = 23002;

        public TextMesh text;
        public TextMesh[] outlines;
        public PaperMeshVisual icon;
        public float lifetime = 0.86f;

        float age;
        float drift;
        RuntimeTextMeshOutline textOutline;

        public static void Show(Vector3 position, int amount, Sprite resourceIcon, Color color)
        {
            var root = new GameObject("Harvest Resource Popup");
            root.transform.position = position;
            root.AddComponent<PaperBillboard>();

            var popup = root.AddComponent<HarvestResourcePopup>();
            popup.drift = Random.Range(-0.08f, 0.08f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            popup.text = CreateText(root.transform, "Text", font, "+" + amount, color, TextOrder, Vector3.zero);
            popup.textOutline = popup.text.gameObject.AddComponent<RuntimeTextMeshOutline>();
            popup.textOutline.SetColors(color, Color.black);
            if (resourceIcon != null)
            {
                var iconObject = new GameObject("Icon");
                iconObject.transform.SetParent(root.transform, false);
                iconObject.transform.localPosition = new Vector3(0.24f, 0.01f, 0f);
                popup.icon = iconObject.AddComponent<PaperMeshVisual>();
                popup.icon.Configure(resourceIcon, Color.white, IconOrder);
                var outline = iconObject.AddComponent<RuntimeSpriteOutline>();
                outline.outlineColor = Color.black;
                outline.thickness = 0.018f;
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);
            transform.position += new Vector3(drift, 0.42f, 0f) * Time.deltaTime;
            transform.localScale = Vector3.one * EvaluateScale(t);
            float alpha = t < 0.5f ? 1f : 1f - Mathf.InverseLerp(0.5f, 1f, t);
            textOutline?.SetAlpha(alpha);
            if (icon != null)
            {
                var color = icon.color;
                color.a = alpha;
                icon.color = color;
            }

            if (age >= lifetime) Destroy(gameObject);
        }

        static TextMesh CreateText(Transform parent, string name, Font font, string value, Color color, int order, Vector3 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(-0.08f, 0f, 0f) + offset;
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.text = value;
            mesh.color = color;
            mesh.fontSize = 42;
            mesh.characterSize = 0.034f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sortingOrder = order;
            return mesh;
        }

        static float EvaluateScale(float t)
        {
            if (t < 0.16f) return Mathf.Lerp(0.55f, 0.92f, Mathf.SmoothStep(0f, 1f, t / 0.16f));
            if (t < 0.34f) return Mathf.Lerp(0.92f, 0.78f, Mathf.SmoothStep(0f, 1f, (t - 0.16f) / 0.18f));
            return 0.78f;
        }
    }
}
